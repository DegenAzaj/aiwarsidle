using System;
using AIWarsIdle.Persistence.Domain;

namespace AIWarsIdle.Persistence.Services
{
    public sealed class AutosaveRunner
    {
        private readonly SaveService _saveService;
        private readonly Func<SaveDataV1> _getCurrentSaveData;
        private readonly float _intervalSeconds;
        private float _elapsedSeconds;

        public AutosaveRunner(SaveService saveService, Func<SaveDataV1> getCurrentSaveData, float intervalSeconds = 30f)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
            _getCurrentSaveData = getCurrentSaveData ?? throw new ArgumentNullException(nameof(getCurrentSaveData));

            if (intervalSeconds <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Autosave interval must be > 0.");
            }

            _intervalSeconds = intervalSeconds;
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            _elapsedSeconds += deltaSeconds;
            if (_elapsedSeconds < _intervalSeconds) return;

            _elapsedSeconds = 0f;
            ForceSave();
        }

        public void ForceSave()
        {
            _saveService.Save(_getCurrentSaveData());
        }

        public void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                ForceSave();
            }
        }

        public void OnApplicationQuit()
        {
            ForceSave();
        }
    }
}

