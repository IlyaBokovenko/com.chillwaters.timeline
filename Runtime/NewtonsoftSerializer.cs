using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using CW.Core.Json;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

namespace CW.Core.Timeline.Serialization
{
    public class NewtonsoftSerializer : ISerializator
    {
        private static JsonSerializerSettings s_settings;
        
        private bool _isPretty;
        private Formatting Formatting => _isPretty ? Formatting.Indented : Formatting.None;
        

        static NewtonsoftSerializer()
        {
            s_settings = CreateSettings();
        }

        private NewtonsoftSerializer(bool isPretty)
        {
            _isPretty = isPretty;
        }

        public static NewtonsoftSerializer Create()
        {
            return new NewtonsoftSerializer(false);
        }
        
        public static NewtonsoftSerializer CreatePretty()
        {
            return new NewtonsoftSerializer(true);
        }

        public string Serialize(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting, s_settings);
        }

        public T Deserialize<T>(string serialize) where T : class
        {
            return JsonConvert.DeserializeObject<T>(serialize, s_settings);
        }

        public object Deserialize(string data, Type type)
        {
            return JsonConvert.DeserializeObject(data, type, s_settings);
        }

        private static JsonSerializerSettings CreateSettings()
        {
            var settings = new JsonSerializerSettings
            {
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,
                ContractResolver = new JsonFXContractResolver(),
                Converters = new JsonConverter[]
                {
                    new SimpleCollectionsConverter(),
                    new StringEnumConverter(),
                    new TLTimeConverter(),
                    //TODO: залипуха. В конфигах не должно быть дробных значений для целочисленных данных
                    new FloatAsIntConverter()
                }
            };
            return settings;
        }

        private class FloatAsIntConverter : JsonConverter<int>
        {
            public override bool CanWrite => false;

            public override void WriteJson(JsonWriter writer, int value, JsonSerializer serializer)
            {
                throw new NotSupportedException($"{nameof(FloatAsIntConverter)} should only be used while deserializing.");
            }

            public override int ReadJson(JsonReader reader, Type objectType, int existingValue, bool hasExistingValue,
                JsonSerializer serializer)
            {
                var str = JToken.ReadFrom(reader).ToObject<string>();
                var delimIdx = str.IndexOf('.');
                if (delimIdx > 0)
                {
                    str = str.Substring(0, delimIdx);
                }

                return int.Parse(str);
            }
        }
        
        // если у класса не указан атрибут JsonObject
        // то сериализует как JsonFx - все публичные члены - не свойства
        private class JsonFXContractResolver : DefaultContractResolver
        {
            private static ConcurrentDictionary<Type, Dictionary<Type, object>> s_mapTypeToSerializationAttributes = new ConcurrentDictionary<Type, Dictionary<Type, object>>();

            public JsonFXContractResolver()
            {
                IgnoreSerializableAttribute = false;
            }

            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                var serializableMembers = base.GetSerializableMembers(objectType);
                if (!HasCustomSerializationAttributes(objectType))
                {
                    serializableMembers.RemoveAll(info => info.MemberType == MemberTypes.Property);
                }

                return serializableMembers;
            }


            private bool HasCustomSerializationAttributes(Type objectType)
            {
                var attrs = GetCachedSerializationAttributes(objectType);
                return attrs.ContainsKey(typeof(JsonObjectAttribute)) ||
                       attrs.ContainsKey(typeof(SerializableAttribute));
            }

            private Dictionary<Type, object> GetCachedSerializationAttributes(Type objectType)
            {
                Dictionary<Type, object> attrs;
                if (!s_mapTypeToSerializationAttributes.TryGetValue(objectType, out attrs))
                {
                    attrs = new Dictionary<Type, object>();
                    object attr = Attribute.GetCustomAttribute(objectType, typeof(JsonObjectAttribute));
                    if(attr != null)
                        attrs[typeof(JsonObjectAttribute)] = attr;
                    attr = Attribute.GetCustomAttribute(objectType, typeof(SerializableAttribute));
                    if(attr != null)
                        attrs[typeof(SerializableAttribute)] = attr;
                    s_mapTypeToSerializationAttributes[objectType] = attrs;
                }

                return attrs;
            }
        }
    }
}