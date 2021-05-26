using System.Collections.Generic;

namespace CW.Core.Timeline.Tests
{
    public static class ITimeableTestExtensions
    {
        private static Dictionary<ITimeable, string> timeable2name = new Dictionary<ITimeable, string>();
        public static T Named<T>(this T timeable, string name) where T : ITimeable
        {
            timeable2name[timeable] = name;
            return timeable;
        }

        public static string Name(this ITimeable timeable)
        {
            timeable2name.TryGetValue(timeable, out string name);
            return name;
        }
    }
}