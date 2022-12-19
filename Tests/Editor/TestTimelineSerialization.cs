using System.Collections.Generic;
using CW.Core.Hash;
using CW.Core.Timeline.Serialization;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;

namespace CW.Core.Timeline.Tests
{
    public class TestTimelineSerialization
    {
        private GlobalTimeline timeline;

        [SetUp]
        public void SetUp()
        {
            GlobalTimeline.SetPoolingPolicy(TimelinePoolingPolicy.Client);
            timeline = new GlobalTimeline(options: GlobalTimeline.Options.Manual);
        }

        [Test]
        public void TestHash()
        {
            var hash = timeline.ComputeHash();
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash()));
            
            timeline.Push(new TestSerializableTimeable(1, "aaa"), TlTime.FromSeconds(1f));
            Assert.That(hash, Is.Not.EqualTo(timeline.ComputeHash()));
            hash = timeline.ComputeHash();
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash()));
            
            timeline.Push(new TestSerializableTimeable(2, "bbb"), TlTime.FromSeconds(2f));
            Assert.That(hash, Is.Not.EqualTo(timeline.ComputeHash()));

            var bw = new TracingHashWriter();
            //hash = timeline.ComputeHash();
            timeline.WriteContentHash(bw);
            hash = bw.ComputeHash();
            Debug.Log($"before:\n{bw}");
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash()));
            
            timeline.Subscribe<TestSerializableTimeable>(MockSubscription);
            bw = new TracingHashWriter();
            timeline.WriteContentHash(bw);
            hash = bw.ComputeHash();
            Debug.Log($"\nafter:\n{bw}");
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash())); // external subscriptions not counted
            hash = timeline.ComputeHash();
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash()));
            
            timeline.Advance(TlTime.FromSeconds(2f));
            Assert.That(hash, Is.Not.EqualTo(timeline.ComputeHash()));
            hash = timeline.ComputeHash();
            Assert.That(hash, Is.EqualTo(timeline.ComputeHash()));
        }
        
        [Test]
        public void TestUnhashableThrows()
        {
            timeline.Push(new UnhashableTimeable(), TlTime.FromSeconds(1f));
            Assert.Throws<TimelineException>(() => timeline.ComputeHash());
        }

        [Test]
        public void TestLambdaThrows()
        {
            timeline.Push(new ActivityWithLambda(), TlTime.FromSeconds(1f));
            Assert.Throws<TimelineException>(() => timeline.ComputeHash());
        }

        [Test]
        public void TestLocalFunctionThrows()
        {
            timeline.Push(new ActivityWithLocalFunction(), TlTime.FromSeconds(1f));
            Assert.Throws<TimelineException>(() => timeline.ComputeHash());
        }

        [Test]
        public void TestSimpleCollectionsDeserialization()
        {
            var json = @"
            {
                ""str"": ""hello"",
                ""list"": [
                    ""aaa"",
                    [""bbb""],
                    {
                        ""int"": 1
                    }
                ],
                ""dict"": {
                    ""float"": 1.1
                }
            }
            ";

            var settings = new JsonSerializerSettings{Converters = new JsonConverter[]{new SimpleCollectionsConverter()}};
            var obj = JsonConvert.DeserializeObject<object>(json, settings);

            var dict = obj as Dictionary<string, object>; 
            Assert.NotNull(dict);
            var list = dict["list"] as List<object>;
            Assert.NotNull(list);
            Assert.That(list[1] is IList<object>);
            Assert.That(list[2] is IDictionary<string, object>);
            Assert.That(dict["dict"] is IDictionary<string, object>);

            var jsonSerialized = JsonConvert.SerializeObject(obj, Formatting.None, settings);
            var objDeserialized = JsonConvert.DeserializeObject<object>(jsonSerialized, settings);
            var jsonReserialized = JsonConvert.SerializeObject(objDeserialized, Formatting.None, settings);
            Assert.That(jsonSerialized, Is.EqualTo(jsonReserialized));
        }

        [JsonObject]
        class Some {}

        [Test]
        public void TestSimpleCollectionsWithIdsDeserialization()
        {
            var dup = new Some();
            var o = new Dictionary<string, object>
            {
                {"a", dup},
                {"b", dup}
            };
            

            var settings1 = new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects
            };

            var str = JsonConvert.SerializeObject(o, settings1);
            Debug.Log(str);
            
            var settings2 = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                Converters = new JsonConverter[]{new SimpleCollectionsConverter()}
            };
            var o2 = JsonConvert.DeserializeObject<object>(str, settings2);

            var dict = o2 as Dictionary<string, object>;
            
            Debug.Log(JsonConvert.SerializeObject(dict));
            
            Assert.NotNull(dict);
            Assert.That(dict["a"], Contains.Key("$id"));
        }

        [Test]
        public void TestSerialization()
        {
            var t = new SerializableTimeableWithCallback("t");
            timeline.Push(t, TlTime.FromSeconds(1f));
            timeline.Advance(TlTime.FromSeconds(1f));
            
            string json = JsonConvert.SerializeObject(timeline, Formatting.Indented, CreateSettings());
            
            var deserialized = JsonConvert.DeserializeObject<GlobalTimeline>(json, CreateSettings());
            
            var debugWriter = new TracingHashWriter();
            timeline.WriteContentHash(debugWriter);
            // Debug.Log("src");
            // Debug.Log(debugWriter);
            
            debugWriter = new TracingHashWriter();
            deserialized.WriteContentHash(debugWriter);
            // Debug.Log("\n\ndeserialized");
            // Debug.Log(debugWriter);
            
            Debug.Log($"\n\njson:\n{json}");
            
            Assert.That(deserialized.ComputeHash(), Is.EqualTo(timeline.ComputeHash()));
        }
        
        [Test]
        public void TestSelfSubscribeSerialization()
        {
            var t = new SelfSubscribingActivity();
            timeline.Push(t, TlTime.FromSeconds(1f));
            timeline.Advance(TlTime.FromSeconds(1f));
            
            string json = JsonConvert.SerializeObject(timeline, Formatting.Indented, CreateSettings());
            Debug.Log(json);
            var deserialized = JsonConvert.DeserializeObject<GlobalTimeline>(json, CreateSettings());
            
            var bw = new TracingHashWriter();
            timeline.WriteContentHash(bw);
            var srcHash = bw.ComputeHash();
            Debug.Log($"src:\n{bw}");
            
            bw = new TracingHashWriter();
            timeline.WriteContentHash(bw);
            var deserializedHash = bw.ComputeHash();
            Debug.Log($"deserialized:\n{bw}");
            
            Assert.That(srcHash, Is.EqualTo(deserializedHash));
        }

        [Test]
        public void TestDeserializeEmpty()
        {
            string json = JsonConvert.SerializeObject(timeline, Formatting.Indented, CreateSettings());
            Assert.DoesNotThrow(() => JsonConvert.DeserializeObject<GlobalTimeline>(json, CreateSettings()));
        }

        [Test]
        public void Test2SubscriptionsOnCompletionOfOneActivity()
        {
            var target = new TestSerializableTimeable("a");
            timeline.Push(target, TlTime.FromSeconds(3f));
            
            var subscriber1 = new SubscribeToOther(target);
            timeline.Push(subscriber1, TlTime.FromSeconds(1f));
            var subscriber2 = new SubscribeToOther(target);
            timeline.Push(subscriber2, TlTime.FromSeconds(1f));
            
            timeline.Advance(TlTime.FromSeconds(1f));
            
            Assert.DoesNotThrow(() =>  JsonConvert.SerializeObject(timeline, Formatting.Indented, CreateSettings()));
        }
        
        JsonSerializerSettings CreateSettings()
        {
            return TimelineJsonSerializator.CreateSettings();
        }

        [Test]
        public void TestSerializeActivity()
        {
            var settings = new JsonSerializerSettings {PreserveReferencesHandling = PreserveReferencesHandling.Objects};
            var activity = new TestSerializableTimeable(1, "aaa");
            timeline.Push(activity, TlTime.FromSeconds(3f));
            string json = JsonConvert.SerializeObject(activity, Formatting.Indented, settings);

            Debug.Log(json);
        }


        [Test, Timeout(1000)]
        public void TestSerializeReferenceLoop()
        {
            var a1 = new SelfReferencingActivity();
            var a2 = new SelfReferencingActivity();

            a1.activity = a2;
            a2.activity = a1;
            
            timeline.Push(a1, TlTime.FromMilliseconds(1000));
            timeline.Push(a2, TlTime.FromMilliseconds(2000));

            var serializer = TimelineJsonSerializator.CreatePretty();

            string json = null;
            Assert.DoesNotThrow(() => json = serializer.Serialize(timeline));
            Debug.Log(json);
        }

        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        private class TestComposedActivity : Activity<TestComposedActivity>, IComposedTimeable, IContentHash
        {
            private CompletionPromise _promise = new CompletionPromise();
            public ICompletionPromise CompletionPromise => _promise;

            [JsonProperty]
            private TlTime _duration;

            public TestComposedActivity(TlTime duration)
            {
                _duration = duration;
            }

            public override void Apply() 
            {
                _promise.Complete(_duration);
            }

            public void WriteContentHash(ITracingHashWriter writer){}
        }

        [Test]
        public void TestSerializeComposedActivity()
        {
            var activity = new TestComposedActivity(1000l.TLMilliseconds());
            timeline.Push(activity, 500l.TLMilliseconds());

            var json = JsonConvert.SerializeObject(timeline, CreateSettings());
            timeline = JsonConvert.DeserializeObject<GlobalTimeline>(json, CreateSettings());
            
            bool triggered = false;
            var deserializedActivity = timeline.ActivityById(activity.Id);
            timeline.Subscribe(deserializedActivity.MakeCompletionMarker(), _ => triggered = true);
            
            timeline.Advance(2000l.TLMilliseconds());
            Assert.True(triggered);
        }

        // [Test]
        // public void TestDateTimeOffsetAsLongConverter()
        // {
        //     var o = DateTimeOffset.Now;
        //     var json = JsonConvert.SerializeObject(o, Formatting.Indented, new DateTimeOffsetAsLongConverter());
        //     var o2 = JsonConvert.DeserializeObject<DateTimeOffset>(json, new DateTimeOffsetAsLongConverter());
        //     Assert.That(o2, Is.EqualTo(o));
        // }

        private void MockSubscription(TestSerializableTimeable _)
        {
        }

        class ActivityWithLambda : Activity<ActivityWithLambda>
        {
            public override void Apply()
            {
                Timeline.Subscribe<Tests.TestTimeable>(timeable => { });
            }
        }

        class ActivityWithLocalFunction : Activity<ActivityWithLocalFunction>
        {
            public override void Apply()
            {
                Timeline.Subscribe<TestTimeable>(LocalFunction);
                void LocalFunction(Tests.TestTimeable _) { }
            }
        }
        private class SubscribeToOther : Activity<SubscribeToOther>, IContentHash
        {
            private TestSerializableTimeable activity;

            public SubscribeToOther(TestSerializableTimeable activity)
            {
                this.activity = activity;
            }

            public override void Apply()
            {
                Timeline.Subscribe(activity.MakeCompletionMarker(), OnComplete);
            }

            public void WriteContentHash(ITracingHashWriter writer)
            {
            }

            private void OnComplete(Completed<ITimeable> _) {}
        }


        [JsonObject(MemberSerialization = MemberSerialization.OptIn)]
        private class TestSerializableTimeable : Activity<TestSerializableTimeable>, IContentHash
        {
            [JsonProperty]
            private string someStr;
            
            public TestSerializableTimeable(string someStr)
            {
                this.someStr = someStr;
            }

            [JsonConstructor]
            public TestSerializableTimeable(long id, string someStr) : this(someStr)
            {
                SetId(id);
            }

            public override void Apply(){}

            public void WriteContentHash(ITracingHashWriter writer)
            {
                writer.Write(someStr.GetHashCode());
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class SerializableTimeableWithCallback : TestSerializableTimeable
        {
            public TestSerializableTimeable fromCallback;
            public static TestSerializableTimeable s_fromCallback;

            public SerializableTimeableWithCallback(string someStr) : base(someStr)
            {
            }

            [JsonConstructor]
            public SerializableTimeableWithCallback(long id, string someStr) : base(id, someStr)
            {
            }

            public override void Apply()
            {
                var test = new TestSerializableTimeable("test");
                Timeline.Push(test, TlTime.FromSeconds(1f));
                Timeline.Subscribe(test, Callback);
                Timeline.Subscribe<TestSerializableTimeable>(Callback);
            }

            public void Callback(TestSerializableTimeable other)
            {
                fromCallback = other;
            }

            public static void StaticCallback(TestSerializableTimeable other)
            {
                s_fromCallback = other;
            }
        }

        private class UnhashableTimeable : Activity<UnhashableTimeable>
        {
            public override void Apply(){}
        }

        private class SelfSubscribingActivity : Activity<SelfSubscribingActivity>, ISimpleTimeable, IContentHash
        {
            public TlTime Duration => TlTime.FromSeconds(1f);

            public override void Apply()
            {
                Timeline.Subscribe(this.CompleteMarker(), WowThatsMe);
            }

            void WowThatsMe(Completed<SelfSubscribingActivity> _)
            {
            }

#region IContentHash
            public void WriteContentHash(ITracingHashWriter writer)
            {
            }
#endregion
        }

        private class SelfReferencingActivity : Activity<SelfReferencingActivity>, ISimpleTimeable, IContentHash
        {
            private static int s_lastId;
            private int id;
            
            [JsonProperty]
            public SelfReferencingActivity activity;

            public SelfReferencingActivity()
            {
                id = s_lastId++;
            }

            public TlTime Duration => TlTime.FromMilliseconds(1000);
            
            public override void Apply()
            {
            }

            public void WriteContentHash(ITracingHashWriter writer)
            {
                writer.Write(id);
            }
        }
    }
}