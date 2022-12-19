using System;
using System.Collections.Generic;
using CW.Core.Hash;

namespace CW.Core.Timeline
{
    public class LocalTimeline : Timeline, IContentHash
    {
        internal ITimelineInternal _parent;
        internal TlTime _offset;
        public ITimelineInternal parent => _parent;
        public TlTime offset => _offset;
        protected override IGlobalTimeline AGlobalTimeline => parent is GlobalTimeline globalTimeline ? globalTimeline : parent.AGlobalTimeline; 

        internal long _timeableId;
        private ITimeable dbgParentTimeable;
        public long TimeableId => _timeableId;

        internal LocalTimeline(ITimelineInternal parent, TlTime offset, long timeableId)
        {
            this._timeableId = timeableId;
            this._parent = parent;
            this._offset = offset;
        }

        public void DbgSetParentTimeable(ITimeable dbgParentTimeable)
        {
            this.dbgParentTimeable = dbgParentTimeable;
        }

        public ITimeable DbgGetParentTimeable()
        {
            return dbgParentTimeable;
        }

        public override TlTime Offset(ITimeable timeable)
        {
            return parent.Offset(timeable) - offset;
        }

        public override TlTime Offset()
        {
            return parent.Offset() + offset;
        }

        public override TlTime Offset(TlTime globalTime)
        {
            return globalTime - Offset();
        }

        public override void Unsubscribe<T>(Action<T> action)
        {
            parent.Unsubscribe(action);
        }

        public override void Unsubscribe<T>(T timeable, Action<T> action)
        {
            parent.Unsubscribe(timeable, action);
        }

        public override string ToString()
        {
            return base.ToString();
        }

        protected override void PushToGlobal(PushInfo push, TlTime offset, Action<ITimeable> onPushed)
        {
            parent.PushToGlobal(push, this.offset + offset, onPushed);
        }

        protected override void SubscribeToGlobal(Subscription subscription)
        {
            parent.SubscribeToGlobal(subscription);
        }

        protected override IEnumerable<ITimeable> TimeablesFor(ITimeline timeline)
        {
            return parent.TimeablesFor(timeline);
        }

        protected override void RemoveActivityByIdInGlobal(long id)
        {
            parent.RemoveActivityByIdInGlobal(id);
        }

#region IContentHash

        public void WriteContentHash(ITracingHashWriter writer)
        {
            writer
                .Trace("id").Write(_timeableId)
                .Trace("parent").Write(parent is LocalTimeline localTimeline ? localTimeline.TimeableId : 0)
                .Trace("offset").Write(_offset.ToMilliseconds);
        }

#endregion
    }
}