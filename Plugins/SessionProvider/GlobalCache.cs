/*
* Copyright (c) 2022 Vaughn Nugent
* 
* Library: VNLib
* Package: SessionProvider
* File: GlobalCache.cs 
*
* GlobalCache.cs is part of SessionProvider which is part of the larger 
* VNLib collection of libraries and utilities.
*
* SessionProvider is free software: you can redistribute it and/or modify 
* it under the terms of the GNU Affero General Public License as 
* published by the Free Software Foundation, either version 3 of the
* License, or (at your option) any later version.
*
* SessionProvider is distributed in the hope that it will be useful,
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

//using VNLib.Data.Caching;
//using VNLib.Net.Messaging.FBM.Client;
//using VNLib.Net.Messaging.FBM.Client.Exceptions;


namespace VNLib.Plugins.Essentials.Sessions
{
    /*internal class GlobalCache : IGlobalCacheProvider
    {
        private readonly FBMClient Client;
        private readonly TimeSpan OperationTimeout;

        public GlobalCache(FBMClient cacheProvider, TimeSpan cancellation)
        {
            this.Client = cacheProvider;
            this.OperationTimeout = cancellation;
        }

        //If the wait handle will block, the client is connected
        bool IGlobalCacheProvider.IsConnected => !Client.ConnectionStatusHandle.WaitOne(0);

        async Task IGlobalCacheProvider.DeleteAsync(string key)
        {
            if (OperationTimeout > TimeSpan.Zero && OperationTimeout < TimeSpan.MaxValue)
            {
                CancellationTokenSource cts = new(OperationTimeout);
                try
                {
                    //Delete value
                    await Client.DeleteObjectAsync(key, cts.Token);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("The operation has been cancelled, due to a timeout");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            else
            {
                try
                {
                    //Delete value
                    await Client.DeleteObjectAsync(key);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
            }
        }

        async Task<T> IGlobalCacheProvider.GetAsync<T>(string key)
        {
            if (OperationTimeout > TimeSpan.Zero && OperationTimeout < TimeSpan.MaxValue)
            {
                CancellationTokenSource cts = new(OperationTimeout);
                try
                {
                    //Try to get the value
                    return await Client.GetObjectAsync<T>(key, cts.Token);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("The operation has been cancelled, due to a timeout");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            else
            {
                try
                {
                    //Try to get the value
                    return await Client.GetObjectAsync<T>(key);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
            }
        }

        async Task IGlobalCacheProvider.SetAsync<T>(string key, T value)
        {
            if (OperationTimeout > TimeSpan.Zero && OperationTimeout < TimeSpan.MaxValue)
            {
                CancellationTokenSource cts = new(OperationTimeout);
                try
                {
                    await Client.AddOrUpdateObjectAsync(key, null, value, cts.Token);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException("The operation has been cancelled, due to a timeout");
                }
                finally
                {
                    cts.Dispose();
                }
            }
            else
            {
                try
                {
                    await Client.AddOrUpdateObjectAsync(key, null, value);
                }
                catch (FBMException fbm)
                {
                    //Catch fbm excpetions and wrap them in global cache exception
                    throw new GlobalCacheException("Failed to delete cache record, see inner exception", fbm);
                }
            }
        }
    }*/
}
