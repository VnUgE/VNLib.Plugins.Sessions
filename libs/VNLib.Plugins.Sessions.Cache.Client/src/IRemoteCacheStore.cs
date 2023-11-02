/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: IRemoteCacheStore.cs 
*
* IRemoteCacheStore.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using System.Threading;
using System.Threading.Tasks;

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// Represents an asynchronous interface to a remote cache store
    /// </summary>
    public interface IRemoteCacheStore
    {
        /// <summary>
        /// Gets an object from the cache provider by key
        /// </summary>
        /// <typeparam name="T">The data type</typeparam>
        /// <param name="objectId">The key/id of the object to recover</param>
        /// <param name="cancellationToken">An optional token to cancel the operation</param>
        /// <returns>A task that resolves the found object or null otherwise</returns>
        Task<T?> GetObjectAsync<T>(string objectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Adds or updates an object in the cache provider by session id and optionally it's new id
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectId">The session id to add or update</param>
        /// <param name="newId">The uniqe id of the item to update</param>
        /// <param name="obj"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        Task AddOrUpdateObjectAsync<T>(string objectId, string? newId, T obj, CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes an object from the cache provider by session id
        /// </summary>
        /// <param name="objectId">The id of the item to delete</param>
        /// <param name="cancellationToken">A token to cancel the operation</param>
        /// <returns>A value that indicates if the item was found and deleted</returns>
        Task<bool> DeleteObjectAsync(string objectId, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets a value that determines if the remote cache store is available
        /// </summary>
        bool IsConnected { get; }
    }
}
