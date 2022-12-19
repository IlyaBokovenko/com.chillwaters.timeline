using System.Collections.Generic;

namespace CW.Extensions.Pooling
{
    public class DictionaryPool2<TKey, TValue> : MemoryPool<Dictionary<TKey, TValue>>, IPoolStat
    {
        public string Label { get; private set; }

        public DictionaryPool2<TKey, TValue> Labeled(string label)
        {
            Label = label;
            PoolRegistry.Register(this);
            return this;
        }

        public static DictionaryPool2<TKey, TValue> Create(MemoryPoolSettings settings = null, int initialCapacity = 0)
        {
            return new DictionaryPool2<TKey, TValue>(new Factory(initialCapacity), settings??MemoryPoolSettings.Default);
        }

        private DictionaryPool2(IFactory<Dictionary<TKey, TValue>> factory, MemoryPoolSettings settings) : base(factory, settings)
        {
        }

        private int _maxActive;
        private int _maxCount;

        protected override void Reinitialize(Dictionary<TKey, TValue> item)
        {
#if DEBUG_CORE_POOLS
            _maxActive = Math.Max(_maxActive, NumActive);
#endif
            item.Clear();
        }

#if DEBUG_CORE_POOLS
        protected override void OnDespawned(Dictionary<TKey, TValue> item)
        {
            _maxCount = Math.Max(_maxCount, item.Count);
        }
#endif
        
#region IPoolStat
        string IPoolStat.Id => Label;
        string IPoolStat.Stat => $"total: {NumTotal} max active: {_maxActive} now active: {NumActive} max length:{_maxCount}";
#endregion

        private class Factory : IFactory<Dictionary<TKey, TValue>>
        {
            private readonly int InitialCapacity;

            public Factory(int initialCapacity)
            {
                InitialCapacity = initialCapacity;
            }

            public Dictionary<TKey, TValue> Create()
            {
                return new Dictionary<TKey, TValue>(InitialCapacity);
            }
        }
    }
}