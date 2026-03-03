using System;
using System.Collections.Generic;
using System.Globalization;
using AIWarsIdle.GameCore.Services;
using AIWarsIdle.PvP.Services;

namespace AIWarsIdle.Analytics
{
    public sealed class AnalyticsEventBusBridge : IDisposable
    {
        private readonly IAnalyticsService _analytics;
        private readonly List<IDisposable> _subscriptions = new();
        private bool _disposed;

        public AnalyticsEventBusBridge(IEventBus eventBus, IAnalyticsService analytics)
        {
            if (eventBus == null) throw new ArgumentNullException(nameof(eventBus));
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));

            _subscriptions.Add(eventBus.Subscribe<OverclockActivatedEvent>(OnOverclockActivated));
            _subscriptions.Add(eventBus.Subscribe<OverclockChargeGainedEvent>(OnOverclockChargeGained));
            _subscriptions.Add(eventBus.Subscribe<OverclockChargeSpentEvent>(OnOverclockChargeSpent));

            _subscriptions.Add(eventBus.Subscribe<OfflineClaimedEvent>(OnOfflineClaimed));
            _subscriptions.Add(eventBus.Subscribe<GeneratorUpgradedEvent>(OnGeneratorUpgraded));
            _subscriptions.Add(eventBus.Subscribe<PrestigeExecutedEvent>(OnPrestigeExecuted));
            _subscriptions.Add(eventBus.Subscribe<AdWatchedEvent>(OnAdWatched));
            _subscriptions.Add(eventBus.Subscribe<SubscriptionStartedEvent>(OnSubscriptionStarted));
            _subscriptions.Add(eventBus.Subscribe<SessionStartedEvent>(OnSessionStarted));

            _subscriptions.Add(eventBus.Subscribe<MapOpenedEvent>(OnMapOpened));
            _subscriptions.Add(eventBus.Subscribe<SectorViewedEvent>(OnSectorViewed));
            _subscriptions.Add(eventBus.Subscribe<SectorAttackEvent>(OnSectorAttack));
            _subscriptions.Add(eventBus.Subscribe<SectorResultEvent>(OnSectorResult));
            _subscriptions.Add(eventBus.Subscribe<SectorOwnershipChangedEvent>(OnSectorOwnershipChanged));
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            for (var i = 0; i < _subscriptions.Count; i++)
            {
                try { _subscriptions[i]?.Dispose(); }
                catch { }
            }

            _subscriptions.Clear();
        }

        private void OnOverclockActivated(OverclockActivatedEvent evt)
        {
            SafeTrack(
                "overclock_activate",
                P("now", evt.NowUnixSeconds),
                P("activeUntil", evt.ActiveUntilUnixSeconds),
                P("chargesRemaining", evt.ChargesRemaining));
        }

        private void OnOverclockChargeGained(OverclockChargeGainedEvent evt)
        {
            SafeTrack(
                "overclock_charge_gain",
                P("now", evt.NowUnixSeconds),
                P("charges", evt.Charges));
        }

        private void OnOverclockChargeSpent(OverclockChargeSpentEvent evt)
        {
            SafeTrack(
                "overclock_charge_spent",
                P("now", evt.NowUnixSeconds),
                P("chargesRemaining", evt.ChargesRemaining));
        }

        private void OnOfflineClaimed(OfflineClaimedEvent evt)
        {
            SafeTrack(
                "offline_claim",
                P("baseAmount", evt.BaseAmount),
                P("multiplier", evt.Multiplier),
                P("amount", evt.FinalAmount));
        }

        private void OnGeneratorUpgraded(GeneratorUpgradedEvent evt)
        {
            SafeTrack(
                "generator_upgrade",
                P("id", evt.GeneratorId),
                P("level", evt.NewLevel),
                P("count", evt.UpgradeCount));
        }

        private void OnPrestigeExecuted(PrestigeExecutedEvent evt)
        {
            SafeTrack(
                "prestige",
                P("count", evt.NewPrestigeCount),
                P("permanentLevel", evt.NewPermanentUpgradeLevel));
        }

        private void OnAdWatched(AdWatchedEvent evt)
        {
            var placement = string.IsNullOrWhiteSpace(evt.Placement) ? "unknown" : evt.Placement;
            SafeTrack("ad_watched", new AnalyticsParam("placement", placement));
        }

        private void OnSubscriptionStarted(SubscriptionStartedEvent evt)
        {
            var source = string.IsNullOrWhiteSpace(evt.Source) ? "unknown" : evt.Source;
            SafeTrack("subscription_started", new AnalyticsParam("source", source));
        }

        private void OnSessionStarted(SessionStartedEvent evt)
        {
            SafeTrack("session_start", P("now", evt.NowUnixSeconds));
        }

        private void OnMapOpened(MapOpenedEvent evt)
        {
            SafeTrack("map_open", P("now", evt.NowUnixSeconds));
        }

        private void OnSectorViewed(SectorViewedEvent evt)
        {
            SafeTrack("sector_view", P("sectorId", evt.SectorId));
        }

        private void OnSectorAttack(SectorAttackEvent evt)
        {
            SafeTrack(
                "sector_attack",
                P("sectorId", evt.SectorId),
                new AnalyticsParam("strategy", evt.Strategy.ToString()));
        }

        private void OnSectorResult(SectorResultEvent evt)
        {
            SafeTrack(
                "sector_result",
                P("sectorId", evt.SectorId),
                new AnalyticsParam("win", evt.Win ? "1" : "0"));
        }

        private void OnSectorOwnershipChanged(SectorOwnershipChangedEvent evt)
        {
            SafeTrack(
                "sector_ownership_changed",
                P("sectorId", evt.SectorId),
                P("from", evt.FromPlayerId),
                P("to", evt.ToPlayerId));
        }

        private void SafeTrack(string name, params AnalyticsParam[] parameters)
        {
            try { _analytics.Track(name, parameters); }
            catch { }
        }

        private static AnalyticsParam P(string key, int value) => new(key, value.ToString(CultureInfo.InvariantCulture));
        private static AnalyticsParam P(string key, long value) => new(key, value.ToString(CultureInfo.InvariantCulture));
        private static AnalyticsParam P(string key, double value) => new(key, value.ToString("R", CultureInfo.InvariantCulture));
    }
}
