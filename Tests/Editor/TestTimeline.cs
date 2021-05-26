using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using CW.Core.Timeline;

namespace CW.Core.Timeline.Tests
{
    public class TestTimeline
    {
        private GlobalTimeline timeline;
        private List<ITimeable> applyOrder;
        private List<ITimeable> publishOrder;

        [SetUp]
        public void SetUp()
        {
            timeline = new GlobalTimeline(options: GlobalTimeline.Options.Auto);
            applyOrder = new List<ITimeable>();
            publishOrder = new List<ITimeable>();
        }

        [Test]
        public void TestSubscriberCalled()
        {
            TLTime expectedTime = TLTime.FromSeconds(3f);
            TLTime resultTime = TLTime.Zero;
            var timeable = TestTimeable.Empty();

            timeline.Subscribe<TestTimeable>(t => resultTime = timeline.Offset(t));
            timeline.Push(timeable, expectedTime);
            Assert.That(resultTime, Is.EqualTo(expectedTime));
        }

        [Test]
        public void TestApplyCalledInRightOrder()
        {
            var earlier = EmptyTimeable();
            var later = EmptyTimeable();
            var parent = TimeableWithApply(_ =>
            {
                timeline.Push(later, TLTime.FromSeconds(5f));
                timeline.Push(earlier, TLTime.FromSeconds(3f));
            });

            timeline.Push(parent, TLTime.FromMilliseconds(0));

            Assert.That(applyOrder, Is.EqualTo(new[] { parent, earlier, later }));
        }

        [Test]
        public void TestStablePushingAndPublishing()
        {
            var t1_1 = EmptyTimeable().Named("t1_1");
            var t1_2 = EmptyTimeable().Named("t1_2");
            var t1_3 = EmptyTimeable().Named("t1_3");
            var t2 = EmptyTimeable().Named("t2");

            var parent = TimeableWithApply(_ =>
            {
                timeline.Push(t1_1, TLTime.FromSeconds(1f));
                timeline.Push(t1_2, TLTime.FromSeconds(1f));
                timeline.Push(t2, TLTime.FromSeconds(2f));
                timeline.Push(t1_3, TLTime.FromSeconds(1f));
            });

            Subscribe<TestTimeable>();

            timeline.Push(parent, TLTime.FromMilliseconds(0));

            Assert.That(applyOrder, Is.EqualTo(new[] { parent, t1_1, t1_2, t1_3, t2 }));
            Assert.That(publishOrder, Is.EqualTo(new[] { parent, t1_1, t1_2, t1_3, t2 }));

        }

        [Test]
        public void TestSubscribeToInstance()
        {
            var t1 = EmptyTimeable();
            var t2 = EmptyTimeable();

            Subscribe(t2);

            timeline.Push(t1, TLTime.FromSeconds(1f));
            timeline.Push(t2, TLTime.FromSeconds(1f));

            Assert.That(publishOrder.Count == 1 && publishOrder[0] == t2);
        }

        [Test]
        public void TestLocaltimelineOffset()
        {
            var t = EmptyTimeable();
            var composed = ComposedWithApply(timeable => timeable.Timeline.Push(t, TLTime.FromSeconds(2f)));

            timeline.Push(composed, TLTime.FromSeconds(3f));

            Assert.AreEqual(timeline.Offset(t), TLTime.FromSeconds(5f));
        }

        [Test]
        public void TestSubscribeAfterPushWorks()
        {
            bool subscribeAfterPushFires = false;
            var test = EmptyTimeable();
            var child = ComposedWithApply(t =>
            {
                t.Timeline.Push(test, TLTime.FromMilliseconds(0));
                t.Timeline.Subscribe(test, _ => subscribeAfterPushFires = true);
            });
            timeline.Push(child, TLTime.FromMilliseconds(0));
            Assert.That(subscribeAfterPushFires, "subscribeAfterPushFires");
        }

        [Test]
        public void TestAdvance()
        {
            var tl = new GlobalTimeline(options: GlobalTimeline.Options.Manual);
            timeline = tl;

            var t1 = EmptyTimeable();
            timeline.Push(t1, TLTime.FromSeconds(1f));
            var t2 = EmptyTimeable();
            timeline.Push(t2, TLTime.FromSeconds(3f));
            
            Assert.That(applyOrder, Is.Empty);
            tl.Advance(TLTime.FromSeconds(0.5f));
            Assert.That(applyOrder, Is.Empty);
            tl.Advance(TLTime.FromSeconds(1f));
            Assert.That(applyOrder, Is.EqualTo(new[]{t1}));
            tl.Advance(TLTime.FromSeconds(2f));
            Assert.That(applyOrder, Is.EqualTo(new[]{t1}));
            tl.Advance(TLTime.FromSeconds(3f));
            Assert.That(applyOrder, Is.EqualTo(new[]{t1, t2}));
            tl.Advance(TLTime.FromSeconds(4f));
            Assert.That(applyOrder, Is.EqualTo(new[]{t1, t2}));
        }

        [Test]
        public void TestCantAdvanceAutoTimeline()
        {
            Assert.Throws<TimelineException>(() => (timeline as GlobalTimeline).Advance(TLTime.FromSeconds(10f)));
        }

        #region Completion

        [Test]
        public void TestCompletionInstant()
        {
            var t = EmptyTimeable();
            var completeTime = TLTime.FromSeconds(0f);
            timeline.Subscribe<Completed<TestTimeable>>(completed => completeTime = timeline.Offset(completed));
            timeline.Push(t, TLTime.FromSeconds(3f));
            Assert.That(completeTime, Is.EqualTo(TLTime.FromSeconds(3f)));
        }

        [Test]
        public void TestCompletionWithDuration()
        {
            var t = EmptySimple(TLTime.FromSeconds(3f));

            Subscribe<Completed<TestSimpleTimeable>>();

            timeline.Push(t, TLTime.FromSeconds(2f));

            Assert.That(publishOrder.Count, Is.EqualTo(1));
            Assert.AreEqual(timeline.Offset(publishOrder[0]), TLTime.FromSeconds(5f));
        }

        [Test]
        public void TestCompletionComposed()
        {
            var composed = ComposedWithApply(timeable => { timeable.Finish(TLTime.FromSeconds(5f)); });

            Subscribe<Completed<TestComposedTimeable>>();

            timeline.Push(composed, TLTime.FromSeconds(2f));

            Assert.AreEqual(timeline.Offset(publishOrder.Last()), TLTime.FromSeconds(7f));
        }

        [Test]
        public void TestSubscribeToCompletionInstance()
        {
            Activity test = EmptyTimeable(); // client might not know exact type of the activity (for instance DependencyWaiter)
            bool completeCaught = false;
            timeline.Subscribe(test.CompleteMarker(), completed => completeCaught = true);
            timeline.Push(test, TLTime.FromMilliseconds(0));
            Assert.That(completeCaught, "completeCaught");
        }

#endregion

        // [Test]
        // public void TestTLTimeEquality()
        // {
        //     var o = TLTime.FromLong(1000);
        //     var eq = TLTime.FromLong(1000);
        //     var noteq = TLTime.FromLong(1234);
        //     
        //     Assert.AreNotEqual(o, noteq);
        //     
        //     Assert.AreEqual(o, o);
        //     Assert.AreEqual(o, eq);
        //     
        //     Assert.AreEqual(o, TLTime.FromFloat(1f));
        //     Assert.AreEqual(o, 1000l);
        //     Assert.AreEqual(o, 1000);
        //     
        //     Assert.AreEqual(TLTime.FromFloat(1f), o);
        //     Assert.AreEqual(1000l, o);
        //     Assert.AreEqual(1000, o);
        // }

        [Test]
        public void TestSubscriptionLocality()
        {
            bool publishedInParentTimeline = false;
            bool publishedInOwnTimeline = false;
            bool publishedInChildTimeline = false;
            bool publishedInSiblingTimeline = false;


            var test = EmptyTimeable();

            var child = ComposedWithApply(t => t.Timeline.Subscribe(test, _ => publishedInChildTimeline = true));

            var timeable = ComposedWithApply(t =>
            {
                t.Timeline.Push(test, TLTime.FromSeconds(0f));
                t.Timeline.Push(child, TLTime.FromSeconds(0f));
                t.Timeline.Subscribe(test, _ => publishedInOwnTimeline = true);
            });

            var sibling = ComposedWithApply(t =>
            {
                t.Timeline.Subscribe(test, _ => publishedInSiblingTimeline = true);
            });

            timeline.Subscribe(test, _ => publishedInParentTimeline = true);
            timeline.Push(sibling, TLTime.FromSeconds(0f));
            timeline.Push(timeable, TLTime.FromSeconds(0f));

            Assert.That(publishedInParentTimeline, Is.True, "parent");
            Assert.That(publishedInOwnTimeline, Is.True, "own");
            Assert.That(publishedInChildTimeline, Is.False, "child");
            Assert.That(publishedInSiblingTimeline, Is.False, "sibling");
        }

        [Test]
        public void TestDontPushToPast()
        {
            timeline.SetCheckForTimeParadoxes(CheckForTimeParadoxesEnum.CheckAndThrow);
            
            var hitler = EmptyTimeable();
            var hitlerKiller = EmptyTimeable();
            var t = SimpleWithApply(TLTime.FromSeconds(5f), timeable => timeline.Push(hitler, TLTime.FromSeconds(4f)));
            timeline.Subscribe(t.CompleteMarker(), completed => timeline.Push(hitlerKiller, TLTime.FromSeconds(3f)));
            Assert.That(() => timeline.Push(t, TLTime.FromMilliseconds(0)), Throws.TypeOf<TimeParadoxException>());
            
            timeline.SetCheckForTimeParadoxes(CheckForTimeParadoxesEnum.DontCheck);         
        }

        #region Subsystems

        [Test]
        public void TestUngroupedSubscriptionsDoesntInterfere()
        {
            var test = EmptyTimeable();

            bool parentByTypeWorked = false;
            bool parentByInstanceWorked = false;
            bool secondParentByTypeWorked = false;
            bool secondParentByInstanceWorked = false;
            bool childByTypeWorked = false;
            bool childByInstanceWorked = false;
            bool secondChildByTypeWorked = false;
            bool secondChildByInstanceWorked = false;
            bool parentGroupedByTypeWorked = false;
            bool parentGroupedByInstanceWorked = false;
            bool childGroupedByTypeWorked = false;
            bool childGroupedByInstanceWorked = false;

            timeline.Subscribe<TestTimeable>(_ => parentByTypeWorked = true);
            timeline.Subscribe(test, _ => parentByInstanceWorked = true);
            timeline.Subscribe<TestTimeable>(_ => secondParentByTypeWorked = true);
            timeline.Subscribe(test, _ => secondParentByInstanceWorked = true);
            timeline.Subscribe<TestTimeable>(_ => parentGroupedByTypeWorked = true, "a");
            timeline.Subscribe(test, _ => parentGroupedByInstanceWorked = true, "b");

            var child = ComposedWithApply(t =>
            {
                t.Timeline.Subscribe<TestTimeable>(_ => childByTypeWorked = true);
                t.Timeline.Subscribe(test, _ => childByInstanceWorked = true);
                t.Timeline.Subscribe<TestTimeable>(_ => secondChildByTypeWorked = true);
                t.Timeline.Subscribe(test, _ => secondChildByInstanceWorked = true);
                t.Timeline.Subscribe<TestTimeable>(_ => childGroupedByTypeWorked = true, "c");
                t.Timeline.Subscribe(test, _ => childGroupedByInstanceWorked = true, "d");
                t.Timeline.Push(test, TLTime.FromMilliseconds(0));
            });

            timeline.Push(child, TLTime.FromMilliseconds(0));

            Assert.That(parentByTypeWorked, "parentByTypeWorked");
            Assert.That(parentByInstanceWorked, "parentByInstanceWorked");
            Assert.That(secondParentByTypeWorked, "secondParentByTypeWorked");
            Assert.That(secondParentByInstanceWorked, "secondParentByInstanceWorked");
            Assert.That(childByTypeWorked, "childByTypeWorked");
            Assert.That(childByInstanceWorked, "childByInstanceWorked");
            Assert.That(secondChildByTypeWorked, "secondChildByTypeWorked");
            Assert.That(secondChildByInstanceWorked, "secondChildByInstanceWorked");
            Assert.That(parentGroupedByTypeWorked, "parentGroupedByTypeWorked");
            Assert.That(parentGroupedByInstanceWorked, "parentGroupedByInstanceWorked");
            Assert.That(childGroupedByTypeWorked, "childGroupedByTypeWorked");
            Assert.That(childGroupedByInstanceWorked, "childGroupedByInstanceWorked");
        }

        [Test]
        public void TestSubsystemsTwoSubscriptionsOnSameTimeline()
        {
            var test = EmptyTimeable();

            Subscribe<TestTimeable>(null, "some");
            Assert.That(() => Subscribe<TestTimeable>(null, "some"), Throws.Exception, "subscribe second typed");

            Subscribe(test, null, "other");
            Assert.That(() => Subscribe(test, null, "other"), Throws.Exception, "subscribe second instance");

            bool typedCalled = false, instanceCalled = false;
            Subscribe<TestTimeable>(_ => typedCalled = true, "another");
            Subscribe(test, _ => instanceCalled = true, "another");
            timeline.Push(test, TLTime.FromMilliseconds(0));
            Assert.That(typedCalled, Is.False, "typedCalled");
            Assert.That(instanceCalled, Is.True, "instanceCalled");
        }

        [Test]
        public void TestSubsystemsChildSubscriptionOverridesParent([Values(false, true)] bool parentIsInstance, [Values(false, true)] bool childIsInstance)
        {
            var test = EmptyTimeable();

            bool parentCalled = false, childCalled = false;

            if (parentIsInstance)
            {
                timeline.Subscribe(test, _ => parentCalled = true, "some");
            }
            else
            {
                timeline.Subscribe<TestTimeable>(_ => parentCalled = true, "some");
            }

            var child = ComposedWithApply(t =>
            {
                t.Timeline.Push(test, TLTime.FromMilliseconds(0));
                if (childIsInstance)
                {
                    t.Timeline.Subscribe(test, _ => childCalled = true, "some");
                }
                else
                {
                    t.Timeline.Subscribe<TestTimeable>(_ => childCalled = true, "some");
                }
            });
            timeline.Push(child, TLTime.FromMilliseconds(0));
            Assert.That(!parentCalled && childCalled, $"child {(childIsInstance ? "instance" : "typed")} overrides parent {(parentIsInstance ? "instance" : "typed")}");
        }

        [Test]
        public void TestSubsystemsChildInstanceSubscriptionOverridesOnlyItsArgument()
        {
            var test = EmptyTimeable();
            var test2 = EmptyTimeable();

            List<ITimeable> parentCalls = new List<ITimeable>();
            bool childCalled = false;
            timeline.Subscribe<TestTimeable>(timeable => parentCalls.Add(timeable), "some");
            var child = ComposedWithApply(t =>
            {
                t.Timeline.Push(test, TLTime.Zero);
                t.Timeline.Push(test2, TLTime.Zero);
                t.Timeline.Subscribe(test, _ => childCalled = true, "some");
            });
            timeline.Push(child, TLTime.Zero);
            Assert.That(childCalled, "childCalled");
            Assert.That(parentCalls.Count, Is.EqualTo(1), "parentCalls.Count == 1");
            Assert.That(parentCalls[0], Is.EqualTo(test2), "parent subscription called for test2");
        }

        #endregion


        private TestTimeable EmptyTimeable()
        {
            return TimeableWithApply(null);
        }

        private TestTimeable TimeableWithApply(Action<TestTimeable> action)
        {
            return TestTimeable.WithApply(timeable =>
            {
                applyOrder.Add(timeable);
                action?.Invoke(timeable);
            });
        }

        private TestSimpleTimeable EmptySimple(TLTime duration)
        {
            return SimpleWithApply(duration, null);
        }

        private TestSimpleTimeable SimpleWithApply(TLTime duration, Action<TestSimpleTimeable> action)
        {
            return TestSimpleTimeable.WithApply(duration, timeable =>
            {
                applyOrder.Add(timeable);
                action?.Invoke(timeable);
            });
        }

        private TestComposedTimeable EmptyComposed()
        {
            return ComposedWithApply(null);
        }

        private TestComposedTimeable ComposedWithApply(Action<TestComposedTimeable> action)
        {
            return TestComposedTimeable.WithApply(timeable =>
           {
               applyOrder.Add(timeable);
               action?.Invoke(timeable);
           });
        }

        private void Subscribe<T>(T timeable, Action<T> action = null, string group = null) where T : ITimeable
        {
            timeline.Subscribe(timeable, t =>
            {
                publishOrder.Add(t);
                action?.Invoke(t);
            }, group);
        }

        private void Subscribe<T>(Action<T> action = null, string group = null) where T : ITimeable
        {
            timeline.Subscribe<T>(timeable =>
            {
                publishOrder.Add(timeable);
                action?.Invoke(timeable);
            }, group);
        }
    }
}
