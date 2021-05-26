using CW.Core.Timeline.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace CW.Core.Timeline.Tests
{
    public class TestTimelineGC
    {
        private GlobalTimeline timeline;

        [SetUp]
        public void SetUp()
        {
            timeline = new GlobalTimeline(options: GlobalTimeline.Options.Manual);
        }

        [Test]
        public void TestGCAppliedAndCompleteActivity()
        {
            var a = new SimpleActivity(10);

            timeline.Push(a, 100L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(99L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(100L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(101L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(109L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(110L.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }


        [Test]
        public void TestDontGCActivitySubscribedToType()
        {
            var a = new AcivitySubscribedToType(10);
            timeline.Push(a, 100L.TLMilliseconds());
            timeline.Advance(200L.TLMilliseconds());

            Assert.NotNull(timeline.ActivityById(a.Id));
        }

        [Test]
        public void TestInstanceSubscription()
        {
            var subscription = new SimpleActivity(10L);
            var a = new AcivitySubscribedToInstance(subscription);

            timeline.Push(a, 100L.TLMilliseconds());
            timeline.Push(subscription, 200L.TLMilliseconds());

            timeline.Advance(199L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(200L.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }

        [Test]
        public void TestCompletionSubscription()
        {
            var subscription = new SimpleActivity(10L);
            var a = new AcivitySubscribedToCompletion(subscription);

            timeline.Push(a, 100L.TLMilliseconds());
            timeline.Push(subscription, 200L.TLMilliseconds());

            timeline.Advance(209L.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(210L.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }

        [Test]
        public void TestGarbageCollectedButReferencedActivitySerialized()
        {
            var beingReferenced = new ActivityReferencingToOther();
            var referenceHolder = new ActivityReferencingToOther();
            referenceHolder.other = beingReferenced;

            timeline.Push(beingReferenced, 100L.TLMilliseconds());
            timeline.Push(referenceHolder, 200L.TLMilliseconds());

            timeline.Advance(150L.TLMilliseconds());

            var serializer = TimelineJsonSerializator.CreatePretty();
            var json = serializer.Serialize(timeline);
            Debug.Log(json);
            var deserializedTimeline = serializer.Deserialize<GlobalTimeline>(json);

            var deserializedReferenceHolder =
                deserializedTimeline.ActivityById<ActivityReferencingToOther>(referenceHolder.Id);
            Assert.NotNull(deserializedReferenceHolder);
            Assert.NotNull(deserializedReferenceHolder.other);
        }

        private class SimpleActivity : Activity<SimpleActivity>, ISimpleTimeable
        {
            private TLTime duration;
            public TLTime Duration => duration;

            public SimpleActivity(long duration)
            {
                this.duration = duration.TLMilliseconds();
            }

            public override void Apply()
            {
            }
        }

        private class AcivitySubscribedToType : SimpleActivity
        {
            public AcivitySubscribedToType(long duration) : base(duration)
            {
            }

            public override void Apply()
            {
                Timeline.Subscribe<SimpleActivity>(Action);
            }

            private void Action(SimpleActivity _)
            {
            }
        }

        private class AcivitySubscribedToInstance : Activity<AcivitySubscribedToInstance>
        {
            private SimpleActivity subscription;

            public AcivitySubscribedToInstance(SimpleActivity subscription)
            {
                this.subscription = subscription;
            }

            public override void Apply()
            {
                Timeline.Subscribe(subscription, Action);
            }

            private void Action(SimpleActivity _)
            {
            }
        }

        private class AcivitySubscribedToCompletion : Activity<AcivitySubscribedToCompletion>
        {
            private SimpleActivity subscription;

            public AcivitySubscribedToCompletion(SimpleActivity subscription)
            {
                this.subscription = subscription;
            }

            public override void Apply()
            {
                Timeline.Subscribe(subscription.CompleteMarker(), Action);
            }

            private void Action(Completed<SimpleActivity> _)
            {
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ActivityReferencingToOther : Activity<ActivityReferencingToOther>
        {
            [JsonProperty] public ActivityReferencingToOther other;

            public override void Apply()
            {
            }
        }
    }
}