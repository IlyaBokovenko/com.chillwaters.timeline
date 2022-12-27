# Purpose #
Timeline was developed as a part of core tech in [Match2](https://www.gamedeveloper.com/design/match-game-mechanics-an-exhaustive-survey) mobile game. The primary goals were to obtain a strict model of time-evolving process, decouple it from visual representation, with possibility to write unit tests, serialize, replay, synchronize with server.  Timeline represents a "time aspect" of some modelled system. Structurally it's just a container for time-ordered activities, which produce each other, depend on each other and do some changes with "state" model. It allows to push activities to certain points of time and subscribe to activites by instance or by type from inside or outside of Timeline. Timeline can be forwarded in time manually or automatically, triggering activities along the way, which triggers subscriptions to activities' start and finish markers, which can lead to pushing new activities to Timeline and so on. From design perspective it resembles a hybrid of scheduler and message bus.
# Features #
* Highly optimized. As part of core tech stack of mobile application Timeline was greately optimized for perfomance and memory usage. It uses memory pools to reduce GC allocations and calcuation throttling to spread work over several steps ([DrainIteration](Runtime/GlobalTimeline.cs))
* Deterministic. Timeline is based on long-precision integer arithmetics, so no rounding problems. Timeline internally uses stable sorted collections, so the same input always produces same results.
* Incapsulated. Timeline is a container which can be cloned or serialized, and copy of Timeline will behave exactly the same as the originator.
* Platform Indenendent. Timeline doesn't use any Unity classes (except for logs which turned OFF by default), so it can be used in any environment. It doesn't depend on any system timers or schedulers, although can be easely syncronized with one.
* Self contained. Timeline framework doesn't depend on any third party libraries besides those which come with Unity.
* Serialized. Thanks to Newtonsoft.Json serializer complemented with a couple of custom [Json converters](Runtime/Timeline+Serialization.cs), Timeline is serializable to optimized and human-readable json format. Being deserialized, Timeline copy is an exact copy of it's originator.
* Supports state hash calculation. Timeline supports interfaces for hashing it's state and state of it's activities. Also some efforts are put to simplify tracing of hash mismatches.
* Server-friendly. Due to the features stated above Timeline can be used on server side and keep it synchronized with client.
* Visualization tool to analyze activity chains (See [Example](#example) section)
# Installation #
* Use Unity Package Manager to add package using Git URL

# Usage #
## Example ##
There is an Example package in package root folder Packages/CW Timeline

Import it in the project and select menu CW Timeline / Open Timeline Visualizer

You'll see a window with a visualization of example timeline. It demonstrates various activities hierarchy with durations, labels and timecodes

In this window you can:
* Click on activity to see it's relations
* Use search field to search activity by description
* Save/Load timeline into JSON
* Compare 2 timelines 

## Basic Concepts ##
[Activity](Runtime/Activity.cs) - a worker unit of timeline. It's a base class for all timeline activities. It implements interface ITimeable in which client should override Apply function which is body of activity for making some useful work. Apply is executed when timeline reaches the moment at which activity was pushed. After that all subscriptions to this activity are triggered.
```csharp
public class SomeActivity : Activity<SomeActivity>
{
    public override void Apply()
    {
        Debug.Log("Do a piece of work");
    }
}
```
[GlobalTimeline](Runtime/GlobalTimeline.cs) - root container for activities. It implements [ITimeline](Runtime/Contract/ITimeline.cs) interface for generic work with activities and some methods to control overall timeline behavior. Two main methods of **ITimeline** interface are **Push** and **Subscribe**
```csharp 
void Push(ITimeable timeable, TLTime offset);
```
Pushes activity into timeline at a certain point of time.
```csharp
void Subscribe<T>(Action<T> action, string subsystem = null) where T : ITimeable;
void Subscribe<T>(T timeable, Action<T> action, string subsystem = null) where T : ITimeable;
```
Subscribes to timeline activity by instance or by type. Subscriptions trigger when timeline reaches moment at which activity was pushed. Timeline advances automatically on every **Push** if GlobalTimeline was created with *Auto* option or manually with **Advance** call if it was created with *Manual* option.
```csharp
public void Advance(TLTime time)
```
Advances timeline at specified point of time in case of Manual timeline.

Activity also has its own timeline, which starts at the moment Activity was pushed to parent timeline. It's called [LocalTimeline](Runtime/LocalTimeline.cs). Chidren activities may be pushed to local timeline of parent activity. Subscriptions to local timeline only trigger for children activities:
```csharp
public class ChildActivity : Activity<ChildActivity>
{
    private string id;

    public ChildActivity(string id)
    {
        this.id = id;
    }

    public override void Apply()
    {
        Debug.Log($"I'm child {id} at time {Timeline.Offset()}");
    }
}

public class ParentActivity : Activity<ParentActivity>
{
    public override void Apply()
    {
        Timeline.Subscribe<ChildActivity>(a => Debug.Log("Child callback on ParentActivity"));
        Timeline.Push(new ChildActivity("B"), TLTime.FromMilliseconds(3));
    }
}

var timeline = new GlobalTimeline();
timeline.Subscribe<ChildActivity>(a => Debug.Log("Child callback on GlobalTimeline"));
timeline.Push(new ChildActivity("A"), TLTime.FromMilliseconds(1));
timeline.Push(new ParentActivity(), TLTime.FromMilliseconds(2));

```
Produces output:
```
I'm child A at time 1
Child callback on GlobalTimeline
I'm child B at time 5
Child callback on GlobalTimeline
Child callback on ParentActivity
```

Activity has a duration. The simpliest type of activity is an event. It has zero duration. If activity implements interface [ISimpleTimeable](Runtime/Contract/ITimeable.cs) it has fixed duration. If activity implements interface [IComposedTimeable](Runtime/Contract/ITimeable.cs) it has duration, not known at the time of creation. Client may subscribe to activity end using completion type for type subscriptions and completion marker for instance subscriptions.
Here is an example of using fixed duration activity and type subscription:
```csharp
public class FixedDuration : Activity<FixedDuration>, ISimpleTimeable
{
    public TLTime Duration => TLTime.FromMilliseconds(3);
    public override void Apply() {}
}
var timeline = new GlobalTimeline();
timeline.Subscribe<FixedDuration>(a => Debug.Log($"activity start: {timeline.Offset(a)}"));
timeline.Subscribe<Completed<FixedDuration>>(a => Debug.Log($"activity finish: {timeline.Offset(a)}"));
timeline.Push(new FixedDuration(), TLTime.FromMilliseconds(2));

```
Produces output:
```
activity start: 2
activity finish: 5
```
Here is an example of using instance subscription:
```csharp
var timeline = new GlobalTimeline();
var activity = new FixedDuration();
timeline.Subscribe(activity, a => Debug.Log($"activity start: {timeline.Offset(a)}"));
timeline.Subscribe(activity.MakeCompletionMarker(), a => Debug.Log($"activity finish: {timeline.Offset(a)}"));
timeline.Push(activity, TLTime.FromMilliseconds(2));
```
For the same output.
        
In order to determine duration at runtime, activity must implement interface [IComposedTimeable](Runtime/Contract/ITimeable.cs) with single property:
```csharp
ICompletionPromise CompletionPromise { get; }
```
[CompletionPromise](Runtime/Contract/ITimeable.cs) is an object which knows when activity finishes and may notify clients about it, passing local completion time as an argument. Here is a sample implementation of composed activity:
```csharp
public class DependentDuration : Activity<DependentDuration>, IComposedTimeable, ICompletionPromise, IDisposable
{
    public ICompletionPromise CompletionPromise => this;

    public override void Apply() {}

    public void YouMayFinish(TLTime time)
    {
        _onFinish?.Invoke(time);
    }

    #region ICompletionPromise

    private event Action<TLTime> _onFinish;
    IDisposable ICompletionPromise.Subscribe(Action<TLTime> callback)
    {
        _onFinish += callback;
        return this;
    }
    #endregion

    #region IDisposable
    public void Dispose() {}
    #endregion
}

var timeline = new GlobalTimeline();
var activity = new DependentDuration();
timeline.Subscribe<DependentDuration>(a => Debug.Log($"activity start: {timeline.Offset(a)}"));
timeline.Subscribe<Completed<DependentDuration>>(a => Debug.Log($"activity finish: {timeline.Offset(a)}"));
timeline.Push(activity, TLTime.FromMilliseconds(2));
activity.YouMayFinish(TLTime.FromMilliseconds(5));

```
Produces output:
```
activity start: 2
activity finish: 7
```
## Subsystems ##
As you may notice **Subscribe** methods accept subsystem string as optional argument. Subscriptions with non-null subsystem have following properties:
* Timeline enforces that there may be only one subscription for each activity type or instance within a subsystem
* Subscription to activity type or instance within a subsystem may be overriden in child timeline
Subsystems are useful when there are different aspects of processed model, which must be handled by distict modules - for instance Sound, Visual, Physics etc.
## Model cleanup ##
As model evolves it can accumulate pretty much activities and their subscriptions, which may negatively affect performance. There are 2 methods to deal with it:
```csharp
public void Purge(TLTime time)
```
This method eliminates all activities and their instance subscriptions before specified time. Use it when you are sure that no activities prior to that moment will be used in the future.

```csharp
public void GC()
```
This method eliminates all activities that are no longer used - i.e. there are no subscriptions to them inside and outside of Timeline. It's autmatically called after **Advance** in *Manual* operational mode.

## Time paradoxes ##
There is an init option *CheckForTimeParadoxes* of **GlobalTimeline**. Usually it's a bad practice to push activity in the past - activity "thinks" that it operates in some past moment of time while data model has already evolved further. So if this option is on, Timeline checks for such activities and throws an exception. This option is off by default due to perfomance reasons but you may turn it on from time to time to check there are no activities pushed in the past.

# Known Limitations #
* Due to its design Timeline pre-simulates a huge activities flow in a single Push call. Although Timeline internals are greatly optimized, it still may cause a significant CPU/Memory spike in case modelled process is too complex. So, probably, it's not good idea to use Timeline in environments requiring stable FPS, or use it in a background thread. You can mitigate CPU spikes though by using [PushIteration](Runtime/GlobalTimeline.cs), which spreads the work overall several steps. 
* Without **Purge** Timeline will grow in memory quickly. So it's client responsibility to periodically clean Timeline from obsolete activities.

# To be done #
* Polymorpic subscriptions. Subscription to parent class catches derived activities
* Disposable activities subscription
* Extract Timeline serialization into distinct package
* Try to utilize ECS for better storage and concurrent activities computation (not sure it's possible)
