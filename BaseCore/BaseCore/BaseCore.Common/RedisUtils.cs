using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;


namespace BaseCore.Common
{
    public static class RedisUtils
    {
        public static async void DeleteAllCacheAsyn(this IConnectionMultiplexer muxer, IConfiguration configuration, string pattern)
        {
            //TODO: change this If use cluster redis
            var defaultDatabase = !string.IsNullOrEmpty(configuration["Redis:DefaultDatabase"])
                ? int.Parse(configuration["Redis:DefaultDatabase"]) : 10;
            var server = muxer.GetServer(configuration["Redis:ConnectionString"]);
            var db = muxer.GetDatabase();
            foreach (var key in server.Keys(defaultDatabase, pattern: "*" + pattern + "*"))
            {
                await db.KeyDeleteAsync(key);
            }
        }
    
        /// <summary>  
        /// Gets or set cache asynchronous.  
        /// </summary>  
        /// <typeparam name="TResult">The type of the result.</typeparam>  
        /// <param name="cache">The distributed cache interface.</param>  
        /// <param name="key">The key for storing cache.</param>  
        /// <param name="storingItem">The storing item.</param>  
        /// <param name="configuration">configuration The cache rule (null: meaning no expire cache, otherwise have expire time cache)</param>  
        /// <returns>  
        /// The result for stored item.  
        /// </returns>  
        public static async Task<TResult> GetOrSetCacheAsync<TResult>(this IDistributedCache cache, string key,
            Func<TResult> storingItem, IConfiguration configuration = null)
            where TResult : class
        {
            var cachedValue = await cache.GetCacheValueAsync<TResult>(key);
            if (cachedValue == null)
            {
                return await cache.StoreValueAsync(key, storingItem, configuration);
            }

            return cachedValue;
        }

        public static TResult GetOrSetCache<TResult>(this IDistributedCache cache, string key,
            Func<TResult> storingItem, IConfiguration configuration = null)
            where TResult : class
        {
            var cachedValue = cache.GetCacheValue<TResult>(key);
            if (cachedValue == null)
            {
                return cache.StoreValue(key, storingItem, configuration);
            }

            return cachedValue;
        }

        /// <summary>  
        /// Gets the cache value asynchronous.  
        /// </summary>  
        /// <typeparam name="TResult">The type of the result.</typeparam>  
        /// <param name="cache">The cache.</param>  
        /// <param name="key">The key for caching.</param>  
        /// <returns>Value of cached item.</returns>  
        public static TResult GetCacheValue<TResult>(this IDistributedCache cache, string key)
            where TResult : class
        {
            try
            {
                var cachedValue = cache.GetString(key.ToLower(CultureInfo.InvariantCulture));
                if (string.IsNullOrEmpty(cachedValue))
                {
                    return null;
                }

                return JsonConvert.DeserializeObject<TResult>(cachedValue);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        /// <summary>  
        /// Gets the cache value asynchronous.  
        /// </summary>  
        /// <typeparam name="TResult">The type of the result.</typeparam>  
        /// <param name="cache">The cache.</param>  
        /// <param name="key">The key for caching.</param>  
        /// <returns>Value of cached item.</returns>  
        public static async Task<TResult> GetCacheValueAsync<TResult>(this IDistributedCache cache, string key)
            where TResult : class
        {
            var cachedValue = await cache.GetStringAsync(key.ToLower(CultureInfo.InvariantCulture));
            if (string.IsNullOrEmpty(cachedValue))
            {
                return null;
            }

            return JsonConvert.DeserializeObject<TResult>(cachedValue);
        }

        /// <summary>  
        /// Stores the value in cache.  
        /// </summary>  
        /// <typeparam name="TResult">The type of the result.</typeparam>  
        /// <param name="cache">The cache.</param>  
        /// <param name="key">The key for caching.</param>  
        /// <param name="storingItem">The storing item.</param>  
        /// <param name="configuration">The cache rule.</param>  
        /// <returns>  
        /// Value of caching item.  
        /// </returns>  
        /// <exception cref="ArgumentNullException">key</exception>  
        public static async Task<TResult> StoreValueAsync<TResult>(this IDistributedCache cache, string key,
            Func<TResult> storingItem, IConfiguration configuration = null)
            where TResult : class
        {
            var storingValue = storingItem();
            if (storingValue != null && storingValue != default(TResult))
            {
                var redisKey = key.ToLower(CultureInfo.InvariantCulture);
                var value = JsonConvert.SerializeObject(storingValue);

                if (!value.Equals("[]")) //value is empty no cache
                {
                    if (configuration != null)
                    {
                        var expireMinute = !string.IsNullOrEmpty(configuration["Redis:CachingExpireMinute"]) ? int.Parse(configuration["Redis:CachingExpireMinute"]) : 1;
                        var cacheRule = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expireMinute)
                        };
                        await cache.SetStringAsync(redisKey, value, cacheRule);
                    }
                    else
                    {
                        await cache.SetStringAsync(redisKey, value);
                    }
                }
            }
            return storingValue;
        }

        /// <summary>  
        /// Stores the value in cache.  
        /// </summary>  
        /// <typeparam name="TResult">The type of the result.</typeparam>  
        /// <param name="cache">The cache.</param>  
        /// <param name="key">The key for caching.</param>  
        /// <param name="storingItem">The storing item.</param>  
        /// <param name="configuration">The cache rule.</param>  
        /// <returns>  
        /// Value of caching item.  
        /// </returns>  
        /// <exception cref="ArgumentNullException">key</exception>  
        public static TResult StoreValue<TResult>(this IDistributedCache cache, string key,
            Func<TResult> storingItem, IConfiguration configuration = null)
            where TResult : class
        {
            var storingValue = storingItem();
            if (storingValue != null && storingValue != default(TResult))
            {
                var redisKey = key.ToLower(CultureInfo.InvariantCulture);
                var value = JsonConvert.SerializeObject(storingValue);

                if (!value.Equals("[]")) //value is empty no cache
                {
                    if (configuration != null)
                    {
                        var expireMinute = !string.IsNullOrEmpty(configuration["Redis:CachingExpireMinute"]) ? int.Parse(configuration["Redis:CachingExpireMinute"]) : 1;
                        var cacheRule = new DistributedCacheEntryOptions
                        {
                            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(expireMinute)
                        };
                        cache.SetString(redisKey, value, cacheRule);
                    }
                    else
                    {
                        cache.SetString(redisKey, value);
                    }
                }
            }
            return storingValue;
        }

        public static string GetPropertyValues(this IDistributedCache cache, Object obj)
        {
            var listItem = new Dictionary<string, string>();
            if (obj != null)
            {
                Type t = obj.GetType();
                PropertyInfo[] props = t.GetProperties().Where(x => x.Name != "Id").ToArray();
                foreach (var prop in props)
                {
                    var value = prop.GetValue(obj);
                    if (prop.GetIndexParameters().Length == 0 && value != null)
                    {
                        listItem[prop.Name] = value.ToString();
                    }
                }
            }
            return JsonConvert.SerializeObject(listItem);
        }
    }
}
