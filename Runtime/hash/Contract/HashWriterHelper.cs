using System;
using System.Collections.Generic;
using System.Linq;

namespace CW.Core.Hash
{
    public static class HashWriterHelper
    {
        private static HashSet<Type> s_supportedTypes;

        static HashWriterHelper()
        {
            s_supportedTypes = new HashSet<Type>(
                typeof(IHashWriter).GetMethods()
                    .Where(info => info.Name == "Write")
                    .Select(info => info.GetParameters()[0].ParameterType));
        }

        public static bool IsSupportedType(Type type)
        {
            return s_supportedTypes.Contains(type);
        }

        public static bool WritePrimitive(object data, IHashWriter writer)
        {
            var type = data.GetType();
            if (type.IsArray)
            {
                var elementType = type.GetElementType();
                switch (Type.GetTypeCode(elementType))
                {
                    case TypeCode.Byte:
                        writer.Write((byte[]) data);
                        return true;
                    case TypeCode.Char:
                        writer.Write((char[]) data);
                        return true;
                }
            }
            else
            {
                switch (Type.GetTypeCode(type))
                {
                    case TypeCode.Boolean:
                        writer.Write((bool) data);
                        return true;
                    case TypeCode.Byte:
                        writer.Write((byte) data);
                        return true;
                    case TypeCode.Char:
                        writer.Write((char) data);
                        return true;
                    case TypeCode.Decimal:
                        writer.Write((decimal) data);
                        return true;
                    case TypeCode.Double:
                        writer.Write((double) data);
                        return true;
                    case TypeCode.Int16:
                        writer.Write((short) data);
                        return true;
                    case TypeCode.Int32:
                        writer.Write((int) data);
                        return true;
                    case TypeCode.Int64:
                        writer.Write((long) data);
                        return true;
                    case TypeCode.SByte:
                        writer.Write((sbyte) data);
                        return true;
                    case TypeCode.Single:
                        writer.Write((float) data);
                        return true;
                    case TypeCode.String:
                        writer.Write((string) data);
                        return true;
                    case TypeCode.UInt16:
                        writer.Write((ushort) data);
                        return true;
                    case TypeCode.UInt32:
                        writer.Write((uint) data);
                        return true;
                    case TypeCode.UInt64:
                        writer.Write((ulong) data);
                        return true;
                }
            }

            return false;
        }
    }
}