using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>Draft pool logic, ported 1:1 from the prototype's MutationManager.</summary>
    public class MutationManager
    {
        public readonly List<MutationDef> defs;
        readonly Dictionary<string, MutationDef> _byId = new();

        public MutationManager(IEnumerable<MutationDef> mutationDefs)
        {
            defs = new List<MutationDef>(mutationDefs);
            defs.Sort((a, b) => a.order.CompareTo(b.order)); // deterministic order for seeded drafts
            foreach (var d in defs) _byId[d.id] = d;
        }

        public MutationDef ById(string id) => _byId.TryGetValue(id, out var d) ? d : null;

        public List<MutationDef> Available(Player player)
        {
            var outp = new List<MutationDef>();
            foreach (var d in defs)
            {
                int s = player.Stacks(d.id);
                if (!d.repeatable && s > 0) continue;
                if (d.maxStacks != 0 && s >= d.maxStacks) continue;
                outp.Add(d);
            }
            return outp;
        }

        static float Weight(string rarity) =>
            rarity == "legendary" ? 0.2f : rarity == "rare" ? 0.5f : 1f;

        public List<MutationDef> Draft(Player player, int n = 3)
        {
            var pool = new List<(MutationDef d, float w)>();
            foreach (var d in Available(player)) pool.Add((d, Weight(d.rarity)));
            var outp = new List<MutationDef>();
            while (outp.Count < n && pool.Count > 0)
            {
                float total = 0f; foreach (var o in pool) total += o.w;
                float r = Rng.Rand(0f, total); int idx = 0;
                for (int i = 0; i < pool.Count; i++) { if ((r -= pool[i].w) <= 0f) { idx = i; break; } }
                outp.Add(pool[idx].d);
                pool.RemoveAt(idx);
            }
            return outp;
        }

        public struct PickResult { public bool evolved; public string name; }

        public PickResult Pick(MutationDef def, Player player)
        {
            player.mutations.TryGetValue(def.id, out int cur);
            int s = cur + 1;
            player.mutations[def.id] = s;
            MutationEffects.Apply(def.id, player);
            if (def.maxStacks != 0 && s == def.maxStacks && !string.IsNullOrEmpty(def.evolveName) && !player.evolved.Contains(def.id))
            {
                player.evolved.Add(def.id);
                MutationEffects.Evolve(def.id, player);
                return new PickResult { evolved = true, name = def.evolveName };
            }
            return new PickResult { evolved = false };
        }
    }
}
