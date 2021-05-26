using Newtonsoft.Json;

namespace CW.Core.Timeline
{
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class Activity : ITimeable
    {
        [JsonProperty("id")]
        public long Id { get; private set; }
        [JsonProperty("timeline")]
        public ITimeline Timeline { get; private set; }
        
        public abstract void Apply();

        public override string ToString()
        {
            return $"[{Id.ToString().PadRight(12)}] {GetType().Name}";
        }

        public void SetId(long id)
        {
            Id = id;
        }

        protected virtual void SetLocalTimeline(ITimeline timeline)
        {
            Timeline = timeline;
        }

        void ITimeable.SetLocalTimeline(ITimeline timeline)
        {
            SetLocalTimeline(timeline);
        }

        public  abstract Completed<ITimeable> MakeCompletionMarker();
    }

    public abstract class Activity<T> : Activity
        where T : Activity
    {
        public override Completed<ITimeable> MakeCompletionMarker()
        {
            return new CompletedImpl<T>(this as T);
        }
    }
}