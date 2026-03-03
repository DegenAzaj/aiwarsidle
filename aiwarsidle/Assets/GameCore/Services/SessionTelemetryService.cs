using System;

namespace AIWarsIdle.GameCore.Services
{
    public sealed class SessionTelemetryService
    {
        private readonly IEventBus _eventBus;

        public SessionTelemetryService(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void TrackSessionStart(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            _eventBus.Publish(new SessionStartedEvent(nowUnixSeconds));
        }
    }
}

