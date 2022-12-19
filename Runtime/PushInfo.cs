using System;
using System.Collections.Generic;
using CW.Core.Hash;
using Newtonsoft.Json;
using CW.Extensions.Pooling;

namespace CW.Core.Timeline
{
    [JsonObject(MemberSerialization.Fields, IsReference = false)]
    public struct PushInfo : IContentHash, IDisposable
    {
        public readonly ITimeable timeable;
        internal Dictionary<ITimeline, int> timelinesTravelled;
        private GlobalTimeline.PushInfoPoolingContext _poolingContext;

        public PushInfo(ITimeable timeable, GlobalTimeline.PushInfoPoolingContext context) : this()
        {
            this.timeable = timeable;
            _poolingContext = context;
            timelinesTravelled =  _poolingContext.timelinesTravelledPool.Spawn();
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
        public void Dispose()
        {
            if (timelinesTravelled != null)
            {
                _poolingContext.timelinesTravelledPool.Despawn(timelinesTravelled);
                timelinesTravelled = null;
            }
        }

#endregion

        private void WriteTimelinesHash(ITracingHashWriter writer)
        {
            writer.Trace("timelines");

            if (_poolingContext.hashHelperArray.Length < timelinesTravelled.Count)
            {
                _poolingContext.hashHelperArray = new ITimeline[timelinesTravelled.Count];
            }

            foreach (var pair in timelinesTravelled)
            {
                _poolingContext.hashHelperArray[pair.Value] = pair.Key;
            }

            using (writer.IndentationBlock())
            {
                for (int level = 0; level < timelinesTravelled.Count; level++)
                {
                    var tl = _poolingContext.hashHelperArray[level];
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