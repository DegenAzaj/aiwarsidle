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
        private readonly SnapshotService _snapshotService;
        private readonly MatchmakingService _matchmakingService;
        private readonly BattleSimService _battleSim;
        private readonly EconomyService _economy;
        private readonly LeagueService _league;

        public PvpMapCombatService(
            GameState state,
            MapService map,
            MapConfig mapConfig,
            SnapshotService snapshotService,
            MatchmakingService matchmakingService,
            BattleSimService battleSim,
            EconomyService economy,
            LeagueService league)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _map = map ?? throw new ArgumentNullException(nameof(map));
            _mapConfig = mapConfig ?? throw new ArgumentNullException(nameof(mapConfig));
            _snapshotService = snapshotService ?? throw new ArgumentNullException(nameof(snapshotService));
            _matchmakingService = matchmakingService ?? throw new ArgumentNullException(nameof(matchmakingService));
            _battleSim = battleSim ?? throw new ArgumentNullException(nameof(battleSim));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _league = league ?? throw new ArgumentNullException(nameof(league));

            _mapConfig.ValidateOrThrow();
        }

        public AttackPreview GetAttackPreview(int sectorId, AttackStrategy strategy, long nowUnixSeconds)
        {
            return GetAttackPreview(sectorId, strategy, nowUnixSeconds, seedBase: ComputePreviewSeedBase(sectorId, strategy));
        }

        public AttackPreview GetAttackPreview(int sectorId, AttackStrategy strategy, long nowUnixSeconds, int seedBase)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (!_map.IsInitialized) return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };
            if (_map.NeedsSeasonReset(nowUnixSeconds)) return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };

            var sector = _map.GetSector(sectorId);
            if (sector == null)
            {
                return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };
            }

            if (!CanAttemptAttack(sector, nowUnixSeconds))
            {
                return new AttackPreview { SectorId = sectorId, Strategy = strategy, WinChanceMin = 0f, WinChanceMax = 0f };
            }

            var attacker = _snapshotService.BuildSnapshot();
            var defender = _matchmakingService.GetDefenderSnapshot(attacker, sector, seed: MixSeed(seedBase, 991));

            const int n = 48;
            var wins = 0;
            for (var i = 0; i < n; i++)
            {
                var seed = MixSeed(seedBase, i + 1);
                var r = _battleSim.Simulate(attacker.PvpPower, defender.PvpPower, strategy, sector.Stability, nowUnixSeconds, seed);
                if (r.Win) wins++;
            }

            var (min, max) = WilsonScoreInterval95(wins, n);

            return new AttackPreview
            {
                SectorId = sectorId,
                Strategy = strategy,
                WinChanceMin = min,
                WinChanceMax = max
            };
        }

        public CombatResult AttackSector(int sectorId, AttackStrategy strategy, long nowUnixSeconds)
        {
            return AttackSector(sectorId, strategy, nowUnixSeconds, seed: ComputeAttackSeed(sectorId, strategy, nowUnixSeconds));
        }

        public CombatResult AttackSector(int sectorId, AttackStrategy strategy, long nowUnixSeconds, int seed)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            if (!_map.IsInitialized)
            {
                return new CombatResult { SectorId = sectorId, Win = false, Battle = new BattleResult(), UpdatedSector = new SectorState { SectorId = sectorId } };
            }
            if (_map.NeedsSeasonReset(nowUnixSeconds))
            {
                return new CombatResult { SectorId = sectorId, Win = false, Battle = new BattleResult(), UpdatedSector = new SectorState { SectorId = sectorId } };
            }

            var sector = _map.GetSector(sectorId);
            if (sector == null)
            {
                return new CombatResult { SectorId = sectorId, Win = false, Battle = new BattleResult(), UpdatedSector = new SectorState { SectorId = sectorId } };
            }

            if (!CanAttemptAttack(sector, nowUnixSeconds))
            {
                return new CombatResult { SectorId = sectorId, Win = false, Battle = new BattleResult(), UpdatedSector = CloneSector(sector) };
            }

            if (_state.PvpAttacksRemaining <= 0)
            {
                return new CombatResult { SectorId = sectorId, Win = false, Battle = new BattleResult(), UpdatedSector = CloneSector(sector) };
            }

            var attacker = _snapshotService.BuildSnapshot();
            var defender = _matchmakingService.GetDefenderSnapshot(attacker, sector, seed: MixSeed(seed, 12345));

            var battle = _battleSim.Simulate(attacker.PvpPower, defender.PvpPower, strategy, sector.Stability, nowUnixSeconds, seed);

            var win = battle.Win;

            _state.PvpAttacksRemaining--;
            if (_state.PvpAttacksRemaining < 0) _state.PvpAttacksRemaining = 0;

            sector.LastCombatUnixSeconds = nowUnixSeconds;

            if (win)
            {
                sector.OwnerPlayerId = _mapConfig.LocalPlayerId;
                sector.OwnerSnapshot = CloneSnapshot(attacker);
                sector.CapturedUnixSeconds = nowUnixSeconds;
                sector.Stability = _mapConfig.GetCaptureStabilityStart(strategy);
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

            return new CombatResult
            {
                SectorId = sectorId,
                Win = win,
                Battle = battle,
                UpdatedSector = CloneSector(sector)
            };
        }

        private bool CanAttemptAttack(SectorState sector, long nowUnixSeconds)
        {
            if (sector == null) return false;
            if (sector.SectorId == _mapConfig.HomeSectorId) return false;
            if (sector.OwnerPlayerId == _mapConfig.LocalPlayerId) return false;

            if (_mapConfig.SectorDefinitions == null || _mapConfig.SectorDefinitions.Length == 0) return false;
            if (_mapConfig.Adjacency == null || _mapConfig.Adjacency.Length == 0) return false;

            if (!_map.HasOwnedNeighbor(sector.SectorId, _mapConfig.LocalPlayerId)) return false;

            if (_mapConfig.SectorAttackCooldownSeconds > 0 && sector.LastCombatUnixSeconds > 0)
            {
                var age = nowUnixSeconds - sector.LastCombatUnixSeconds;
                if (age >= 0 && age < _mapConfig.SectorAttackCooldownSeconds) return false;
            }

            return true;
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

        private static (float min, float max) WilsonScoreInterval95(int wins, int n)
        {
            if (n <= 0) return (0f, 0f);

            const double z = 1.96;
            var phat = wins / (double)n;

            var denom = 1.0 + ((z * z) / n);
            var center = (phat + ((z * z) / (2.0 * n))) / denom;
            var margin = (z * Math.Sqrt((phat * (1.0 - phat) / n) + ((z * z) / (4.0 * n * n)))) / denom;

            var lo = center - margin;
            var hi = center + margin;

            if (lo < 0) lo = 0;
            if (hi > 1) hi = 1;

            return ((float)lo, (float)hi);
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
