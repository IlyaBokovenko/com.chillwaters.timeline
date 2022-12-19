using System;

namespace CW.Core.Timeline
{
    public interface ITimeable
    {
        long Id { get; }
        
        // TODO сделать интерфейс ITimeableInternal, перенести туда SetId и SetLocalTimeline 
        void SetId(long id);
        void SetLocalTimeline(ITimeline timeline);
        void Apply();
        Completed<ITimeable> MakeCompletionMarker();
    }
    
    public interface Completed<out T> : ITimeable
    where T : ITimeable
    {
        T Activity { get; }
        Type InterfaceType { get; }
    }

    public interface ISimpleTimeable : ITimeable
    {
        TlTime Duration { get; }
    }

    public interface ICompletionPromise
    {
        IDisposable Subscribe(Action<TlTime> callback);
    }

    public interface IComposedTimeable : ITimeable
    {
        ICompletionPromise CompletionPromise { get; }
    }
}