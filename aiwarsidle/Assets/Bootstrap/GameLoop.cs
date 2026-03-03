using System;
using AIWarsIdle.Analytics;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.GameCore.Domain;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.Monetization;
using AIWarsIdle.Persistence.Domain;
using AIWarsIdle.Persistence.Services;
using AIWarsIdle.PvP.Config;
using AIWarsIdle.PvP.Services;

namespace AIWarsIdle.Bootstrap
{
    public sealed class GameLoop : IDisposable
    {
        private readonly BootstrapTime _time;
        private readonly AutosaveRunner _autosave;
        private readonly BufferedAnalyticsService _analytics;
        private readonly AnalyticsEventBusBridge _analyticsBridge;
        private readonly MapService _map;
        private readonly OverclockService _overclock;
        private readonly ProductionService _production;
        private readonly OfflineClaimService _offline;
        private readonly PvpAttackChargesService _pvpAttacks;
        private readonly LeagueService _league;
        private readonly SessionTelemetryService _sessionTelemetry;

        private double _analyticsFlushCarry;
        private readonly double _analyticsFlushIntervalSeconds;

        public GameState State { get; }
        public OfflineClaimService OfflineClaim => _offline;

        public long LastNowUnixSeconds { get; private set; }

        public GameLoop(
            SaveService saveService,
            SaveDataV1 initialSave,
            BalanceConfig balanceConfig,
            OverclockConfig overclockConfig,
            MapConfig mapConfig,
            PvpAttacksConfig pvpAttacksConfig,
            LeagueConfig leagueConfig,
            float autosaveIntervalSeconds = 30f,
            float analyticsFlushIntervalSeconds = 2f)
        {
            if (saveService == null) throw new ArgumentNullException(nameof(saveService));
            if (initialSave == null) throw new ArgumentNullException(nameof(initialSave));
            if (balanceConfig == null) throw new ArgumentNullException(nameof(balanceConfig));
            if (overclockConfig == null) throw new ArgumentNullException(nameof(overclockConfig));
            if (mapConfig == null) throw new ArgumentNullException(nameof(mapConfig));

            if (autosaveIntervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(autosaveIntervalSeconds), "Autosave interval must be > 0.");
            }

            _analyticsFlushIntervalSeconds = analyticsFlushIntervalSeconds <= 0f ? 0 : analyticsFlushIntervalSeconds;

            _time = new BootstrapTime();

            var eventBus = new EventBus();

            var analyticsSink = new NullAnalyticsSink();
            _analytics = new BufferedAnalyticsService(analyticsSink);
            _analyticsBridge = new AnalyticsEventBusBridge(eventBus, _analytics);

            State = SaveDataV1GameStateMapper.ToGameState(initialSave);

            var subscription = new SubscriptionService();
            var economy = new EconomyService(State, eventBus);

            _overclock = new OverclockService(State, overclockConfig, eventBus);

            var productionBonus = new MapProductionBonusProvider(State.MapState, mapConfig);
            _production = new ProductionService(
                State,
                balanceConfig,
                economy,
                overclock: _overclock,
                permanentMultiplierProvider: productionBonus,
                subscription: subscription);

            _offline = new OfflineClaimService(State, balanceConfig, _production, economy, eventBus);

            _map = new MapService(State.MapState, mapConfig, _overclock);

            _pvpAttacks = pvpAttacksConfig == null ? null : new PvpAttackChargesService(State, pvpAttacksConfig);
            _league = leagueConfig == null ? null : new LeagueService(State, leagueConfig);

            _sessionTelemetry = new SessionTelemetryService(eventBus);

            _autosave = new AutosaveRunner(
                saveService,
                getCurrentSaveData: () => SaveDataV1GameStateMapper.ToSaveDataV1(State),
                intervalSeconds: autosaveIntervalSeconds);

            InitializeSession();
        }

        private void InitializeSession()
        {
            var now = _time.NowUnixSeconds;
            LastNowUnixSeconds = now;

            _overclock.Tick(now);
            _pvpAttacks?.Tick(now);
            _league?.ResetSeasonIfNeeded(now);

            _map.AdvanceTime(now);

            _offline.BankOfflineGain(now);

            _sessionTelemetry.TrackSessionStart(now);

            _autosave.ForceSave();
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            var now = _time.NowUnixSeconds;
            LastNowUnixSeconds = now;

            _overclock.Tick(now);
            _pvpAttacks?.Tick(now);
            _league?.ResetSeasonIfNeeded(now);
            _map.AdvanceTime(now);

            _production.Tick(now, deltaSeconds);

            _autosave.Tick(deltaSeconds);

            if (_analyticsFlushIntervalSeconds > 0)
            {
                _analyticsFlushCarry += deltaSeconds;
                if (_analyticsFlushCarry >= _analyticsFlushIntervalSeconds)
                {
                    _analyticsFlushCarry = 0;
                    _analytics.Flush(maxEvents: 256);
                }
            }
        }

        public void OnApplicationPause(bool isPaused)
        {
            _autosave.OnApplicationPause(isPaused);
        }

        public void OnApplicationQuit()
        {
            _autosave.OnApplicationQuit();
        }

        public void Dispose()
        {
            _analyticsBridge?.Dispose();
        }
    }
}
