using System;
using System.Globalization;

namespace CW.Core.Timeline
{
    public struct TLTime : IComparable<TLTime>
    {
        long _value;
    
        public static TLTime Zero => new TLTime(0L);
    
        public long ToMilliseconds => _value;
        public float ToSeconds => (float) ((double) _value / 1000);

        TLTime(long value)
        {
            _value = value;
        }
        public static TLTime FromSeconds(float value)
        {
            return new TLTime((long) ((double)value * 1000));
        }
        
        public static TLTime FromSeconds(decimal value)
        {
            return new TLTime((long) ((double)value * 1000));
        }
    
        public static TLTime FromMilliseconds(long value)
        {
            return new TLTime(value);
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        #region math

        public static bool operator >(TLTime a, TLTime b)
        {
            return a._value > b._value;
        }

        public static bool operator <(TLTime a, TLTime b)
        {
            return a._value < b._value;
        }
    
        public static bool operator >=(TLTime a, TLTime b)
        {
            return a._value >= b._value;
        }

        public static bool operator <=(TLTime a, TLTime b)
        {
            return a._value <= b._value;
        }

        public static TLTime operator +(TLTime a, TLTime b)
        {
            return new TLTime(a._value + b._value);
        }

        public static TLTime operator *(TLTime a, int b)
        {
            return new TLTime(a._value * b);
        }
        
        public static TLTime operator *(int b, TLTime a)
        {
            return new TLTime(a._value * b);
        }

        public static TLTime operator -(TLTime a, TLTime b)
        {
            return new TLTime(a._value - b._value);
        }

        public static bool operator ==(TLTime a, TLTime b)
        {
            return a._value == b._value;
        }

        public static bool operator !=(TLTime a, TLTime b)
        {
            return !(a == b);
        }
    
        #endregion

        #region conversions

        // public static implicit operator TLTime(long value)
        // {
        //     return FromMilliseconds(value);
        // }
        //
        // public static implicit operator long(TLTime value)
        // {
        //     return value._value;
        // }

        // public static implicit operator TLTime(float value)
        // {
        //     return FromSeconds(value);
        // }
        //
        // public static implicit operator float(TLTime value)
        // {
        //     return value.ToSeconds;
        // }

        #endregion

        #region equality & comparison

        public int CompareTo(TLTime other)
        {
            return _value.CompareTo(other._value);
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case TLTime other:
                    return _value == other._value;
                // case float fl:
                //     return ToSeconds == fl;
                // case long lng:
                //     return ToMilliseconds == lng;
                // case int intgr:
                //     return ToMilliseconds == intgr;
                default:
                    return false;
            }
        }

        public override int GetHashCode()
        {
            return _value.GetHashCode();
        }

        #endregion
    }

    public static class TLTimeExtensions
    {
        public static TLTime TLMilliseconds(this long val)
        {
            return TLTime.FromMilliseconds(val);
        }
    
        public static TLTime TLSeconds(this float val)
        {
            return TLTime.FromSeconds(val);
        }

        public static string ToString(this TLTime tlTime, CultureInfo _)
        {
            return tlTime.ToString();
        }
    }
}