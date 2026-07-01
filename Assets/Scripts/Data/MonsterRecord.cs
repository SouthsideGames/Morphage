using System;
using System.Collections.Generic;

namespace Mutagen
{
    /// <summary>
    /// A saved monster from a past run: run outcome/stats plus the full genome needed to
    /// re-render the creature procedurally via <see cref="PlayerVisual"/> / <see cref="MonsterPortrait"/>.
    /// Kept JsonUtility-friendly (public fields, arrays, no dictionaries; empty slots = "").
    /// </summary>
    [Serializable]
    public class MonsterRecord
    {
        public string name = "Specimen";
        public bool won;
        public int wave, level, kills;
        public float timeSurvived, dps, damageTaken;
        public string seed = "";
        public long stamp; // unix seconds, 0 if unknown

        // ---- genome (reconstructs the visual) ----
        public string[] moveSlots = { "", "", "", "" };
        public string[] mutIds = Array.Empty<string>();   // parallel to mutStacks
        public int[] mutStacks = Array.Empty<int>();
        public string[] evolved = Array.Empty<string>();
        public float maxHp = 100f, radius = 16f;
    }

    [Serializable]
    public class MonsterCollection
    {
        public List<MonsterRecord> monsters = new();
    }
}
