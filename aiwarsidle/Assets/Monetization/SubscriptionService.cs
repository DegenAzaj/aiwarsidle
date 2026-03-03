using System;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.Monetization
{
    public sealed class SubscriptionService : ISubscriptionService
    {
        private readonly IEventBus _eventBus;
        public bool IsActive { get; private set; }

        public SubscriptionService(bool isActive = false, IEventBus eventBus = null)
        {
            IsActive = isActive;
            _eventBus = eventBus;
        }

        public void SetActive(bool isActive)
        {
            var wasActive = IsActive;
            IsActive = isActive;

            if (!wasActive && IsActive)
            {
                _eventBus?.Publish(new SubscriptionStartedEvent(source: "unknown"));
            }
        }
    }
}
