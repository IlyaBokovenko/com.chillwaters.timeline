using System;

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

        private TlTime duration;
        public TlTime Duration => duration;

        private TestSimpleTimeable(TlTime duration, Action<TestSimpleTimeable> action)
        {
            this.duration = duration;
            this.action = action;
        }

        public static TestSimpleTimeable WithApply(TlTime duration, Action<TestSimpleTimeable> action)
        {
            return new TestSimpleTimeable(duration, action);
        }

        public static TestSimpleTimeable Empty(TlTime duration)
        {
            return new TestSimpleTimeable(duration, null);
        }

        public override void Apply()
        {
            action?.Invoke(this);
        }
    }

    internal class TestComposedTimeable  : Activity<TestComposedTimeable >, IComposedTimeable
    {
        private Action<TestComposedTimeable> action;


        public CompletionPromise completionPromise = new CompletionPromise();
        public ICompletionPromise CompletionPromise => completionPromise;

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
    }
}