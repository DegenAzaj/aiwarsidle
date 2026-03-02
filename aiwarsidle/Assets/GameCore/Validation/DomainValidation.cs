using System;
using AIWarsIdle.GameCore.Domain;

namespace AIWarsIdle.GameCore.Validation
{
    public static class DomainValidation
    {
        public static void ValidateSectorId(int sectorId)
        {
            if (sectorId < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sectorId), "SectorId must be >= 0.");
            }
        }

        public static void ValidateStability(float stability)
        {
            if (stability < 0f || stability > 100f)
            {
                throw new ArgumentOutOfRangeException(nameof(stability), "Stability must be in range 0..100.");
            }
        }

        public static void ValidateUnixSeconds(long unixSeconds, string paramName)
        {
            if (unixSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(paramName, "Timestamp must be >= 0.");
            }
        }

        public static void ValidateSectorState(SectorState sector)
        {
            if (sector == null) throw new ArgumentNullException(nameof(sector));

            ValidateSectorId(sector.SectorId);
            ValidateStability(sector.Stability);
            ValidateUnixSeconds(sector.LastCombatUnixSeconds, nameof(sector.LastCombatUnixSeconds));
            ValidateUnixSeconds(sector.CapturedUnixSeconds, nameof(sector.CapturedUnixSeconds));
        }

        public static void ValidateOverclockState(OverclockState state, int maxCharges)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (maxCharges < 0) throw new ArgumentOutOfRangeException(nameof(maxCharges), "Max charges must be >= 0.");

            if (state.Charges < 0 || state.Charges > maxCharges)
            {
                throw new ArgumentOutOfRangeException(nameof(state.Charges), $"Charges must be in range 0..{maxCharges}.");
            }

            ValidateUnixSeconds(state.ActiveUntilUnixSeconds, nameof(state.ActiveUntilUnixSeconds));
            ValidateUnixSeconds(state.NextChargeAtUnixSeconds, nameof(state.NextChargeAtUnixSeconds));
        }
    }
}

