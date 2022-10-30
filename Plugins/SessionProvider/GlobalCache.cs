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
