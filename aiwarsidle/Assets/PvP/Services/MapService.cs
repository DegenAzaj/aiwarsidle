using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class MapService
    {
        private readonly MapState _state;
        private readonly MapConfig _config;
        private readonly OverclockService _overclock;
        private long _lastTickUnixSeconds;

        public MapService(MapState state, MapConfig config, OverclockService overclock = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _overclock = overclock;

            _config.ValidateOrThrow();
            _state.Sectors ??= Array.Empty<SectorState>();
        }

        public void TickStability(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (_lastTickUnixSeconds == 0)
            {
                _lastTickUnixSeconds = nowUnixSeconds;
                return;
            }

            var delta = nowUnixSeconds - _lastTickUnixSeconds;
            if (delta <= 0) return;
            _lastTickUnixSeconds = nowUnixSeconds;

            if (_config.StabilityGrowthPerSecond <= 0f) return;
            if (_state.Sectors.Length == 0) return;

            for (var i = 0; i < _state.Sectors.Length; i++)
            {
                var sector = _state.Sectors[i];
                if (sector == null) continue;

                var growth = _config.StabilityGrowthPerSecond;

                if (IsFreshCapture(sector, nowUnixSeconds) && _overclock != null)
                {
                    growth *= (float)_overclock.GetFreshCaptureStabilityGrowthMultiplier(nowUnixSeconds);
                }

                var next = sector.Stability + (growth * delta);
                if (next > 100f) next = 100f;
                if (next < 0f) next = 0f;
                sector.Stability = next;
            }
        }

        private bool IsFreshCapture(SectorState sector, long nowUnixSeconds)
        {
            if (_config.FreshCaptureWindowSeconds <= 0) return false;
            if (sector.CapturedUnixSeconds <= 0) return false;

            var age = nowUnixSeconds - sector.CapturedUnixSeconds;
            return age >= 0 && age <= _config.FreshCaptureWindowSeconds;
        }
    }
}

