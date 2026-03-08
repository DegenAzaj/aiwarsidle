using System;
using System.Collections.Generic;
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
        private readonly Dictionary<int, HashSet<int>> _neighborsBySectorId;
        private long _lastTickUnixSeconds;

        public MapService(MapState state, MapConfig config, OverclockService overclock = null)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _overclock = overclock;

            _config.ValidateOrThrow();
            _state.Sectors ??= Array.Empty<SectorState>();
            _neighborsBySectorId = BuildNeighborMap(_config);
        }

        public MapState GetState()
        {
            return _state;
        }

        public SectorState GetSector(int sectorId)
        {
            if (sectorId < 0) throw new ArgumentOutOfRangeException(nameof(sectorId), "SectorId must be >= 0.");

            for (var i = 0; i < _state.Sectors.Length; i++)
            {
                var sector = _state.Sectors[i];
                if (sector == null) continue;
                if (sector.SectorId == sectorId) return sector;
            }

            return null;
        }

        public bool IsHomeSector(int sectorId)
        {
            return _config.IsHomeSector(sectorId);
        }

        public bool IsAdjacent(int fromSectorId, int toSectorId)
        {
            if (_neighborsBySectorId == null) return false;
            return _neighborsBySectorId.TryGetValue(fromSectorId, out var set) && set.Contains(toSectorId);
        }

        public bool HasOwnedNeighbor(int sectorId, int ownerPlayerId)
        {
            if (ownerPlayerId <= 0) return false;
            if (_neighborsBySectorId == null) return false;

            for (var i = 0; i < _state.Sectors.Length; i++)
            {
                var sector = _state.Sectors[i];
                if (sector == null) continue;
                if (sector.OwnerPlayerId != ownerPlayerId) continue;
                if (IsAdjacent(sector.SectorId, sectorId)) return true;
            }

            return false;
        }

        public bool HasHomeConnectedOwnedNeighbor(int sectorId, int ownerPlayerId)
        {
            if (ownerPlayerId <= 0) return false;
            if (_neighborsBySectorId == null) return false;

            var connectedOwnedSectors = MapConnectivityService.BuildHomeConnectedSectorSet(_state, _config, ownerPlayerId);
            if (connectedOwnedSectors.Count == 0) return false;

            foreach (var ownedSectorId in connectedOwnedSectors)
            {
                if (IsAdjacent(ownedSectorId, sectorId)) return true;
            }

            return false;
        }

        public bool IsInitialized => GetSector(_config.HomeSectorId)?.OwnerPlayerId == _config.LocalPlayerId;

        public bool NeedsSeasonReset(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (_config.SectorDefinitions == null || _config.SectorDefinitions.Length == 0) return false;
            var currentSeasonId = ComputeSeasonId(nowUnixSeconds);
            return _state.MapSeasonId != currentSeasonId;
        }

        public long GetCurrentSeasonEndsAtUnixSeconds(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");

            var seasonLenSeconds = (long)_config.MapSeasonLengthDays * 24L * 60L * 60L;
            if (seasonLenSeconds <= 0) throw new InvalidOperationException("Map season length must be > 0.");

            var anchor = _config.MapSeasonAnchorUnixSecondsUtc;
            var delta = nowUnixSeconds - anchor;
            if (delta < 0) return anchor + seasonLenSeconds;

            var seasonId = ComputeSeasonId(nowUnixSeconds);
            return anchor + ((seasonId + 1L) * seasonLenSeconds);
        }

        public void AdvanceTime(long nowUnixSeconds)
        {
            ResetMapSeasonIfNeeded(nowUnixSeconds);
            TickStability(nowUnixSeconds);
        }

        public void ResetMapSeasonIfNeeded(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (_config.SectorDefinitions == null || _config.SectorDefinitions.Length == 0) return;

            var currentSeasonId = ComputeSeasonId(nowUnixSeconds);
            EnsureSectorsMatchConfigIfPossible();

            var home = FindSectorById(_config.HomeSectorId);
            var needsInit = home == null || home.OwnerPlayerId != _config.LocalPlayerId || !AreAdditionalHomesInitialized();

            if (_state.MapSeasonId != currentSeasonId)
            {
                _state.MapSeasonId = currentSeasonId;
                ResetAllSectorsToSeasonStart(nowUnixSeconds);
                _lastTickUnixSeconds = 0;
                return;
            }

            if (needsInit)
            {
                ResetAllSectorsToSeasonStart(nowUnixSeconds);
                _lastTickUnixSeconds = 0;
                return;
            }

            EnsureHomeSectorInvariants(nowUnixSeconds);
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
                if (IsHomeSector(sector.SectorId)) continue;

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

        private int ComputeSeasonId(long nowUnixSeconds)
        {
            var seasonLenSeconds = (long)_config.MapSeasonLengthDays * 24L * 60L * 60L;
            if (seasonLenSeconds <= 0) throw new InvalidOperationException("Map season length must be > 0.");

            var anchor = _config.MapSeasonAnchorUnixSecondsUtc;
            var delta = nowUnixSeconds - anchor;
            if (delta <= 0) return 0;
            return (int)(delta / seasonLenSeconds);
        }

        private void ResetAllSectorsToSeasonStart(long nowUnixSeconds)
        {
            for (var i = 0; i < _state.Sectors.Length; i++)
            {
                var sector = _state.Sectors[i] ??= new SectorState();
                if (IsHomeSector(sector.SectorId))
                {
                    sector.OwnerPlayerId = _config.GetHomeOwnerPlayerId(sector.SectorId);
                    sector.OwnerSnapshot = new PvpSnapshot();
                    sector.Stability = _config.HomeSectorStability;
                    sector.LastCombatUnixSeconds = 0;
                    sector.CapturedUnixSeconds = nowUnixSeconds;
                    continue;
                }

                sector.OwnerPlayerId = 0;
                sector.OwnerSnapshot = new PvpSnapshot();
                sector.Stability = _config.StabilityStartNeutral;
                sector.LastCombatUnixSeconds = 0;
                sector.CapturedUnixSeconds = 0;
            }
        }

        private void EnsureHomeSectorInvariants(long nowUnixSeconds)
        {
            var homeIds = _config.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                var home = FindSectorById(homeIds[i]);
                if (home == null) continue;

                home.OwnerPlayerId = _config.GetHomeOwnerPlayerId(home.SectorId);
                home.Stability = _config.HomeSectorStability;
                if (home.CapturedUnixSeconds <= 0) home.CapturedUnixSeconds = nowUnixSeconds;
            }
        }

        private void EnsureSectorsMatchConfigIfPossible()
        {
            if (_config.SectorDefinitions == null || _config.SectorDefinitions.Length == 0) return;

            var existing = _state.Sectors ?? Array.Empty<SectorState>();
            var byId = new Dictionary<int, SectorState>();

            for (var i = 0; i < existing.Length; i++)
            {
                var sector = existing[i];
                if (sector == null) continue;
                if (sector.SectorId < 0) continue;
                if (!byId.ContainsKey(sector.SectorId)) byId.Add(sector.SectorId, sector);
            }

            var next = new SectorState[_config.SectorDefinitions.Length];
            for (var i = 0; i < _config.SectorDefinitions.Length; i++)
            {
                var def = _config.SectorDefinitions[i];
                if (def == null) continue;

                if (!byId.TryGetValue(def.SectorId, out var sector) || sector == null)
                {
                    sector = new SectorState { SectorId = def.SectorId };
                }

                sector.SectorId = def.SectorId;
                next[i] = sector;
            }

            _state.Sectors = next;
        }

        private SectorState FindSectorById(int sectorId)
        {
            var sectors = _state.Sectors;
            if (sectors == null || sectors.Length == 0) return null;

            for (var i = 0; i < sectors.Length; i++)
            {
                var s = sectors[i];
                if (s == null) continue;
                if (s.SectorId == sectorId) return s;
            }

            return null;
        }

        private bool AreAdditionalHomesInitialized()
        {
            var homeIds = _config.GetEffectiveHomeSectorIds();
            for (var i = 0; i < homeIds.Length; i++)
            {
                var sector = FindSectorById(homeIds[i]);
                if (sector == null) return false;
                if (sector.OwnerPlayerId != _config.GetHomeOwnerPlayerId(homeIds[i])) return false;
            }

            return true;
        }

        private static Dictionary<int, HashSet<int>> BuildNeighborMap(MapConfig config)
        {
            if (config.SectorDefinitions == null || config.SectorDefinitions.Length == 0) return null;

            var map = new Dictionary<int, HashSet<int>>();
            for (var i = 0; i < config.SectorDefinitions.Length; i++)
            {
                var def = config.SectorDefinitions[i];
                if (def == null) continue;
                map[def.SectorId] = new HashSet<int>();
            }

            if (config.Adjacency == null || config.Adjacency.Length == 0) return map;

            for (var i = 0; i < config.Adjacency.Length; i++)
            {
                var e = config.Adjacency[i];
                if (!map.TryGetValue(e.A, out var aSet)) continue;
                if (!map.TryGetValue(e.B, out var bSet)) continue;
                aSet.Add(e.B);
                bSet.Add(e.A);
            }

            return map;
        }
    }
}
