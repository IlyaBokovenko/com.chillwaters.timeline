using System;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public abstract class Timeline : ITimelineInternal, ITimeline
    {
        public abstract TLTime Offset();
        public abstract TLTime Offset(ITimeable timeable);
        public abstract TLTime Offset(TLTime globalTime);

        public void Subscribe<T>(Action<T> action, string subsystem = null) where T : ITimeable
        {
            var subcription = Subscription<T>.ToType(this, action);
            subcription.subsystem = subsystem;
            SubscribeToGlobal(subcription);
        }

        public void Subscribe<T>(T timeable, Action<T> action, string subsystem = null) where T : ITimeable
        {
            var subcription = Subscription<T>.ToInstance(this, action, timeable);
            subcription.subsystem = subsystem;
            SubscribeToGlobal(subcription);
        }

        public abstract void Unsubscribe<T>(Action<T> action) where T : ITimeable;

        public abstract void Unsubscribe<T>(T timeable, Action<T> action) where T : ITimeable;

        public TLTime ToTimeline(ITimeline other, TLTime myTime)
        {
            return Offset() - other.Offset() + myTime;
        }

        public void Push(ITimeable timeable, TLTime offset)
        {
            var push = new PushInfo(timeable);
            ((ITimelineInternal)this).PushToGlobal(push, offset, ProcessPushed);
        }

        public IEnumerable<ITimeable> Timeables()
        {
            return TimeablesFor(this);
        }

        private void ProcessPushed(ITimeable timeable)
        {
            if (timeable is Completed<ITimeable>)
            {
                return;
            }

            var localTimeline = ((ITimelineInternal)this).CreateLocalTimeline(timeable);
            timeable.SetLocalTimeline(localTimeline);

            if (timeable is IComposedTimeable composedTimeable) 
            {
                ProcessCompletionOfComposed(composedTimeable, localTimeline);
            }
            else
            {
                Subscribe(timeable, PushCompletionMarker);
            }
        }
        
        protected void ProcessCompletionOfComposed(IComposedTimeable composedTimeable)
        {
            var activity = composedTimeable as Activity;
            var timeline = activity.Timeline;
            ProcessCompletionOfComposed(composedTimeable, timeline);
        }

        private void ProcessCompletionOfComposed(IComposedTimeable composedTimeable, ITimeline timeline)
        {
            IDisposable subscription = null;
            subscription = composedTimeable.CompletionPromise.Subscribe(localTime =>
            {
                timeline.Push(composedTimeable.MakeCompletionMarker(), localTime);
                subscription?.Dispose();
            });
        }

        private void PushCompletionMarker(ITimeable timeable)
        {
            var offset = Offset(timeable);

            if (timeable is ISimpleTimeable simpleTimeable)
            {
                offset += simpleTimeable.Duration;
            }

            Push(timeable.MakeCompletionMarker(), offset);

            Unsubscribe(timeable, PushCompletionMarker);
        }

        protected abstract void PushToGlobal(PushInfo push, TLTime offset, Action<ITimeable> onPushed);

        void ITimelineInternal.PushToGlobal(PushInfo push, TLTime offset, Action<ITimeable> onPushed)
        {
            push.TravelThrough(this);
            PushToGlobal(push, offset, onPushed);
        }

        protected abstract void SubscribeToGlobal(Subscription subscription);

        void ITimelineInternal.SubscribeToGlobal(Subscription subscription)
        {
            SubscribeToGlobal(subscription);
        }

        ITimeline ITimelineInternal.CreateLocalTimeline(ITimeable timeable)
        {
            var timeline = new LocalTimeline(this, Offset(timeable), timeable.Id);
            timeline.DbgSetParentTimeable(timeable);
            return timeline;
        }

        protected abstract IEnumerable<ITimeable> TimeablesFor(ITimeline timeline);
        IEnumerable<ITimeable> ITimelineInternal.TimeablesFor(ITimeline timeline)
        {
            return TimeablesFor(timeline);
        }

        protected abstract void RemoveActivityByIdInGlobal(long id);
        void ITimelineInternal.RemoveActivityByIdInGlobal(long activityID)
        {
            RemoveActivityByIdInGlobal(activityID);
        }

        public void RemoveActivityById(long id)
        {
            RemoveActivityByIdInGlobal(id);
        }

        protected abstract IGlobalTimeline AGlobalTimeline { get; }
        IGlobalTimeline ITimelineInternal.AGlobalTimeline => AGlobalTimeline;
    }
}