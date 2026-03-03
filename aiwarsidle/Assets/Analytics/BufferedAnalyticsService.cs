using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Analytics
{
    public sealed class BufferedAnalyticsService : IAnalyticsService
    {
        private readonly IAnalyticsSink _sink;
        private readonly int _maxPendingEvents;
        private readonly Queue<(string Name, AnalyticsParam[] Parameters)> _queue = new();

        public BufferedAnalyticsService(IAnalyticsSink sink, int maxPendingEvents = 1024)
        {
            _sink = sink;
            _maxPendingEvents = maxPendingEvents <= 0 ? 0 : maxPendingEvents;
        }

        public int PendingCount => _queue.Count;

        public void Track(string name, params AnalyticsParam[] parameters)
        {
            if (string.IsNullOrWhiteSpace(name)) return;
            if (_sink == null) return;
            if (_maxPendingEvents == 0) return;

            AnalyticsParam[] copy;
            if (parameters == null || parameters.Length == 0)
            {
                copy = Array.Empty<AnalyticsParam>();
            }
            else
            {
                copy = new AnalyticsParam[parameters.Length];
                Array.Copy(parameters, copy, parameters.Length);
            }

            try
            {
                if (_queue.Count >= _maxPendingEvents)
                {
                    // Drop newest when full to avoid unbounded memory growth.
                    return;
                }

                _queue.Enqueue((name, copy));
            }
            catch
            {
                // Never allow analytics to crash gameplay.
            }
        }

        public int Flush(int maxEvents = 256)
        {
            if (_sink == null) return 0;
            if (maxEvents <= 0) return 0;

            var sent = 0;
            while (sent < maxEvents && _queue.Count > 0)
            {
                var (name, parameters) = _queue.Dequeue();
                try
                {
                    _sink.Send(name, parameters ?? Array.Empty<AnalyticsParam>());
                }
                catch
                {
                    // Swallow backend errors; keep draining so we don't stall the game loop.
                }

                sent++;
            }

            return sent;
        }
    }
}

