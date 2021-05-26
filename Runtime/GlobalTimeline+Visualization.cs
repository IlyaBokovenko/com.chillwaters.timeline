using System;
using System.Collections.Generic;
using CW.Core.Timeline.Serialization;
using Newtonsoft.Json;

namespace CW.Core.Timeline
{
    public static class GlobalTimelineVisualizationHelper
    {
        public static GlobalTimelineVisualizationData BuildVisualization(this GlobalTimeline globalTimeline)
        {
            var root = new ActivityVisualizationData(0, "root");
            var activity2view = new Dictionary<ITimeable, ActivityVisualizationData>();
            var timeline2view = new Dictionary<ITimeline, ActivityVisualizationData>();
            var activity2parent = new Dictionary<ITimeable, ActivityVisualizationData>();

            foreach (var timed in globalTimeline._timeline)
            {
                var offset = timed.offset;

                ActivityVisualizationData view = null;
                if (timed.value is Completed<Activity> completed)
                {
                    var activity = completed.Activity;
                    view = EnsureViewCreated(activity);
                    view.SetEnd(offset);
                    view.SetStartRaw(activity.Timeline.Offset());
                    EnsureAdopted(activity, view);
                }
                else
                {
                    var activity = timed.value as Activity;
                    view = EnsureViewCreated(activity);
                    view.SetStart(offset);
                    EnsureAdopted(activity, view);
                }
            }
            
            return new GlobalTimelineVisualizationData(root);

            void EnsureAdopted(Activity activity, ActivityVisualizationData view)
            {
                if (activity2parent.ContainsKey(activity))
                {
                    return;
                }

                ActivityVisualizationData parent;
                    
                if ((activity.Timeline as LocalTimeline).parent is LocalTimeline parentTimeline)
                {
                    if (!timeline2view.ContainsKey(parentTimeline))
                    {
                        Activity parentActivity = (Activity)parentTimeline.DbgGetParentTimeable();
                        var parentView = EnsureViewCreated(parentActivity, true);
                        parentView.SetStartRaw(parentTimeline.Offset());
                        EnsureAdopted(parentActivity, parentView);
                    }

                    parent = timeline2view[parentTimeline];
                }
                else
                {
                    parent = root;
                }
                    
                parent.AddChild(view);
                activity2parent[activity] = parent;
            }

            ActivityVisualizationData EnsureViewCreated(Activity activity, bool phantom = false)
            {
                ActivityVisualizationData view;
                if (!activity2view.TryGetValue(activity, out view))
                {
                    view = new ActivityVisualizationData((int)activity.Id, activity.ToString());
                    view.IsPhantom = phantom;
                    activity2view[activity] = view;
                    timeline2view[activity.Timeline] = view;
                }

                return view;
            }
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class GlobalTimelineVisualizationData
    {
        [JsonProperty]
        public readonly ActivityVisualizationData Root;
        public TLTime MinTime { get; private set; }
        public TLTime MaxTime { get; private set; }
        public int Depth { get; private set; }

        public GlobalTimelineVisualizationData()
        {
        }

        [JsonConstructor]
        public GlobalTimelineVisualizationData(ActivityVisualizationData root)
        {
            Root = root;
            CalcMinMaxTime();
            CalcDepth();
            CalcParentOffsets();
        }

        private void CalcParentOffsets()
        {
            Root.RecursiveDo(parent =>
            {
                if (parent.HasBeginning && parent.HasChildren)
                {
                    foreach (var child in parent.Children)
                    {
                        if (child.HasBeginning)
                        {
                            child.SetRelativeOffset(child.Start - parent.Start);
                        }
                    }
                }
            });
        }

        private void CalcDepth()
        {
            IterateNode(Root, 0);
            
            void IterateNode(ActivityVisualizationData node, int depth)
            {
                if (Depth < depth)
                {
                    Depth = depth;
                }

                if (node.Children != null)
                {
                    foreach (var child in node.Children)
                    {
                        IterateNode(child, depth + 1);
                    }
                }
            }
        }

        private void CalcMinMaxTime()
        {
            TLTime maxTime = TLTime.Zero;
            TLTime minTime = TLTime.FromMilliseconds(long.MaxValue);
            Root.RecursiveDo(v =>
            {
                if (v.IsRoot) 
                    return;
                
                if (v.HasBeginning)
                {
                    if(v.Start < minTime)
                        minTime = v.Start;
                    
                    if (v.Start > maxTime)
                        maxTime = v.Start;
                }

                if (v.IsComplete)
                {
                    if(v.End > maxTime)
                        maxTime = v.End;

                    if (v.End < minTime)
                        minTime = v.End;
                }
            });

            MaxTime = maxTime;
            MinTime = minTime;
        }

        public string Serialize()
        {
            return JsonConvert.SerializeObject(this, new TLTimeConverter());
        }
    }

    [JsonObject(MemberSerialization.OptIn)]
    public class ActivityVisualizationData
    {
        [JsonProperty]
        public readonly int Id;

        public string Description => $"{((RelativeOffset.HasValue ? RelativeOffset.Value : Start).ToString().PadRight(10))} {_description}";

        [JsonProperty("Description")]
        private readonly string _description; 
        
        [JsonProperty]
        public TLTime Start { get; private set; }
        [JsonProperty]
        public TLTime End { get; private set; }
        [JsonProperty]
        public bool IsComplete { get; private set; }
        [JsonProperty]
        public bool HasBeginning { get; private set; }
        
        [JsonProperty]
        public bool IsPhantom;

        [JsonProperty]
        public List<ActivityVisualizationData> Children;

        [JsonProperty]
        public int Depth;

        public bool HasChildren => Children != null;
        public bool IsRoot => Id == 0;

        public TLTime? RelativeOffset { get; private set; }

        public ActivityVisualizationData()
        {
        }

        [JsonConstructor]
        public ActivityVisualizationData(int id, string description)
        {
            Id = id;
            _description = description;
        }

        public void SetStart(TLTime time)
        {
            Start = time;
            HasBeginning = true;
        }
        
        public void SetStartRaw(TLTime time)
        {
            Start = time;
        }

        public void SetEnd(TLTime time)
        {
            End = time;
            IsComplete = true;
        }

        public void SetRelativeOffset(TLTime parentOffset)
        {
            RelativeOffset = parentOffset;
        }

        public void AddChild(ActivityVisualizationData child)
        {
            if (Children == null)
            {
                Children = new List<ActivityVisualizationData>();
            }

            Children.Add(child);
        }

        public void RecursiveDo(Action<ActivityVisualizationData> action)
        {
            action(this);
            if (HasChildren)
            {
                foreach (var child in Children)
                {
                    child.RecursiveDo(action);
                }
            }
        }
    }
}