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
        private readonly IEventBus _eventBus;

        public RewardedAdsUseCaseService(
            IAdsService ads,
            OfflineClaimService offlineClaim,
            PvpAttackChargesService attacks,
            IEventBus eventBus = null)
        {
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _offlineClaim = offlineClaim ?? throw new ArgumentNullException(nameof(offlineClaim));
            _attacks = attacks;
            _eventBus = eventBus;
        }

        public bool TryShowOfflineClaimX2(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (_offlineClaim.PendingOfflineGain <= 0) return false;

            _ads.ShowRewardedAd(() =>
            {
                if (_offlineClaim.PendingOfflineGain <= 0) return;
                _offlineClaim.Claim(multiplier: 2);
                _eventBus?.Publish(new AdWatchedEvent(PlacementOfflineX2));
            });

            return true;
        }

        public bool TryShowDailyPvpAttack(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            if (_attacks == null) return false;
            if (!_attacks.CanClaimDailyAdAttack(nowUnixSeconds)) return false;

            _ads.ShowRewardedAd(() =>
            {
                if (!_attacks.TryClaimDailyAdAttack(nowUnixSeconds)) return;
                _eventBus?.Publish(new AdWatchedEvent(PlacementPvpAttackDaily));
            });

            return true;
        }
    }
}
