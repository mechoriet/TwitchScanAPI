using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class Statistics
    {
        private readonly List<IStatistic> _statistics;
        private Dictionary<Type, List<(IStatistic Statistic, MethodInfo UpdateMethod)>> _eventHandlers;

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
                .Select(Activator.CreateInstance) // Dynamically create an instance of each discovered statistic class
                .Cast<IStatistic>()
                .ToList();

            return statistics;
        }

        private Dictionary<Type, List<(IStatistic, MethodInfo)>> BuildEventHandlers()
        {
            var handlers = new Dictionary<Type, List<(IStatistic, MethodInfo)>>();

            foreach (var statistic in _statistics)
            {
                // Find all 'Update' methods that accept exactly one parameter
                var methods = statistic.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => m.Name == "Update" && m.GetParameters().Length == 1);

                foreach (var method in methods)
                {
                    // Use the method's parameter type as the event type
                    var eventType = method.GetParameters()[0].ParameterType;

                    // Initialize the handler list for this event type if it doesn't already exist
                    if (!handlers.ContainsKey(eventType))
                    {
                        handlers[eventType] = new List<(IStatistic, MethodInfo)>();
                    }

                    // Add the statistic and its corresponding 'Update' method to the list for this event type
                    handlers[eventType].Add((statistic, method));
                }
            }

            return handlers;
        }
        
        /// <summary>
        /// Reset all statistics to their initial state.
        /// </summary>
        public void Reset()
        {
            _statistics.Clear();
            _statistics.AddRange(DiscoverStatistics());
            _eventHandlers.Clear();
            _eventHandlers = BuildEventHandlers();
        }

        public IDictionary<string, object> GetAllStatistics()
        {
            return _statistics.ToDictionary(stat => stat.Name, stat => stat.GetResult());
        }

        public object? GetStatistic(string name)
        {
            var stat = _statistics.Find(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return stat?.GetResult();
        }

        public void Update<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlers)) return;

            // Invoke each statistic's 'Update' method, passing in the event data
            foreach (var (statistic, method) in handlers)
            {
                if (eventData != null) method.Invoke(statistic, new object[] { eventData });
            }
        }
    }
}