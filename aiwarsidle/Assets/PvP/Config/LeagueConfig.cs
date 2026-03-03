using System;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/League Config", fileName = "LeagueConfig")]
    public sealed class LeagueConfig : ScriptableObject
    {
        [Header("Leagues")]
        [Tooltip("Ascending SeasonPoints thresholds per league index. Example (5 leagues): [0, 100, 200, 500, 1000].")]
        public int[] LeaguePointThresholds = { 0, 100, 200, 500, 1000 };

        [Header("Season")]
        public LeagueSeasonMode SeasonMode = LeagueSeasonMode.CalendarMonthUtc;

        [Min(1)]
        [Tooltip("Only used when SeasonMode=FixedDays.")]
        public int FixedSeasonLengthDays = 28;

        [Min(0)]
        [Tooltip("Only used when SeasonMode=FixedDays. UnixTimeSeconds (UTC) anchor for season boundaries.")]
        public long FixedSeasonAnchorUnixSecondsUtc = 0;

        [Tooltip("If you change SeasonMode/length/anchor during a live season, consider applying the new scheme from the next season boundary (apply from next season) to avoid surprise mid-season resets.")]
        public bool ApplySchemeChangesFromNextSeason = true;

        public void ValidateOrThrow()
        {
            if (LeaguePointThresholds == null || LeaguePointThresholds.Length == 0)
            {
                throw new InvalidOperationException($"{nameof(LeaguePointThresholds)} must be non-empty.");
            }

            if (LeaguePointThresholds[0] != 0)
            {
                throw new InvalidOperationException($"{nameof(LeaguePointThresholds)}[0] must be 0.");
            }

            for (var i = 0; i < LeaguePointThresholds.Length; i++)
            {
                if (LeaguePointThresholds[i] < 0)
                {
                    throw new InvalidOperationException($"{nameof(LeaguePointThresholds)}[{i}] must be >= 0.");
                }

                if (i > 0 && LeaguePointThresholds[i] < LeaguePointThresholds[i - 1])
                {
                    throw new InvalidOperationException($"{nameof(LeaguePointThresholds)} must be ascending.");
                }
            }

            if (SeasonMode == LeagueSeasonMode.FixedDays)
            {
                if (FixedSeasonLengthDays <= 0)
                {
                    throw new InvalidOperationException($"{nameof(FixedSeasonLengthDays)} must be > 0.");
                }
                if (FixedSeasonAnchorUnixSecondsUtc < 0)
                {
                    throw new InvalidOperationException($"{nameof(FixedSeasonAnchorUnixSecondsUtc)} must be >= 0.");
                }
            }
        }
    }
}

