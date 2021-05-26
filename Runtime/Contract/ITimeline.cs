using System;
using System.Collections;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public interface ITimeline
    {
        void Push(ITimeable timeable, TLTime offset);
        void Subscribe<T>(Action<T> action, string subsystem = null) where T : ITimeable;
        void Subscribe<T>(T timeable, Action<T> action, string subsystem = null) where T : ITimeable;
        void Unsubscribe<T>(Action<T> action) where T : ITimeable;
        void Unsubscribe<T>(T timeable, Action<T> action) where T : ITimeable;
        TLTime Offset();
        TLTime Offset(ITimeable timeable);
        TLTime Offset(TLTime globalTime);
        TLTime ToTimeline(ITimeline other, TLTime myTime);
        void RemoveActivityById(long id);
    }

    public interface IGlobalTimeline : ITimeline
    {
        IEnumerator PushIteration(ITimeable timeable, TLTime offset);
        bool IsAdvanceable { get; }
        void Advance(TLTime offset);
        IEnumerator AdvanceIteration(TLTime offset);
        TLTime Now { get; }

        ITimeable ActivityById(long id);
        T ActivityById<T>(long id) where T : ITimeable;
        IEnumerable<T> Select<T>(Func<T, bool> condition);
        
        void Purge(TLTime time);
        void PurgeAll();

        void GC();
    }
}