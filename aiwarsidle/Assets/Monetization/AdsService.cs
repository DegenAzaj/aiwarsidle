using System;

using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Monetization
{
    public sealed class AdsService : IAdsService
    {
        public void ShowRewardedAd(Action onSuccess)
        {
            onSuccess?.Invoke();
        }
    }
}
