using System;
using System.IO;
using AIWarsIdle.GameCore.Config;
using AIWarsIdle.Persistence.IO;
using AIWarsIdle.Persistence.Services;
using AIWarsIdle.PvP.Config;
using UnityEngine;

namespace AIWarsIdle.Bootstrap
{
    public sealed class GameBootstrapper : MonoBehaviour
    {
        [Header("Configs")]
        [SerializeField] private BalanceConfig _balanceConfig;
        [SerializeField] private OverclockConfig _overclockConfig;
        [SerializeField] private MapConfig _mapConfig;
        [SerializeField] private PvpAttacksConfig _pvpAttacksConfig;
        [SerializeField] private LeagueConfig _leagueConfig;

        [Header("Save")]
        [SerializeField] private string _saveSubdirectory = "save";
        [SerializeField] private string _saveFileNameWithoutExtension = "save";

        [Header("Loop")]
        [SerializeField] private float _autosaveIntervalSeconds = 30f;
        [SerializeField] private float _analyticsFlushIntervalSeconds = 2f;

        private GameLoop _loop;

        public GameLoop Loop => _loop;
        public string SaveSubdirectory => _saveSubdirectory;
        public string SaveFileNameWithoutExtension => _saveFileNameWithoutExtension;

        public BalanceConfig BalanceConfig { get => _balanceConfig; set => _balanceConfig = value; }
        public OverclockConfig OverclockConfig { get => _overclockConfig; set => _overclockConfig = value; }
        public MapConfig MapConfig { get => _mapConfig; set => _mapConfig = value; }
        public PvpAttacksConfig PvpAttacksConfig { get => _pvpAttacksConfig; set => _pvpAttacksConfig = value; }
        public LeagueConfig LeagueConfig { get => _leagueConfig; set => _leagueConfig = value; }

        public void SetSaveLocationForTests(string subdirectory, string fileNameWithoutExtension)
        {
            _saveSubdirectory = subdirectory;
            _saveFileNameWithoutExtension = fileNameWithoutExtension;
        }

        private void Start()
        {
            Initialize();
        }

        public void Initialize()
        {
            if (_loop != null) return;

            if (_balanceConfig == null) throw new InvalidOperationException($"{nameof(BalanceConfig)} is required.");
            if (_overclockConfig == null) throw new InvalidOperationException($"{nameof(OverclockConfig)} is required.");
            if (_mapConfig == null) throw new InvalidOperationException($"{nameof(MapConfig)} is required.");

            if (string.IsNullOrWhiteSpace(_saveSubdirectory)) _saveSubdirectory = "save";
            if (string.IsNullOrWhiteSpace(_saveFileNameWithoutExtension)) _saveFileNameWithoutExtension = "save";

            var dir = Path.Combine(Application.persistentDataPath, _saveSubdirectory);
            var paths = new SaveFilePaths(dir, _saveFileNameWithoutExtension);
            var saveService = new SaveService(paths, new SaveDataMigrator());

            var save = saveService.LoadOrCreate();

            _loop = new GameLoop(
                saveService,
                save,
                _balanceConfig,
                _overclockConfig,
                _mapConfig,
                _pvpAttacksConfig,
                _leagueConfig,
                autosaveIntervalSeconds: _autosaveIntervalSeconds,
                analyticsFlushIntervalSeconds: _analyticsFlushIntervalSeconds);
        }

        private void Update()
        {
            _loop?.Tick(Time.deltaTime);
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            _loop?.OnApplicationPause(pauseStatus);
        }

        private void OnApplicationQuit()
        {
            _loop?.OnApplicationQuit();
        }

        private void OnDestroy()
        {
            _loop?.Dispose();
        }
    }
}
