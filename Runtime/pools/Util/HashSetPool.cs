using System.Collections.Generic;

namespace CW.Extensions.Pooling
{
    public class HashSetPool<T> : StaticMemoryPool<HashSet<T>>
    {
        static HashSetPool<T> _instance = new HashSetPool<T>();

        public HashSetPool()
        {
            OnSpawnMethod = OnSpawned;
            OnDespawnedMethod = OnDespawned;
        }

        public static HashSetPool<T> Instance
        {
            get { return _instance; }
        }

        static void OnSpawned(HashSet<T> items)
        {
            if (items.Count > 0)
            {
                throw new PoolingException();
            }
        }

        static void OnDespawned(HashSet<T> items)
        {
            items.Clear();
        }
    }
}
