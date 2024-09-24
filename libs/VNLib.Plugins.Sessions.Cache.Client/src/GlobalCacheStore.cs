/*
* Copyright (c) 2023 Vaughn Nugent
* 
* Library: VNLib
* Package: VNLib.Plugins.Sessions.Cache.Client
* File: GlobalCacheStore.cs 
*
* GlobalCacheStore.cs is part of VNLib.Plugins.Sessions.Cache.Client which is part of the larger 
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

using VNLib.Data.Caching;
using VNLib.Utils.Logging;

namespace VNLib.Plugins.Sessions.Cache.Client
{

    /// <summary>
    /// A wrapper class to provide a <see cref="IRemoteCacheStore"/> from 
    /// a <see cref="IGlobalCacheProvider"/> client instance
    /// </summary>
    /// <remarks>
    /// Initiailzes a new <see cref="GlobalCacheStore"/> with the backing <see cref="IGlobalCacheProvider"/>
    /// global cache
    /// </remarks>
    /// <param name="globalCache">The backing cache store</param>
    /// <param name="bufferSize">The size of the buffer used to serialize session objects</param>
    /// <param name="debugLog">An optional log provider for writing serializing events to</param>
    /// <exception cref="ArgumentNullException"></exception>
    public sealed class GlobalCacheStore(IGlobalCacheProvider globalCache, int bufferSize, ILogProvider? debugLog) 
        : IRemoteCacheStore
    {
        private readonly IGlobalCacheProvider _cache = globalCache ?? throw new ArgumentNullException(nameof(globalCache));

        private readonly SessionDataSerialzer _serialzer = new(bufferSize, debugLog);

        ///<inheritdoc/>
        public bool IsConnected => _cache.IsConnected;

        ///<inheritdoc/>
        public Task AddOrUpdateObjectAsync<T>(string objectId, string? newId, T obj, CancellationToken cancellationToken = default)
        {
            return _cache.AddOrUpdateAsync(objectId, newId, obj, _serialzer, cancellationToken);
        }
      
        ///<inheritdoc/>
        public Task<bool> DeleteObjectAsync(string objectId, CancellationToken cancellationToken = default)
        {
            return _cache.DeleteAsync(objectId, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<T?> GetObjectAsync<T>(string objectId, CancellationToken cancellationToken = default)
        {
            return _cache.GetAsync<T>(objectId, _serialzer, cancellationToken);
        }
    }
}
