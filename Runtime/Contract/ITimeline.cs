using System;
using System.Collections;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public interface ITimeline
    {
        void Push(ITimeable timeable, TlTime offset);
        void Subscribe<T>(Action<T> action, string subsystem = null) where T : ITimeable;
        void Subscribe<T>(T timeable, Action<T> action, string subsystem = null) where T : ITimeable;
        void Unsubscribe<T>(Action<T> action) where T : ITimeable;
        void Unsubscribe<T>(T timeable, Action<T> action) where T : ITimeable;
        TlTime Offset();
        TlTime Offset(ITimeable timeable);
        TlTime Offset(TlTime globalTime);
        TlTime ToTimeline(ITimeline other, TlTime myTime);
        void RemoveActivityById(long id);
    }

    public interface IGlobalTimeline : ITimeline
    {
        IEnumerator PushIteration(ITimeable timeable, TlTime offset);
        bool IsAdvanceable { get; }
        void Advance(TlTime offset);
        IEnumerator AdvanceIteration(TlTime offset);
        TlTime Now { get; }

        ITimeable ActivityById(long id);
        T ActivityById<T>(long id) where T : ITimeable;
        IEnumerable<T> Select<T>(Func<T, bool> condition);
        
        void Purge(TlTime time);
        void PurgeAll();

        void GC();
    }
}