/*
* Copyright (c) 202 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: SessionStore.cs 
*
* SessionStore.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Sessions.Cache.Client is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* VNLib.Plugins.Sessions.Cache.Client is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
* GNU Affero General Public License for more details.
*
* You should have received a copy of the GNU Affero General Public License
* along with this program.  If not, see https://www.gnu.org/licenses/.
*/

using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;

using VNLib.Net.Http;
using VNLib.Utils.Logging;
using VNLib.Plugins.Essentials.Sessions;
using VNLib.Plugins.Sessions.Cache.Client.Exceptions;

namespace VNLib.Plugins.Sessions.Cache.Client
{

    /// <summary>
    /// Provides an abstract <see cref="ISessionStore{TSession}"/> for managing sessions for HTTP connections
    /// remote cache backed sessions
    /// </summary>
    /// <typeparam name="TSession"></typeparam>
    public abstract class SessionStore<TSession> : ISessionStore<TSession> where TSession: IRemoteSession
    {

        /// <summary>
        /// Used to serialize access to session instances. Unless overridden, uses a <see cref="SessionSerializer{TSession}"/>
        /// implementation.
        /// </summary>
        protected virtual ISessionSerialzer<TSession> Serializer { get; } = new SessionSerializer<TSession>(100);

        /// <summary>
        /// The <see cref="ISessionIdFactory"/> that provides session ids for connections
        /// </summary>
        protected abstract ISessionIdFactory IdFactory { get; }

        /// <summary>
        /// The backing cache store
        /// </summary>
        protected abstract IRemoteCacheStore Cache { get; }

        /// <summary>
        /// The session factory, produces sessions from their initial data and session-id
        /// </summary>
        protected abstract ISessionFactory<TSession> SessionFactory { get; }

        /// <summary>
        /// The log provider for writing background update exceptions to
        /// </summary>
        protected abstract ILogProvider Log { get; }

        ///<inheritdoc/>
        public virtual async ValueTask<TSession?> GetSessionAsync(IHttpEvent entity, CancellationToken cancellationToken)
        {
            //Wrap exceptions in session exceptions
            try
            {
                if (!IdFactory.CanService(entity))
                {
                    return default;
                }

                //Get the session id from the entity
                string? sessionId = IdFactory.TryGetSessionId(entity);

                //If regeneration is not supported, return since id is null
                if (sessionId == null && !IdFactory.RegenerationSupported)
                {
                    return default;
                }

                if (sessionId == null)
                {
                    //Get new sessionid
                    sessionId = IdFactory.RegenerateId(entity);

                    //Create a new session
                    TSession? session = SessionFactory.GetNewSession(entity, sessionId, null);

                    if (session != null)
                    {
                        //Enter wait for the new session, this call should not block or yield
                        await Serializer.WaitAsync(session, cancellationToken);
                    }

                    return session;
                }
                else
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    Dictionary<string, string>? sessionData = null;

                    //See if the session is currently in use, if so wait for access to it
                    if (Serializer.TryGetSession(sessionId, out TSession? activeSession))
                    {
                        //reuse the session
                        await Serializer.WaitAsync(activeSession, cancellationToken);

                        //after wait has been completed, check the status of the session
                        if (activeSession.IsValid(out Exception? cause))
                        {
                            //Session is valid, we can use it
                            return activeSession;
                        }

                        //release the wait, no use for the session in invalid state
                        Serializer.Release(activeSession);

                        //Rethrow exception that cause invalidation
                        if (cause != null)
                        {
                            throw cause;
                        }

                        //Regen id and continue loading
                    }
                    else
                    {
                        //Session cannot be found local, so we need to retrieve it from cache
                        sessionData = await Cache.GetObjectAsync<Dictionary<string, string>>(sessionId, cancellationToken);
                    }


                    //If the cache entry is null, we may choose to regenrate the session id
                    if (sessionData == null && IdFactory.RegenIdOnEmptyEntry)
                    {
                        sessionId = IdFactory.RegenerateId(entity);
                    }

                    //Create a new session
                    TSession? session = SessionFactory.GetNewSession(entity, sessionId, sessionData);

                    if (session != null)
                    {
                        //Enter wait for the new session
                        await Serializer.WaitAsync(session, cancellationToken);
                    }

                    return session;
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (SessionException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new SessionException("An exception occured during session processing", ex);
            }
        }

        ///<inheritdoc/>
        public virtual ValueTask ReleaseSessionAsync(TSession session, IHttpEvent entity)
        {
            //Get status on release
            SessionStatus status = session.GetStatus();

            //confirm the cache client is connected
            if (status != SessionStatus.None && !Cache.IsConnected)
            {
                throw new SessionUpdateFailedException("The required session operation cannot be completed because the cache is not connected");
            }

            //Delete status is required
            if (status.HasFlag(SessionStatus.Delete))
            {
                //Delete the session
                _ = DeleteSessionAsync(session);
            }
            else if (status.HasFlag(SessionStatus.RegenId))
            {
                if (!IdFactory.RegenerationSupported)
                {
                    throw new SessionException("Session id regeneration is not supported by this store");
                }

                //Get new id for session
                string newId = IdFactory.RegenerateId(entity);

                //Update data and id
                _ = UpdateSessionAndIdAsync(session, newId);
            }
            else if (status.HasFlag(SessionStatus.Detach))
            {
                /*
                 * Special case. We are regenerating the session id, but we are not updating the session.
                 * This will cause the client's session id to detach from the current session.
                 * 
                 * All other updates will be persisted to the cache. 
                 * 
                 * The id should require regeneration on the user's next request then attach a new session.
                 * 
                 * The session is still valid, however the current connection should effectivly be 'detatched' 
                 * from it.
                 */

                if (!IdFactory.RegenerationSupported)
                {
                    throw new SessionException("Session id regeneration is not supported by this store");
                }

                _ = IdFactory.RegenerateId(entity);
                _ = UpdateSessionAndIdAsync(session, null);
            }
            else if (status.HasFlag(SessionStatus.UpdateOnly))
            {
                //Just run update
                _ = UpdateSessionAndIdAsync(session, null);
            }
            else
            {
                //Always release the session after update
                Serializer.Release(session);
            }

            return ValueTask.CompletedTask;
        }

        /// <summary>
        /// Updates a mondified session on release, and optionally updates 
        /// its id
        /// </summary>
        /// <param name="session"></param>
        /// <param name="newId">The session's new id</param>
        /// <returns>A task that completes when the session update is complete</returns>
        /// <remarks>
        /// Unless overridden
        /// </remarks>
        protected virtual async Task UpdateSessionAndIdAsync(TSession session, string? newId)
        {
            try
            {
                //Get the session's data
                IDictionary<string, string> sessionData = session.GetSessionData();

                //Update the session's data async
                await Cache.AddOrUpdateObjectAsync(session.SessionID, newId, sessionData);

                /*
                 * If the session id changes, the old sesion can be invalidated
                 * and the session will be recovered from cache on load
                 */

                if (newId != null)
                {
                    session.Destroy(null);
                }
                else
                {
                    session.SessionUpdateComplete();
                }
            }
            catch (Exception ex)
            {
                Log.Error("Exception raised during session update, ID {id} NewID {nid}\n{ex}", session.SessionID, newId, ex);

                //Destroy the session with an error
                session.Destroy(ex);
            }
            finally
            {
                //Always release the session after update
                Serializer.Release(session);
            }
        }

        /// <summary>
        /// Delets a session when the session has the <see cref="SessionStatus.Delete"/>
        /// flag set
        /// </summary>
        /// <param name="session">The session to delete</param>
        /// <returns>A task that completes when the session is destroyed</returns>
        protected virtual async Task DeleteSessionAsync(TSession session)
        {
            Exception? cause = null;

            try
            {
                //Update the session's data async
                _ = await Cache.DeleteObjectAsync(session.SessionID);
            }
            catch (Exception ex)
            {
                Log.Error("Exception raised during session delete, ID {id}\n{ex}", session.SessionID, ex);
                cause = ex;
            }
            finally
            {
                //Always destroy the session
                session.Destroy(cause);

                //Release the session now that delete has been set
                Serializer.Release(session);
            }
        }
    }
}
