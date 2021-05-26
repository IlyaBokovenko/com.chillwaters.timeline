using System;
using CW.Core.Hash;
using Newtonsoft.Json;

namespace CW.Core.Timeline
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Subscription : IContentHash
    {
        [JsonProperty("sourceTimeline")]
        internal ITimeline _sourceTimeline;
        public ITimeline SourceTimeline => _sourceTimeline;

        public abstract Type Type { get; }
        public abstract ITimeable Timeable { get; }
        public abstract Delegate Action { get; }
        public bool IsInstance => Timeable != null;

        [JsonProperty]
        public string subsystem;

        public abstract void Call(ITimeable timeable);
        public abstract bool IsMy<T>(Action<T> action) where T : ITimeable;

        [JsonConstructor]
        protected Subscription(ITimeline sourceTimeline)
        {
            _sourceTimeline = sourceTimeline;
        }

#region IContentHash
        public virtual void WriteContentHash(ITracingHashWriter writer)
        {
            if (!(SourceTimeline is IGlobalTimeline))
            {
                writer.Trace("timeline");
                var contentHasher = SourceTimeline as IContentHash;
                using (writer.IndentationBlock())
                {
                    contentHasher.WriteContentHash(writer);
                }
            }

            if (IsInstance)
            {
                if (!(Timeable is IContentHash hasher))
                {
                    throw new TimelineException($"{Timeable} must support IContentHash");
                }

                writer.Trace("activity");
                using (writer.IndentationBlock())
                {
                    hasher.WriteContentHash(writer);    
                }
            }
            else
            {
                writer.Trace("type").Write(Type.Name);
            }

            if (subsystem != null)
            {
                writer.Trace("subsystem").Write(subsystem);
            }
        }

#endregion
    }

    [JsonObject(MemberSerialization.OptIn, IsReference = true)]
    public class Subscription<T> : Subscription where T : ITimeable
    {
        [JsonProperty]
        private T timeable;

        //public bool IsInstance => timeable != null;
        public override Type Type => typeof(T);
        public override ITimeable Timeable => timeable;
        public override Delegate Action => action;

        [JsonProperty]
        public readonly Action<T> action;

        [JsonConstructor]
        private Subscription(ITimeline sourceTimeline, Action<T> action) : base(sourceTimeline)
        {
            this.action = action;
        }

        public static Subscription ToType(ITimeline timeline, Action<T> action)
        {
            var subs = new Subscription<T>(timeline, action);
            return subs;
        }

        public static Subscription ToInstance(ITimeline timeline, Action<T> action, T timeable)
        {
            if (timeable == null)
            {
                throw new ArgumentException("timeable should not be null in instance subscription");
            }

            var subs = new Subscription<T>(timeline, action) { timeable = timeable };
            return subs;
        }

        public override bool IsMy<T1>(Action<T1> action)
        {
            return typeof(T1) == typeof(T) &&
                   (Delegate)action == (Delegate)this.action;
        }

        public override void Call(ITimeable timeable)
        {
            if (this.timeable != null)
            {
                if (!this.timeable.Equals(timeable))
                {
                    throw new ArgumentException("subscription handler called with timeable other than it's subscribed on");
                }
            }
            else if (!(timeable is T))
            {
                throw new ArgumentException("subscription handler called with timeable type other than it's subscribed on");
            }

            // в случае подписки на instance completion
            // timeable у нас Completed<SomeActivitySubclass> а хендлер ожидает Completed<Activity>
            // все сработает благодаря контрвариативности Completed
            // см. также залипуху в GlobalTimeline.Publish
            if (timeable is Completed<ITimeable> completionMarker)
            {
                action((T)completionMarker);
                return;
            }

            action((T)timeable);
        }

        public override void WriteContentHash(ITracingHashWriter writer)
        {
            base.WriteContentHash(writer);
            
            if (IsActionLambda())
            {
                throw new TimelineException($"can't calculate content hash of lambda {action}");
            }

            writer.Trace("method").Write(action.Method.Name);
            if (action.Target != null)
            {
                var traceLine = writer.Trace("target");
                switch (action.Target)
                { 
                    case GlobalTimeline globalTimeline:
                        traceLine.Write(0);
                        break;
                    case LocalTimeline localTimeline:
                        traceLine.Write(localTimeline.TimeableId);
                        break;
                    case ITimeable timeable:
                        traceLine.Write(timeable.Id);
                        break;
                    default:
                        throw new TimelineException("Action target must be timeline or activity in order to calculate content hash");
                }
            }

            bool IsActionLambda()
            {
                var declaringType = action.Method.DeclaringType;
                return declaringType.IsNestedPrivate && declaringType.IsSealed && !declaringType.IsVisible;
            }
        }

        public override string ToString()
        {
            return $"{(IsInstance ? $"To Instance: {timeable}" : $"To Type: {Type}")}, Target: {action.Target}:{action.Method.Name}";
        }
    }
}