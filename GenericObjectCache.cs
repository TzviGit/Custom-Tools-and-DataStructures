using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace Infra
{

    /// <summary>
    /// Generic Wrapper class for in-memory caching based on <see cref="MemoryCache"/>.
    /// </summary>
    /// <typeparam name="TKey">The type of the cache keys.</typeparam>
    /// <typeparam name="TValue">The type of the cache values.</typeparam>
    public class GenericObjectCache<TKey, TValue> : IEnumerable<TValue>
    {
        #region Fields       

        /// <summary>
        /// the default time before item will be expired if not accessed
        /// </summary>
        private readonly TimeSpan _defaultExpirationPeriod;

        private MemoryCache _innerCache;
        const string DEFAULT_NAME = "DEFAULT_NAME";

        private readonly object fireUpdateEventLocker = new object();
        private readonly object fireRemoveEventLocker = new object();

        #endregion

        /// <summary>
        /// Gets the name of the Container
        /// </summary>
        public string Name => _innerCache.Name;

        #region Events

        /////// <summary>
        /////// Event triggered when an item is updated to the cache.
        /////// </summary>
        //private readonly EventDistributer<TValue> itemUpdatedEvent = new EventDistributer<TValue>();

        ///// <summary>
        ///// Event triggered when an item is Removed in the cache.
        ///// </summary>
        //private readonly EventDistributer<TValue> itemRemovedEvent = new EventDistributer<TValue>();


        ///// <summary>
        ///// Event triggered when an item is updated to the cache.
        ///// </summary>
        private event Action<TValue> itemUpdatedEvent;

        /// <summary>
        /// Event triggered when an item is Removed in the cache.
        /// </summary>
        private event Action<TValue> itemRemovedEvent;

        public void RegisterForUpdateEvents(Action<TValue> uponUpdate)
        {
            //Default param for processingOptions will be null
            itemUpdatedEvent += uponUpdate;
        }

        public void RegisterForDeletionEvents(Action<TValue> uponDelete)
        {
            //Default param for processingOptions will be null
            itemRemovedEvent += uponDelete;
        }

        public void UnregisterForUpdateEvents(Action<TValue> uponUpdate)
        {
            itemUpdatedEvent -= uponUpdate;
        }


        public void UnregisterForDeletionEvents(Action<TValue> uponDelete)
        {
            itemRemovedEvent -= uponDelete;
        }

        #endregion

        #region Constructor

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericObjectCache{TKey, TValue}"/> class.
        /// </summary>
        /// <param name="defaultExpirationPeriod">the default time before item will be expired if not updated</param>
        public GenericObjectCache(TimeSpan defaultExpirationPeriod)
        : this(defaultExpirationPeriod, DEFAULT_NAME)
        {

        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericObjectCache{TKey, TValue}"/> class. 
        /// </summary>
        /// <param name="defaultExpirationPeriod">the default time before item will be expired if not updated</param>
        /// <param name="cacheName">the name of the cache</param>
        public GenericObjectCache(TimeSpan defaultExpirationPeriod, string cacheName)
        {
            if (string.IsNullOrEmpty(cacheName))
            {
                throw new ArgumentException("name cannot be null or empty string");
            }

            _defaultExpirationPeriod = defaultExpirationPeriod;
            _innerCache = new MemoryCache(cacheName);
        }

        #endregion

        #region Public Methods



        /// <summary>
        /// Returns number of Items currently in the Container
        /// </summary>
        public int Count => _innerCache.Count();

        /// <summary>
        /// Checks if container has item under the given key
        /// </summary>
        /// <param name="key"></param>
        /// <returns>true if container has item under the given key, else false</returns>
        public bool ContainsKey(TKey key)
        {
            var keyString = key.GetHashCode().ToString();
            return _innerCache.Contains(keyString);
        }

        /// <summary>
        /// Tries to remove an item from the cache.
        /// </summary>
        /// <param name="key">The key of the cache item to remove.</param>
        /// <param name="deleted_item">When this method returns, contains the removed item if it exists, otherwise the default value of the type.</param>
        /// <returns><c>true</c> if the item was removed; otherwise, <c>false</c>.</returns>
        public bool TryRemoveItem(TKey key, out TValue deleted_item, bool fireDeletionEvent = true)
        {
            deleted_item = default;
            bool itemWasRemoved = false;

            var keyString = key.GetHashCode().ToString();
            var removedItem = _innerCache.Remove(keyString);

            if (removedItem is TValue)
            {
                deleted_item = (TValue)removedItem;
                itemWasRemoved = true;

                if (fireDeletionEvent)
                {
                    itemRemovedEvent?.Invoke(deleted_item);
                }
            }

            return itemWasRemoved;

        }



        /// <summary>
        /// Tries to get an item from the cache.
        /// </summary>
        /// <param name="key">The key of the cache item to retrieve.</param>
        /// <param name="item">When this method returns, contains the retrieved item if it exists, otherwise the default value of the type.</param>
        /// <returns><c>true</c> if the item was found; otherwise, <c>false</c>.</returns>
        public bool TryGetItem(TKey key, out TValue item)
        {
            bool foundItem = false;
            item = default;

            var keyString = key.GetHashCode().ToString();
            CacheItem itemInCache = _innerCache.GetCacheItem(keyString);

            if (itemInCache != null && itemInCache.Value is TValue)
            {
                item = (TValue)itemInCache.Value;
                foundItem = true;
            }

            return foundItem;


        }




        /// <summary>
        /// Updates an existing cache item or adds a new item with the specified key and value.
        /// </summary>
        /// <param name="key">The key of the cache item to update or add.</param>
        /// <param name="Item">The new value of the cache item.</param>
        /// <param name="expirationTime">The expiration_time of the cache item.</param>          
        public TValue AddOrSetItem(TKey key, TValue Item, TimeSpan expirationTime)
        {
            var keyString = key.GetHashCode().ToString();

            CacheItemPolicy policy = new CacheItemPolicy { RemovedCallback = UponExpirationRemoval, AbsoluteExpiration = DateTimeOffset.UtcNow.Add(expirationTime) };
            _innerCache.Set(keyString, Item, policy);

            lock (fireUpdateEventLocker)
            {
                itemUpdatedEvent?.Invoke(Item);
            }

            return Item;
        }

        /// <summary>
        /// Updates an existing cache item or adds a new item with the specified key and value.
        /// </summary>
        /// <param name="key">The key of the cache item to update or add.</param>
        /// <param name="Item">The new value of the cache item.</param>        
        public void AddOrSetItem(TKey key, TValue Item)
        {
            AddOrSetItem(key, Item, expirationTime: _defaultExpirationPeriod);
        }

        /// <summary>
        /// Tries to Add new Key /Item to the Cache. use this method if you only want to add to container if key does not yet exist.
        /// </summary>
        /// <param name="key">The key of the cache item to update or add.</param>
        /// <param name="Item">The new value of the cache item.</param>
        /// <param name="expirationTime">The expiration_time of the cache item.</param>   
        /// <returns>true if the insertion try succeeds, or false if there is an already an entry in the cache with the same key as key.</returns>
        public bool TryAddItem(TKey key, TValue Item, TimeSpan expirationTime)
        {
            var keyString = key.GetHashCode().ToString();

            CacheItemPolicy policy = new CacheItemPolicy { RemovedCallback = UponExpirationRemoval, AbsoluteExpiration = DateTimeOffset.UtcNow.Add(expirationTime) };
            bool addedSuccessfullly = _innerCache.Add(keyString, Item, policy);

            if (addedSuccessfullly)
            {
                lock (fireUpdateEventLocker)
                {
                    itemUpdatedEvent?.Invoke(Item);
                }
            }

            return addedSuccessfullly;

        }


        /// <summary>
        /// Tries to Add new Key /Item to the Cache. use this method if you only want to add to container if key does not yet exist.
        /// </summary>
        /// <param name="key">The key of the cache item to update or add.</param>
        /// <param name="Item">The new value of the cache item.</param>   
        /// <returns>true if the insertion try succeeds, or false if there is an already an entry in the cache with the same key as key.</returns>
        public bool TryAddItem(TKey key, TValue Item)
        {
            return TryAddItem(key, Item, _defaultExpirationPeriod);

        }

        /// <summary>
        /// Change the expiration time of an item
        /// </summary>
        /// <param name="key">The key of the cache item to update</param>
        /// <param name="newExpirationTime">The new expiration_time of the cache item</param>
        public void ChangeExpirationTime(TKey key, TimeSpan newExpirationTime)
        {
            var keyString = key.GetHashCode().ToString();
            var itemInCache = _innerCache.GetCacheItem(keyString);

            if (itemInCache is CacheItem)
            {
                CacheItemPolicy policy = new CacheItemPolicy { RemovedCallback = UponExpirationRemoval, AbsoluteExpiration = DateTimeOffset.UtcNow.Add(newExpirationTime) };
                _innerCache.Set(itemInCache, policy);
            }
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Function called when internal memoryCache evicts an item from the cache because of Aging (expiration)
        /// </summary>
        /// <param name="arg"></param>
        private void UponExpirationRemoval(CacheEntryRemovedArguments arg)
        {
            //only fire Auto delegate if reason is expiration, not if removed manualy
            if (arg != null && arg.RemovedReason == CacheEntryRemovedReason.Expired)
            {
                if (arg.CacheItem?.Value is TValue removedItem)
                {
                    itemRemovedEvent?.Invoke(removedItem);
                }
            }
        }

        #endregion

        #region IEnumerable Implementation

        /// <summary>
        /// Returns an enumerator that iterates through the cache and skips expired items.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the cache.</returns>
        public IEnumerator<TValue> GetEnumerator()
        {

            foreach (var kvp in _innerCache.ToList())
            {
                if (kvp.Value is TValue val)
                {
                    yield return val;
                }
            }
            //return _NewInnerCache.Select(kvp => (TValue)kvp.Value).GetEnumerator();
        }

        /// <summary>
        /// Returns an enumerator that iterates through the cache and skips expired items.
        /// </summary>
        /// <returns>An enumerator that can be used to iterate through the cache.</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Just refreshes the LastUpdate time of cache object so as to avoid expiration
        /// </summary>
        /// <param name="key">The key of the cache item to Touch</param>
        public void Touch(TKey key)
        {
            ChangeExpirationTime(key, _defaultExpirationPeriod);
        }

        /// <summary>
        /// Empty out the Cache
        /// </summary>
        public void Clear()
        {
            try
            {
                var name = Name;
                _innerCache = new MemoryCache(!string.IsNullOrEmpty(name) ? name : DEFAULT_NAME);

                #region Alternative way
                //var keys = _innerCache.Select(kvp => kvp.Key).ToList();

                //foreach (var k in keys)
                //{
                //    _innerCache.Remove(k);
                //} 
                #endregion
            }
            catch (Exception ex)
            {
                //
            }
        }

        /// <summary>
        /// Returns a List of all non-expired the TValue items in the Cache
        /// </summary>
        /// <returns></returns>
        public List<TValue> GetAllItems()
        {
            // this will Invoke our Overide of the Enumerator Function
            return this.ToList();
        }



        #endregion

    }
}
