
using System;

namespace CW.Core.Json
{
    public interface ISerializator
    {
        string Serialize(object obj);
        T Deserialize<T>(string serialize) where T : class;
        object Deserialize(string data, Type type);
    }
}