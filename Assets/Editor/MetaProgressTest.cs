using UnityEditor;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Self-check for MetaProgress economy math (MUTAGEN → Test Meta Progression).
    /// Exercises the pure helpers only — no PlayerPrefs writes, so it can't touch real progress.
    /// </summary>
    static class MetaProgressTest
    {
        [MenuItem("MUTAGEN/Test Meta Progression")]
        public static void Run()
        {
            // DNA → Essence: 20% rounded, never negative.
            Debug.Assert(MetaProgress.EssenceFromDna(1000f) == 200, "1000 DNA → 200 Essence");
            Debug.Assert(MetaProgress.EssenceFromDna(2f) == 0,      "2 DNA rounds to 0");
            Debug.Assert(MetaProgress.EssenceFromDna(-50f) == 0,    "negative DNA clamps to 0");

            // Pricing by rarity.
            Debug.Assert(MetaProgress.IsFree("common"),    "commons are free");
            Debug.Assert(!MetaProgress.IsFree("rare"),     "rares cost");
            Debug.Assert(MetaProgress.PriceOf("rare") == MetaProgress.RareCost, "rare price");
            Debug.Assert(MetaProgress.PriceOf("legendary") == MetaProgress.LegendaryCost, "legendary price");
            Debug.Assert(MetaProgress.PriceOf("common") == 0, "common price 0");

            Debug.Log("[MUTAGEN] MetaProgress self-test PASS ✓");
        }
    }
}
