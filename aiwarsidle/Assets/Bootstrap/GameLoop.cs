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
        private readonly EventBus _eventBus;
        private readonly MapService _map;
        private readonly OverclockService _overclock;
        private readonly ProductionService _production;
        private readonly EconomyService _economy;
        private readonly UpgradeService _upgrades;
        private readonly PrestigeService _prestige;
        private readonly OfflineClaimService _offline;
        private readonly PvpAttackChargesService _pvpAttacks;
        private readonly LeagueService _league;
        private readonly SessionTelemetryService _sessionTelemetry;
        private readonly SubscriptionService _subscription;
        private readonly RewardedAdsUseCaseService _rewardedAds;

        private double _analyticsFlushCarry;
        private readonly double _analyticsFlushIntervalSeconds;
        private bool _isApplicationPaused;

        public GameState State { get; }
        public OfflineClaimService OfflineClaim => _offline;

        public long LastNowUnixSeconds { get; private set; }

        public IEventBus EventBus => _eventBus;
        public EconomyService Economy => _economy;
        public ProductionService Production => _production;
        public UpgradeService Upgrades => _upgrades;
        public PrestigeService Prestige => _prestige;
        public OverclockService Overclock => _overclock;
        public ISubscriptionService Subscription => _subscription;
        public RewardedAdsUseCaseService RewardedAds => _rewardedAds;
        public long NowUnixSeconds => LastNowUnixSeconds;

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

            _eventBus = new EventBus();

            var analyticsSink = new NullAnalyticsSink();
            _analytics = new BufferedAnalyticsService(analyticsSink);
            _analyticsBridge = new AnalyticsEventBusBridge(_eventBus, _analytics);

            State = SaveDataV1GameStateMapper.ToGameState(initialSave);

            _subscription = new SubscriptionService();
            _economy = new EconomyService(State, _eventBus);

            _overclock = new OverclockService(State, overclockConfig, _eventBus);

            var productionBonus = new MapProductionBonusProvider(State.MapState, mapConfig);
            _production = new ProductionService(
                State,
                balanceConfig,
                _economy,
                overclock: _overclock,
                permanentMultiplierProvider: productionBonus,
                subscription: _subscription);

            _upgrades = new UpgradeService(State, balanceConfig, _economy, _eventBus);
            _prestige = new PrestigeService(State, balanceConfig, _eventBus);

            _offline = new OfflineClaimService(State, balanceConfig, _production, _economy, _eventBus);

            _map = new MapService(State.MapState, mapConfig, _overclock);

            _pvpAttacks = pvpAttacksConfig == null ? null : new PvpAttackChargesService(State, pvpAttacksConfig);
            _league = leagueConfig == null ? null : new LeagueService(State, leagueConfig);
            _rewardedAds = new RewardedAdsUseCaseService(new AdsService(), _offline, _pvpAttacks, _eventBus);

            _sessionTelemetry = new SessionTelemetryService(_eventBus);

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
            var now = _time.NowUnixSeconds;
            LastNowUnixSeconds = now;

            if (isPaused)
            {
                _isApplicationPaused = true;
                _overclock.Tick(now);
                _pvpAttacks?.Tick(now);
                _league?.ResetSeasonIfNeeded(now);
                _map.AdvanceTime(now);
                _offline.MarkBackgrounded(now);
                _autosave.OnApplicationPause(isPaused);
                return;
            }

            if (!_isApplicationPaused)
            {
                _autosave.OnApplicationPause(isPaused);
                return;
            }

            _isApplicationPaused = false;
            _overclock.Tick(now);
            _pvpAttacks?.Tick(now);
            _league?.ResetSeasonIfNeeded(now);
            _map.AdvanceTime(now);
            _offline.BankOfflineGain(now);
            _autosave.OnApplicationPause(isPaused);
            _autosave.ForceSave();
        }

        public void OnApplicationQuit()
        {
            _autosave.OnApplicationQuit();
        }

        public void ForceSave()
        {
            _autosave.ForceSave();
        }

        public void Dispose()
        {
            _analyticsBridge?.Dispose();
        }
    }
}
