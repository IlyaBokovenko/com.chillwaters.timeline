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
            GlobalTimeline.SetPoolingPolicy(TimelinePoolingPolicy.Client);
            timeline = new GlobalTimeline(options:  GlobalTimeline.Options.Manual);
        }

        [Test]
        public void TestGCAppliedAndCompleteActivity()
        {
            var a = new SimpleActivity(10);
            
            timeline.Push(a, 100l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(99l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(100l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(101l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));

            timeline.Advance(109l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(110l.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }
        
        
        [Test]
        public void TestDontGCActivitySubscribedToType()
        {
            var a = new AcivitySubscribedToType(10);
            timeline.Push(a, 100l.TLMilliseconds());
            timeline.Advance(200l.TLMilliseconds());
            
            Assert.NotNull(timeline.ActivityById(a.Id));
        }
        
        [Test]
        public void TestInstanceSubscription()
        {
            var subscription = new SimpleActivity(10l);
            var a = new AcivitySubscribedToInstance(subscription);
            
            timeline.Push(a, 100l.TLMilliseconds());
            timeline.Push(subscription, 200l.TLMilliseconds());
            
             timeline.Advance(199l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(200l.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }
        
        [Test]
        public void TestCompletionSubscription()
        {
            var subscription = new SimpleActivity(10l);
            var a = new AcivitySubscribedToCompletion(subscription);
            
            timeline.Push(a, 100l.TLMilliseconds());
            timeline.Push(subscription, 200l.TLMilliseconds());
            
            timeline.Advance(209l.TLMilliseconds());
            Assert.NotNull(timeline.ActivityById(a.Id));
            
            timeline.Advance(210l.TLMilliseconds());
            Assert.Null(timeline.ActivityById(a.Id));
        }
        
        [Test]
        public void TestGarbageCollectedButReferencedActivitySerialized()
        {
            ActivityReferencingToOther beingReferenced = new ActivityReferencingToOther();
            ActivityReferencingToOther referenceHolder = new ActivityReferencingToOther();
            referenceHolder.other = beingReferenced;
            
            timeline.Push(beingReferenced, 100l.TLMilliseconds());
            timeline.Push(referenceHolder, 200l.TLMilliseconds());
            
            timeline.Advance(150l.TLMilliseconds());

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
            private TlTime duration;
            public TlTime Duration => duration;

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

            private void Action(SimpleActivity _) { }
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
                Timeline.Subscribe( subscription, Action);
            }

            private void Action(SimpleActivity _) { }
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
                Timeline.Subscribe( subscription.CompleteMarker(), Action);
            }

            private void Action(Completed<SimpleActivity> _){ }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ActivityReferencingToOther : Activity<ActivityReferencingToOther>
        {
            [JsonProperty]
            public ActivityReferencingToOther other;
            public override void Apply()
            {
            }
        }
    }
}