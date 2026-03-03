using AIWarsIdle.Analytics;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Bootstrap
{
    internal sealed class NullAnalyticsSink : IAnalyticsSink
    {
        public void Send(string name, AnalyticsParam[] parameters)
        {
        }
    }
}

