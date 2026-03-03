using System;
using AIWarsIdle.GameCore.Services;

namespace AIWarsIdle.PvP.Services
{
    public sealed class PvpTelemetryService
    {
        private readonly IEventBus _eventBus;

        public PvpTelemetryService(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public void TrackMapOpen(long nowUnixSeconds)
        {
            if (nowUnixSeconds < 0) throw new ArgumentOutOfRangeException(nameof(nowUnixSeconds), "Timestamp must be >= 0.");
            _eventBus.Publish(new MapOpenedEvent(nowUnixSeconds));
        }

        public void TrackSectorView(int sectorId)
        {
            if (sectorId < 0) throw new ArgumentOutOfRangeException(nameof(sectorId), "SectorId must be >= 0.");
            _eventBus.Publish(new SectorViewedEvent(sectorId));
        }
    }
}

