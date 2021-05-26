using System;

[assembly:System.Runtime.CompilerServices.InternalsVisibleTo("Unity.Timeline.EditorTests")]

namespace CW.Core.Timeline.Tests
{
    internal class TestTimeable  : Activity<TestTimeable >
    {
        private Action<TestTimeable> action;

        private TestTimeable(Action<TestTimeable> action)
        {
            this.action = action;
        }

        public static TestTimeable WithApply(Action<TestTimeable> action)
        {
            return new TestTimeable(action);
        }

        public static TestTimeable Empty()
        {
            return new TestTimeable(null);
        }

        public override void Apply()
        {
            action?.Invoke(this);
        }
    }

    internal class TestSimpleTimeable  : Activity<TestSimpleTimeable >, ISimpleTimeable
    {
        private Action<TestSimpleTimeable> action;

        private TLTime duration;
        public TLTime Duration => duration;

        private TestSimpleTimeable(TLTime duration, Action<TestSimpleTimeable> action)
        {
            this.duration = duration;
            this.action = action;
        }

        public static TestSimpleTimeable WithApply(TLTime duration, Action<TestSimpleTimeable> action)
        {
            return new TestSimpleTimeable(duration, action);
        }

        public static TestSimpleTimeable Empty(TLTime duration)
        {
            return new TestSimpleTimeable(duration, null);
        }

        public override void Apply()
        {
            action?.Invoke(this);
        }
    }

    internal class TestCompletionPromise : ICompletionPromise
    {
        private event Action<TLTime> _subscription;

        IDisposable ICompletionPromise.Subscribe(Action<TLTime> callback)
        {
            _subscription += callback;
            return new Disposable(this, callback);
        }

        public void Finish(TLTime time)
        {
            _subscription?.Invoke(time);
        }

        private struct Disposable : IDisposable
        {
            private TestCompletionPromise _timeable;
            private Action<TLTime> _subject;

            public Disposable(TestCompletionPromise timeable, Action<TLTime> subject)
            {
                _timeable = timeable;
                _subject = subject;
            }

            public void Dispose()
            {
                _timeable._subscription -= _subject;
            }
        }
    }

    internal class TestComposedTimeable  : Activity<TestComposedTimeable>, IComposedTimeable
    {
        private Action<TestComposedTimeable> action;
        private TestCompletionPromise _promise = new TestCompletionPromise();
        public ICompletionPromise CompletionPromise => _promise;

        private TestComposedTimeable(Action<TestComposedTimeable> action)
        {
            this.action = action;
        }

        public static TestComposedTimeable WithApply(Action<TestComposedTimeable> action)
        {
            return new TestComposedTimeable(action);
        }

        public static TestComposedTimeable Empty()
        {
            return new TestComposedTimeable(null);
        }

        public override void Apply()
        {
            action?.Invoke(this);
        }

        public void Finish(TLTime time)
        {
            _promise.Finish(time);
        }
    }
}