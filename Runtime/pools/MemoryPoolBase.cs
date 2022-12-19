using System;
using System.Collections.Generic;

namespace CW.Extensions.Pooling
{
    public enum PoolExpandMethods
    {
        OneAtATime,
        Double,
        Disabled
    }

    [Serializable]
    public class MemoryPoolSettings
    {
        public int InitialSize;
        public int MaxSize;
        public PoolExpandMethods ExpandMethod;

        public MemoryPoolSettings()
        {
            InitialSize = 0;
            MaxSize = int.MaxValue;
            ExpandMethod = PoolExpandMethods.OneAtATime;
        }

        public MemoryPoolSettings(int initialSize, int maxSize, PoolExpandMethods expandMethod)
        {
            InitialSize = initialSize;
            MaxSize = maxSize;
            ExpandMethod = expandMethod;
        }

        public static readonly MemoryPoolSettings Default = new MemoryPoolSettings();
    }

    public class MemoryPoolBase<TContract> : IMemoryPool
    {
        Stack<TContract> _inactiveItems;
        IFactory<TContract> _factory;
        MemoryPoolSettings _settings;

        int _activeCount;

        public MemoryPoolBase(IFactory<TContract> factory, MemoryPoolSettings settings)
        {
            Init(factory, settings);
        }
        
        protected MemoryPoolBase()
        {
        }

        protected void Init(IFactory<TContract> factory, MemoryPoolSettings settings)
        {
            _settings = settings ?? MemoryPoolSettings.Default;
            _factory = factory;
            _inactiveItems = new Stack<TContract>(_settings.InitialSize);

            for (int i = 0; i < _settings.InitialSize; i++)
            {
                _inactiveItems.Push(AllocNew());
            }
        }

        public IEnumerable<TContract> InactiveItems => _inactiveItems;

        public int NumTotal => NumInactive + NumActive;

        public int NumInactive => _inactiveItems.Count;

        public int NumActive => _activeCount;

        public Type ItemType => typeof(TContract);

        void IMemoryPool.Despawn(object item)
        {
            Despawn((TContract)item);
        }

        public void Despawn(TContract item)
        {
            // Too expensive
            // if (_inactiveItems.Contains(item))
            // {
            //     throw new PoolingException(string.Format("Tried to return an item to pool {0} twice", args));
            // }

            _activeCount--;

            _inactiveItems.Push(item);


            OnDespawned(item);

            if (_inactiveItems.Count > _settings.MaxSize)
            {
                Resize(_settings.MaxSize);
            }
        }

        TContract AllocNew()
        {
            try
            {
                var item = _factory.Create();

                if (item == null)
                {
                    throw  new PoolingException(
                        $"Factory '{_factory.GetType()}' returned null value when creating via {GetType()}!");
                }

                OnCreated(item);

                return item;
            }
            catch (Exception e)
            {
                throw new PoolingException(
                    $"Error during construction of type '{typeof(TContract)}' via {GetType()}.Create method!", e);
            }
        }

        public void Clear()
        {
            Resize(0);
        }

        public void ShrinkBy(int numToRemove)
        {
            Resize(_inactiveItems.Count - numToRemove);
        }

        public void ExpandBy(int numToAdd)
        {
            Resize(_inactiveItems.Count + numToAdd);
        }

        protected TContract GetInternal()
        {
            if (_inactiveItems.Count == 0)
            {
                ExpandPool();
                if (_inactiveItems.Count == 0)
                {
                    throw new PoolingException();
                }
            }

            var item = _inactiveItems.Pop();
            _activeCount++;
            OnSpawned(item);
            return item;
        }

        public void Resize(int desiredPoolSize)
        {
            if (_inactiveItems.Count == desiredPoolSize)
            {
                return;
            }

            if (_settings.ExpandMethod == PoolExpandMethods.Disabled)
            {
                throw new PoolingException(
                    $"Pool factory '{GetType()}' attempted resize but pool set to fixed size of '{_inactiveItems.Count}'!");
            }

            if (!(desiredPoolSize >= 0))
            {
                throw new PoolingException("Attempted to resize the pool to a negative amount");
            }

            while (_inactiveItems.Count > desiredPoolSize)
            {
                OnDestroyed(_inactiveItems.Pop());
            }

            while (desiredPoolSize > _inactiveItems.Count)
            {
                _inactiveItems.Push(AllocNew());
            }

            if (_inactiveItems.Count != desiredPoolSize)
            {
                throw new PoolingException();
            }
        }

        void ExpandPool()
        {
            switch (_settings.ExpandMethod)
            {
                case PoolExpandMethods.Disabled:
                {
                    throw new PoolingException(
                        $"Pool factory '{GetType()}' exceeded its fixed size of '{_inactiveItems.Count}'!"
                        );
                }
                case PoolExpandMethods.OneAtATime:
                {
                    ExpandBy(1);
                    break;
                }
                case PoolExpandMethods.Double:
                {
                    if (NumTotal == 0)
                    {
                        ExpandBy(1);
                    }
                    else
                    {
                        ExpandBy(NumTotal);
                    }
                    break;
                }
                default:
                {
                    throw new PoolingException();
                }
            }
        }

        protected virtual void OnDespawned(TContract item)
        {
            // Optional
        }

        protected virtual void OnSpawned(TContract item)
        {
            // Optional
        }

        protected virtual void OnCreated(TContract item)
        {
            // Optional
        }

        protected virtual void OnDestroyed(TContract item)
        {
            // Optional
        }
    }
}
