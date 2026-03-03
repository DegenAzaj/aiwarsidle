using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Monetization
{
    public sealed class SubscriptionService : ISubscriptionService
    {
        public bool IsActive { get; private set; }

        public SubscriptionService(bool isActive = false)
        {
            IsActive = isActive;
        }

        public void SetActive(bool isActive)
        {
            IsActive = isActive;
        }
    }
}

