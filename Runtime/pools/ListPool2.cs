using System.Collections.Generic;

namespace CW.Extensions.Pooling
{
    public class ListPool2<T> : MemoryPool<List<T>>, IPoolStat
    {
        public string Label { get; private set; }

        public ListPool2<T> Labeled(string label)
        {
            Label = label;
            PoolRegistry.Register(this);
            return this;
        }

        public static ListPool2<T> Create(MemoryPoolSettings settings = null, int initialCapacity = 0)
        {
            return new ListPool2<T>(new Factory(initialCapacity), settings??MemoryPoolSettings.Default);
        }

        private ListPool2(IFactory<List<T>> factory, MemoryPoolSettings settings) : base(factory, settings)
        {
        }


        private int _maxActive;
        private int _maxCount;

        protected override void Reinitialize(List<T> item)
        {
#if DEBUG_CORE_POOLS
            _maxActive = Math.Max(_maxActive, NumActive);
#endif
                
            item.Clear();
        }

#if DEBUG_CORE_POOLS
        protected override void OnDespawned(List<T> item)
        {
            _maxCount = Math.Max(_maxCount, item.Count);
        }
#endif

#region IPoolStat
        string IPoolStat.Id => Label;
        string IPoolStat.Stat => $"total: {NumTotal} max active: {_maxActive} now active: {NumActive} max length:{_maxCount}";
#endregion

        private class Factory : IFactory<List<T>>
        {
            private readonly int InitialCapacity;

            public Factory(int initialCapacity)
            {
                InitialCapacity = initialCapacity;
            }

            public List<T> Create()
            {
                return new List<T>(InitialCapacity);
            }
        }
        
    }
}