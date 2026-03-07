using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class LeagueService
    {
        private readonly GameState _state;
        private readonly LeagueConfig _config;

        public LeagueService(GameState state, LeagueConfig config)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _config.ValidateOrThrow();
        }

        public void ResetSeasonIfNeeded(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var currentSeasonId = ComputeSeasonId(nowUnixSeconds);
            if (_state.LeagueSeasonId == currentSeasonId) return;

            _state.LeagueSeasonId = currentSeasonId;
            _state.SeasonPoints = 0;
            _state.League = GetLeagueForPoints(0);
        }

        public void AddSeasonPoints(long nowUnixSeconds, int deltaPoints)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            ResetSeasonIfNeeded(nowUnixSeconds);

            var next = _state.SeasonPoints + deltaPoints;
            if (next < 0) next = 0;

            _state.SeasonPoints = next;
            _state.League = GetLeagueForPoints(_state.SeasonPoints);
        }

        public int GetLeagueForPoints(int seasonPoints)
        {
            if (seasonPoints < 0) seasonPoints = 0;

            var thresholds = _config.LeaguePointThresholds;
            var league = 0;
            for (var i = 0; i < thresholds.Length; i++)
            {
                if (seasonPoints >= thresholds[i]) league = i;
            }

            return league;
        }

        public long GetCurrentSeasonEndsAtUnixSeconds(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            return _config.SeasonMode switch
            {
                LeagueSeasonMode.CalendarMonthUtc => GetCalendarMonthEndUnixSeconds(nowUnixSeconds),
                LeagueSeasonMode.FixedDays => GetFixedSeasonEndUnixSeconds(nowUnixSeconds),
                _ => throw new InvalidOperationException("Unknown season mode.")
            };
        }

        private int ComputeSeasonId(long nowUnixSeconds)
        {
            // NOTE: If you change the season scheme in a live build, you may want to apply it from the next season boundary
            // ("apply from next season") to avoid surprising mid-season resets.
            return _config.SeasonMode switch
            {
                LeagueSeasonMode.CalendarMonthUtc => ComputeCalendarMonthUtcId(nowUnixSeconds),
                LeagueSeasonMode.FixedDays => ComputeFixedDaysId(nowUnixSeconds),
                _ => throw new InvalidOperationException("Unknown season mode.")
            };
        }

        private static int ComputeCalendarMonthUtcId(long nowUnixSeconds)
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).UtcDateTime;
            checked
            {
                return (utc.Year * 12) + (utc.Month - 1);
            }
        }

        private int ComputeFixedDaysId(long nowUnixSeconds)
        {
            var seasonLenSeconds = (long)_config.FixedSeasonLengthDays * 24L * 60L * 60L;
            if (seasonLenSeconds <= 0) throw new InvalidOperationException("Fixed season length must be > 0.");

            var anchor = _config.FixedSeasonAnchorUnixSecondsUtc;
            var delta = nowUnixSeconds - anchor;
            if (delta <= 0) return 0;
            return (int)(delta / seasonLenSeconds);
        }

        private static long GetCalendarMonthEndUnixSeconds(long nowUnixSeconds)
        {
            var utc = DateTimeOffset.FromUnixTimeSeconds(nowUnixSeconds).UtcDateTime;
            var nextMonth = new DateTimeOffset(new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc)).AddMonths(1);
            return nextMonth.ToUnixTimeSeconds();
        }

        private long GetFixedSeasonEndUnixSeconds(long nowUnixSeconds)
        {
            var seasonLenSeconds = (long)_config.FixedSeasonLengthDays * 24L * 60L * 60L;
            if (seasonLenSeconds <= 0) throw new InvalidOperationException("Fixed season length must be > 0.");

            var anchor = _config.FixedSeasonAnchorUnixSecondsUtc;
            var seasonId = ComputeFixedDaysId(nowUnixSeconds);
            return anchor + ((seasonId + 1L) * seasonLenSeconds);
        }
    }
}
