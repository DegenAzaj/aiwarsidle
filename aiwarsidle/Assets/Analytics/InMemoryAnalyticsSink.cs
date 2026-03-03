using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Analytics
{
    public sealed class InMemoryAnalyticsSink : IAnalyticsSink
    {
        public readonly struct TrackedEvent
        {
            public readonly string Name;
            public readonly AnalyticsParam[] Parameters;

            public TrackedEvent(string name, AnalyticsParam[] parameters)
            {
                Name = name;
                Parameters = parameters;
            }
        }

        private readonly List<TrackedEvent> _events = new();

        public IReadOnlyList<TrackedEvent> Events => _events;

        public void Send(string name, AnalyticsParam[] parameters)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            _events.Add(new TrackedEvent(name, parameters ?? Array.Empty<AnalyticsParam>()));
        }
    }
}

