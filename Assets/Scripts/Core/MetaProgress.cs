using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>JSON-serialized meta-progression state (currency + bought unlock ids).</summary>
    [System.Serializable]
    public class MetaSave
    {
        public int essence;
        public List<string> unlocked = new();
    }

    /// <summary>
    /// Between-runs meta-progression: a persistent currency ("Essence") banked from the DNA
    /// collected each run, plus the set of draft ids (moves + modifiers) bought in the shop.
    /// Persisted as JSON to <c>persistentDataPath</c> via <see cref="SaveSystem"/> — same store as
    /// the monster archive (inspectable, portable, no PlayerPrefs).
    ///
    /// Unlock rule is data-driven off rarity: <b>commons are free from the start</b>; rares and
    /// legendaries must be bought. New content slots in automatically (a new common is free, a new
    /// rare/legendary goes to the shop) — no hand-maintained list.
    ///
    /// DETERMINISM: the unlock gate narrows the seeded draft pool, so two peers (or the headless
    /// harness) with different unlocks would desync. <see cref="Enforce"/> gates the gate — it is
    /// true ONLY for real solo play and false for co-op / headless, which use the full pool. It
    /// defaults false (fail-safe) and every run entry point sets it explicitly.
    /// </summary>
    public static class MetaProgress
    {
        // --- economy knobs (ponytail: flat, tune from Balance Lab data later) ---
        public const float DnaToEssence = 0.2f;   // bank this fraction of a run's collected DNA
        public const int   RareCost      = 200;
        public const int   LegendaryCost = 400;

        /// <summary>When false the draft gate is bypassed (full pool). See class note.</summary>
        public static bool Enforce = false;

        static MetaSave _save;
        static MetaSave Save => _save ??= SaveSystem.LoadMeta();

        public static int Essence => Save.essence;

        // ---- pure helpers (unit-tested by MetaProgressTest) ----
        public static int EssenceFromDna(float dna) => Mathf.Max(0, Mathf.RoundToInt(dna * DnaToEssence));
        public static int PriceOf(string rarity) => rarity == "legendary" ? LegendaryCost : rarity == "rare" ? RareCost : 0;
        public static bool IsFree(string rarity) => PriceOf(rarity) == 0;

        // ---- unlock state ----
        public static bool IsUnlocked(MutationDef d) => d == null || IsFree(d.rarity) || Save.unlocked.Contains(d.id);
        public static int  CostOf(MutationDef d) => d == null ? 0 : PriceOf(d.rarity);

        /// <summary>Bank essence from a finished run's DNA. Returns the amount gained.</summary>
        public static int AwardRun(float dnaCollected)
        {
            int gained = EssenceFromDna(dnaCollected);
            if (gained > 0) { Save.essence += gained; SaveSystem.SaveMeta(Save); }
            return gained;
        }

        /// <summary>Buy an unlock. Returns true only if it was locked and affordable.</summary>
        public static bool Buy(MutationDef d)
        {
            if (d == null || IsUnlocked(d)) return false;
            int c = CostOf(d);
            if (Save.essence < c) return false;
            Save.essence -= c;
            Save.unlocked.Add(d.id);
            SaveSystem.SaveMeta(Save);
            return true;
        }

        /// <summary>Wipe currency + unlocks (debug / settings).</summary>
        public static void ResetAll()
        {
            _save = new MetaSave();
            SaveSystem.SaveMeta(_save);
        }
    }
}
