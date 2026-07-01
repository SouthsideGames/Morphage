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
                case "power":     p.atkDmg += 2f; break;
                case "carapace":  p.damageReduction = Mathf.Min(0.6f, p.damageReduction + 0.06f); break;
                case "vitality":  p.maxHp *= 1.2f; p.hp = p.maxHp; break;
                case "reflexes":  p.dashCdMax *= 0.85f; break;
                case "scavenger": p.dnaBonus += 0.15f; break;
                case "toxicskin": p.poisonDps += 4f; break;
                case "glasscannon": p.atkDmg += 6f; p.maxHp *= 0.8f; p.hp = Mathf.Min(p.hp, p.maxHp); break;
                case "crit":      p.critChance = Mathf.Min(0.6f, p.critChance + 0.08f); break;
                case "vampire":   p.lifesteal += 0.06f; break;
                case "haste":     p.hasteMul *= 0.9f; break;
                case "bulk":      p.maxHp += 30f; p.hp = p.maxHp; p.r += 2f; break;
                case "swift":     p.baseSpeed *= 1.12f; p.speed = p.baseSpeed; break;
                case "revive":    p.reviveCharges += 1; break;
                case "quills":    p.spikeDmg += 10f; break;
                case "carnivore": p.healPerKill += 2f; break;
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
                case "power":     p.atkDmg += 4f; break;                                   // Overdrive
                case "carapace":  p.damageReduction = Mathf.Min(0.6f, p.damageReduction + 0.08f); break; // Living Shell
                case "vitality":  p.regen += 3f; break;                                    // Hardy Cells
                case "reflexes":  p.dashCdMax *= 0.7f; break;                              // Blink
                case "scavenger": p.dnaRange *= 1.4f; p.dnaBonus += 0.1f; break;           // Apex Forager
                case "toxicskin": p.poisonDps += 8f; break;                                // Virulent
                case "crit":      p.critMult += 0.5f; p.critChance = Mathf.Min(0.6f, p.critChance + 0.05f); break; // Meltdown
                case "vampire":   p.lifesteal += 0.05f; p.maxHp += 20f; break;              // Bloodthirst
                case "haste":     p.hasteMul *= 0.85f; break;                               // Overclock
                case "bulk":      p.maxHp += 30f; p.r += 2f; p.damageReduction = Mathf.Min(0.6f, p.damageReduction + 0.05f); break; // Behemoth
                case "swift":     p.baseSpeed *= 1.15f; p.speed = p.baseSpeed; p.dashCdMax *= 0.8f; break; // Blur
                case "quills":    p.spikeDmg += 16f; break;                                 // Spineback
                case "carnivore": p.healPerKill += 3f; break;                               // Devour
                // firebreath/laser/barrage/charge/quake: evolution is just level scaling
            }
        }
    }
}
