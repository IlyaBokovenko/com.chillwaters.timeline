using System;

namespace CW.Core.Timeline
{
    public class CompletionPromise : ICompletionPromise
    {
        private event Action<TlTime> _handlers;
        public IDisposable Subscribe(Action<TlTime> callback)
        {
            return new Subscription(this, callback);
        }

        public void Complete(TlTime time)
        {
            _handlers?.Invoke(time);
        }
        
        private class Subscription : IDisposable
        {
            private CompletionPromise _promise;
            private Action<TlTime> _callback;

            public Subscription(CompletionPromise promise, Action<TlTime> callback)
            {
                _promise = promise;
                _callback = callback;

                _promise._handlers += callback;
            }

            public void Dispose()
            {
                _promise._handlers -= _callback;
            }
        }
    }
}