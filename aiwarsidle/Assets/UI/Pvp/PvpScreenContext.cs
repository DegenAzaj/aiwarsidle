using AIWarsIdle.Bootstrap;
using AIWarsIdle.PvP.Services;
using UnityEngine;

namespace AIWarsIdle.UI.Pvp
{
    public sealed class PvpScreenContext : MonoBehaviour
    {
        [Header("Dependencies")]
        [Tooltip("Optional. If null, will try to find one in the scene at runtime.")]
        [SerializeField] private GameBootstrapper _bootstrapper;

        public GameBootstrapper Bootstrapper => _bootstrapper;
        public GameLoop Loop => _bootstrapper != null ? _bootstrapper.Loop : null;
        public MapService Map => Loop?.Map;
        public PvpMapCombatService Combat => Loop?.PvpCombat;
        public HexHackDuelService HexHackDuel => Loop?.HexHackDuel;
        public PvpAttackChargesService PvpAttacks => Loop?.PvpAttacks;
        public LeagueService League => Loop?.League;
        public SnapshotService Snapshot => Loop?.Snapshot;

        private void Awake()
        {
            if (_bootstrapper != null) return;

            _bootstrapper = FindBootstrapper();
            if (_bootstrapper == null)
            {
                Debug.LogWarning($"{nameof(PvpScreenContext)}: Missing {nameof(GameBootstrapper)} reference.");
            }
        }

        private static GameBootstrapper FindBootstrapper()
        {
#if UNITY_2023_1_OR_NEWER
            var found = Object.FindFirstObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
            if (found != null) return found;
            return Object.FindAnyObjectByType<GameBootstrapper>(FindObjectsInactive.Exclude);
#else
            return Object.FindObjectOfType<GameBootstrapper>();
#endif
        }
    }
}
