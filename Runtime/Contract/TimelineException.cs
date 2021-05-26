using System;

namespace CW.Core.Timeline
{
    public class TimelineException : Exception
    {
        public TimelineException()
        {
        }

        public TimelineException(string description) : base(description)
        {
        }
    }
}