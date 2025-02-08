using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using TwitchScanAPI.Data.Statistics.Annotations;

namespace TwitchScanAPI.Data.Statistics.Base
{
    public class Statistics
    {
        private ImmutableDictionary<Type, ImmutableList<(IStatistic Statistic, MethodInfo UpdateMethod)>>
            _eventHandlers;

        private ImmutableList<IStatistic> _statistics;

        public Statistics()
        {
            _statistics = DiscoverStatistics().ToImmutableList();
            _eventHandlers = BuildEventHandlers().ToImmutableDictionary();
        }

        private List<IStatistic> DiscoverStatistics()
        {
            var statisticType = typeof(IStatistic);
            var statistics = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => statisticType.IsAssignableFrom(t) && t is { IsInterface: false, IsAbstract: false })
                .Select(Activator.CreateInstance) // Dynamically create an instance of each discovered statistic class
                .Cast<IStatistic>()
                .ToList();

            return statistics;
        }

        private ImmutableDictionary<Type, ImmutableList<(IStatistic, MethodInfo)>> BuildEventHandlers()
        {
            var handlers = new Dictionary<Type, ImmutableList<(IStatistic, MethodInfo)>>();

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
                        handlers[eventType] = ImmutableList<(IStatistic, MethodInfo)>.Empty;

                    // Add the statistic and its corresponding 'Update' method to the list for this event type
                    handlers[eventType] = handlers[eventType].Add((statistic, method)); // Return a new ImmutableList
                }
            }

            return handlers.ToImmutableDictionary(); // Convert the Dictionary to an ImmutableDictionary
        }

        /// <summary>
        ///     Reset all statistics to their initial state.
        /// </summary>
        public void Reset()
        {
            // Reassign with a new immutable list and immutable dictionary
            _statistics = DiscoverStatistics().ToImmutableList();
            _eventHandlers = BuildEventHandlers().ToImmutableDictionary();
            Cleanup();
        }

        public IDictionary<string, object> GetAllStatistics()
        {
            return _statistics
                .Where(stat =>
                    !stat.GetType().GetCustomAttributes(typeof(IgnoreStatisticAttribute), false)
                        .Any()) // Filter out ignored statistics
                .ToDictionary(stat => stat.Name, stat => stat.GetResult());
        }
        
        private void Cleanup()
        {
            foreach (var statistic in _statistics)
            {
                if (statistic is IDisposable disposable) disposable.Dispose();
            }
        }

        public object? GetStatistic(string name)
        {
            var stat = _statistics.FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
            return stat?.GetResult();
        }

        public async Task Update<TEvent>(TEvent eventData)
        {
            var eventType = typeof(TEvent);
            if (!_eventHandlers.TryGetValue(eventType, out var handlers)) return;

            // Invoke each statistic's 'Update' method, passing in the event data
            foreach (var (statistic, method) in handlers)
            {
                if (eventData == null) continue;
                var result = method.Invoke(statistic, new object[] { eventData });

                // Check if the method returns a Task
                if (result is Task task) await task; // Await if it's a Task
            }
        }
    }
}