using System;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public interface ITimelineInternal
    {
        TlTime Offset();
        TlTime Offset(ITimeable timeable);
        TlTime Offset(TlTime globalTime);
        void PushToGlobal(PushInfo push, TlTime offset, Action<ITimeable> onPushed);
        void SubscribeToGlobal(Subscription subscription);
        void Unsubscribe<T>(Action<T> action) where T : ITimeable;
        void Unsubscribe<T>(T timeable, Action<T> action) where T : ITimeable;
        ITimeline CreateLocalTimeline(ITimeable timeable);
        IEnumerable<ITimeable> TimeablesFor(ITimeline timeline);
        void RemoveActivityByIdInGlobal(long activityID);
        IGlobalTimeline AGlobalTimeline { get; }
    }
}