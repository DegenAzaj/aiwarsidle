using System;
using UnityEngine;

namespace AIWarsIdle.Bootstrap
{
    internal sealed class BootstrapTime
    {
        private long _unixBaseSeconds;
        private double _realtimeBaseSeconds;
        private bool _initialized;

        public long NowUnixSeconds
        {
            get
            {
                EnsureInitialized();
                var elapsed = Time.realtimeSinceStartupAsDouble - _realtimeBaseSeconds;
                if (elapsed < 0) elapsed = 0;
                return _unixBaseSeconds + (long)Math.Floor(elapsed);
            }
        }

        private void EnsureInitialized()
        {
            if (_initialized) return;
            _initialized = true;
            _unixBaseSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _realtimeBaseSeconds = Time.realtimeSinceStartupAsDouble;
        }
    }
}

