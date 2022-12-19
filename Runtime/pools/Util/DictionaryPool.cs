using System.Collections.Generic;

namespace CW.Extensions.Pooling
{
    public class DictionaryPool<TKey, TValue> : StaticMemoryPool<Dictionary<TKey, TValue>>
    {
        static DictionaryPool<TKey, TValue> _instance = new DictionaryPool<TKey, TValue>();

        public DictionaryPool()
        {
            OnSpawnMethod = OnSpawned;
            OnDespawnedMethod = OnDespawned;
        }

        public static DictionaryPool<TKey, TValue> Instance
        {
            get { return _instance; }
        }

        static void OnSpawned(Dictionary<TKey, TValue> items)
        {
            if (items.Count > 0)
            {
                throw new PoolingException();
            }
        }

        static void OnDespawned(Dictionary<TKey, TValue> items)
        {
            items.Clear();
        }
    }
}

