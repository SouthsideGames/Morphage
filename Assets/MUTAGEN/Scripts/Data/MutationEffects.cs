using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Stat hooks for mutations (v0.4). Apply() runs on learn AND on each level-up; Evolve() runs
    /// once at max stacks. Moves with passive riders (venom, tailswipe, mitosis) live here too;
    /// pure attack behaviour is in <see cref="Moves"/>. Magnitudes are 1:1 with the HTML.
    /// </summary>
    public static class MutationEffects
    {
        public static void Apply(string id, Player p)
        {
            switch (id)
            {
                // modifiers
                case "arms":   break; // cooldown reduction handled by Player.CdMul()
                case "regen":  p.regen += 3f; break;
                case "thick":  p.maxHp += 40f; p.hp = p.maxHp; break;
                case "wings":  p.baseSpeed *= 1.22f; p.speed = p.baseSpeed; break;
                case "magnet": p.dnaRange *= 1.8f; break;
                // moves with passive riders
                case "venom":     p.poisonDps += 6f; break;
                case "tailswipe": p.spikeDmg += 8f; break;
                // bite/firebreath/laser/barrage/charge/quake/mitosis: pure attack, no rider
            }
        }

        public static void Evolve(string id, Player p)
        {
            switch (id)
            {
                case "arms":   p.multishot = true; break;
                case "regen":  p.regen *= 1.6f; p.maxHp += 20f; p.hp = p.maxHp; break;
                case "thick":  p.damageReduction = 0.30f; break;
                case "wings":  p.stormborn = true; p.baseSpeed *= 1.1f; p.speed = p.baseSpeed; break;
                case "magnet": p.dnaBonus += 0.25f; p.dnaRange *= 1.3f; break;
                case "venom":  p.necrosis = true; p.poisonDps += 4f; break;
                case "tailswipe": p.spikeDmg += 16f; break;
                case "mitosis": p.clonesBuffed = true; break;
                // firebreath/laser/barrage/charge/quake: evolution is just level scaling
            }
        }
    }
}
