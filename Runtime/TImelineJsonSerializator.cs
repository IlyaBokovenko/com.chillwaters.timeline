using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using CW.Core.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CW.Core.Timeline.Serialization
{

    public class TimelineJsonSerializator : ISerializator
    {
        private bool _isPretty;

        public Action<ITimeable> TimeablePostInitializeCallback { get; set; }
        public Action<GlobalTimeline> GlobalTimelineSetCallback { get; set; }

        TimelineJsonSerializator(bool pretty)
        {
            _isPretty = pretty;
        }

        public string Serialize(object obj) 
        {
            string json = JsonConvert.SerializeObject(obj, _isPretty ? Formatting.Indented : Formatting.None, CreateSettingsInternal());
            return json;
        }

        public T Deserialize<T>(string serialize) where T : class
        {
            return JsonConvert.DeserializeObject<T>(serialize, CreateSettingsInternal()); 
        }

        public object Deserialize(string data, Type type)
        {
            return JsonConvert.DeserializeObject(data, type, CreateSettingsInternal());
        }

        public object DeserializeToSimpleCollections(string data)
        {
            var settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                Converters = new JsonConverter[]{new SimpleCollectionsConverter()}
            };

            var deserialized = JsonConvert.DeserializeObject<Dictionary<string, object>>(data, settings);
            return deserialized;
        }

        public static TimelineJsonSerializator CreatePretty()
        {
            return new TimelineJsonSerializator(true);
        }

        public static TimelineJsonSerializator Create()
        {
            return new TimelineJsonSerializator(false);
        }

        public static JsonSerializerSettings CreateSettings(Action<ITimeable> timeablePostInitializeCallback = null, Action<GlobalTimeline> globalTimelineSetCallback = null)
        {
            var ctx = new TimelineSerializationContext();
            ctx.TimeablePostInitializeCallback = timeablePostInitializeCallback;
            ctx.GlobalTimelineSetCallback = globalTimelineSetCallback;
            return new JsonSerializerSettings
            {
                PreserveReferencesHandling = PreserveReferencesHandling.Objects,
                TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
                TypeNameHandling = TypeNameHandling.Auto,
                SerializationBinder = new KnownTypesBinder(),
                ReferenceResolverProvider = () => ctx,
                Context = new StreamingContext(StreamingContextStates.All, ctx),
                Converters = new JsonConverter[]
                {
                    new GlobalTimelineConverter(),
                    new ITimelineConverter(),
                    new PushInfoConverter(),
                    new ActionConverter(),
                    new TLTimeConverter()
                }
            };
        }

        private JsonSerializerSettings CreateSettingsInternal()
        {
            return CreateSettings(TimeablePostInitializeCallback, GlobalTimelineSetCallback);
        }
        
        public class KnownTypesBinder : ISerializationBinder
        {
            private static Dictionary<string, Type> s_String2Type; 
            private static Dictionary<Type, string> s_Type2String;

            static KnownTypesBinder()
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                // workaround - Google API dll get crushed on GetTypes()
                assemblies = assemblies.Where(assembly => !assembly.FullName.Contains("Google")).ToArray();
                var acceptableTypes = assemblies.SelectMany(
                    assembly => assembly.DefinedTypes.Where(
                        info => typeof(ITimeable).IsAssignableFrom(info.AsType()))).Select(info => info.AsType()).ToList();
                acceptableTypes.Add(typeof(GlobalTimeline));
                acceptableTypes.Add(typeof(LocalTimeline));
                acceptableTypes.Add(typeof(Timeline));
                acceptableTypes.Add(typeof(PushInfo));

                s_String2Type = new Dictionary<string, Type>();
                s_Type2String = new Dictionary<Type, string>();
                foreach (var type in acceptableTypes)
                {
                    var typeName = type.Name;

                    if (s_String2Type.ContainsKey(typeName))
                    {
                        var message = $"Meta activity names must be unique. Duplicates are: {type.FullName}, {s_String2Type[typeName].FullName}";
                        throw new TimelineException(message);
                    }
                    s_String2Type[typeName] = type;
                    
                    if (s_Type2String.ContainsKey(type))
                    {
                        var message = $"Impossible. Activity added second time: {type}";
                        throw new TimelineException(message);
                    }
                    s_Type2String[type] = typeName;
                }
            }
            
            
            public Type BindToType(string assemblyName, string typeName)
            {
                var comps = typeName.Split(':');
                if (comps.Length > 1)
                {
                    var type = BindToType(assemblyName, string.Join(":", comps.Skip(1)));
                    switch (comps[0])
                    {
                        case "generic_completion_marker":
                        {
                            var finalType = typeof(Completed<>).MakeGenericType(type);
                            return finalType;
                        }
                        case "completion_marker":
                        {
                            var finalType = typeof(CompletedImpl<>).MakeGenericType(type);
                            return finalType;
                        }
                        case "subscription":
                        {
                            var finalType = typeof(Subscription<>).MakeGenericType(type);
                            return finalType;
                        }
                    }
                }

                try
                {
                    return s_String2Type[typeName];
                }
                catch (KeyNotFoundException _)
                {
                    throw new KeyNotFoundException($"Type not found for name {typeName}");
                }
            }

            
            
            public void BindToName(Type serializedType, out string assemblyName, out string typeName)
            {
                if (serializedType.IsGenericType)
                {
                    if (serializedType.GetGenericTypeDefinition() ==  typeof(Completed<>))
                    {
                        var type = serializedType.GetGenericArguments()[0];
                        BindToName(type, out assemblyName, out typeName);
                        typeName = $"generic_completion_marker:{typeName}";
                        return;
                    }
                    if (serializedType.GetGenericTypeDefinition() ==  typeof(CompletedImpl<>))
                    {
                        var type = serializedType.GetGenericArguments()[0];
                        BindToName(type, out assemblyName, out typeName);
                        typeName = $"completion_marker:{typeName}";
                        return;
                    }
                    if(serializedType.GetGenericTypeDefinition() ==  typeof(Subscription<>))
                    {
                        var type = serializedType.GetGenericArguments()[0];
                        BindToName(type, out assemblyName, out typeName);
                        typeName = $"subscription:{typeName}";
                        return;
                    }
                }

                assemblyName = null;
                try
                {
                    typeName = s_Type2String[serializedType];
                }
                catch (KeyNotFoundException e)
                {
                    throw new KeyNotFoundException($"Name not found for type {serializedType}");
                }
            }
        }

    }

    public class SimpleCollectionsConverter : JsonConverter
    {
        public override bool CanWrite => false;

        public override bool CanConvert(Type objectType)
        {
            return 
                objectType == typeof(object) ||
                objectType == typeof(Dictionary<string, object>) ||
                objectType == typeof(List<object>);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotSupportedException($"{nameof(SimpleCollectionsConverter)} should only be used while deserializing.");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;

            object obj = null;
            switch (reader.TokenType)
            {
                case JsonToken.Null:
                    return null;
                // case JsonToken.Integer when objectType == typeof(object) && (long)reader.Value <= int.MaxValue && (long)reader.Value >= int.MinValue:
                //     return Convert.ToInt32(reader.Value);
                case JsonToken.StartObject:
                    obj = new Dictionary<string, object>();
                    serializer.Populate(reader, obj);
                    break;
                case JsonToken.StartArray:
                    var list = new List<object>();
                    serializer.Populate(reader, list);
                    // obj = list.ToArray();
                    obj = list;
                    break;
                default:
                    obj = JToken.ReadFrom(reader).ToObject<object>();
                    break;
            }
            
            return obj;
        }
    }
    
}