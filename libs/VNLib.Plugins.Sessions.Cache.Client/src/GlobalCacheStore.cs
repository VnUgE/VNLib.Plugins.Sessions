﻿/*
* Copyright (c) 2022 Vaughn Nugent
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

namespace VNLib.Plugins.Sessions.Cache.Client
{
    /// <summary>
    /// A wrapper class to provide a <see cref="IRemoteCacheStore"/> from 
    /// a <see cref="IGlobalCacheProvider"/> client instance
    /// </summary>
    public sealed class GlobalCacheStore : IRemoteCacheStore
    {
        private readonly IGlobalCacheProvider _cache;
        
        public GlobalCacheStore(IGlobalCacheProvider globalCache)
        {
            _cache = globalCache ?? throw new ArgumentNullException(nameof(globalCache));
        }

        ///<inheritdoc/>
        public Task AddOrUpdateObjectAsync<T>(string objectId, string? newId, T obj, CancellationToken cancellationToken = default)
        {
            return _cache.AddOrUpdateAsync(objectId, newId, obj, cancellationToken);
        }

        ///<inheritdoc/>
        public Task DeleteObjectAsync(string objectId, CancellationToken cancellationToken = default)
        {
            return _cache.DeleteAsync(objectId, cancellationToken);
        }

        ///<inheritdoc/>
        public Task<T?> GetObjectAsync<T>(string objectId, CancellationToken cancellationToken = default)
        {
            return _cache.GetAsync<T>(objectId, cancellationToken);
        }
    }
}