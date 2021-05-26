using System;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public class CompletionWaiter : ICompletionPromise
    {
        private event Action<TLTime> completionPromise;
        private ITimeline timeline;

        private HashSet<Activity> dependencies = new HashSet<Activity>();
        private bool isCompleted;
        bool isClosed = true;
        TLTime? lastCompletionTime;

        public IDisposable Subscribe(Action<TLTime> observer)
        {
            completionPromise += observer;
            return new CompletionWaiterDisposable(this, observer);
        }

        public CompletionWaiter(ITimeline timeline)
        {
            this.timeline = timeline;
        }

        public CompletionWaiter Opened()
        {
            isClosed = false;
            return this;
        }

        public CompletionWaiter Close()
        {
            isClosed = true;
            if (dependencies.Count == 0 && lastCompletionTime.HasValue)
            {
                if (!isCompleted)
                {
                    isCompleted = true;
                    completionPromise?.Invoke(lastCompletionTime.Value);
                }
            }

            return this;
        }


        public void AddDependency<T>(T dependency) where T : Activity
        {
            dependencies.Add(dependency);
            timeline.Subscribe(dependency.CompleteMarker(), OnDependencyComplete);
        }

        void OnDependencyComplete<T>(Completed<T> completed) where T : Activity
        {
            timeline.Unsubscribe(completed, OnDependencyComplete);
            
            TLTime time = timeline.Offset(completed);
            Activity dependency = completed.Activity;
            lastCompletionTime = time;
            dependencies.Remove(dependency);
            if (dependencies.Count == 0)
            {
                if (isClosed)
                {
                    isCompleted = true;
                    completionPromise?.Invoke(time);
                }
            }
        }

        public void CompleteIfEmpty(TLTime time)
        {
            Close();

            if (isCompleted)
            {
                return;
            }

            if (dependencies.Count == 0)
            {
                completionPromise?.Invoke(time);
            }
        }

        private struct CompletionWaiterDisposable : IDisposable
        {
            private CompletionWaiter _waiter;
            private Action<TLTime> _subject;

            public CompletionWaiterDisposable(CompletionWaiter waiter, Action<TLTime> subject)
            {
                _waiter = waiter;
                _subject = subject;
            }

            public void Dispose()
            {
                _waiter.completionPromise -= _subject;
            }
        }
    }
}