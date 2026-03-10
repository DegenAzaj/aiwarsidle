using System;
using System.Collections.Generic;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class FactionSnapshotService
    {
        private const int VirtualSimulationStepSeconds = 15 * 60;
        private const int MaxUpgradePurchasesPerStep = 128;

        private readonly GameState _state;
        private readonly MapConfig _mapConfig;
        private readonly PvpConfig _pvpConfig;
        private readonly BalanceConfig _balanceConfig;
        private readonly SnapshotService _localSnapshotService;

        public FactionSnapshotService(
            GameState state,
            MapConfig mapConfig,
            PvpConfig pvpConfig,
            SnapshotService localSnapshotService = null,
            BalanceConfig balanceConfig = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _pvpConfig = pvpConfig ?? throw new ArgumentNullException(nameof(pvpConfig));
            _localSnapshotService = localSnapshotService;
            _balanceConfig = balanceConfig;

            _mapConfig.ValidateOrThrow();
            _pvpConfig.ValidateOrThrow();
            _balanceConfig?.ValidateOrThrow();

            EnsureMatchStartAnchorsCaptured();
        }

        public PvpSnapshot BuildCurrentSnapshot(int playerId)
        {
            if (playerId <= 0) return new PvpSnapshot();

            if (playerId == _mapConfig.LocalPlayerId && _localSnapshotService != null)
            {
                return CloneSnapshot(_localSnapshotService.BuildSnapshot());
            }

            return BuildBotSnapshot(playerId);
        }

        private PvpSnapshot BuildBotSnapshot(int playerId)
        {
            if (_balanceConfig == null)
            {
                return BuildFallbackBotSnapshot(playerId);
            }

            var virtualState = CreateVirtualBotState();
            var elapsedSeconds = ResolveElapsedSeconds();
            if (elapsedSeconds > 0)
            {
                SimulateVirtualEconomy(virtualState, playerId, elapsedSeconds);
            }

            var production = CreateVirtualProductionService(virtualState, playerId, blendTowardsCurrentMapBonus: 1.0);
            var snapshotService = new SnapshotService(virtualState, _pvpConfig, production, playerId, _mapConfig);
            var snapshot = snapshotService.BuildSnapshot();
            snapshot.PvpPower = Math.Max(0, snapshot.PvpPower + GetOpeningPowerOffset(playerId));
            snapshot.League = 0;
            snapshot.SeasonPoints = 0;
            return snapshot;
        }

        private PvpSnapshot BuildFallbackBotSnapshot(int playerId)
        {
            var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, playerId);
            var basePower = _state.MapState?.MatchStartLocalPvpPower ?? 0;
            if (basePower <= 0 && _localSnapshotService != null)
            {
                basePower = _localSnapshotService.BuildSnapshot()?.PvpPower ?? 0;
            }

            basePower = SanitizeNonNegative(basePower);
            basePower += GetOpeningPowerOffset(playerId);
            basePower += Math.Max(0, connectedOwned.Count - 1) * _pvpConfig.SectorPvpPowerPerSector;

            return new PvpSnapshot
            {
                PvpPower = basePower,
                League = 0,
                SeasonPoints = 0
            };
        }

        private void EnsureMatchStartAnchorsCaptured()
        {
            _state.MapState ??= new MapState();
            _state.MapState.MatchStartLocalGeneratorLevels ??= new int[GameState.GeneratorCount];

            var hasAnchor =
                _state.MapState.MatchStartLocalPvpPower > 0 ||
                _state.MapState.MatchStartLocalBasePps > 0 ||
                _state.MapState.MatchStartLocalPrestigeCount > 0 ||
                _state.MapState.MatchStartLocalPermanentUpgradeLevel > 0;

            if (hasAnchor || _localSnapshotService == null) return;

            var anchor = _localSnapshotService.BuildMatchStartAnchor();
            _state.MapState.MatchStartLocalPvpPower = SanitizeNonNegative(anchor.PvpPower);
            _state.MapState.MatchStartLocalBasePps = SanitizeNonNegative(anchor.BasePps);
            _state.MapState.MatchStartLocalSoftCurrency = SanitizeNonNegative(anchor.SoftCurrency);
            _state.MapState.MatchStartLocalLifetimeEarnedSoftCurrency = SanitizeNonNegative(anchor.LifetimeEarnedSoftCurrency);
            _state.MapState.MatchStartLocalLifetimeEarnedSoftCurrencyAtLastPrestige = SanitizeNonNegative(anchor.LifetimeEarnedSoftCurrencyAtLastPrestige);
            _state.MapState.MatchStartLocalPrestigeCount = Math.Max(0, anchor.PrestigeCount);
            _state.MapState.MatchStartLocalPermanentUpgradeLevel = Math.Max(0, anchor.PermanentUpgradeLevel);
            Array.Copy(anchor.GeneratorLevels, _state.MapState.MatchStartLocalGeneratorLevels, Math.Min(anchor.GeneratorLevels.Length, _state.MapState.MatchStartLocalGeneratorLevels.Length));

            if (_state.MapState.MatchStartUnixSeconds < 0) _state.MapState.MatchStartUnixSeconds = 0;
        }

        private GameState CreateVirtualBotState()
        {
            var anchorLevels = _state.MapState?.MatchStartLocalGeneratorLevels;
            var virtualState = new GameState
            {
                SoftCurrency = ResolveAnchorSoftCurrency(),
                LifetimeEarnedSoftCurrency = ResolveAnchorLifetimeEarned(),
                LifetimeEarnedSoftCurrencyAtLastPrestige = ResolveAnchorLifetimeAtLastPrestige(),
                PrestigeCount = ResolveAnchorPrestigeCount(),
                PermanentUpgradeLevel = ResolveAnchorPermanentLevel()
            };

            if (anchorLevels != null)
            {
                Array.Copy(anchorLevels, virtualState.GeneratorLevels, Math.Min(anchorLevels.Length, virtualState.GeneratorLevels.Length));
            }

            virtualState.MapState.MapSeasonId = _state.MapState?.MapSeasonId ?? 0;
            virtualState.MapState.MatchStartUnixSeconds = _state.MapState?.MatchStartUnixSeconds ?? 0;
            virtualState.MapState.CurrentUnixSeconds = _state.MapState?.CurrentUnixSeconds ?? 0;
            virtualState.MapState.Sectors = _state.MapState?.Sectors ?? Array.Empty<SectorState>();
            return virtualState;
        }

        private void SimulateVirtualEconomy(GameState virtualState, int playerId, long elapsedSeconds)
        {
            var economy = new EconomyService(virtualState);
            var production = CreateVirtualProductionService(virtualState, playerId, blendTowardsCurrentMapBonus: 0.5);
            var upgrades = new UpgradeService(virtualState, _balanceConfig, economy);
            var prestige = new PrestigeService(virtualState, _balanceConfig);
            var tempoMultiplier = GetEconomyTempoMultiplier(playerId);

            var remaining = elapsedSeconds;
            while (remaining > 0)
            {
                var stepSeconds = (int)Math.Min(VirtualSimulationStepSeconds, remaining);
                remaining -= stepSeconds;

                var pps = production.CalculateBaseProductionPerSecondWithoutSubscription();
                if (pps > 0)
                {
                    economy.AddCurrency((pps * stepSeconds) * tempoMultiplier, CurrencySource.OnlineProduction);
                }

                if (prestige.CanPrestige())
                {
                    prestige.ExecutePrestige();
                    continue;
                }

                SpendAvailableCurrencyOnBestUpgrades(virtualState, production, upgrades);
            }

            if (prestige.CanPrestige())
            {
                prestige.ExecutePrestige();
            }
        }

        private ProductionService CreateVirtualProductionService(GameState virtualState, int playerId, double blendTowardsCurrentMapBonus)
        {
            var economy = new EconomyService(virtualState);
            var mapBonus = new BotMapProductionBonusProvider(_state.MapState, _mapConfig, playerId, blendTowardsCurrentMapBonus);
            return new ProductionService(virtualState, _balanceConfig, economy, permanentMultiplierProvider: mapBonus);
        }

        private void SpendAvailableCurrencyOnBestUpgrades(GameState virtualState, ProductionService production, UpgradeService upgrades)
        {
            for (var purchase = 0; purchase < MaxUpgradePurchasesPerStep; purchase++)
            {
                var bestGeneratorId = -1;
                var bestScore = 0.0;

                for (var generatorId = 0; generatorId < GameState.GeneratorCount; generatorId++)
                {
                    var cost = upgrades.GetUpgradeCost(generatorId);
                    if (cost <= 0 || cost > virtualState.SoftCurrency) continue;

                    var score = EstimateUpgradeScore(virtualState, production, generatorId, cost);
                    if (score <= bestScore) continue;

                    bestScore = score;
                    bestGeneratorId = generatorId;
                }

                if (bestGeneratorId < 0) break;
                upgrades.UpgradeGenerator(bestGeneratorId);
            }
        }

        private static double EstimateUpgradeScore(GameState state, ProductionService production, int generatorId, double cost)
        {
            var level = state.GeneratorLevels[generatorId];
            var before = production.CalculateGeneratorProductionPerSecond(generatorId);

            state.GeneratorLevels[generatorId] = level + 1;
            var after = production.CalculateGeneratorProductionPerSecond(generatorId);
            state.GeneratorLevels[generatorId] = level;

            var delta = after - before;
            if (delta <= 0 || cost <= 0) return 0;
            return delta / cost;
        }

        private long ResolveElapsedSeconds()
        {
            var current = _state.MapState?.CurrentUnixSeconds ?? 0;
            var start = _state.MapState?.MatchStartUnixSeconds ?? 0;
            if (current <= 0 || start <= 0 || current <= start) return 0;
            return current - start;
        }

        private double ResolveAnchorSoftCurrency()
        {
            var soft = _state.MapState?.MatchStartLocalSoftCurrency ?? 0;
            return SanitizeNonNegative(soft);
        }

        private double ResolveAnchorLifetimeEarned()
        {
            var lifetime = _state.MapState?.MatchStartLocalLifetimeEarnedSoftCurrency ?? 0;
            return SanitizeNonNegative(lifetime);
        }

        private double ResolveAnchorLifetimeAtLastPrestige()
        {
            var baseline = _state.MapState?.MatchStartLocalLifetimeEarnedSoftCurrencyAtLastPrestige ?? 0;
            return SanitizeNonNegative(baseline);
        }

        private int ResolveAnchorPrestigeCount()
        {
            return Math.Max(0, _state.MapState?.MatchStartLocalPrestigeCount ?? 0);
        }

        private int ResolveAnchorPermanentLevel()
        {
            return Math.Max(0, _state.MapState?.MatchStartLocalPermanentUpgradeLevel ?? 0);
        }

        private double GetEconomyTempoMultiplier(int playerId)
        {
            var raw = GetProfileMultiplier(playerId) * GetBotConfigMultiplier(playerId);
            return Math.Clamp(raw, 0.9, 1.1);
        }

        private double GetOpeningPowerMultiplier(int playerId)
        {
            var bucket = Math.Abs((playerId * 193) + ((_state.MapState?.MapSeasonId ?? 0) * 17)) % 1000;
            var t = bucket / 999.0;
            var spread = -0.03 + (0.06 * t);
            return 1.0 + spread;
        }

        private double GetOpeningPowerOffset(int playerId)
        {
            var anchorPower = _state.MapState?.MatchStartLocalPvpPower ?? 0;
            anchorPower = SanitizeNonNegative(anchorPower);
            if (anchorPower <= 0) return 0;
            return anchorPower * (GetOpeningPowerMultiplier(playerId) - 1.0);
        }

        private double GetProfileMultiplier(int playerId)
        {
            return (Math.Abs(playerId) % 3) switch
            {
                0 => 1.02,
                1 => 0.98,
                _ => 1.00
            };
        }

        private double GetBotConfigMultiplier(int playerId)
        {
            var min = _mapConfig.BotPowerMinMultiplier;
            var max = _mapConfig.BotPowerMaxMultiplier;
            if (min <= 0f || max <= 0f || min > max) return 1.0;

            var bucket = Math.Abs((playerId * 97) + (_state.MapState?.MapSeasonId ?? 0)) % 1000;
            var t = bucket / 999.0;
            return min + ((max - min) * t);
        }

        private static double SanitizeNonNegative(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0) return 0;
            return value;
        }

        private static PvpSnapshot CloneSnapshot(PvpSnapshot src)
        {
            return src == null
                ? new PvpSnapshot()
                : new PvpSnapshot
                {
                    PvpPower = src.PvpPower,
                    League = src.League,
                    SeasonPoints = src.SeasonPoints
                };
        }

        private sealed class BotMapProductionBonusProvider : IPermanentProductionMultiplierProvider
        {
            private readonly MapState _mapState;
            private readonly MapConfig _mapConfig;
            private readonly int _playerId;
            private readonly double _blendTowardsCurrentMapBonus;

            public BotMapProductionBonusProvider(MapState mapState, MapConfig mapConfig, int playerId, double blendTowardsCurrentMapBonus)
            {
                _mapState = mapState ?? throw new ArgumentNullException(nameof(mapState));
                _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
                _playerId = playerId;
                _blendTowardsCurrentMapBonus = Math.Clamp(blendTowardsCurrentMapBonus, 0.0, 1.0);
            }

            public double GetPermanentMultiplier()
            {
                var current = ComputeCurrentMultiplier();
                if (_blendTowardsCurrentMapBonus >= 1.0) return current;
                return 1.0 + ((current - 1.0) * _blendTowardsCurrentMapBonus);
            }

            private double ComputeCurrentMultiplier()
            {
                var sectors = _mapState.Sectors;
                if (sectors == null || sectors.Length == 0) return 1.0;

                var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_mapState, _mapConfig, _playerId);
                if (connectedOwned.Count == 0) return 1.0;

                var ownedCount = 0;
                var rawBonusPercent = 0.0;

                for (var i = 0; i < sectors.Length; i++)
                {
                    var sector = sectors[i];
                    if (sector == null) continue;
                    if (sector.OwnerPlayerId != _playerId) continue;
                    if (!connectedOwned.Contains(sector.SectorId)) continue;

                    ownedCount++;
                    rawBonusPercent += ResolveProductionBonusPercent(sector.SectorId);
                }

                if (ownedCount <= 0) return 1.0;

                var tierMult = GetDiminishingMultiplier(ownedCount);
                var bonusPercent = rawBonusPercent * tierMult;
                if (_mapConfig.SectorBonusCapPercent > 0f && bonusPercent > _mapConfig.SectorBonusCapPercent)
                {
                    bonusPercent = _mapConfig.SectorBonusCapPercent;
                }

                var bonusMultiplier = 1.0 + (bonusPercent / 100.0);

                var extra = Math.Max(0, ownedCount - _mapConfig.MaintenanceFreeSectors);
                var maintenancePenaltyPercent = extra * _mapConfig.MaintenancePenaltyPercentPerExtraSector;
                var maintenanceMultiplier = 1.0 - (maintenancePenaltyPercent / 100.0);
                if (maintenanceMultiplier < _mapConfig.MaintenanceMinMultiplier) maintenanceMultiplier = _mapConfig.MaintenanceMinMultiplier;
                if (maintenanceMultiplier > 1.0) maintenanceMultiplier = 1.0;

                var total = bonusMultiplier * maintenanceMultiplier;
                if (double.IsNaN(total) || double.IsInfinity(total) || total <= 0) return 1.0;
                return total;
            }

            private double ResolveProductionBonusPercent(int sectorId)
            {
                var defs = _mapConfig.SectorDefinitions ?? Array.Empty<MapConfig.SectorDefinition>();
                for (var i = 0; i < defs.Length; i++)
                {
                    var def = defs[i];
                    if (def == null) continue;
                    if (def.SectorId == sectorId) return def.ProductionBonusPercent;
                }

                return 0.0;
            }

            private double GetDiminishingMultiplier(int ownedSectorCount)
            {
                var tiers = _mapConfig.ProductionBonusDiminishingTiers;
                if (tiers == null || tiers.Length == 0) return 1.0;

                for (var i = 0; i < tiers.Length; i++)
                {
                    var tier = tiers[i];
                    if (tier == null) continue;
                    if (ownedSectorCount <= tier.MaxOwnedSectors) return tier.BonusMultiplier;
                }

                for (var i = tiers.Length - 1; i >= 0; i--)
                {
                    var tier = tiers[i];
                    if (tier == null) continue;
                    return tier.BonusMultiplier;
                }

                return 1.0;
            }
        }
    }
}
