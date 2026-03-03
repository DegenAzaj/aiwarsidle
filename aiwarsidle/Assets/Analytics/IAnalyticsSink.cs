using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Analytics
{
    public interface IAnalyticsSink
    {
        void Send(string name, AnalyticsParam[] parameters);
    }
}

