using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using JsonReader = Newtonsoft.Json.JsonReader;
using JsonSerializationException = Newtonsoft.Json.JsonSerializationException;
using JsonWriter = Newtonsoft.Json.JsonWriter;

public class NoopJsonWriter : JsonWriter
{
    public override void Flush()
    {
    }

    public override void Close(){}

    public override void WriteStartObject(){}

    public override void WriteEndObject(){}

    public override void WriteStartArray(){}

    public override void WriteEndArray(){}

    public override void WriteStartConstructor(string name){}

    public override void WriteEndConstructor(){}

    public override void WritePropertyName(string name){}

    public override void WritePropertyName(string name, bool escape){}

    public override void WriteEnd(){}

    protected override void WriteEnd(JsonToken token){}

    protected override void WriteIndent(){}

    protected override void WriteValueDelimiter(){}

    protected override void WriteIndentSpace(){}

    public override void WriteNull(){}

    public override void WriteUndefined(){}

    public override void WriteRaw(string json){}

    public override void WriteRawValue(string json){}

    public override void WriteValue(string value){}

    public override void WriteValue(int value){}

    public override void WriteValue(uint value){}

    public override void WriteValue(long value){}

    public override void WriteValue(ulong value){}

    public override void WriteValue(float value){}

    public override void WriteValue(double value){}

    public override void WriteValue(bool value){}

    public override void WriteValue(short value){}

    public override void WriteValue(ushort value){}

    public override void WriteValue(char value){}

    public override void WriteValue(byte value){}

    public override void WriteValue(sbyte value){}

    public override void WriteValue(decimal value){}

    public override void WriteValue(DateTime value){}

    public override void WriteValue(DateTimeOffset value){}

    public override void WriteValue(Guid value){}

    public override void WriteValue(TimeSpan value){}

    public override void WriteValue(int? value){}

    public override void WriteValue(uint? value){}

    public override void WriteValue(long? value){}

    public override void WriteValue(ulong? value){}

    public override void WriteValue(float? value){}

    public override void WriteValue(double? value){}

    public override void WriteValue(bool? value){}

    public override void WriteValue(short? value){}

    public override void WriteValue(ushort? value){}

    public override void WriteValue(char? value){}

    public override void WriteValue(byte? value){}

    public override void WriteValue(sbyte? value){}

    public override void WriteValue(decimal? value){}

    public override void WriteValue(DateTime? value){}

    public override void WriteValue(DateTimeOffset? value){}

    public override void WriteValue(Guid? value){}

    public override void WriteValue(TimeSpan? value){}

    public override void WriteValue(byte[] value){}

    public override void WriteValue(Uri value){}

    public override void WriteValue(object value){}

    public override void WriteComment(string text){}

    public override void WriteWhitespace(string ws){}
}

namespace CW.Core.Timeline.Serialization
{
    internal class TimelineSerializationContext : IReferenceResolver
    {
        public Action<ITimeable> TimeablePostInitializeCallback;
        public Action<GlobalTimeline> GlobalTimelineSetCallback;

        private GlobalTimeline _globalTimeline;

        public void SetGlobal(GlobalTimeline tl)
        {
            _globalTimeline = tl;
            GlobalTimelineSetCallback?.Invoke(tl);
        }

        public GlobalTimeline GetGlobal()
        {
            return _globalTimeline;
        }

        public void PostInitialize(ITimeable timeable)
        {
            TimeablePostInitializeCallback?.Invoke(timeable);
        }

        #region IReferenceResolver

        private IReferenceResolver _defaultReferenceResolver = JsonSerializer.Create().ReferenceResolver;

        BidirectionalDictionary<string, ITimeable> _bdTimeables =
            new BidirectionalDictionary<string, ITimeable>(EqualityComparer<string>.Default,
                new IdentityEqualityComparer<ITimeable>());

        BidirectionalDictionary<string, ITimeable>
            _bdCompleteMarkers = new BidirectionalDictionary<string, ITimeable>();

        BidirectionalDictionary<string, ITimeline> _bdTimelines = new BidirectionalDictionary<string, ITimeline>();

        public object ResolveReference(object context, string reference)
        {
            if (reference == "global timeline")
            {
                return GetGlobal();
            }

            var typeAndId = reference.Split(':');
            if (typeAndId.Length == 1)
            {
                return _defaultReferenceResolver.ResolveReference(context, reference);
            }

            switch (typeAndId[0])
            {
                case "activity":
                    _bdTimeables.TryGetByFirst(reference, out var activity);
                    return activity;
                case "complete_marker":
                    _bdCompleteMarkers.TryGetByFirst(reference, out var completion);
                    return completion;
                case "timeline":
                    _bdTimelines.TryGetByFirst(reference, out var timeline);
                    return timeline;
            }

            return null;
        }

        public string GetReference(object context, object value)
        {
            string id;
            switch (value)
            {
                case Completed<ITimeable> completed:
                    if (_bdCompleteMarkers.TryGetBySecond(completed, out id))
                        return id;
                    id = $"complete_marker:{completed.Activity.Id}";
                    _bdCompleteMarkers.Set(id, completed);
                    return id;
                case ITimeable timeable:
                    if (_bdTimeables.TryGetBySecond(timeable, out id))
                        return id;
                    id = $"activity:{timeable.Id}";
                    _bdTimeables.Set(id, timeable);
                    return id;
                case LocalTimeline localTimeline:
                    if (_bdTimelines.TryGetBySecond(localTimeline, out id))
                        return id;
                    id = $"timeline:{localTimeline.TimeableId}";
                    _bdTimelines.Set(id, localTimeline);
                    return id;
                case GlobalTimeline globalTimeline:
                    _globalTimeline = globalTimeline;
                    return $"global timeline";
                default:
                    return _defaultReferenceResolver.GetReference(context, value);
            }
        }

        public bool IsReferenced(object context, object value)
        {
            switch (value)
            {
                case Completed<ITimeable> completed:
                    return _bdCompleteMarkers.TryGetBySecond(completed, out var _);
                case ITimeable timeable:
                    return _bdTimeables.TryGetBySecond(timeable, out var _);
                case LocalTimeline localTimeline:
                    return _bdTimelines.TryGetBySecond(localTimeline, out var _);
                case GlobalTimeline globalTimeline:
                    return _globalTimeline != null;
                default:
                    return _defaultReferenceResolver.IsReferenced(context, value);
            }
        }


        public void AddReference(object context, string reference, object value)
        {
            if (reference == "global timeline")
            {
                _globalTimeline = value as GlobalTimeline;
                return;
            }

            var typeAndId = reference.Split(':');
            if (typeAndId.Length == 1)
            {
                _defaultReferenceResolver.AddReference(context, reference, value);
            }
            else
            {
                switch (typeAndId[0])
                {
                    case "complete_marker":
                        _bdCompleteMarkers.Set(reference, value as ITimeable);
                        break;
                    case "activity":
                        _bdTimeables.Set(reference, value as ITimeable);
                        break;
                    case "timeline":
                        _bdTimelines.Set(reference, value as ITimeline);
                        break;
                    default:
                        throw new TimelineException(
                            $"Not supported reference type during deserialization: {reference}");
                }
            }
        }

        #endregion

        private class BidirectionalDictionary<TFirst, TSecond>
            where TFirst : class
            where TSecond : class
        {
            private readonly IDictionary<TFirst, TSecond> _firstToSecond;
            private readonly IDictionary<TSecond, TFirst> _secondToFirst;
            private readonly string _duplicateFirstErrorMessage;
            private readonly string _duplicateSecondErrorMessage;

            public BidirectionalDictionary()
                : this(EqualityComparer<TFirst>.Default, EqualityComparer<TSecond>.Default)
            {
            }

            public BidirectionalDictionary(IEqualityComparer<TFirst> firstEqualityComparer,
                IEqualityComparer<TSecond> secondEqualityComparer)
                : this(
                    firstEqualityComparer,
                    secondEqualityComparer,
                    "Duplicate item already exists for '{0}'.",
                    "Duplicate item already exists for '{0}'.")
            {
            }

            public BidirectionalDictionary(IEqualityComparer<TFirst> firstEqualityComparer,
                IEqualityComparer<TSecond> secondEqualityComparer,
                string duplicateFirstErrorMessage, string duplicateSecondErrorMessage)
            {
                _firstToSecond = new Dictionary<TFirst, TSecond>(firstEqualityComparer);
                _secondToFirst = new Dictionary<TSecond, TFirst>(secondEqualityComparer);
                _duplicateFirstErrorMessage = duplicateFirstErrorMessage;
                _duplicateSecondErrorMessage = duplicateSecondErrorMessage;
            }

            public void Set(TFirst first, TSecond second)
            {
                if (_firstToSecond.TryGetValue(first, out TSecond existingSecond))
                {
                    if (!existingSecond.Equals(second))
                    {
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, _duplicateFirstErrorMessage, first));
                    }
                }

                if (_secondToFirst.TryGetValue(second, out TFirst existingFirst))
                {
                    if (!existingFirst.Equals(first))
                    {
                        throw new ArgumentException(
                            string.Format(CultureInfo.InvariantCulture, _duplicateSecondErrorMessage, second));
                    }
                }

                _firstToSecond.Add(first, second);
                _secondToFirst.Add(second, first);
            }

            public bool TryGetByFirst(TFirst first, out TSecond second)
            {
                return _firstToSecond.TryGetValue(first, out second);
            }

            public bool TryGetBySecond(TSecond second, out TFirst first)
            {
                return _secondToFirst.TryGetValue(second, out first);
            }
        }
    }

    sealed class IdentityEqualityComparer<T> : IEqualityComparer<T>
        where T : class
    {
        public int GetHashCode(T value)
        {
            return RuntimeHelpers.GetHashCode(value);
        }

        public bool Equals(T left, T right)
        {
            return ReferenceEquals(left, right); // Reference identity comparison
        }
    }

    internal class LocalTimelineConverter : JsonConverter<LocalTimeline>
    {
        public override void WriteJson(JsonWriter writer, LocalTimeline value, JsonSerializer serializer)
        {
            var json = new JObject(
                new JProperty("id", value.TimeableId),
                new JProperty("parent",
                    value.parent is LocalTimeline localTimeline
                        ? $"timeline:{localTimeline.TimeableId}"
                        : "global timeline"),
                new JProperty("offset", JToken.FromObject(value.offset, serializer))
            );

            json.WriteTo(writer);
        }

        public override LocalTimeline ReadJson(JsonReader reader, Type objectType, LocalTimeline existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            return ReadToken(JObject.ReadFrom(reader), hasExistingValue, serializer);
        }

        public static LocalTimeline ReadToken(JToken json, bool hasExistingValue, JsonSerializer serializer)
        {
            var id = json["id"].ToObject<long>(serializer);
            var parentId = json["parent"].ToObject<string>(serializer);
            var offset = json["offset"].ToObject<TlTime>(serializer);

            if (hasExistingValue)
            {
                throw new JsonSerializationException($"Can't be: Timeline {id} already exists");
            }

            var context = (TimelineSerializationContext) serializer.Context.Context;

            var parentTimeline = (ITimelineInternal) context.ResolveReference(serializer, parentId);
            var timeline = new LocalTimeline(parentTimeline, offset, id);

            return timeline;
        }
    }

    // нужен потому что таймлайн в активностях как ITimeline и мы не сериализуем тип - у десериализатора не будет шанса сопоставить json конкретному типу
    internal class ITimelineConverter : JsonConverter<ITimeline>
    {
        private static readonly LocalTimelineConverter LocalTimelineConverter = new LocalTimelineConverter();

        public override void WriteJson(JsonWriter writer, ITimeline value, JsonSerializer serializer)
        {
            if (value is LocalTimeline localTimeline)
            {
                var resolver = serializer.ReferenceResolver;
                if (resolver.IsReferenced(serializer, value))
                {
                    var reference = resolver.GetReference(serializer, value);
                    writer.WriteValue(reference);
                }
                else
                {
                    resolver.GetReference(serializer, value);
                    LocalTimelineConverter.WriteJson(writer, localTimeline, serializer);
                }
            }
            else
            {
                throw new JsonSerializationException(
                    $"unexpected timeable: {value}. Global timeline should be resolved by TimelineSerializationContext (as IReferenceResolver)");
            }
        }

        public override ITimeline ReadJson(JsonReader reader, Type objectType, ITimeline existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var resolver = serializer.ReferenceResolver;

            var token = JToken.ReadFrom(reader);
            if (token.Type == JTokenType.String)
            {
                var reference = token.ToObject<string>();
                var value = (ITimeline) resolver.ResolveReference(serializer, reference);
                return value;
            }
            else if (token is JObject jobj && jobj.TryGetValue("$ref", out var reference))
            {
                return (ITimeline) resolver.ResolveReference(serializer, reference.ToObject<string>());
            }
            else
            {
                var value = LocalTimelineConverter.ReadToken(token, hasExistingValue, serializer);
                resolver.GetReference(serializer, value);
                return value;
            }
        }
    }

    // internal class ITimelineRefConverter : JsonConverter<ITimeline>
    // {
    //     public override void WriteJson(JsonWriter writer, ITimeline value, JsonSerializer serializer)
    //     {
    //         writer.WriteValue(value is LocalTimeline localTimeline ? localTimeline.TimeableId : 0);
    //     }
    //
    //     public override ITimeline ReadJson(JsonReader reader, Type objectType, ITimeline existingValue, bool hasExistingValue,
    //         JsonSerializer serializer)
    //     {
    //         var ctx = (TimelineSerializationContext)serializer.Context.Context;
    //         var id = (long)reader.Value;
    //         if (id == 0)
    //             return ctx.GetGlobal();
    //         else
    //             return ctx.EnsureLocalTimeline(id);
    //     }
    // }
    //
    // internal class ITimeableRefConverter : JsonConverter<ITimeable>
    // {
    //     public override void WriteJson(JsonWriter writer, ITimeable value, JsonSerializer serializer)
    //     {
    //         writer.WriteValue(value.Id);
    //     }
    //
    //     public override ITimeable ReadJson(JsonReader reader, Type objectType, ITimeable existingValue, bool hasExistingValue,
    //         JsonSerializer serializer)
    //     {
    //         var ctx = (TimelineSerializationContext)serializer.Context.Context;
    //         var id = (long)reader.Value;
    //         return ctx.GetTimeable(id);
    //     }
    // }

    internal class ActionConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType)
        {
            var canConvert = objectType.IsGenericType
                             && objectType.GetGenericTypeDefinition() == typeof(Action<>)
                             && typeof(ITimeable).IsAssignableFrom(objectType.GetGenericArguments()[0]);
            return canConvert;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            Delegate d = (Delegate) value;

            if (IsActionLambda(d))
            {
                throw new JsonException(
                    $"Activity subscription must be static or instance method of another activity {d.Method.DeclaringType.Name}.{d.Method.Name}");
            }

            var caller = d.Method.IsStatic ? d.Method.DeclaringType : ((Delegate) value).Target.GetType();
            var arg = d.Method.GetParameters()[0].ParameterType;

            var callerIsTimeable = typeof(ITimeable).IsAssignableFrom(caller);
            var callerIsTimeline = typeof(Timeline).IsAssignableFrom(caller);
            var argIsTimeable = typeof(ITimeable).IsAssignableFrom(arg);
            if (!(callerIsTimeable || callerIsTimeline)
                || !argIsTimeable)
            {
                throw new JsonException($"Caller {caller} and arg {arg} must be ITimeable or LocalTimeline");
            }

            var json = new JObject(
                new JProperty("callerIsTimeline", callerIsTimeline)
            );

            if (callerIsTimeline)
            {
                var timeline = d.Target as Timeline;
                var jObj = JObject.FromObject(timeline, serializer);
                json.Add("instance", jObj);
            }
            else
            {
                serializer.SerializationBinder.BindToName(caller, out var assemblyName, out var typeName);
                json.Add("callerType", JValue.FromObject(typeName));
                var isStatic = d.Method.IsStatic;
                json.Add("isStatic", isStatic);
                if (!isStatic)
                {
                    var timeable = d.Target as ITimeable;
                    var jObj = JObject.FromObject(timeable, serializer);
                    json.Add("instance", jObj);
                }
            }

            serializer.SerializationBinder.BindToName(arg, out var assemblyName2, out var typeName2);
            json.Add("argType", JValue.FromObject(typeName2));
            json.Add("method", d.Method.Name);

            json.WriteTo(writer);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            var json = JObject.ReadFrom(reader);
            var callerIsTimeline = json["callerIsTimeline"].ToObject<bool>(serializer);
            var methodName = json["method"].ToObject<string>(serializer);
            var arg = serializer.SerializationBinder.BindToType(null, json["argType"].ToObject<string>());

            if (callerIsTimeline)
            {
                var instance = json["instance"].ToObject<Timeline>(serializer);
                var methodInfo = typeof(Timeline).GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
                var actionType = typeof(Action<>).MakeGenericType(arg);
                var action = Delegate.CreateDelegate(actionType, instance, methodInfo);
                return action;
            }
            else
            {
                var caller = serializer.SerializationBinder.BindToType(null, json["callerType"].ToObject<string>());
                var isStatic = json["isStatic"].ToObject<bool>(serializer);
                if (isStatic)
                {
                    var methodInfo = caller.GetMethod(methodName, new[] {arg});
                    var actionType = typeof(Action<>).MakeGenericType(arg);
                    var action = Delegate.CreateDelegate(actionType, methodInfo);
                    return action;
                }
                else
                {
                    var instance = json["instance"].ToObject<ITimeable>(serializer);

                    var methodInfo = caller.GetMethod(
                        methodName,
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                        null,
                        new[] {arg},
                        null);
                    var actionType = typeof(Action<>).MakeGenericType(arg);
                    var action = Delegate.CreateDelegate(actionType, instance, methodInfo);
                    return action;
                }
            }
        }

        bool IsActionLambda(Delegate d)
        {
            var declaringType = d.Method.DeclaringType;
            return declaringType.IsNestedPrivate && declaringType.IsSealed && !declaringType.IsVisible;
        }
    }


    internal class PushInfoConverter : JsonConverter<PushInfo>
    {
        public override void WriteJson(JsonWriter writer, PushInfo value, JsonSerializer serializer)
        {
            var timelinesTravelled = value.timelinesTravelled
                .OrderBy(pair => pair.Value)
                .Select(pair => JObject.FromObject(pair.Key, serializer));

            var json = new JObject(
                new JProperty("timeable", JObject.FromObject(value.timeable, serializer)),
                new JProperty("timelinesTravelled", JArray.FromObject(timelinesTravelled, serializer))
            );

            json.WriteTo(writer);
        }

        public override PushInfo ReadJson(JsonReader reader, Type objectType, PushInfo existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var ctx = (TimelineSerializationContext) serializer.Context.Context;

            var json = JObject.ReadFrom(reader);

            var timeable = json["timeable"].ToObject<ITimeable>(serializer);

            var pushInfo = new PushInfo(timeable, ctx.GetGlobal().APushInfoPoolingContext);

            int i = 0;
            foreach (JObject jTimeline in (json["timelinesTravelled"] as JArray))
            {
                var timeline = jTimeline.ToObject<ITimeline>(serializer);
                pushInfo.timelinesTravelled[timeline] = i++;
            }

            return pushInfo;
        }
    }

    public class TLTimeConverter : JsonConverter<TlTime>
    {
        public override void WriteJson(JsonWriter writer, TlTime value, JsonSerializer serializer)
        {
            writer.WriteValue(value.ToMilliseconds);
        }

        public override TlTime ReadJson(JsonReader reader, Type objectType, TlTime existingValue, bool hasExistingValue,
            JsonSerializer serializer)
        {
            var longValue = JToken.ReadFrom(reader).ToObject<long>();
            return TlTime.FromMilliseconds(longValue);
        }
    }

    internal class GlobalTimelineConverter : JsonConverter<GlobalTimeline>
    {
        public override void WriteJson(JsonWriter writer, GlobalTimeline value, JsonSerializer serializer)
        {
            var ctx = (TimelineSerializationContext) serializer.Context.Context;
            ctx.SetGlobal(value);

            writer.WriteStartObject();

            writer.WritePropertyName("isManual");
            writer.WriteValue(value.IsAdvanceable);

            writer.WritePropertyName("lastID");
            writer.WriteValue(value._lastID);

            writer.WritePropertyName("time");
            JToken.FromObject(value._time, serializer).WriteTo(writer);

            {
                var activities = value._timeline.Select(timed => timed.value).ToList();
                AddNotExistingCompletedFromInstanceHandlers(value, activities);
                var jActivities = JArray.FromObject(activities, serializer);
                writer.WritePropertyName("activities");
                jActivities.WriteTo(writer);
            }

            {
                var timeline = JArray.FromObject(value._timeline, serializer);
                writer.WritePropertyName("timeline");
                timeline.WriteTo(writer);
            }

            {
                writer.WritePropertyName("pushInfos");
                writer.WriteStartArray();
                var pushinfos = value._pushInfos.OrderBy(pair => pair.Key.Id);
                foreach (var pair in pushinfos)
                {
                    var jObj = new JObject(
                        new JProperty("timeable", JObject.FromObject(pair.Key, serializer)),
                        new JProperty("pushInfo", JObject.FromObject(pair.Value, serializer))
                    );
                    jObj.WriteTo(writer, serializer.Converters.ToArray());
                }

                writer.WriteEndArray();
            }

            {
                writer.WritePropertyName("typeHandlers");
                writer.WriteStartArray();
                var typeHanlders = value._typeHandlers.OrderBy(pair => pair.Key.Name);
                foreach (var pair in typeHanlders)
                {
                    var serializableSubs = pair.Value.Where(value.IsSerializable).ToList();
                    if (!serializableSubs.Any())
                        continue;

                    serializer.SerializationBinder.BindToName(pair.Key, out var _, out var typeStr);
                    var jType = JToken.FromObject(typeStr);
                    var jSubs = JArray.FromObject(serializableSubs, serializer);
                    var jPair = new JObject(
                        new JProperty("type", jType),
                        new JProperty("subscriptions", jSubs)
                    );
                    jPair.WriteTo(writer);
                }

                writer.WriteEndArray();
            }

            {
                writer.WritePropertyName("instanceHandlers");
                writer.WriteStartArray();
                var instanceHanlders = value._instanceHandlers.OrderBy(pair => pair.Key.Id);
                foreach (var pair in instanceHanlders)
                {
                    var serializableSubs = pair.Value.Where(value.IsSerializable).ToList();
                    if (!serializableSubs.Any())
                        continue;

                    var jPair = new JObject(
                        new JProperty("timeable", JObject.FromObject(pair.Key, serializer)),
                        new JProperty("subscriptions", JArray.FromObject(serializableSubs, serializer))
                    );
                    jPair.WriteTo(writer);
                }

                writer.WriteEndArray();
            }

            {
                writer.WritePropertyName("subscriptionOrder");
                writer.WriteStartArray();
                var filteredSusbSubscriptionOrder = value.SerializableSubscriptionOrder();
                foreach (var pair in filteredSusbSubscriptionOrder)
                {
                    JObject.FromObject(pair.Key, serializer).WriteTo(writer);
                }

                writer.WriteEndArray();
            }

            writer.WriteEndObject();
        }

        // для нормальной сериализации все активности таймлайна должны быть сериализованы в первую очередь  
        // казалось бы можно использовать коллекцию GlobalTimelne.timeline - там все активности в отсортированном порядке
        // однако можно подписаться на несуществующую активность CompletionMarker() - тогда маркер будет в подписках, но пока activity не сработала, его не будет в GlobalTimelne.timeline
        // поэтому активности GlobalTimelne.timeline нужно объединить со всеми Completed активностями из instanceHandlers
        private void AddNotExistingCompletedFromInstanceHandlers(GlobalTimeline value, List<ITimeable> activities)
        {
            var comparer = new IdentityEqualityComparer<ITimeable>();
            var timelineActivities = new HashSet<ITimeable>(activities, comparer);
            var completionMarkers =
                new HashSet<ITimeable>(value._instanceHandlers.Keys.Where(timeable => timeable is Completed<ITimeable>),
                    comparer);
            var activitiesToAdd = completionMarkers.Except(timelineActivities, comparer).ToArray();
            activities.AddRange(activitiesToAdd);
        }

        public override GlobalTimeline ReadJson(JsonReader reader, Type objectType, GlobalTimeline existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var ctx = (TimelineSerializationContext) serializer.Context.Context;
            var json = JObject.ReadFrom(reader);

            var timeline = new GlobalTimeline();
            ctx.SetGlobal(timeline);

            var options = new GlobalTimeline.Options
            {
                AdvanceType = json["isManual"].ToObject<bool>() ? TimelineAdvanceType.Manual : TimelineAdvanceType.Auto
            };
            timeline._options = options;
            timeline._lastID = json["lastID"].ToObject<long>();
            timeline._time = json["time"].ToObject<TlTime>(serializer);

            var activities = json["activities"].ToObject<List<ITimeable>>(serializer);
            foreach (var activity in activities)
            {
                ctx.PostInitialize(activity);
            }

            var timelineInternal = json["timeline"].ToObject<List<TimedTimeable>>(serializer);
            timeline._timeline = timelineInternal;

            var jPushInfos = (JArray) json["pushInfos"];
            var pushInfos = new Dictionary<ITimeable, PushInfo>();
            foreach (JObject jObj in jPushInfos)
            {
                var timeable = jObj["timeable"].ToObject<ITimeable>(serializer);
                var pushInfo = jObj["pushInfo"].ToObject<PushInfo>(serializer);
                pushInfos[timeable] = pushInfo;
            }

            timeline._pushInfos = pushInfos;

            var jTypeHandlers = (JArray) json["typeHandlers"];
            var typeHandlers = new Dictionary<Type, List<Subscription>>();
            foreach (JObject jPair in jTypeHandlers)
            {
                var type = serializer.SerializationBinder.BindToType(null, jPair["type"].ToObject<string>(serializer));
                ;
                var handlers = jPair["subscriptions"].ToObject<List<Subscription>>(serializer);
                typeHandlers[type] = handlers;
            }

            timeline._typeHandlers = typeHandlers;

            var jInstanceHandlers = (JArray) json["instanceHandlers"];
            var instanceHandlers = new Dictionary<ITimeable, List<Subscription>>();
            foreach (JObject jPair in jInstanceHandlers)
            {
                var timeable = jPair["timeable"].ToObject<ITimeable>(serializer);
                var instance = jPair["subscriptions"].ToObject<List<Subscription>>(serializer);
                instanceHandlers[timeable] = instance;
            }

            timeline._instanceHandlers = instanceHandlers;

            var jSubscriptionOrder = (JArray) json["subscriptionOrder"];
            var subscriptionOrder = new Dictionary<Subscription, int>();
            int i = 0;
            foreach (JObject jSubscription in jSubscriptionOrder)
            {
                var subscription = jSubscription.ToObject<Subscription>(serializer);
                subscriptionOrder[subscription] = i++;
            }

            timeline._subscriptionOrder = subscriptionOrder;

            timeline.Reconstruct();

            return timeline;
        }
    }
}