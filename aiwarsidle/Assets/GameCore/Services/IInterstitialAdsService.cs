using System;

namespace AIWarsIdle.GameCore.Services
{
    public interface IInterstitialAdsService
    {
        bool TryShowInterstitial(string placement, Action onClosed = null);
    }
}

