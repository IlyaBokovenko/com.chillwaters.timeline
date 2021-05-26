using System;
using System.Collections.Generic;
using CW.Core.Hash;
using CW.Extensions.Pooling;
using Newtonsoft.Json;

namespace CW.Core.Timeline
{
    [JsonObject(MemberSerialization.Fields, IsReference = false)]
    public struct PushInfo : IContentHash, IDisposable
    {
        public readonly ITimeable timeable;
        internal Dictionary<ITimeline, int> timelinesTravelled;

        public PushInfo(ITimeable timeable) : this()
        {
            GlobalTimeline.EnsurePools();
            
            this.timeable = timeable;
            timelinesTravelled = s_timelinesTravelledPool.Spawn();
        }

        public void TravelThrough(ITimeline timeline)
        {
            timelinesTravelled[timeline] = timelinesTravelled.Count;
        }

        public bool InMyLocality(Subscription subscription)
        {
            return IsMyLocality(subscription.SourceTimeline);
        }

        public bool IsMyLocality(ITimeline timeline)
        {
            return timelinesTravelled.ContainsKey(timeline);
        }

        public int Depth(Subscription subscription)
        {
            return timelinesTravelled.Count - timelinesTravelled[subscription.SourceTimeline];
        }

#region IContentHash
        public void WriteContentHash(ITracingHashWriter writer)
        {
            writer.Trace("id").Write(timeable.Id);
            WriteTimelinesHash(writer);
        }

#endregion

#region pooling

        private static IMemoryPool<Dictionary<ITimeline, int>> s_timelinesTravelledPool;

        private static ITimeline[] s_hashHelperArray = new ITimeline[50];

        internal static void EnsurePools(TimelinePoolingPolicy.CollectionPoolPolicy policy)
        {
            if (s_timelinesTravelledPool == null)
            {
                s_timelinesTravelledPool = DictionaryPool2<ITimeline, int>.Create(policy.PoolSettings, policy.CollectionCapacity).Labeled("s_timelinesTravelledPool");
            }
        }

        public void Dispose()
        {
            if (timelinesTravelled != null)
            {
                s_timelinesTravelledPool.Despawn(timelinesTravelled);
                timelinesTravelled = null;
            }
        }

#endregion

        private void WriteTimelinesHash(ITracingHashWriter writer)
        {
            writer.Trace("timelines");

            if (s_hashHelperArray.Length < timelinesTravelled.Count)
            {
                s_hashHelperArray = new ITimeline[timelinesTravelled.Count];
            }

            foreach (var pair in timelinesTravelled)
            {
                s_hashHelperArray[pair.Value] = pair.Key;
            }

            using (writer.IndentationBlock())
            {
                for (int level = 0; level < timelinesTravelled.Count; level++)
                {
                    var tl = s_hashHelperArray[level];
                    var line = writer.Trace($"level").Write(level).Trace("timeline");
                    if (tl is LocalTimeline localTimeline)
                    {
                        line.Write(localTimeline.TimeableId);
                    }
                    else
                    {
                        line.Trace("global").Write(-1);
                    }
                }
            }
        }
    }
}