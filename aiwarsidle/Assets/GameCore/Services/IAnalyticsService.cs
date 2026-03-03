using System;

namespace AIWarsIdle.GameCore.Services
{
    public readonly struct AnalyticsParam
    {
        public readonly string Key;
        public readonly string Value;

        public AnalyticsParam(string key, string value)
        {
            Key = key ?? throw new ArgumentNullException(nameof(key));
            Value = value ?? throw new ArgumentNullException(nameof(value));
        }
    }

    public interface IAnalyticsService
    {
        void Track(string name, params AnalyticsParam[] parameters);
    }
}

