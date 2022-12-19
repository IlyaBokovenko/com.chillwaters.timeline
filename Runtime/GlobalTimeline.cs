using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using CW.Core.Hash;
using CW.Extensions.Pooling;
using Newtonsoft.Json;

#if TIMELINE_UNITY_LOGS
using UnityEngine;
#endif

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Timeline.EditorTests")]

namespace CW.Core.Timeline
{
    public struct TimelinePoolingPolicy
    {
        public CollectionPoolPolicy SubscriptionListsPolicy;
        public CollectionPoolPolicy SubscriptionDictionariesPolicy;
        public CollectionPoolPolicy SubscriptionBigListsPolicy;
        public CollectionPoolPolicy PushInfosPolicy;
        
        public static readonly TimelinePoolingPolicy Client =
            new TimelinePoolingPolicy
            {
                SubscriptionListsPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 10}, CollectionCapacity = 20},
                SubscriptionBigListsPolicy = new CollectionPoolPolicy{PoolSettings = new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 1}, CollectionCapacity = 10},
                SubscriptionDictionariesPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 1}, CollectionCapacity = 10},
                PushInfosPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 10000}, CollectionCapacity = 10},
            };
        
        public static readonly TimelinePoolingPolicy Server =
            new TimelinePoolingPolicy
            {
                SubscriptionListsPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 10}, CollectionCapacity = 20},
                SubscriptionBigListsPolicy = new CollectionPoolPolicy{PoolSettings = new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 1}, CollectionCapacity = 20},
                SubscriptionDictionariesPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 1}, CollectionCapacity = 10},
                PushInfosPolicy = new CollectionPoolPolicy{PoolSettings =  new MemoryPoolSettings {ExpandMethod = PoolExpandMethods.Double, InitialSize = 1000}, CollectionCapacity = 50},
            };

        public static readonly TimelinePoolingPolicy Default = Server;

        public struct CollectionPoolPolicy
        {
            public MemoryPoolSettings PoolSettings;
            public int CollectionCapacity;
        }
    }

    public partial class GlobalTimeline : Timeline, IGlobalTimeline, IContentHash
    {
        public struct Options
        {
            public TimelineAdvanceType AdvanceType;
            public CheckForTimeParadoxesEnum CheckForTimeParadoxes;
            
            public static Options Auto => new Options{AdvanceType = TimelineAdvanceType.Auto};
            public static Options Manual => new Options{AdvanceType = TimelineAdvanceType.Manual};
        }

        static readonly TimelineComparer comparer = TimelineComparer.Strait();
        static readonly TimelineComparer insertAfter = TimelineComparer.InsertAfter();
        static readonly TimelineComparer insertBefore = TimelineComparer.InsertBefore();
        static readonly string ungroupedKey = Guid.NewGuid().ToString();
        
        // pooling
        private static TimelinePoolingPolicy? s_poolingPolicy;
        private IMemoryPool<List<Subscription>> _poolSubscriptionLists;
        private IMemoryPool<Dictionary<string, List<Subscription>>> _poolSubscriptionDictionaries;
        private IMemoryPool<List<Subscription>> _poolSubscriptionBigLists;
        public class PushInfoPoolingContext
        {
            public IMemoryPool<Dictionary<ITimeline, int>> timelinesTravelledPool;
            public ITimeline[] hashHelperArray;
        }
        private PushInfoPoolingContext _pushInfoPoolingContext;
        public PushInfoPoolingContext APushInfoPoolingContext => _pushInfoPoolingContext;
        
        public event Action OnApplied;
        public event Action PreAdvance;
        public event Action<TlTime> PostAdvance;
        
        internal long _lastID;

        private IEnumerator _drainIterator;
        int _capacity;

        internal List<TimedTimeable> _timeline;
        List<TimedTimeable> _unappliedActivities;
        Dictionary<ITimeable, TlTime> _activityOffsets;
        internal Dictionary<ITimeable, PushInfo> _pushInfos;

        internal Dictionary<Type, List<Subscription>> _typeHandlers;
        internal Dictionary<ITimeable, List<Subscription>> _instanceHandlers;
        internal Dictionary<Subscription, int> _subscriptionOrder;
        internal int _curSubscriptionOrder;

        internal Dictionary<long, ITimeable> _id2activity;

        internal Options _options;
        
        internal TlTime _time;
        public TlTime Now => _time;

        public bool IsAdvanceable => _options.AdvanceType == TimelineAdvanceType.Manual;

        protected override IGlobalTimeline AGlobalTimeline => this;

        public GlobalTimeline(int capacity = 1000, Options options = default(Options))
        {
            _capacity = capacity;
            _options = options;
            _time = TlTime.FromMilliseconds(_options.AdvanceType == TimelineAdvanceType.Manual ? 0l : long.MaxValue);
            Clear();
            if (s_poolingPolicy == null)
            {
                throw new TimelineException("Pools not set");
            }
            CreatePools(s_poolingPolicy.Value);
        }
        
        public static void SetPoolingPolicy(TimelinePoolingPolicy policy)
        {
            s_poolingPolicy = policy;
        }

        private void CreatePools(TimelinePoolingPolicy policy)
        {
            var p = policy.SubscriptionListsPolicy;
            _poolSubscriptionLists = ListPool2<Subscription>.Create(p.PoolSettings, p.CollectionCapacity).Labeled("_poolSubscriptionLists");

            p = policy.SubscriptionDictionariesPolicy;
            _poolSubscriptionDictionaries = DictionaryPool2<string, List<Subscription>>.Create(p.PoolSettings, p.CollectionCapacity).Labeled("_poolSubscriptionDictionaries");

            p = policy.SubscriptionBigListsPolicy;
            _poolSubscriptionBigLists = ListPool2<Subscription>.Create(p.PoolSettings, p.CollectionCapacity).Labeled("_poolSubscriptionBigLists");

            p = policy.PushInfosPolicy;
            _pushInfoPoolingContext = new PushInfoPoolingContext
            {
                timelinesTravelledPool = DictionaryPool2<ITimeline, int>.Create(p.PoolSettings, p.CollectionCapacity)
                    .Labeled("_timelinesTravelledPool"),
                hashHelperArray = new ITimeline[50]
            };
        }

        internal void Reconstruct()
        {
            _unappliedActivities = _timeline.Where(timeable => timeable.offset > _time).ToList();
            _activityOffsets = _timeline.ToDictionary(timeable => timeable.value, timeable => timeable.offset);
            _id2activity = _timeline.ToDictionary(timeable => timeable.value.Id, timeable => timeable.value);
            _curSubscriptionOrder = _subscriptionOrder.Any() ? _subscriptionOrder.Max(pair => pair.Value) + 1 : 0;
            
            foreach (var timed in _timeline)
            {
                if (timed.value is IComposedTimeable composedTimeable)
                {
                    ProcessCompletionOfComposed(composedTimeable);
                }
            }
        }

        protected override void RemoveActivityByIdInGlobal(long id)
        {
            var timeable = ActivityById(id);
            if (timeable == null)
            {
                return;
            }

            _id2activity.Remove(id);
            _timeline.Remove(_timeline.Find(x => x.value == timeable));
            _unappliedActivities.Remove(_unappliedActivities.Find(x => x.value == timeable));

            PurgeTimeable(timeable);
        }

        public ITimeable ActivityById(long id)
        {
            if (_id2activity.TryGetValue(id, out var result))
            {
                return result;
            }
            return null;
        }

        public T ActivityById<T>(long id) where T : ITimeable
        {
            return (T)ActivityById(id);
        }

        public override TlTime Offset()
        {
            return TlTime.Zero;
        }

        public override TlTime Offset(ITimeable timeable)
        {
            return _activityOffsets[timeable];
        }

        public override TlTime Offset(TlTime globalTime)
        {
            return globalTime;
        }

        public IEnumerable<T> Select<T>(Func<T, bool> condition)
        {
            return _unappliedActivities.Where(timed => timed.value is T t && condition(t)).Select(timed => timed.value)
                .Cast<T>();
        }

        public void PurgeAll()
        {
            _unappliedActivities.Clear();
            PurgeActivities(TlTime.FromMilliseconds(long.MaxValue));
        }

        public void Purge(TlTime time)
        {
            if (_unappliedActivities.Any())
            {
                throw new TimelineException("There are unapplied activities exist");
            }

            PurgeActivities(time);
        }

        public void GC()
        {
            // var s = TimelineJsonSerializator.CreatePretty();
            // var before = s.Serialize(this);

            int nCollected;
            while ((nCollected = GCCycle()) > 0)
            {
#if TIMELINE_UNITY_LOGS                
                Debug.Log($"Timeline GC cycle. Collected {nCollected} activities");
#endif
                // var after = s.Serialize(this);
                // Log.Info($"before:\n{before}");
                // Log.Info($"after:\n{after}");
            }
        }

        private int GCCycle()
        {
            // all activities which subscribed to types must stay
            var subscribedToType = new HashSet<ITimeable>();
            foreach (var subs in _typeHandlers.SelectMany(pair => pair.Value))
            {
                if (HasInternalTarget(subs, out var target) && target is ITimeable timeable)
                {
                    subscribedToType.Add(timeable);
                }
            }
            
            var newTimeline = new LinkedList<TimedTimeable>();
            var toPurge = new HashSet<ITimeable>();
            var completeActivities = new Dictionary<ITimeable, LinkedListNode<TimedTimeable>>();
            var activitiesWhichHasWorkToDo = new HashSet<ITimeable>();
            foreach (var timed in  ((IEnumerable<TimedTimeable>)_timeline).Reverse())
            {
                var node = new LinkedListNode<TimedTimeable>(timed);
                
                bool isCompletion = false;
                if (timed.value is Completed<ITimeable> completed)
                {
                    isCompletion = true;
                    if (timed.offset <= _time)
                    {
                        completeActivities[completed.Activity] = node;
                    }
                }

                // unapplied activities stay
                if (timed.offset > _time)
                {
                    newTimeline.AddLast(node);
                    AddReferencers(timed.value, activitiesWhichHasWorkToDo);
                    continue;
                }

                // applied completions will be purged later if their activity purged 
                if (isCompletion)
                {
                    newTimeline.AddLast(node);
                    continue;
                }

                if (!completeActivities.ContainsKey(timed.value) // applied but incomplete activities stay
                    || activitiesWhichHasWorkToDo.Contains(timed.value) // applied but subscribed to incomplete activities stay
                    || subscribedToType.Contains(timed.value)) // applied but subscribed to type stay
                {
                    newTimeline.AddLast(node);
                    continue;
                }
                
                // else purge activity with its completion
                toPurge.Add(timed.value);
                if (completeActivities.TryGetValue(timed.value, out var completionNode))
                {
                    newTimeline.Remove(completionNode);
                    toPurge.Add(completionNode.Value.value);
                }
            }

            var nCollected = _timeline.Count - newTimeline.Count;
            if (nCollected < 0)
            {
                throw new TimelineException("Garbage Collector have added activities");
            }

            if (nCollected == 0)
            {
                return 0;
            }

            _timeline = newTimeline.Reverse().ToList();
            foreach (var timeable in toPurge)
            {
                PurgeTimeable(timeable);
            }

            return nCollected;

            void AddReferencers(ITimeable timeable, HashSet<ITimeable> set)
            {
                if (_instanceHandlers.TryGetValue(timeable, out var subs))
                {
                    foreach (var sub in subs)
                    {
                        if (HasInternalTarget(sub, out var tgt) && tgt is ITimeable referencer)
                        {
                            set.Add(referencer);
                        }
                    }
                }
            }
        }

        //HashSet<Subscription> oldSubscription = new HashSet<Subscription>();

        void PurgeActivities(TlTime time)
        {
            var now = time;
            var i = ~_timeline.BinarySearch(new TimedTimeable(now, null), insertBefore);
            var recordsToPurge = _timeline.GetRange(0, i);
            _timeline.RemoveRange(0, i);

            foreach (var timeable in recordsToPurge.Select(timed => timed.value))
            {
                PurgeTimeable(timeable);
            }

            //var newSubscriptions = new HashSet<Subscription>(subscriptionOrder.Keys);
            //var diff = new HashSet<Subscription>(newSubscriptions);
            //diff.ExceptWith(oldSubscription);

            //if (diff.Any())
            //{
            //    var descr = string.Join("\n", diff);
            //    descr = "Accumulated subscriptions:\n" + descr;
            //    Debug.LogError(descr);
            //}
            //oldSubscription = newSubscriptions;
        }

        void PurgeTimeable(ITimeable timeable)
        {
            _id2activity.Remove(timeable.Id);
            _activityOffsets.Remove(timeable);
            if (_pushInfos.TryGetValue(timeable, out var info))
            {
                _pushInfos.Remove(timeable);
                info.Dispose();
            }
            PurgeInstanceSubscriptions(timeable);
        }

        void PurgeInstanceSubscriptions(ITimeable timeable)
        {
            if (_instanceHandlers.TryGetValue(timeable, out List<Subscription> subs))
            {
                _instanceHandlers.Remove(timeable);
                foreach (var subscription in subs)
                {
                    _subscriptionOrder.Remove(subscription);
                }
            }
        }

        #region IContentHash

        public void WriteContentHash(ITracingHashWriter writer)
        {
            writer.Trace("isManual").Write(_options.AdvanceType == TimelineAdvanceType.Manual);
            writer.Trace("_lastID").Write(_lastID);
            writer.Trace("_time").Write(_time.ToMilliseconds);

            writer.Trace("activities");
            using (writer.IndentationBlock())
            {
                foreach (var timed in _timeline)
                {
                    timed.WriteContentHash(writer);
                }
            }

            writer.Trace("_pushInfos");
            using (writer.IndentationBlock())
            {
                var pushinfos = _pushInfos.OrderBy(pair => pair.Key.Id);
                foreach (var pushInfo in pushinfos)
                {
                    writer.Trace("id").Write(pushInfo.Key.Id);
                    writer.Trace("value");
                    using (writer.IndentationBlock())
                    {
                        pushInfo.Value.WriteContentHash(writer);    
                    }
                }
            }

            var serializableSubscriptionOrder = SerializableSubscriptionOrder();

            var typeHandlers = _typeHandlers.OrderBy(pair => pair.Key.Name);
            writer.Trace("typeHandlers");
            using (writer.IndentationBlock())
            {
                foreach (var pair in typeHandlers)
                {
                    var serializableSubs = pair.Value.Where(IsSerializable);
                    if(!serializableSubs.Any())
                        continue;

                    writer.Trace("type").Write(pair.Key.Name);
                    writer.Trace("subscriptions");
                    using (writer.IndentationBlock())
                    {
                        foreach (var subscription in serializableSubs)
                        {
                            writer.Trace("order").Write(serializableSubscriptionOrder[subscription]);
                            writer.Trace("subscription");
                            using (writer.IndentationBlock())
                            {
                                subscription.WriteContentHash(writer);    
                            }
                        }
                    }
                }
            }

            
            var instanceHandlers = _instanceHandlers.OrderBy(pair =>
            {
                if (pair.Key is Completed<ITimeable> completed)
                {
                    return (1, completed.Activity.Id);
                }
                else
                {
                    return (0, pair.Key.Id);
                }
            });
            writer.Trace("instance handlers");
            using (writer.IndentationBlock())
            {
                foreach (var pair in instanceHandlers)
                {
                    var serializableSubs = pair.Value.Where(IsSerializable);
                    if(!serializableSubs.Any())
                        continue;

                    var id = pair.Key is Completed<ITimeable> completed ? $"completed for {completed.Activity.Id}" : $"activity:{pair.Key.Id}";
                    writer.Trace("id").Write(id);
                    writer.Trace("subscriptions");
                    using (writer.IndentationBlock())
                    {
                        foreach (var subscription in serializableSubs)
                        {
                            writer.Trace("order").Write(serializableSubscriptionOrder[subscription]);
                            writer.Trace("subscription");
                            using (writer.IndentationBlock())
                            {
                                subscription.WriteContentHash(writer);    
                            }
                        }
                    }
                }
            }
        }

        #endregion
        public override string ToString()
        {
            var str = string.Join("\n", _timeline.Select(timed => $"[{(IsUnapplied(timed, 0) ? " " : "V")}] [{timed.offset.ToMilliseconds.ToString("000.000")}] {Descr(timed)}"));
            return str;

            string Descr(TimedTimeable timed)
            {
                return timed.value.ToString();
            }
        }

        protected override void PushToGlobal(PushInfo push, TlTime offset, Action<ITimeable> onPushed)
        {
            PushCore(push, offset, onPushed);
            EnsureDraining(_time);
        }

        public IEnumerator PushIteration(ITimeable timeable, TlTime offset)
        {
            _drainIterator = DrainIteration(_time);
            Push(timeable, offset);
            return _drainIterator;
        }

        private void PushCore(PushInfo push, TlTime offset, Action<ITimeable> onPushed)
        {
            var timeable = push.timeable;

            if (_activityOffsets.ContainsKey(timeable))
            {
                throw new ArgumentException("Can't push timeable twice");
            }

            var id = ++_lastID; // starts with 1. 0 means "no id"
            timeable.SetId(id);

            _id2activity[id] = timeable;

            _pushInfos[timeable] = push;
            _activityOffsets[timeable] = offset;
            var record = new TimedTimeable(offset, timeable);

            var index = _timeline.BinarySearch(record, insertAfter);

            if (_options.CheckForTimeParadoxes != CheckForTimeParadoxesEnum.DontCheck)
            {
                CheckForTimeParadoxes(record, ~index);
            }

            _timeline.Insert(~index, record);

            index = _unappliedActivities.BinarySearch(record, insertAfter);
            _unappliedActivities.Insert(~index, record);

            onPushed?.Invoke(timeable);
        }

        private void EnsureDraining(TlTime tillTime)
        {
            if (_drainIterator == null)
            {
                _drainIterator = DrainIteration(tillTime);
                while (_drainIterator.MoveNext())
                {
                }
            }
        }

        private IEnumerator DrainIteration(TlTime tillTime)
        {
            while (_unappliedActivities.Count > 0)
            {
                var toApply = _unappliedActivities[0];
                if (toApply.offset > tillTime)
                {
                    break;
                }

                _time = toApply.offset;

                _unappliedActivities.RemoveAt(0);
                toApply.value.Apply();
                Publish(toApply.value);
                OnApplied?.Invoke();
                yield return null;
            }

            _time = tillTime;
            _drainIterator = null;
        }

        void Publish(ITimeable timeable)
        {
            using (var dispose = DisposeBlock.Spawn())
            {
                var grouped = SubscriptionsGroupedBySubsystem(dispose, timeable);
                var pushInfo = _pushInfos[timeable];
                var subsToCall = dispose.Spawn(_poolSubscriptionBigLists);
                foreach (var group in grouped)
                {
                    if (group.Key == ungroupedKey)
                    {
                        subsToCall.AddRange(group.Value);
                    }
                    else if (!_areSubsystemsDisabled)
                    {
                        int deepestLevel = 0;
                        Subscription deepest = null;
                        foreach (var subscription in group.Value)
                        {
                            var depth = pushInfo.Depth(subscription);
                            if (depth >= deepestLevel)
                            {
                                if (depth == deepestLevel)
                                {
                                    if (subscription.IsInstance)
                                    {
                                        deepest = subscription;
                                        deepestLevel = depth;
                                    }
                                }
                                else
                                {
                                    deepest = subscription;
                                    deepestLevel = depth;
                                }
                            }
                        }

                        subsToCall.Add(deepest);
                    }
                }

                subsToCall.Sort((s1, s2) => _subscriptionOrder[s1].CompareTo(_subscriptionOrder[s2]));

                foreach (var subscription in subsToCall)
                {
                    subscription.Call(timeable);
                }
            }

            PurgeInstanceSubscriptions(timeable);
        }
        
        Dictionary<string, List<Subscription>> SubscriptionsGroupedBySubsystem(DisposeBlock dispose, ITimeable timeable)
        {
            var pushInfo = _pushInfos[timeable];
            
            // залипуха
            // в случае Complete подписываются на интерфейс типа Subscribe<Completed<Some>>
            // Completed - интерфейс а не тип. Это нужно для ковариативности в Subscription.Call
            // и для дизайна - чтобы в интерфейсе ITimeable были только интерфейсы
            // а timeable - конкретная имплементация. Нужно кастануть его к типу подписки то есть к интерфейсу 
            var tp = timeable.GetType();
            if (timeable is Completed<ITimeable> completed)
            {
                tp = completed.InterfaceType;
            }
            
            var subscriptions = dispose.Spawn(_poolSubscriptionLists);
            if (_typeHandlers.TryGetValue(tp, out List<Subscription> tmp))
            {
                subscriptions.AddRange(tmp);
            }

            if (_instanceHandlers.TryGetValue(timeable, out tmp))
            {
                subscriptions.AddRange(tmp);
            }

            var dict = dispose.Spawn(_poolSubscriptionDictionaries);
            foreach (var subscription in subscriptions)
            {
                if(!pushInfo.InMyLocality(subscription))
                    continue;

                var key = subscription.subsystem ?? ungroupedKey;
                if (!dict.TryGetValue(key, out var subsystemSubscriptions))
                {
                    subsystemSubscriptions = dispose.Spawn(_poolSubscriptionLists); 
                    dict.Add(key, subsystemSubscriptions);
                }
                subsystemSubscriptions.Add(subscription);
            }

            return dict;
        }

        protected override void SubscribeToGlobal(Subscription subscription)
        {
            if (subscription.subsystem != null)
            {
                EnsureSingleSubscription(subscription);
            }

            _subscriptionOrder[subscription] = _curSubscriptionOrder++;

            if (subscription.IsInstance)
            {
                var key = subscription.Timeable;

                List<Subscription> subs;
                if (!_instanceHandlers.TryGetValue(key, out subs))
                {
                    subs = new List<Subscription>();
                    _instanceHandlers[key] = subs;
                }
                subs.Add(subscription);
            }
            else
            {
                var key = subscription.Type;
                
                List<Subscription> subs;
                if (!_typeHandlers.TryGetValue(key, out subs))
                {
                    subs = new List<Subscription>();
                    _typeHandlers[key] = subs;
                }
                subs.Add(subscription);
            }
        }

        void EnsureSingleSubscription(Subscription subscription)
        {
            if (subscription.IsInstance)
            {
                var key = subscription.Timeable;
                if (_instanceHandlers.TryGetValue(key, out List<Subscription> subscriptions))
                {
                    if (subscriptions.Any(otherSub => otherSub.subsystem == subscription.subsystem && otherSub.SourceTimeline == subscription.SourceTimeline))
                    {
                        throw new ArgumentException($"Double subscription to instance of type {key.GetType()}");
                    }
                }
            }
            else
            {
                var typeKey = subscription.Type;
                if (_typeHandlers.TryGetValue(typeKey, out List<Subscription> subscriptions))
                {
                    if (subscriptions.Any(otherSub => otherSub.subsystem == subscription.subsystem && otherSub.SourceTimeline == subscription.SourceTimeline))
                    {
                        throw new ArgumentException($"Double subscription to type {typeKey}");
                    }
                }
            }
        }

        public override void Unsubscribe<T>(Action<T> action)
        {
            bool ConditionToRemove(Subscription subs)
            {
                return subs.IsMy(action);
            }

            var key = typeof(T);

            using(var dispose = DisposeBlock.Spawn())
            {
                var toRemove = dispose.Spawn(_poolSubscriptionLists);
                
                if (_typeHandlers.ContainsKey(key))
                {
                    toRemove.AddRange(_typeHandlers[key].Where(ConditionToRemove));
                    _typeHandlers[key].RemoveAll(ConditionToRemove);
                }

                var affectedSubscriptions = _instanceHandlers
                    .Where(pair => pair.Key.GetType() == key)
                    .Select(pair => pair.Value);
                
                foreach (var list in affectedSubscriptions)
                {
                    toRemove.AddRange(list.Where(ConditionToRemove));
                    list.RemoveAll(ConditionToRemove);
                }

                RemoveFromOrder(toRemove);
            }
        }

        public override void Unsubscribe<T>(T timeable, Action<T> action)
        {
            bool ConditionToRemove(Subscription subs)
            {
                return subs.Timeable == (ITimeable)timeable && subs.IsMy(action);
            }

            using (var dispose = DisposeBlock.Spawn())
            {
                var toRemove = dispose.Spawn(_poolSubscriptionLists);
                foreach (var subscription in _instanceHandlers[timeable])
                {
                    if (ConditionToRemove(subscription))
                    {
                        toRemove.Add(subscription);
                    }
                }
                _instanceHandlers[timeable].RemoveAll(ConditionToRemove);
                RemoveFromOrder(toRemove);
            }
        }
        
        public void SetCheckForTimeParadoxes(CheckForTimeParadoxesEnum value)
        {
            _options.CheckForTimeParadoxes = value;
        }

        void Clear()
        {
            _drainIterator = null;
            _timeline = new List<TimedTimeable>(_capacity);
            _unappliedActivities = new List<TimedTimeable>(_capacity);
            _activityOffsets = new Dictionary<ITimeable, TlTime>(_capacity);
            _pushInfos = new Dictionary<ITimeable, PushInfo>(_capacity);
            _typeHandlers = new Dictionary<Type, List<Subscription>>(_capacity);
            _instanceHandlers = new Dictionary<ITimeable, List<Subscription>>(_capacity);
            _subscriptionOrder = new Dictionary<Subscription, int>(_capacity);
            _id2activity = new Dictionary<long, ITimeable>(_capacity);
        }

        protected override IEnumerable<ITimeable> TimeablesFor(ITimeline timeline)
        {
            return this._timeline.Where(timed => _pushInfos[timed.value].IsMyLocality(timeline)).Select(timed => timed.value);
        }

        void RemoveFromOrder(IEnumerable<Subscription> subscriptions)
        {
            foreach (var subscription in subscriptions)
            {
                _subscriptionOrder.Remove(subscription);
            }
        }

        void CheckForTimeParadoxes(TimedTimeable record, int index)
        {
            List<TimedTimeable> appliedActivitiesAfterRecord = new List<TimedTimeable>();
            if (index < _timeline.Count)
            {
                var unappliedMinSearchIndex = _unappliedActivities.BinarySearch(record, comparer);
                if (unappliedMinSearchIndex < 0)
                {
                    unappliedMinSearchIndex = ~unappliedMinSearchIndex;
                }

                for (int i = index; i < _timeline.Count; i++)
                {
                    var next = _timeline[i];
                    if (next.value is Completed<ITimeable>)
                    {
                        continue;
                    }

                    if (IsUnapplied(next, unappliedMinSearchIndex))
                    {
                        continue;
                    }
                    else
                    {
                        appliedActivitiesAfterRecord.Add(next);
                    }
                }

                if (appliedActivitiesAfterRecord.Count > 0)
                {
                    if (_options.CheckForTimeParadoxes == CheckForTimeParadoxesEnum.CheckAndThrow)
                    {
                        throw new TimeParadoxException(record, appliedActivitiesAfterRecord);
                    }
                    else if (_options.CheckForTimeParadoxes == CheckForTimeParadoxesEnum.CheckAndLog)
                    {
#if TIMELINE_UNITY_LOGS
                        Debug.LogError(TimeParadoxException.CreateDescription(record, appliedActivitiesAfterRecord));
#endif
                    }
                }
            }
        }

        bool IsUnapplied(TimedTimeable timed, int minIndex)
        {
            var indexInUnapplied = _unappliedActivities.BinarySearch(minIndex, _unappliedActivities.Count - minIndex, timed, insertBefore);
            indexInUnapplied = ~indexInUnapplied;
            while (indexInUnapplied < _unappliedActivities.Count && _unappliedActivities[indexInUnapplied].offset == timed.offset)
            {
                if (_unappliedActivities[indexInUnapplied].value == timed.value)
                {
                    return true;
                }

                indexInUnapplied++;
            }

            return false;
        }

        class TimelineComparer : IComparer<TimedTimeable>
        {
            int resultIfEqual;

            TimelineComparer(int resultIfEqual)
            {
                this.resultIfEqual = resultIfEqual;
            }

            public static TimelineComparer InsertBefore()
            {
                return new TimelineComparer(+1);
            }

            public static TimelineComparer InsertAfter()
            {
                return new TimelineComparer(-1);
            }

            public static TimelineComparer Strait()
            {
                return new TimelineComparer(0);
            }

            public int Compare(TimedTimeable x, TimedTimeable y)
            {
                int result = x.offset.CompareTo(y.offset);

                if (result == 0)
                {
                    return resultIfEqual;
                }
                else
                {
                    return result;
                }
            }
        }

        public void Advance(TlTime time)
        {
            PreAdvance?.Invoke();
            CheckManualMode();
            EnsureDraining(time);
            GC();
            PostAdvance?.Invoke(time);
        }

        public IEnumerator AdvanceIteration(TlTime time)
        {
            CheckManualMode();

            _drainIterator = DrainIteration(time);
            while (_drainIterator.MoveNext())
            {
                yield return _drainIterator.Current;
            }
            yield return null;
            GC();
        }

        private void CheckManualMode()
        {
            if (_options.AdvanceType == TimelineAdvanceType.Auto)
            {
                throw new TimelineException("This timeline is auto so it can't be advanced manually'");
            }
        }

        internal bool IsSerializable(Subscription subs)
        {
            if (subs.Action.Method.IsStatic)
            {
                return true;
            }

            if (HasInternalTarget(subs, out _))
            {
                return true;
            }

            return false;
        }

        internal bool HasInternalTarget(Subscription subs, out object target)
        {
            target = null;
            
            if (subs.Action.Method.IsStatic)
            {
                return false;
            }
            
            var allActivities = _activityOffsets.Keys;
            switch (subs.Action.Target)
            {
                case ITimelineInternal timelineInternal when timelineInternal.AGlobalTimeline == this:
                    target = timelineInternal;
                    return true;
                case ITimeable timeable when allActivities.Contains(timeable):
                    target = timeable;
                    return true;
                default:
                    return false;
            }
        }

        internal Dictionary<Subscription, int> SerializableSubscriptionOrder()
        {
            var order2sub = _subscriptionOrder.Inverse();
            var orders = order2sub.Keys.OrderBy(i => i).ToArray();
            var filteredSubscriptionOrder = new Dictionary<Subscription, int>();
            int newOrder = 0;
            foreach (var order in orders)
            {
                var sub = order2sub[order];
                if (!IsSerializable(sub))
                {
                    continue;
                }
                filteredSubscriptionOrder[sub] = newOrder++;
            }

            return filteredSubscriptionOrder;
        }
    }

    public class Timed<T> : IContentHash
    {
        public readonly TlTime offset;
        public readonly T value;

        public Timed(TlTime offset, T value)
        {
            this.offset = offset;
            this.value = value;
        }

        #region IContentHash
        public void WriteContentHash(ITracingHashWriter writer)
        {
            var hasher = value as IContentHash;
            if (hasher == null)
            {
                throw new TimelineException($"{value} must support IContentHash");
            }
            
            writer.Trace("offset").Write(offset.ToMilliseconds);
            writer.Trace("activity");
            using (writer.IndentationBlock())
            {
                hasher.WriteContentHash(writer);    
            }
        }

        #endregion
    }

    [JsonObject(MemberSerialization.Fields, IsReference = false)]
    public class TimedTimeable : Timed<ITimeable>  
    {
        public TimedTimeable(TlTime offset, ITimeable value) : base(offset, value)
        {
        }
    }

    public class TimeParadoxException : TimelineException
    {
        public readonly TimedTimeable recordToPush;
        public readonly TimedTimeable[] appliedRecordsAfter;

        public TimeParadoxException(TimedTimeable recordToPush, IEnumerable<TimedTimeable> appliedRecordsAfter) : base(CreateDescription(recordToPush, appliedRecordsAfter))
        {
            this.recordToPush = recordToPush;
            this.appliedRecordsAfter = appliedRecordsAfter.ToArray();
        }

        public static string CreateDescription(TimedTimeable recordToPush, IEnumerable<TimedTimeable> appliedRecordsAfter)
        {
            var applied = string.Join("\n", appliedRecordsAfter.Select(timed => $"{timed.value.GetType().Name} [{timed.offset}]"));
            return
                $"Trying to push activity {recordToPush.value.GetType().Name} at time {recordToPush.offset} while applied activities exist after that time:\n{applied}";
        }
    }

    internal static class TimedExtension
    {
        public static Timed<T> Timed<T>(this T value, TlTime offset)
        {
            return new Timed<T>(offset, value);
        }
    }
    internal static class DictionaryExtensions
    {
        public static Dictionary<TValue, TKey> Inverse<TKey, TValue>(this IDictionary<TKey, TValue> source)
        {
            var dictionary = new Dictionary<TValue, TKey>();
            foreach (var entry in source)
            {
                if (dictionary.ContainsKey(entry.Value)) 
                    throw new ArgumentException("Dictionary contains duplicate values");
                
                dictionary.Add(entry.Value, entry.Key);
            }
            return dictionary;
        } 
    }
}