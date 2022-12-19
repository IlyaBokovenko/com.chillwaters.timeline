using System;
using System.Globalization;

namespace CW.Core.Timeline
{
    public struct TlTime : IComparable<TlTime>
    {
        long _value;
    
        public static TlTime Zero => new TlTime(0L);
    
        public long ToMilliseconds => _value;
        public float ToSeconds => (float) ((double) _value / 1000);

        TlTime(long value)
        {
            _value = value;
        }
        public static TlTime FromSeconds(float value)
        {
            return new TlTime((long) ((double)value * 1000));
        }
    
        public static TlTime FromMilliseconds(long value)
        {
            return new TlTime(value);
        }

        public override string ToString()
        {
            return _value.ToString();
        }

        #region math

        public static bool operator >(TlTime a, TlTime b)
        {
            return a._value > b._value;
        }

        public static bool operator <(TlTime a, TlTime b)
        {
            return a._value < b._value;
        }
    
        public static bool operator >=(TlTime a, TlTime b)
        {
            return a._value >= b._value;
        }

        public static bool operator <=(TlTime a, TlTime b)
        {
            return a._value <= b._value;
        }

        public static TlTime operator +(TlTime a, TlTime b)
        {
            return new TlTime(a._value + b._value);
        }

        public static TlTime operator -(TlTime a, TlTime b)
        {
            return new TlTime(a._value - b._value);
        }

        public static bool operator ==(TlTime a, TlTime b)
        {
            return a._value == b._value;
        }

        public static bool operator !=(TlTime a, TlTime b)
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

        public int CompareTo(TlTime other)
        {
            return _value.CompareTo(other._value);
        }

        public override bool Equals(object obj)
        {
            switch (obj)
            {
                case TlTime other:
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
        public static TlTime TLMilliseconds(this long val)
        {
            return TlTime.FromMilliseconds(val);
        }
    
        public static TlTime TLSeconds(this float val)
        {
            return TlTime.FromSeconds(val);
        }

        public static string ToString(this TlTime tlTime, CultureInfo _)
        {
            return tlTime.ToString();
        }
    }
}