using System;
using System.Collections.Generic;

namespace CW.Core.Timeline
{
    public class CompletionWaiter : ICompletionPromise
    {
        private CompletionPromise _completionPromise = new CompletionPromise();
        
        private ITimeline _timeline;

        private HashSet<Activity> _dependencies = new HashSet<Activity>();
        private bool _isCompleted;
        bool _isClosed = true;
        TlTime? _lastCompletionTime;

        public IDisposable Subscribe(Action<TlTime> callback)
        {
            return _completionPromise.Subscribe(callback);
        }

        public CompletionWaiter(ITimeline timeline)
        {
            this._timeline = timeline;
        }

        public CompletionWaiter Opened()
        {
            _isClosed = false;
            return this;
        }

        public CompletionWaiter Close()
        {
            _isClosed = true;
            if (_dependencies.Count == 0 && _lastCompletionTime.HasValue)
            {
                if (!_isCompleted)
                {
                    _isCompleted = true;
                    _completionPromise.Complete(_lastCompletionTime.Value);
                }
            }

            return this;
        }


        public void AddDependency<T>(T dependency) where T : Activity
        {
            _dependencies.Add(dependency);
            _timeline.Subscribe(dependency.CompleteMarker(), OnDependencyComplete);
        }

        void OnDependencyComplete<T>(Completed<T> completed) where T : Activity
        {
            _timeline.Unsubscribe(completed, OnDependencyComplete);
            
            TlTime time = _timeline.Offset(completed);
            Activity dependency = completed.Activity;
            _lastCompletionTime = time;
            _dependencies.Remove(dependency);
            if (_dependencies.Count == 0)
            {
                if (_isClosed)
                {
                    _isCompleted = true;
                    _completionPromise.Complete(_lastCompletionTime.Value);
                }
            }
        }

        public void CompleteIfEmpty(TlTime time)
        {
            Close();

            if (_isCompleted)
            {
                return;
            }

            if (_dependencies.Count == 0)
            {
                _completionPromise.Complete(_lastCompletionTime.Value);
            }
        }
    }
}