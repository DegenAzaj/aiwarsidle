using System;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Config;

namespace AIWarsIdle.PvP.Services
{
    public sealed class PvpMapCombatService
    {
        private readonly GameState _state;
        private readonly MapService _map;
        private readonly MapConfig _mapConfig;
        private readonly PvpAttackChargesService _attacks;
        private readonly SnapshotService _snapshotService;
        private readonly MatchmakingService _matchmakingService;
        private readonly BattleSimService _battleSim;
        private readonly EconomyService _economy;
        private readonly LeagueService _league;
        private readonly IEventBus _eventBus;

        public PvpMapCombatService(
            GameState state,
            MapService map,
            MapConfig mapConfig,
            PvpAttackChargesService attacks,
            SnapshotService snapshotService,
            MatchmakingService matchmakingService,
            BattleSimService battleSim,
            EconomyService economy,
            LeagueService league,
            IEventBus eventBus = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _matchmakingService = matchmakingService ?? throw new ArgumentNullException(nameof(matchmakingService));
            _battleSim = battleSim ?? throw new ArgumentNullException(nameof(battleSim));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _league = league ?? throw new ArgumentNullException(nameof(league));
            _eventBus = eventBus;

            _mapConfig.ValidateOrThrow();
        }

        public AttackPreview GetAttackPreview(int sectorId, AttackStrategy strategy, long nowUnixSeconds)
        {
            return GetAttackPreview(sectorId, strategy, nowUnixSeconds, seedBase: ComputePreviewSeedBase(sectorId, strategy));
        }

        public AttackPreview GetAttackPreview(int sectorId, AttackStrategy strategy, long nowUnixSeconds, int seedBase)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var evaluation = EvaluateAttack(sectorId, nowUnixSeconds);
            if (!evaluation.CanAttack)
            {
                return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };
            }

            var sector = _map.GetSector(sectorId);
            if (sector == null) return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };

            var attacker = _snapshotService.BuildSnapshot();
            var defender = _matchmakingService.GetDefenderSnapshot(attacker, sector, seed: MixSeed(seedBase, 991), attackerPlayerId: _mapConfig.LocalPlayerId);
            var modifiers = BuildCombatModifiers(attackerPlayerId: _mapConfig.LocalPlayerId, defenderPlayerId: sector.OwnerPlayerId, targetSectorId: sector.SectorId);
            var previewBattle = _battleSim.Simulate(
                attacker.PvpPower,
                defender.PvpPower,
                strategy,
                sector.Stability,
                nowUnixSeconds,
                seedBase,
                flankBonus: modifiers.FlankBonus,
                defenseBonus: modifiers.DefenseBonus,
                maintenanceMultiplier: modifiers.MaintenanceMultiplier,
                underdogBonus: modifiers.UnderdogBonus);

            return new AttackPreview
            {
                SectorId = sectorId,
                Strategy = strategy,
                WinChanceMin = (float)previewBattle.WinChance,
                WinChanceMax = (float)previewBattle.WinChance
            };
        }

        public CombatResult AttackSector(int sectorId, AttackStrategy strategy, long nowUnixSeconds)
        {
            return AttackSector(sectorId, strategy, nowUnixSeconds, seed: ComputeAttackSeed(sectorId, strategy, nowUnixSeconds));
        }

        public CombatResult AttackSector(int sectorId, AttackStrategy strategy, long nowUnixSeconds, int seed)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var sector = _map.GetSector(sectorId);
            var evaluation = EvaluateAttack(sectorId, nowUnixSeconds);
            if (!evaluation.CanAttack)
            {
                return new CombatResult
                {
                    SectorId = sectorId,
                    Win = false,
                    Battle = new BattleResult(),
                    UpdatedSector = sector == null ? new SectorState { SectorId = sectorId } : CloneSector(sector)
                };
            }

            _eventBus?.Publish(new SectorAttackEvent(sectorId, strategy));

            var attacker = _snapshotService.BuildSnapshot();
            var defender = _matchmakingService.GetDefenderSnapshot(attacker, sector, seed: MixSeed(seed, 12345), attackerPlayerId: _mapConfig.LocalPlayerId);
            var modifiers = BuildCombatModifiers(attackerPlayerId: _mapConfig.LocalPlayerId, defenderPlayerId: sector.OwnerPlayerId, targetSectorId: sector.SectorId);
            var battle = _battleSim.Simulate(
                attacker.PvpPower,
                defender.PvpPower,
                strategy,
                sector.Stability,
                nowUnixSeconds,
                seed,
                flankBonus: modifiers.FlankBonus,
                defenseBonus: modifiers.DefenseBonus,
                maintenanceMultiplier: modifiers.MaintenanceMultiplier,
                underdogBonus: modifiers.UnderdogBonus);

            var win = battle.Win;

            _attacks.TrySpendOne(nowUnixSeconds);

            sector.LastCombatUnixSeconds = nowUnixSeconds;

            if (win)
            {
                var previousOwner = sector.OwnerPlayerId;
                sector.OwnerPlayerId = _mapConfig.LocalPlayerId;
                sector.OwnerSnapshot = CloneSnapshot(attacker);
                sector.CapturedUnixSeconds = nowUnixSeconds;
                sector.Stability = _mapConfig.GetCaptureStabilityStart(strategy);

                if (previousOwner != sector.OwnerPlayerId)
                {
                    _eventBus?.Publish(new SectorOwnershipChangedEvent(sectorId, previousOwner, sector.OwnerPlayerId));
                }
            }
            else
            {
                var next = sector.Stability + _mapConfig.StabilityGainOnDefenseWin;
                if (next > 100f) next = 100f;
                if (next < 0f) next = 0f;
                sector.Stability = next;
            }

            var points = win ? _mapConfig.WinSeasonPoints : _mapConfig.LoseSeasonPoints;
            _league.AddSeasonPoints(nowUnixSeconds, points);

            var reward = win ? _mapConfig.WinSoftReward : _mapConfig.LoseSoftReward;
            if (reward > 0)
            {
                _economy.AddCurrency(reward, CurrencySource.PvpReward);
            }

            battle.LeaguePointsDelta = points;
            battle.SoftReward = reward;

            _eventBus?.Publish(new SectorResultEvent(sectorId, win));

            return new CombatResult
            {
                SectorId = sectorId,
                Win = win,
                Battle = battle,
                UpdatedSector = CloneSector(sector)
            };
        }

        public PvpAttackEvaluation EvaluateAttack(int sectorId, long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (!_map.IsInitialized)
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.MapUninitialized,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (_map.NeedsSeasonReset(nowUnixSeconds))
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.SeasonResetPending,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            var sector = _map.GetSector(sectorId);
            if (sector == null)
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.SectorMissing,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (_map.IsHomeSector(sector.SectorId))
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.HomeSector,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (sector.OwnerPlayerId == _mapConfig.LocalPlayerId)
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.AlreadyOwned,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (_mapConfig.SectorDefinitions == null || _mapConfig.SectorDefinitions.Length == 0 || _mapConfig.Adjacency == null || _mapConfig.Adjacency.Length == 0)
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.MissingMapDefinitions,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (!_map.HasHomeConnectedOwnedNeighbor(sector.SectorId, _mapConfig.LocalPlayerId))
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.NoAdjacentOwnedSector,
                    RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds)
                };
            }

            if (_mapConfig.SectorAttackCooldownSeconds > 0 && sector.LastCombatUnixSeconds > 0)
            {
                var age = nowUnixSeconds - sector.LastCombatUnixSeconds;
                if (age >= 0 && age < _mapConfig.SectorAttackCooldownSeconds)
                {
                    var cooldownEndsAt = sector.LastCombatUnixSeconds + _mapConfig.SectorAttackCooldownSeconds;
                    return new PvpAttackEvaluation
                    {
                        CanAttack = false,
                        BlockReason = PvpAttackBlockReason.CooldownActive,
                        RemainingAttacks = _attacks.GetRemaining(nowUnixSeconds),
                        CooldownEndsAtUnixSeconds = cooldownEndsAt,
                        CooldownRemainingSeconds = cooldownEndsAt > nowUnixSeconds ? cooldownEndsAt - nowUnixSeconds : 0
                    };
                }
            }

            var remaining = _attacks.GetRemaining(nowUnixSeconds);
            if (remaining <= 0)
            {
                return new PvpAttackEvaluation
                {
                    CanAttack = false,
                    BlockReason = PvpAttackBlockReason.NoAttackCharges,
                    RemainingAttacks = remaining
                };
            }

            return new PvpAttackEvaluation
            {
                CanAttack = true,
                BlockReason = PvpAttackBlockReason.None,
                RemainingAttacks = remaining
            };
        }

        private int ComputePreviewSeedBase(int sectorId, AttackStrategy strategy)
        {
            // include MapSeasonId so previews shift on season reset, and include PvpPower rounded to keep seed stable-ish
            var attacker = _snapshotService.BuildSnapshot();
            var powerKey = (int)Math.Min(int.MaxValue, Math.Floor(attacker.PvpPower));
            return MixSeed(_map.GetState().MapSeasonId, sectorId, (int)strategy, powerKey);
        }

        private int ComputeAttackSeed(int sectorId, AttackStrategy strategy, long nowUnixSeconds)
        {
            unchecked
            {
                var t = (int)(nowUnixSeconds ^ (nowUnixSeconds >> 32));
                return MixSeed(_map.GetState().MapSeasonId, sectorId, (int)strategy, t);
            }
        }

        private static SectorState CloneSector(SectorState src)
        {
            var snapshot = src.OwnerSnapshot == null ? new PvpSnapshot() : CloneSnapshot(src.OwnerSnapshot);

            return new SectorState
            {
                SectorId = src.SectorId,
                OwnerPlayerId = src.OwnerPlayerId,
                OwnerSnapshot = snapshot,
                Stability = src.Stability,
                LastCombatUnixSeconds = src.LastCombatUnixSeconds,
                CapturedUnixSeconds = src.CapturedUnixSeconds
            };
        }

        private static PvpSnapshot CloneSnapshot(PvpSnapshot src)
        {
            return new PvpSnapshot { PvpPower = src.PvpPower, League = src.League, SeasonPoints = src.SeasonPoints };
        }

        private CombatModifiers BuildCombatModifiers(int attackerPlayerId, int defenderPlayerId, int targetSectorId)
        {
            return new CombatModifiers(
                ComputeFlankBonus(attackerPlayerId, targetSectorId),
                _battleSim.Config.DefenseBonus,
                ComputeMaintenanceMultiplier(attackerPlayerId),
                ComputeUnderdogBonus(attackerPlayerId, defenderPlayerId));
        }

        private double ComputeFlankBonus(int attackerPlayerId, int targetSectorId)
        {
            var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, attackerPlayerId);
            if (connectedOwned.Count == 0) return 1.0;

            var adjacentAttackers = 0;
            foreach (var ownedSectorId in connectedOwned)
            {
                if (_map.IsAdjacent(ownedSectorId, targetSectorId)) adjacentAttackers++;
            }

            var extraAttackers = Math.Max(0, adjacentAttackers - 1);
            var raw = 1.0 + (_battleSim.Config.FlankBonusPerExtraAttacker * extraAttackers);
            return Math.Min(_battleSim.Config.FlankBonusMaxMultiplier, raw);
        }

        private double ComputeMaintenanceMultiplier(int attackerPlayerId)
        {
            var connectedOwned = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, attackerPlayerId);
            var extra = Math.Max(0, connectedOwned.Count - _battleSim.Config.CombatMaintenanceFreeSectors);
            var penalty = extra * _battleSim.Config.CombatMaintenancePenaltyPerExtraSector;
            var multiplier = 1.0 - penalty;
            if (multiplier < _battleSim.Config.CombatMaintenanceMinMultiplier) multiplier = _battleSim.Config.CombatMaintenanceMinMultiplier;
            if (multiplier > 1.0) multiplier = 1.0;
            return multiplier;
        }

        private double ComputeUnderdogBonus(int attackerPlayerId, int defenderPlayerId)
        {
            if (defenderPlayerId <= 0 || defenderPlayerId == attackerPlayerId) return 1.0;

            var attackerCount = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, attackerPlayerId).Count;
            var defenderCount = MapConnectivityService.BuildHomeConnectedSectorSet(_state.MapState, _mapConfig, defenderPlayerId).Count;
            var deficit = defenderCount - attackerCount;
            if (deficit <= 0) return 1.0;

            var t = Math.Min(1.0, deficit / (double)_battleSim.Config.UnderdogSectorDeficitForMaxBonus);
            return 1.0 + (_battleSim.Config.UnderdogMaxAttackBonus * t);
        }

        private readonly struct CombatModifiers
        {
            public readonly double FlankBonus;
            public readonly double DefenseBonus;
            public readonly double MaintenanceMultiplier;
            public readonly double UnderdogBonus;

            public CombatModifiers(double flankBonus, double defenseBonus, double maintenanceMultiplier, double underdogBonus)
            {
                FlankBonus = flankBonus;
                DefenseBonus = defenseBonus;
                MaintenanceMultiplier = maintenanceMultiplier;
                UnderdogBonus = underdogBonus;
            }
        }

        private static int MixSeed(int a, int b, int c, int d)
        {
            unchecked
            {
                var h = 17;
                h = (h * 31) + a;
                h = (h * 31) + b;
                h = (h * 31) + c;
                h = (h * 31) + d;
                return h;
            }
        }

        private static int MixSeed(int seed, int salt)
        {
            unchecked
            {
                return (seed * 31) + salt;
            }
        }
    }
}
