using System.Collections.Generic;
using System.Linq;

namespace CW.Extensions.Pooling
{
    public interface IPoolStat
    {
        string Id { get; }
        string Stat { get; }
    }

    public static class PoolRegistry
    {
        private static List<IPoolStat> _stat = new List<IPoolStat>();
        public static void Register(IPoolStat stat)
        {
            _stat.Add(stat);
        }

        public static string Stat => string.Join("\n", _stat.Select(stat => $"{stat.Id}: {stat.Stat}"));
    }
}