using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.Persistence.Domain;

namespace AIWarsIdle.Persistence.Services
{
    public static class SaveDataV1GameStateMapper
    {
        public static GameState ToGameState(SaveDataV1 save)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            save.Normalize();

            var state = new GameState();
            ApplyToGameState(save, state);
            return state;
        }

        public static void ApplyToGameState(SaveDataV1 save, GameState state)
        {
            if (save == null) throw new ArgumentNullException(nameof(save));
            if (state == null) throw new ArgumentNullException(nameof(state));

            save.Normalize();

            state.SoftCurrency = save.SoftCurrency;
            state.LifetimeEarnedSoftCurrency = save.LifetimeEarnedSoftCurrency;
            state.LifetimeEarnedSoftCurrencyAtLastPrestige = save.LifetimeEarnedSoftCurrencyAtLastPrestige;
            state.PremiumCurrency = save.PremiumCurrency;

            if (state.GeneratorLevels == null || state.GeneratorLevels.Length != GameState.GeneratorCount)
            {
                state.GeneratorLevels = new int[GameState.GeneratorCount];
            }

            if (save.GeneratorLevels != null)
            {
                Array.Copy(save.GeneratorLevels, state.GeneratorLevels, Math.Min(save.GeneratorLevels.Length, state.GeneratorLevels.Length));
            }

            state.PrestigeCount = save.PrestigeCount;
            state.PermanentUpgradeLevel = save.PermanentUpgradeLevel;

            state.LeagueSeasonId = save.LeagueSeasonId;
            state.League = save.League;
            state.SeasonPoints = save.SeasonPoints;

            state.PvpAttacksRemaining = save.PvpAttacksRemaining;
            state.LastPvpAttackRegenUnixSeconds = save.LastPvpAttackRegenUnixSeconds;
            state.NextPvpAttackRegenAtUnixSeconds = save.NextPvpAttackRegenAtUnixSeconds;
            state.LastPvpAdAttackClaimUnixSeconds = save.LastPvpAdAttackClaimUnixSeconds;

            state.LastLoginUnixSeconds = save.LastLoginUnixSeconds;
            state.PendingOfflineGain = save.PendingOfflineGain;
            state.LastBankedOfflineRawSeconds = save.LastBankedOfflineRawSeconds;
            state.LastBankedOfflineEffectiveSeconds = save.LastBankedOfflineEffectiveSeconds;

            state.MapState ??= new MapState();
            state.MapState.MapSeasonId = save.MapSeasonId;

            var sectors = save.Sectors ?? Array.Empty<SectorSaveData>();
            var mappedSectors = new SectorState[sectors.Length];

            for (var i = 0; i < sectors.Length; i++)
            {
                var s = sectors[i] ?? new SectorSaveData();
                s.Normalize();

                var sectorState = new SectorState
                {
                    SectorId = s.SectorId,
                    OwnerPlayerId = s.OwnerPlayerId,
                    Stability = s.Stability,
                    LastCombatUnixSeconds = s.LastCombatUnixSeconds,
                    CapturedUnixSeconds = s.CapturedUnixSeconds,
                    OwnerSnapshot = new PvpSnapshot
                    {
                        PvpPower = s.OwnerPvpPower,
                        League = s.OwnerLeague,
                        SeasonPoints = s.OwnerSeasonPoints
                    }
                };

                mappedSectors[i] = sectorState;
            }

            state.MapState.Sectors = mappedSectors;

            state.Overclock ??= new OverclockState();
            state.Overclock.Charges = save.Overclock?.Charges ?? 0;
            state.Overclock.ActiveUntilUnixSeconds = save.Overclock?.ActiveUntilUnixSeconds ?? 0;
            state.Overclock.NextChargeAtUnixSeconds = save.Overclock?.NextChargeAtUnixSeconds ?? 0;
        }

        public static SaveDataV1 ToSaveDataV1(GameState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));

            var save = new SaveDataV1();
            ApplyToSaveDataV1(state, save);
            save.Normalize();
            return save;
        }

        public static void ApplyToSaveDataV1(GameState state, SaveDataV1 save)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (save == null) throw new ArgumentNullException(nameof(save));

            save.Version = 1;

            save.SoftCurrency = state.SoftCurrency;
            save.LifetimeEarnedSoftCurrency = state.LifetimeEarnedSoftCurrency;
            save.LifetimeEarnedSoftCurrencyAtLastPrestige = state.LifetimeEarnedSoftCurrencyAtLastPrestige;
            save.PremiumCurrency = state.PremiumCurrency;

            if (save.GeneratorLevels == null || save.GeneratorLevels.Length != GameState.GeneratorCount)
            {
                save.GeneratorLevels = new int[GameState.GeneratorCount];
            }

            if (state.GeneratorLevels != null)
            {
                Array.Copy(state.GeneratorLevels, save.GeneratorLevels, Math.Min(state.GeneratorLevels.Length, save.GeneratorLevels.Length));
            }

            save.PrestigeCount = state.PrestigeCount;
            save.PermanentUpgradeLevel = state.PermanentUpgradeLevel;

            save.LeagueSeasonId = state.LeagueSeasonId;
            save.League = state.League;
            save.SeasonPoints = state.SeasonPoints;

            save.PvpAttacksRemaining = state.PvpAttacksRemaining;
            save.LastPvpAttackRegenUnixSeconds = state.LastPvpAttackRegenUnixSeconds;
            save.NextPvpAttackRegenAtUnixSeconds = state.NextPvpAttackRegenAtUnixSeconds;
            save.LastPvpAdAttackClaimUnixSeconds = state.LastPvpAdAttackClaimUnixSeconds;

            save.LastLoginUnixSeconds = state.LastLoginUnixSeconds;
            save.PendingOfflineGain = state.PendingOfflineGain;
            save.LastBankedOfflineRawSeconds = state.LastBankedOfflineRawSeconds;
            save.LastBankedOfflineEffectiveSeconds = state.LastBankedOfflineEffectiveSeconds;

            save.MapSeasonId = state.MapState?.MapSeasonId ?? 0;

            var sectors = state.MapState?.Sectors ?? Array.Empty<SectorState>();
            save.Sectors = new SectorSaveData[sectors.Length];

            for (var i = 0; i < sectors.Length; i++)
            {
                var s = sectors[i] ?? new SectorState();
                var snap = s.OwnerSnapshot ?? new PvpSnapshot();

                save.Sectors[i] = new SectorSaveData
                {
                    SectorId = s.SectorId,
                    OwnerPlayerId = s.OwnerPlayerId,
                    OwnerPvpPower = snap.PvpPower,
                    OwnerLeague = snap.League,
                    OwnerSeasonPoints = snap.SeasonPoints,
                    Stability = s.Stability,
                    LastCombatUnixSeconds = s.LastCombatUnixSeconds,
                    CapturedUnixSeconds = s.CapturedUnixSeconds
                };
            }

            save.Overclock ??= new OverclockSaveData();
            save.Overclock.Charges = state.Overclock?.Charges ?? 0;
            save.Overclock.ActiveUntilUnixSeconds = state.Overclock?.ActiveUntilUnixSeconds ?? 0;
            save.Overclock.NextChargeAtUnixSeconds = state.Overclock?.NextChargeAtUnixSeconds ?? 0;
        }
    }
}
