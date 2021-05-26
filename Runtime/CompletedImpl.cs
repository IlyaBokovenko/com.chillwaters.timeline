using System;
using CW.Core.Hash;
using Newtonsoft.Json;

namespace CW.Core.Timeline
{
    [JsonObject(MemberSerialization.OptIn)]
    public class CompletedImpl<T> : Completed<T>, IContentHash
        where T : Activity
    {
        [JsonProperty("activity")]
        public T Activity { get; }
        public Type InterfaceType => typeof(Completed<T>);

        public void Apply() { }
        
        public CompletedImpl(T activity)
        {
            Activity = activity;
        }

        [JsonConstructor]
        internal CompletedImpl(long id, T activity)
        {
            SetId(id);
            Activity = activity;
        }

        public Completed<ITimeable> MakeCompletionMarker()
        {
            throw new TimelineException("Completion marker should never be asked for completion marker");
        }

        [JsonProperty("id")]
        public long Id { get; private set; }
        public void SetId(long id)
        {
            Id = id;
        }

        public void SetLocalTimeline(ITimeline timeline)
        {
            throw new System.NotImplementedException();
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            return obj is Completed<T> completed && completed.Activity.Equals(Activity);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = Activity.GetHashCode();
                return hashCode * 397;
            }
        }

        public static bool operator ==(CompletedImpl<T> obj1, CompletedImpl<T> obj2)
        {
            if (ReferenceEquals(obj1, obj2))
            {
                return true;
            }

            if (ReferenceEquals(obj1, null))
            {
                return false;
            }
            if (ReferenceEquals(obj2, null))
            {
                return false;
            }

            return obj1.Activity == obj2.Activity;
        }

        // this is second one '!='
        public static bool operator !=(CompletedImpl<T> obj1, CompletedImpl<T> obj2)
        {
            return !(obj1 == obj2);
        }

        public override string ToString()
        {
            return $"[{GetHashCode().ToString().PadRight(12)}] Completed {Activity}";
        }

#region IContentHash
        public void WriteContentHash(ITracingHashWriter writer)
        {
           writer.Trace("id").Write(Activity.Id); 
        }
#endregion
        
    }

    public static class ActivityExtensions
    {
        public static Completed<T> CompleteMarker<T>(this T activity) where T : Activity
        {
            return new CompletedImpl<T>(activity);
        }
    }
}