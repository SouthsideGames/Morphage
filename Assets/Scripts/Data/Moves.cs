using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>One move's runtime data. exec(player, game, level) performs the attack.</summary>
    public class MoveDef
    {
        public string id, name, abbr;
        public string icon;                   // Resources key for a Texture2D (e.g. "Icons/Bite"); null = text fallback
        public Color color;
        public float cd;
        public bool faceMove;                 // aim by movement dir (else auto-aim nearest)
        public Action<Player, Game, int> exec;
    }

    /// <summary>
    /// v0.4 move table (Pokémon-style loadout). Behaviour lives in delegates that call Player
    /// combat helpers — 1:1 with the prototype's MOVES table. Numbers match mutant-arena.html v0.4.
    /// </summary>
    public static class Moves
    {
        public static readonly Dictionary<string, MoveDef> All = new()
        {
            ["bite"] = new MoveDef { id = "bite", name = "Bite", abbr = "BITE", icon = "Icons/Teeth", color = Palette.PlayerBody, cd = 0.5f, faceMove = true,
                exec = (p, g, lvl) => p.MeleeArc(g, 66f + lvl * 4f, 1.1f, p.atkDmg * (1.1f + 0.3f * (lvl - 1)), Palette.PlayerBody) },
            ["firebreath"] = new MoveDef { id = "firebreath", name = "Fire Breath", abbr = "FIRE", icon = "Icons/Fire", color = Palette.FireGlow, cd = 1.2f, faceMove = true,
                exec = (p, g, lvl) => p.Cone(g, 150f + lvl * 16f, 0.5f, p.atkDmg * (0.9f + 0.3f * (lvl - 1)), true) },
            ["laser"] = new MoveDef { id = "laser", name = "Laser Eyes", abbr = "LASER", icon = "Icons/Eye", color = Palette.LaserEye, cd = 1.7f,
                exec = (p, g, lvl) => p.Beam(g, p.atkDmg * (1.6f + 0.5f * (lvl - 1))) },
            ["venom"] = new MoveDef { id = "venom", name = "Venom Spit", abbr = "VENOM", icon = "Icons/Slime", color = Palette.Poison, cd = 0.9f,
                exec = (p, g, lvl) => p.ProjAim(g, p.atkDmg * (0.8f + 0.25f * (lvl - 1)), Palette.Poison, p.poisonDps) },
            ["tailswipe"] = new MoveDef { id = "tailswipe", name = "Tail Swipe", abbr = "TAIL", icon = "Icons/MorningStar", color = Palette.Spike, cd = 1.3f,
                exec = (p, g, lvl) => p.Nova(g, 82f + lvl * 10f, p.spikeDmg * (1f + 0.3f * (lvl - 1)), Palette.Spike, 16f) },
            ["barrage"] = new MoveDef { id = "barrage", name = "Barrage", abbr = "BARR", icon = "Icons/Shuriken", color = Palette.Barrage, cd = 1.1f,
                exec = (p, g, lvl) => p.Spread(g, 3 + Mathf.Min(lvl - 1, 2), p.atkDmg * 0.5f, Palette.Barrage) },
            ["charge"] = new MoveDef { id = "charge", name = "Charge", abbr = "CHRG", icon = "Icons/StrongArm", color = Palette.Xp, cd = 1.5f, faceMove = true,
                exec = (p, g, lvl) => p.ChargeMove(g, p.atkDmg * (1.4f + 0.4f * (lvl - 1))) },
            ["quake"] = new MoveDef { id = "quake", name = "Quake", abbr = "QUAKE", icon = "Icons/Explosion", color = Palette.Quake, cd = 2.4f,
                exec = (p, g, lvl) => p.QuakeMove(g, 118f + lvl * 16f, p.atkDmg * (1.8f + 0.5f * (lvl - 1))) },
            ["mitosis"] = new MoveDef { id = "mitosis", name = "Split Into Two", abbr = "SPLIT", icon = "Icons/Multiplayer", color = Palette.CloneBody, cd = 6f,
                exec = (p, g, lvl) => p.Mitosis(g, 1 + lvl) },
            ["spore"] = new MoveDef { id = "spore", name = "Spore Burst", abbr = "SPORE", icon = "Icons/Seeds", color = Palette.Poison, cd = 1.3f,
                exec = (p, g, lvl) => p.Ring(g, 8 + Mathf.Min(lvl - 1, 3), p.atkDmg * (0.5f + 0.15f * (lvl - 1)), Palette.Poison) },
            ["stinger"] = new MoveDef { id = "stinger", name = "Stinger Volley", abbr = "STING", icon = "Icons/Arrow", color = Palette.Barrage, cd = 1.0f,
                exec = (p, g, lvl) => p.Spread(g, 5 + Mathf.Min(lvl - 1, 2), p.atkDmg * 0.45f, Palette.Barrage) },
            ["spikes"] = new MoveDef { id = "spikes", name = "Ground Spikes", abbr = "SPIKE", icon = "Icons/Spear", color = Palette.Spike, cd = 1.4f, faceMove = true,
                exec = (p, g, lvl) => p.LineStrike(g, 150f + lvl * 14f, 26f, p.atkDmg * (1.2f + 0.35f * (lvl - 1)), Palette.Spike) },
            ["frost"] = new MoveDef { id = "frost", name = "Frost Breath", abbr = "FROST", icon = "Icons/Snowflake", color = new Color(0.29f, 0.84f, 1f), cd = 1.3f, faceMove = true,
                exec = (p, g, lvl) => p.FrostCone(g, 140f + lvl * 16f, 0.5f, p.atkDmg * (0.8f + 0.25f * (lvl - 1)), new Color(0.29f, 0.84f, 1f)) },
            ["screech"] = new MoveDef { id = "screech", name = "Sonic Screech", abbr = "SCRCH", icon = "Icons/Tornado", color = new Color(0.72f, 0.8f, 1f), cd = 1.8f,
                exec = (p, g, lvl) => p.Screech(g, 110f + lvl * 14f, p.atkDmg * (1.1f + 0.3f * (lvl - 1)), new Color(0.72f, 0.8f, 1f)) },
            ["lightning"] = new MoveDef { id = "lightning", name = "Lightning Arc", abbr = "ARC", icon = "Icons/LightningBolt", color = new Color(0.29f, 0.84f, 1f), cd = 1.6f,
                exec = (p, g, lvl) => p.Chain(g, 3 + Mathf.Min(lvl - 1, 2), p.atkDmg * (1.0f + 0.35f * (lvl - 1)), 260f, new Color(0.29f, 0.84f, 1f)) },
            ["tentacle"] = new MoveDef { id = "tentacle", name = "Tentacle Lash", abbr = "LASH", icon = "Icons/Lasso", color = new Color(0.81f, 0.44f, 0.65f), cd = 1.1f, faceMove = true,
                exec = (p, g, lvl) => p.Lash(g, 120f + lvl * 12f, p.atkDmg * (0.9f + 0.3f * (lvl - 1)), new Color(0.81f, 0.44f, 0.65f)) },
            ["gravity"] = new MoveDef { id = "gravity", name = "Gravity Pulse", abbr = "GRAV", icon = "Icons/Galaxy", color = new Color(0.61f, 0.55f, 1f), cd = 2.0f,
                exec = (p, g, lvl) => p.GravityPulse(g, 120f + lvl * 14f, p.atkDmg * (1.2f + 0.4f * (lvl - 1)), new Color(0.61f, 0.55f, 1f)) },
            ["acid"] = new MoveDef { id = "acid", name = "Acid Pool", abbr = "ACID", icon = "Icons/Pond", color = Palette.Poison, cd = 3.0f,
                exec = (p, g, lvl) => p.DropZone(g, 70f + lvl * 6f, p.atkDmg * (0.7f + 0.2f * (lvl - 1)), 3.5f, 0f, 1f, true, Palette.Poison) },
            ["well"] = new MoveDef { id = "well", name = "Gravity Well", abbr = "WELL", icon = "Icons/Planet", color = new Color(0.61f, 0.55f, 1f), cd = 3.5f,
                exec = (p, g, lvl) => p.DropZone(g, 95f + lvl * 6f, p.atkDmg * (0.45f + 0.15f * (lvl - 1)), 2.5f, 1.6f, 0.6f, false, new Color(0.61f, 0.55f, 1f)) },
            ["inferno"] = new MoveDef { id = "inferno", name = "Inferno", abbr = "INFRN", icon = "Icons/Volcano", color = Palette.FireGlow, cd = 3.2f,
                exec = (p, g, lvl) => p.DropZone(g, 80f + lvl * 8f, p.atkDmg * (1.0f + 0.3f * (lvl - 1)), 3.0f, 0f, 1f, false, Palette.FireGlow) },
        };

        public static MoveDef Get(string id) => id != null && All.TryGetValue(id, out var m) ? m : null;
        public static bool Is(string id) => id != null && All.ContainsKey(id);
    }
}
