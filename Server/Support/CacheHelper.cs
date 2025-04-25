using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace IPC_Demo;

/// <summary>
/// A hand-rolled replacement for <see cref="System.Runtime.Caching.MemoryCache"/> that offers generic support.
/// </summary>
/// <typeparam name="T"></typeparam>
/// <remarks>The default timer interval is 2 seconds.</remarks>
public class CacheHelper<T> : IDisposable
{
    public event ItemEvictedHandler? ItemEvicted;
    public event ItemUpdatedHandler? ItemUpdated;
    public delegate void ItemEvictedHandler(EvictionInfo<T> evictionInfo);
    public delegate void ItemUpdatedHandler(ObjectInfo<T> updateInfo);
    readonly Dictionary<string, CacheItem<T>> _cache = new Dictionary<string, CacheItem<T>>();
    readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(2);
    Timer? _evictionTimer = null;
    bool _disposed = false;

    #region [Constructors]
    public CacheHelper()
    {
        _evictionTimer = new Timer(EvictExpiredItems, null, _checkInterval, _checkInterval);
    }

    public CacheHelper(TimeSpan checkInterval)
    {
        if (checkInterval == TimeSpan.Zero || checkInterval == TimeSpan.MinValue)
            checkInterval = TimeSpan.FromSeconds(1);

        _evictionTimer = new Timer(EvictExpiredItems, null, checkInterval, checkInterval);
    }
    #endregion

    /// <summary>
    /// Event for object eviction.
    /// </summary>
    /// <param name="evictionInfo"><see cref="EvictionInfo{T}"/></param>
    protected virtual void OnItemEvicted(EvictionInfo<T> evictionInfo) => ItemEvicted?.Invoke(evictionInfo);

    /// <summary>
    /// Event for object update.
    /// </summary>
    /// <param name="updateInfo"><see cref="ObjectInfo{T}"/></param>
    protected virtual void OnItemUpdated(ObjectInfo<T> updateInfo) => ItemUpdated?.Invoke(updateInfo);

    /// <summary>
    /// Adds or updates any object in the <see cref="_cache"/>.
    /// </summary>
    /// <param name="key">the key name</param>
    /// <param name="value">the object to cache</param>
    /// <param name="timeToLive">how long to keep until expiry</param>
    public void AddOrUpdate(string key, T? value, TimeSpan timeToLive)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (timeToLive == TimeSpan.MinValue || timeToLive.Ticks <= 1)
            timeToLive = TimeSpan.FromMilliseconds(1);

        DateTime expire = DateTime.Now.Add(timeToLive);

        lock (_cache)
        {
            if (_cache.ContainsKey(key))
            {
                _cache[key].Value = value;
                _cache[key].ExpirationTime = expire;
                _cache[key].TimeToLive = timeToLive;
                OnItemUpdated(new ObjectInfo<T>(key, value, expire));
            }
            else
            {
                _cache[key] = new CacheItem<T>
                {
                    Value = value,
                    ExpirationTime = expire,
                    TimeToLive = timeToLive
                };
            }
        }
    }

    /// <summary>
    /// Adds or updates any object in the <see cref="_cache"/>.
    /// </summary>
    /// <param name="key">the key name</param>
    /// <param name="value">the object to cache</param>
    /// <param name="timeToExpire">the date when the object will expire</param>
    public void AddOrUpdate(string key, T? value, DateTime timeToExpire)
    {
        if (string.IsNullOrEmpty(key))
            return;

        if (timeToExpire == DateTime.MinValue || timeToExpire <= DateTime.Now)
            timeToExpire = DateTime.Now.AddMilliseconds(1);

        DateTime expire = new DateTime(timeToExpire.Ticks);
        TimeSpan ttl = expire - DateTime.Now;

        lock (_cache)
        {
            if (_cache.ContainsKey(key))
            {
                _cache[key].Value = value;
                _cache[key].ExpirationTime = expire;
                _cache[key].TimeToLive = ttl;
                OnItemUpdated(new ObjectInfo<T>(key, value, expire));
            }
            else
            {
                _cache[key] = new CacheItem<T>
                {
                    Value = value,
                    ExpirationTime = expire,
                    TimeToLive = ttl
                };
            }
        }
    }

    /// <summary>
    /// Fetches the matching <typeparamref name="T"/> for the given <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key name</param>
    /// <remarks>The <see cref="CacheItem{T}.ExpirationTime"/> will be updated if the <paramref name="key"/> is found.</remarks>
    public T? Get(string key)
    {
        if (string.IsNullOrEmpty(key))
            return default;

        lock (_cache)
        {
            if (_cache.ContainsKey(key))
            {
                // Update the item's expiration and return its value.
                _cache[key].ExpirationTime = DateTime.Now.Add(_cache[key].TimeToLive);
                return _cache[key].Value;
            }
        }
        return default;
    }

    /// <summary>
    /// Fetches the expiration for the provided <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key name</param>
    /// <returns>expiration <see cref="DateTime"/> if found, null otherwise</returns>
    public DateTime? GetExpiration(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var ci))
                return ci.ExpirationTime;
        }
        return null;
    }

    /// <summary>
    /// Fetches the time-to-live for the provided <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key name</param>
    /// <returns>expiration <see cref="TimeSpan"/> if found, null otherwise</returns>
    public TimeSpan? GetTimeToLive(string key)
    {
        if (string.IsNullOrEmpty(key))
            return null;

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var ci))
                return ci.TimeToLive;
        }
        return null;
    }

    /// <summary>
    /// Checks <see cref="_cache"/> for any vague match to the specified <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key to find</param>
    public bool Contains(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return _cache.Any(k => k.Key.Contains(key));
    }

    /// <summary>
    /// Checks <see cref="_cache"/> for any exact match to the specified <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key to find</param>
    public bool Equals(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        return _cache.Any(k => k.Key.Equals(key));
    }

    /// <summary>
    /// Removes any <see cref="CacheItem{T}"/> who matches the specified <paramref name="key"/>.
    /// </summary>
    /// <param name="key">the key to remove</param>
    public bool Remove(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        lock (_cache)
        {
            if (_cache.TryGetValue(key, out var cacheItem))
            {
                _cache.Remove(key);
                // User removed, so fire the event with the reason "Manual"
                OnItemEvicted(new EvictionInfo<T>(key, cacheItem.Value, cacheItem.ExpirationTime, "Manual"));
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Fetches any keys present in the <see cref="_cache"/>.
    /// </summary>
    /// <returns><see cref="List{T}"/></returns>
    public List<string> GetAllKeys()
    {
        lock (_cache)
        {
            return _cache.Keys.ToList();
        }
    }

    /// <summary>
    /// Fetches all objects in the <see cref="_cache"/>.
    /// </summary>
    /// <returns><see cref="IEnumerable{T}"/></returns>
    public IEnumerable<KeyValuePair<string, CacheItem<T>>>? GetCacheAsEnumerable()
    {
        lock (_cache)
        {
            return _cache.AsEnumerable();
        }
    }

    /// <summary>
    /// Timer callback event for <see cref="Dictionary{TKey, TValue}"/> cache.
    /// </summary>
    void EvictExpiredItems(object? state)
    {
        List<string> expiredKeys = new();

        lock (_cache)
        {
            foreach (var entry in _cache)
            {
                if (entry.Value.ExpirationTime <= DateTime.Now)
                {
                    expiredKeys.Add(entry.Key);
                }
            }
            foreach (var key in expiredKeys)
            {
                var cacheItem = _cache[key];
                _cache.Remove(key);
                // Fire the eviction event when an item expires
                OnItemEvicted(new EvictionInfo<T>(key, cacheItem.Value, cacheItem.ExpirationTime, "Expired"));
            }
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this); // prevent finalizer
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                if (_evictionTimer != null)
                {
                    _cache.Clear();
                    Debug.WriteLine($"[DEBUG] {nameof(_cache)} cleared during disposal");
                    _evictionTimer.Dispose();
                    _evictionTimer = null;
                }
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// In the event that a finalizer is not called.
    /// </summary>
    ~CacheHelper() => Dispose(false);
}

#region [Supporting Classes]
/// <summary>
/// Supporting class for <see cref="CacheHelper{T}"/> objects.
/// </summary>
public class CacheItem<T>
{
    /// <summary>
    /// The value object of the cache item.
    /// </summary>
    public T? Value { get; set; }
    /// <summary>
    /// The time when the cache item should be evicted.
    /// </summary>
    public DateTime ExpirationTime { get; set; }
    /// <summary>
    /// This value is stored for the get method only so the expiration can be refreshed after access.
    /// </summary>
    public TimeSpan TimeToLive { get; set; }
}

/// <summary>
/// Supporting class for <see cref="CacheHelper{T}"/> events.
/// </summary>
public class ObjectInfo<T>
{
    public string Key { get; set; }
    public T? Value { get; set; }
    public DateTime ExpirationTime { get; set; }
    public ObjectInfo(string key, T? value, DateTime expirationTime)
    {
        Key = key;
        Value = value;
        ExpirationTime = expirationTime;
    }
}

/// <summary>
/// Inherits from <see cref="ObjectInfo{T}"/>.
/// </summary>
public class EvictionInfo<T> : ObjectInfo<T>
{
    public string Reason { get; set; }
    public EvictionInfo(string key, T? value, DateTime expirationTime, string reason) : base(key, value, expirationTime)
    {
        Reason = reason;
    }
}

/// <summary>
/// Test class for <see cref="CacheHelper{T}"/>.
/// </summary>
public class CachHelperTest
{
    public static void Run()
    {
        _ = Task.Run(async () =>
        {
            Debug.WriteLine("\r\n --== Home-Brew Cache Test ==-- \r\n");
            var cache = new CacheHelper<string>();
            cache.ItemEvicted += OnItemEvicted;
            cache.ItemUpdated += OnItemUpdated;
            try
            {
                cache.AddOrUpdate("key1", Extensions.GenerateKeyCode(), TimeSpan.FromSeconds(3));
                cache.AddOrUpdate("key2", Extensions.GenerateKeyCode(), TimeSpan.FromHours(24));
                cache.AddOrUpdate("key3", Extensions.GenerateKeyCode(), TimeSpan.FromDays(30));
                cache.AddOrUpdate("key4", Extensions.GenerateKeyCode(), DateTime.Now.AddMinutes(1));
                cache.AddOrUpdate("key5", Extensions.GenerateKeyCode(), TimeSpan.FromSeconds(7));
                var keys = cache.GetAllKeys();
                Debug.WriteLine($"[DEBUG] Current cache keys: {string.Join(", ", keys)}");
                await Task.Delay(6000);

                var key5 = cache.Get("key5"); // refresh the expire by fetching
                if (string.IsNullOrEmpty(cache.Get("unknown")))
                    Debug.WriteLine($"Key \"unknown\" does not exist.");

                var dt = cache.GetExpiration("key5");
                if (dt != null)
                    Debug.WriteLine($"\r\n\"key5\" will expire at {dt.Value.ToLongTimeString()} on {dt.Value.ToLongDateString()}");

                await Task.Delay(6000);

                Debug.WriteLine($"[DEBUG] The current cache does {(cache.Contains("key1") ? "" : "not ")}contain the key \"key1\"");
                Debug.WriteLine($"[DEBUG] The current cache does {(cache.Contains("key2") ? "" : "not ")}contain the key \"key2\"");
                Debug.WriteLine($"[DEBUG] The current cache does {(cache.Contains("key3") ? "" : "not ")}contain the key \"key3\"");
                Debug.WriteLine($"[DEBUG] The current cache does {(cache.Contains("key5") ? "" : "not ")}contain the key \"key5\"");
                Debug.WriteLine("");

                cache.Remove("key2");

                foreach (var ci in cache.GetCacheAsEnumerable())
                {
                    Debug.WriteLine($"[DEBUG] Cache item {ci.Key} value: {ci.Value.Value}");
                }
            }
            finally
            {
                await Task.Delay(4000);
                cache.Dispose();
            }
        });
    }

    #region [Cache Events]
    static void OnItemUpdated(ObjectInfo<string> info) => Debug.WriteLine(
        $" - Cache item updated: Key='{info.Key}'\r\n" +
        $" - Value='{info.Value}'\r\n" +
        $" - Expiration='{info.ExpirationTime}'\r\n");

    static void OnItemEvicted(EvictionInfo<string> info) => Debug.WriteLine(
        $" - Cache item evicted: Key='{info.Key}'\r\n" +
        $" - Value='{info.Value}'\r\n" +
        $" - Reason='{info.Reason}'\r\n" +
        $" - Expiration='{info.ExpirationTime}'\r\n");
    #endregion
}
#endregion