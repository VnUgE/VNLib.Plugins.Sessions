/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Essentials.Sessions.OAuth
* File: OAuth2Session.cs 
*
* OAuth2Session.cs is part of VNLib.Plugins.Essentials.Sessions.OAuth which is part of the larger 
* VNLib collection of libraries and utilities.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is free software: you can redistribute it and/or modify 
* it under the terms of the GNU General Public License as published
* by the Free Software Foundation, either version 2 of the License,
* or (at your option) any later version.
*
* VNLib.Plugins.Essentials.Sessions.OAuth is distributed in the hope that it will be useful,
* but WITHOUT ANY WARRANTY; without even the implied warranty of
* MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
* General Public License for more details.
*
* You should have received a copy of the GNU General Public License 
* along with VNLib.Plugins.Essentials.Sessions.OAuth. If not, see http://www.gnu.org/licenses/.
*/

using System;

using VNLib.Net.Http;
using VNLib.Net.Messaging.FBM.Client;
using VNLib.Plugins.Sessions.Cache.Client;
using VNLib.Plugins.Sessions.Cache.Client.Exceptions;

using static VNLib.Plugins.Essentials.Sessions.ISessionExtensions;

namespace VNLib.Plugins.Essentials.Sessions.OAuth
{
    /// <summary>
    /// The implementation of the OAuth2 session container for HTTP sessions
    /// </summary>
    internal sealed class OAuth2Session : RemoteSession
    {
        private readonly Action<OAuth2Session> InvalidateCache;

        /// <summary>
        /// Initalizes a new <see cref="OAuth2Session"/>
        /// </summary>
        /// <param name="sessionId">The session id (or token)</param>
        /// <param name="client">The <see cref="FBMClient"/> used as the backing cache provider</param>
        /// <param name="backgroundTimeOut">The ammount of time to wait for a background operation (delete, update, get)</param>
        /// <param name="invalidCache">Called when the session has been marked as invalid and the close even hook is being executed</param>
        public OAuth2Session(string sessionId, FBMClient client, TimeSpan backgroundTimeOut, Action<OAuth2Session> invalidCache)
            : base(sessionId, client, backgroundTimeOut)
        {
            InvalidateCache = invalidCache;
            IsInvalid = false;
        }

        public bool IsInvalid { get; private set; }


        ///<inheritdoc/>
        ///<exception cref="NotSupportedException"></exception>
        public override string Token
        {
            get => throw new NotSupportedException("Token property is not supported for OAuth2 sessions");
            set => throw new NotSupportedException("Token property is not supported for OAuth2 sessions");
        }

        ///<inheritdoc/>
        protected override void IndexerSet(string key, string value)
        {
            //Guard protected entires
            switch (key)
            {
                case TOKEN_ENTRY:
                case LOGIN_TOKEN_ENTRY:
                    throw new InvalidOperationException("Token entry may not be changed!");
            }
            base.IndexerSet(key, value);
        }
        ///<inheritdoc/>
        ///<exception cref="SessionStatusException"></exception>
        public override async Task WaitAndLoadAsync(IHttpEvent entity, CancellationToken token = default)
        {
            //Wait to enter lock
            await base.WaitAndLoadAsync(entity, token);
            if (IsInvalid)
            {
                //Release lock
                MainLock.Release();
                throw new SessionStatusException("The session has been invalidated");
            }
            //Set session type
            if (IsNew)
            {
                SessionType = SessionType.OAuth2;
            }
        }
        ///<inheritdoc/>
        protected override async ValueTask<Task?> UpdateResource(bool isAsync, IHttpEvent state)
        {
            Task? result = null;
            //Invalid flag is set, so exit
            if (IsInvalid)
            {
                result = Task.CompletedTask;
            }
            //Check flags in priority level, Invalid is highest state priority
            else if (Flags.IsSet(INVALID_MSK))
            {
                //Clear all stored values
                DataStore!.Clear();
                //Delete the entity syncronously
                await ProcessDeleteAsync();
                //Set invalid flag
                IsInvalid = true;
                //Invlidate cache
                InvalidateCache(this);
                result = Task.CompletedTask;
            }
            else if (Flags.IsSet(MODIFIED_MSK))
            {
                //Send update to server
                result = Task.Run(ProcessUpdateAsync);
            }
            
            //Clear all flags
            Flags.ClearAll();
            
            return result;
        }
    }
}
