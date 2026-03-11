using System;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/PvP Config", fileName = "PvpConfig")]
    public sealed class PvpConfig : ScriptableObject
    {
        [Header("Randomness")]
        [Min(0.01f)]
        public float PowerVarianceMin = 0.95f;

        [Min(0.01f)]
        public float PowerVarianceMax = 1.05f;

        [Header("Strategy multipliers")]
        [Min(0.01f)]
        public float StrategyMultiplierAggressive = 1.05f;

        [Min(0.01f)]
        public float StrategyMultiplierStable = 1.00f;

        [Min(0.01f)]
        public float StrategyMultiplierRisky = 1.10f;

        [Header("Defense vs Stability")]
        [Min(0.01f)]
        public float StabilityMultiplierMin = 0.90f;

        [Min(0.01f)]
        public float StabilityMultiplierMax = 1.20f;

        [Header("Combat Outcome")]
        [Min(0.01f)]
        public float DefenseBonus = 1.10f;

        [Min(0f)]
        public float FlankBonusPerExtraAttacker = 0.10f;

        [Min(1f)]
        public float FlankBonusMaxMultiplier = 1.30f;

        [Header("Combat Maintenance")]
        [Min(0)]
        public int CombatMaintenanceFreeSectors = 6;

        [Range(0f, 1f)]
        public float CombatMaintenancePenaltyPerExtraSector = 0.03f;

        [Range(0.01f, 1f)]
        public float CombatMaintenanceMinMultiplier = 0.75f;

        [Header("Underdog")]
        [Range(0f, 1f)]
        public float UnderdogMaxAttackBonus = 0.25f;

        [Min(1)]
        public int UnderdogSectorDeficitForMaxBonus = 8;

        [Header("Siege / Breakout")]
        [Min(0f)]
        public float BreakoutBonusPerBlockedHomeNeighbor = 0.10f;

        [Min(1f)]
        public float BreakoutBonusMaxMultiplier = 1.30f;

        [Header("Isolation")]
        [Min(1)]
        public int IsolationExpectedSupportNeighbors = 2;

        [Range(0f, 1f)]
        public float IsolationPenaltyPerMissingSupport = 0.10f;

        [Range(0.01f, 1f)]
        public float IsolationMinDefenseMultiplier = 0.70f;

        [Header("PvpPower progression")]
        public double PrestigePvpPowerPerPrestige = 1.0;
        public double PermanentPvpPowerPerLevel = 1.0;
        public double SectorPvpPowerPerSector = 1.0;

        [Header("Production to PvpPower")]
        [Tooltip("Max bonus applied multiplicatively to core power. 0..1 means up to +0%..+100%.")]
        public double ProdToPvpMaxBonus = 0.20;

        [Tooltip("Soft-cap PPS point: when base PPS equals this value, production-derived bonus reaches half of ProdToPvpMaxBonus.")]
        public double ProdToPvpHalfCapPps = 100.0;

        public void ValidateOrThrow()
        {
            ValidateFinitePositiveOrThrow(PowerVarianceMin, nameof(PowerVarianceMin));
            ValidateFinitePositiveOrThrow(PowerVarianceMax, nameof(PowerVarianceMax));
            if (PowerVarianceMin > PowerVarianceMax)
            {
                throw new InvalidOperationException($"{nameof(PowerVarianceMin)} must be <= {nameof(PowerVarianceMax)}.");
            }

            ValidateFinitePositiveOrThrow(StrategyMultiplierAggressive, nameof(StrategyMultiplierAggressive));
            ValidateFinitePositiveOrThrow(StrategyMultiplierStable, nameof(StrategyMultiplierStable));
            ValidateFinitePositiveOrThrow(StrategyMultiplierRisky, nameof(StrategyMultiplierRisky));

            ValidateFinitePositiveOrThrow(StabilityMultiplierMin, nameof(StabilityMultiplierMin));
            ValidateFinitePositiveOrThrow(StabilityMultiplierMax, nameof(StabilityMultiplierMax));
            if (StabilityMultiplierMin > StabilityMultiplierMax)
            {
                throw new InvalidOperationException($"{nameof(StabilityMultiplierMin)} must be <= {nameof(StabilityMultiplierMax)}.");
            }

            ValidateFinitePositiveOrThrow(DefenseBonus, nameof(DefenseBonus));
            ValidateFiniteNonNegativeOrThrow(FlankBonusPerExtraAttacker, nameof(FlankBonusPerExtraAttacker));
            ValidateFinitePositiveOrThrow(FlankBonusMaxMultiplier, nameof(FlankBonusMaxMultiplier));
            if (FlankBonusMaxMultiplier < 1f)
            {
                throw new InvalidOperationException($"{nameof(FlankBonusMaxMultiplier)} must be >= 1.");
            }

            if (CombatMaintenanceFreeSectors < 0)
            {
                throw new InvalidOperationException($"{nameof(CombatMaintenanceFreeSectors)} must be >= 0.");
            }
            ValidateFiniteNonNegativeOrThrow(CombatMaintenancePenaltyPerExtraSector, nameof(CombatMaintenancePenaltyPerExtraSector));
            if (CombatMaintenancePenaltyPerExtraSector > 1f)
            {
                throw new InvalidOperationException($"{nameof(CombatMaintenancePenaltyPerExtraSector)} must be <= 1.");
            }
            ValidateFinitePositiveOrThrow(CombatMaintenanceMinMultiplier, nameof(CombatMaintenanceMinMultiplier));
            if (CombatMaintenanceMinMultiplier > 1f)
            {
                throw new InvalidOperationException($"{nameof(CombatMaintenanceMinMultiplier)} must be <= 1.");
            }

            ValidateFiniteNonNegativeOrThrow(UnderdogMaxAttackBonus, nameof(UnderdogMaxAttackBonus));
            if (UnderdogMaxAttackBonus > 1f)
            {
                throw new InvalidOperationException($"{nameof(UnderdogMaxAttackBonus)} must be <= 1.");
            }
            if (UnderdogSectorDeficitForMaxBonus <= 0)
            {
                throw new InvalidOperationException($"{nameof(UnderdogSectorDeficitForMaxBonus)} must be > 0.");
            }

            ValidateFiniteNonNegativeOrThrow(BreakoutBonusPerBlockedHomeNeighbor, nameof(BreakoutBonusPerBlockedHomeNeighbor));
            ValidateFinitePositiveOrThrow(BreakoutBonusMaxMultiplier, nameof(BreakoutBonusMaxMultiplier));
            if (BreakoutBonusMaxMultiplier < 1f)
            {
                throw new InvalidOperationException($"{nameof(BreakoutBonusMaxMultiplier)} must be >= 1.");
            }

            if (IsolationExpectedSupportNeighbors <= 0)
            {
                throw new InvalidOperationException($"{nameof(IsolationExpectedSupportNeighbors)} must be > 0.");
            }
            ValidateFiniteNonNegativeOrThrow(IsolationPenaltyPerMissingSupport, nameof(IsolationPenaltyPerMissingSupport));
            if (IsolationPenaltyPerMissingSupport > 1f)
            {
                throw new InvalidOperationException($"{nameof(IsolationPenaltyPerMissingSupport)} must be <= 1.");
            }
            ValidateFinitePositiveOrThrow(IsolationMinDefenseMultiplier, nameof(IsolationMinDefenseMultiplier));
            if (IsolationMinDefenseMultiplier > 1f)
            {
                throw new InvalidOperationException($"{nameof(IsolationMinDefenseMultiplier)} must be <= 1.");
            }

            ValidateFiniteNonNegativeOrThrow(PrestigePvpPowerPerPrestige, nameof(PrestigePvpPowerPerPrestige));
            ValidateFiniteNonNegativeOrThrow(PermanentPvpPowerPerLevel, nameof(PermanentPvpPowerPerLevel));
            ValidateFiniteNonNegativeOrThrow(SectorPvpPowerPerSector, nameof(SectorPvpPowerPerSector));

            ValidateFiniteNonNegativeOrThrow(ProdToPvpMaxBonus, nameof(ProdToPvpMaxBonus));
            if (ProdToPvpMaxBonus > 1.0)
            {
                throw new InvalidOperationException($"{nameof(ProdToPvpMaxBonus)} must be <= 1.");
            }

            ValidateFinitePositiveOrThrow(ProdToPvpHalfCapPps, nameof(ProdToPvpHalfCapPps));
        }

        private static void ValidateFinitePositiveOrThrow(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value <= 0f)
            {
                throw new InvalidOperationException($"{name} must be > 0.");
            }
        }

        private static void ValidateFinitePositiveOrThrow(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value <= 0.0)
            {
                throw new InvalidOperationException($"{name} must be > 0.");
            }
        }

        private static void ValidateFiniteNonNegativeOrThrow(double value, string name)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new InvalidOperationException($"{name} must be finite.");
            }

            if (value < 0.0)
            {
                throw new InvalidOperationException($"{name} must be >= 0.");
            }
        }
    }
}
