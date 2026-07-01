using System;

namespace Mutagen
{
    /// <summary>
    /// One mutation-combo synergy. `category` is "Damage" (conditional amp applied in
    /// <see cref="Enemy.Hurt"/>) or "Emergent" (a cooldown/behaviour effect elsewhere).
    /// `active` tests whether a player currently has the recipe. This table is the single
    /// source of truth for both the HUD chips (<see cref="Game.ActiveSynergies"/>) and the
    /// Synergy codex UI.
    /// </summary>
    public readonly struct SynergyDef
    {
        public readonly string name, category, recipe, effect;
        public readonly Func<Player, bool> active;
        public SynergyDef(string name, string category, string recipe, string effect, Func<Player, bool> active)
        { this.name = name; this.category = category; this.recipe = recipe; this.effect = effect; this.active = active; }
    }

    public static class Synergies
    {
        public static readonly SynergyDef[] All =
        {
            // ---- Damage (conditional amps in Enemy.Hurt) ----
            new SynergyDef("Brittle", "Damage", "Frost Breath",
                "Slowed foes take +25%", p => p.HasMove("frost")),
            new SynergyDef("Plague", "Damage", "Toxic Skin + a poison move",
                "Poisoned foes take +15%", p => p.Has("toxicskin") && (p.HasMove("venom") || p.HasMove("spore") || p.HasMove("stinger"))),
            new SynergyDef("Cryotoxin", "Damage", "Frost Breath + Venom Spit",
                "Slowed & poisoned foes take +20%", p => p.HasMove("frost") && p.HasMove("venom")),
            new SynergyDef("Wildfire", "Damage", "Fire Breath + Venom Spit",
                "Poisoned foes burn for +18%", p => p.HasMove("firebreath") && p.HasMove("venom")),
            new SynergyDef("Executioner", "Damage", "Unstable Cells + Carnivore",
                "Foes under 30% HP take +35%", p => p.Has("crit") && p.Has("carnivore")),
            new SynergyDef("Ambush", "Damage", "Charge + Mutagen Surge",
                "Foes above 90% HP take +25%", p => p.HasMove("charge") && p.Has("power")),
            new SynergyDef("Giant Slayer", "Damage", "Bulk + Quake or Gravity Pulse",
                "Bosses take +20%", p => p.Has("bulk") && (p.HasMove("quake") || p.HasMove("gravity"))),

            // ---- Emergent (cooldown / behaviour effects) ----
            new SynergyDef("Overcharge", "Emergent", "Laser Eyes + Arms",
                "Extra Arms cut laser cooldown", p => p.HasMove("laser") && p.Has("arms")),
            new SynergyDef("Static Field", "Emergent", "Lightning Arc + Arms",
                "Extra Arms speed up chains", p => p.HasMove("lightning") && p.Has("arms")),
            new SynergyDef("Bramble Plate", "Emergent", "Tail Swipe + Thick Hide",
                "Thick Hide boosts reflect damage", p => p.HasMove("tailswipe") && p.Has("thick")),
            new SynergyDef("Bloodfeast", "Emergent", "Vampirism + Spore or Stinger",
                "Many hits multiply lifesteal", p => p.Has("vampire") && (p.HasMove("spore") || p.HasMove("stinger"))),
            new SynergyDef("Vortex", "Emergent", "Gravity Pulse + Sonic Screech",
                "Stacked crowd control", p => p.HasMove("gravity") && p.HasMove("screech")),
            new SynergyDef("Critical Mass", "Emergent", "Unstable Cells + Glass Cannon",
                "High-risk crit burst build", p => p.Has("crit") && p.Has("glasscannon")),
            new SynergyDef("Adrenaline", "Emergent", "Adrenal Glands + Reflexes",
                "All move cooldowns −8%", p => p.Has("haste") && p.Has("reflexes")),
            new SynergyDef("Afterburner", "Emergent", "Wings + Swift Fins",
                "Move speed +15%", p => p.Has("wings") && p.Has("swift")),
            new SynergyDef("Thornmail", "Emergent", "Quills + Carapace",
                "Reflect damage +50%", p => p.Has("quills") && p.Has("carapace")),
            new SynergyDef("Regenesis", "Emergent", "Regeneration + Vitality",
                "Regeneration +60%", p => p.Has("regen") && p.Has("vitality")),
        };
    }
}
