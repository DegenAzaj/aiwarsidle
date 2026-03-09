using System;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.Persistence.Domain
{
    public sealed class SaveDataV1
    {
        public int Version = 1;

        public double SoftCurrency;
        public double LifetimeEarnedSoftCurrency;
        public double LifetimeEarnedSoftCurrencyAtLastPrestige;
        public int PremiumCurrency;
        public int[] GeneratorLevels = new int[GameState.GeneratorCount];
        public int PrestigeCount;
        public int PermanentUpgradeLevel;

        public int LeagueSeasonId;
        public int League;
        public int SeasonPoints;

        public int PvpAttacksRemaining;
        public long LastPvpAttackRegenUnixSeconds;
        public long NextPvpAttackRegenAtUnixSeconds;
        public long LastPvpAdAttackClaimUnixSeconds;

        public int MapSeasonId;
        public double MatchStartLocalPvpPower;
        public SectorSaveData[] Sectors = Array.Empty<SectorSaveData>();

        public OverclockSaveData Overclock = new();

        public long LastLoginUnixSeconds;
        public double PendingOfflineGain;
        public long LastBankedOfflineRawSeconds;
        public long LastBankedOfflineEffectiveSeconds;

        public void Normalize()
        {
            if (GeneratorLevels == null)
            {
                GeneratorLevels = new int[GameState.GeneratorCount];
            }
            else if (GeneratorLevels.Length != GameState.GeneratorCount)
            {
                var fixedLevels = new int[GameState.GeneratorCount];
                Array.Copy(GeneratorLevels, fixedLevels, Math.Min(GeneratorLevels.Length, fixedLevels.Length));
                GeneratorLevels = fixedLevels;
            }

            // Seed a playable fresh start (avoid "stuck at 0 currency" with all generators at level 0).
            // Only applies to brand-new / empty progression.
            var anyGeneratorLevelPositive = false;
            for (var i = 0; i < GeneratorLevels.Length; i++)
            {
                if (GeneratorLevels[i] > 0)
                {
                    anyGeneratorLevelPositive = true;
                    break;
                }
            }

            if (!anyGeneratorLevelPositive
                && SoftCurrency <= 0
                && LifetimeEarnedSoftCurrency <= 0
                && PrestigeCount <= 0
                && PermanentUpgradeLevel <= 0
                && GeneratorLevels.Length > 0)
            {
                GeneratorLevels[0] = 1;
            }

            if (Sectors == null)
            {
                Sectors = Array.Empty<SectorSaveData>();
            }

            for (var i = 0; i < Sectors.Length; i++)
            {
                Sectors[i] ??= new SectorSaveData();
                Sectors[i].Normalize();
            }

            Overclock ??= new OverclockSaveData();
            Overclock.Normalize();

            if (LifetimeEarnedSoftCurrency < 0) LifetimeEarnedSoftCurrency = 0;
            if (LifetimeEarnedSoftCurrencyAtLastPrestige < 0) LifetimeEarnedSoftCurrencyAtLastPrestige = 0;
            if (LifetimeEarnedSoftCurrencyAtLastPrestige > LifetimeEarnedSoftCurrency)
            {
                LifetimeEarnedSoftCurrencyAtLastPrestige = LifetimeEarnedSoftCurrency;
            }
            if (LastPvpAttackRegenUnixSeconds < 0) LastPvpAttackRegenUnixSeconds = 0;
            if (NextPvpAttackRegenAtUnixSeconds < 0) NextPvpAttackRegenAtUnixSeconds = 0;
            if (LastPvpAdAttackClaimUnixSeconds < 0) LastPvpAdAttackClaimUnixSeconds = 0;
            if (double.IsNaN(MatchStartLocalPvpPower) || double.IsInfinity(MatchStartLocalPvpPower) || MatchStartLocalPvpPower < 0)
            {
                MatchStartLocalPvpPower = 0;
            }
            if (LastLoginUnixSeconds < 0) LastLoginUnixSeconds = 0;
            if (PendingOfflineGain < 0) PendingOfflineGain = 0;
            if (LastBankedOfflineRawSeconds < 0) LastBankedOfflineRawSeconds = 0;
            if (LastBankedOfflineEffectiveSeconds < 0) LastBankedOfflineEffectiveSeconds = 0;
            if (LastBankedOfflineEffectiveSeconds > LastBankedOfflineRawSeconds)
            {
                LastBankedOfflineEffectiveSeconds = LastBankedOfflineRawSeconds;
            }
        }
    }

    public sealed class SectorSaveData
    {
        public int SectorId;
        public int OwnerPlayerId;

        public double OwnerPvpPower;
        public int OwnerLeague;
        public int OwnerSeasonPoints;

        public float Stability;
        public long LastCombatUnixSeconds;
        public long CapturedUnixSeconds;

        public void Normalize()
        {
            if (Stability < 0f) Stability = 0f;
            if (Stability > 100f) Stability = 100f;

            if (LastCombatUnixSeconds < 0) LastCombatUnixSeconds = 0;
            if (CapturedUnixSeconds < 0) CapturedUnixSeconds = 0;
        }
    }

    public sealed class OverclockSaveData
    {
        public int Charges = 2;
        public long ActiveUntilUnixSeconds;
        public long NextChargeAtUnixSeconds;

        public void Normalize()
        {
            if (Charges < 0) Charges = 0;

            if (ActiveUntilUnixSeconds < 0) ActiveUntilUnixSeconds = 0;
            if (NextChargeAtUnixSeconds < 0) NextChargeAtUnixSeconds = 0;
        }
    }
}
