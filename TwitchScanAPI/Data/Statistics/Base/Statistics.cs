using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TwitchScanAPI.Models.Twitch;

namespace TwitchScanAPI.Data.Statistics.Chat.Base
{
    public class Statistics
    {
        private readonly List<IStatistic> _statistics;
        private readonly Dictionary<Type, List<(IStatistic Statistic, MethodInfo UpdateMethod)>> _eventHandlers;

        public Statistics()
        {
            _statistics = DiscoverStatistics();
            _eventHandlers = BuildEventHandlers();
        }

        private List<IStatistic> DiscoverStatistics()
        {
            var statisticType = typeof(IStatistic);
            var statistics = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => statisticType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(Activator.CreateInstance)
                .Cast<IStatistic>()
                .ToList();

            return statistics;
        }

        private Dictionary<Type, List<(IStatistic, MethodInfo)>> BuildEventHandlers()
        {
            var handlers = new Dictionary<Type, List<(IStatistic, MethodInfo)>>();

            foreach (var statistic in _statistics)
            {
                var methods = statistic.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Update" && m.GetParameters().Length == 1);
                
                foreach (var method in methods)
                {
                    var eventType = method.GetParameters()[0].ParameterType;
                    if (!handlers.ContainsKey(eventType))
                    {
                        handlers[eventType] = new List<(IStatistic, MethodInfo)>();
                    }
                    handlers[eventType].Add((statistic, method));
                }
            }

            return handlers;
        }

        public IDictionary<string, object> GetAllStatistics()
        {
            return _statistics.ToDictionary(stat => stat.Name, stat => stat.GetResult());
        }

        public object GetStatistic(string name)
        {
            var stat = _statistics.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return stat?.GetResult();
        }

        public void Update<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlers)) return;
            foreach (var (statistic, method) in handlers)
            {
                method.Invoke(statistic, new object[] { eventData });
            }
        }
    }
}