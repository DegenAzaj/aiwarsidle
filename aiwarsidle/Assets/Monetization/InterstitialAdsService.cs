using System;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Monetization
{
    public sealed class InterstitialAdsService : IInterstitialAdsService
    {
        private readonly ISubscriptionService _subscription;

        public InterstitialAdsService(ISubscriptionService subscription = null)
        {
            _subscription = subscription;
        }

        public bool TryShowInterstitial(string placement, Action onClosed = null)
        {
            if (_subscription?.IsActive == true) return false;

            onClosed?.Invoke();
            return true;
        }
    }
}

