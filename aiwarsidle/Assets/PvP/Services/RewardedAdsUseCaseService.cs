using System;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.PvP.Services
{
    public sealed class RewardedAdsUseCaseService
    {
        public const string PlacementOfflineX2 = "offline_x2";
        public const string PlacementPvpAttackDaily = "pvp_attack_daily";

        private readonly IAdsService _ads;
        private readonly OfflineClaimService _offlineClaim;
        private readonly PvpAttackChargesService _attacks;
        private readonly IAnalyticsService _analytics;

        public RewardedAdsUseCaseService(
            IAdsService ads,
            OfflineClaimService offlineClaim,
            PvpAttackChargesService attacks,
            IAnalyticsService analytics = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _offlineClaim = offlineClaim ?? throw new ArgumentNullException(nameof(offlineClaim));
            _attacks = attacks ?? throw new ArgumentNullException(nameof(attacks));
            _analytics = analytics;
        }

        public bool TryShowOfflineClaimX2(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (_offlineClaim.PendingOfflineGain <= 0) return false;

            _ads.ShowRewardedAd(() =>
            {
                var pending = _offlineClaim.PendingOfflineGain;
                if (pending <= 0) return;

                _offlineClaim.Claim(multiplier: 2);
                _analytics?.Track("ad_watched", new AnalyticsParam("placement", PlacementOfflineX2));
                _analytics?.Track("offline_claim", new AnalyticsParam("multiplier", "2"), new AnalyticsParam("amount", (pending * 2).ToString("R")));
            });

            return true;
        }

        public bool TryShowDailyPvpAttack(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (!_attacks.CanClaimDailyAdAttack(nowUnixSeconds)) return false;

            _ads.ShowRewardedAd(() =>
            {
                if (!_attacks.TryClaimDailyAdAttack(nowUnixSeconds)) return;
                _analytics?.Track("ad_watched", new AnalyticsParam("placement", PlacementPvpAttackDaily));
            });

            return true;
        }
    }
}

