using System;
using UnityEngine;

namespace AIWarsIdle.PvP.Config
{
    [CreateAssetMenu(menuName = "AI Wars Idle/PvP Attacks Config", fileName = "PvpAttacksConfig")]
    public sealed class PvpAttacksConfig : ScriptableObject
    {
        [Header("Charges")]
        [Min(0)]
        public int MaxAttacks = 5;

        [Header("Regen (seconds)")]
        [Min(1)]
        public int RegenSeconds = 2 * 60 * 60;

        [Header("Rewarded Ads")]
        [Min(0)]
        public int AdExtraAttacksPerDay = 1;

        [Header("Premium purchase (contract only)")]
        [Min(0)]
        public int PremiumExtraAttacksPerPurchase = 0;

        [Min(0)]
        public int PremiumCurrencyCostPerPurchase = 0;

        public void ValidateOrThrow()
        {
            if (MaxAttacks < 0) throw new InvalidOperationException($"{nameof(MaxAttacks)} must be >= 0.");
            if (MaxAttacks > 0 && RegenSeconds <= 0) throw new InvalidOperationException($"{nameof(RegenSeconds)} must be > 0 when {nameof(MaxAttacks)} > 0.");

            if (AdExtraAttacksPerDay < 0) throw new InvalidOperationException($"{nameof(AdExtraAttacksPerDay)} must be >= 0.");

            if (PremiumExtraAttacksPerPurchase < 0) throw new InvalidOperationException($"{nameof(PremiumExtraAttacksPerPurchase)} must be >= 0.");
            if (PremiumCurrencyCostPerPurchase < 0) throw new InvalidOperationException($"{nameof(PremiumCurrencyCostPerPurchase)} must be >= 0.");
        }
    }
}

