using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>One move's runtime data. exec(player, game, level) performs the attack.</summary>
    public class MoveDef
    {
        public string id, name, abbr;
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
            ["bite"] = new MoveDef { id = "bite", name = "Bite", abbr = "BITE", color = Palette.PlayerBody, cd = 0.5f, faceMove = true,
                exec = (p, g, lvl) => p.MeleeArc(g, 66f + lvl * 4f, 1.1f, p.atkDmg * (1.1f + 0.3f * (lvl - 1)), Palette.PlayerBody) },
            ["firebreath"] = new MoveDef { id = "firebreath", name = "Fire Breath", abbr = "FIRE", color = Palette.FireGlow, cd = 1.2f, faceMove = true,
                exec = (p, g, lvl) => p.Cone(g, 150f + lvl * 16f, 0.5f, p.atkDmg * (0.9f + 0.3f * (lvl - 1)), true) },
            ["laser"] = new MoveDef { id = "laser", name = "Laser Eyes", abbr = "LASER", color = Palette.LaserEye, cd = 1.7f,
                exec = (p, g, lvl) => p.Beam(g, p.atkDmg * (1.6f + 0.5f * (lvl - 1))) },
            ["venom"] = new MoveDef { id = "venom", name = "Venom Spit", abbr = "VENOM", color = Palette.Poison, cd = 0.9f,
                exec = (p, g, lvl) => p.ProjAim(g, p.atkDmg * (0.8f + 0.25f * (lvl - 1)), Palette.Poison, p.poisonDps) },
            ["tailswipe"] = new MoveDef { id = "tailswipe", name = "Tail Swipe", abbr = "TAIL", color = Palette.Spike, cd = 1.3f,
                exec = (p, g, lvl) => p.Nova(g, 82f + lvl * 10f, p.spikeDmg * (1f + 0.3f * (lvl - 1)), Palette.Spike, 16f) },
            ["barrage"] = new MoveDef { id = "barrage", name = "Barrage", abbr = "BARR", color = Palette.Barrage, cd = 1.1f,
                exec = (p, g, lvl) => p.Spread(g, 3 + Mathf.Min(lvl - 1, 2), p.atkDmg * 0.5f, Palette.Barrage) },
            ["charge"] = new MoveDef { id = "charge", name = "Charge", abbr = "CHRG", color = Palette.Xp, cd = 1.5f, faceMove = true,
                exec = (p, g, lvl) => p.ChargeMove(g, p.atkDmg * (1.4f + 0.4f * (lvl - 1))) },
            ["quake"] = new MoveDef { id = "quake", name = "Quake", abbr = "QUAKE", color = Palette.Quake, cd = 2.4f,
                exec = (p, g, lvl) => p.QuakeMove(g, 118f + lvl * 16f, p.atkDmg * (1.8f + 0.5f * (lvl - 1))) },
            ["mitosis"] = new MoveDef { id = "mitosis", name = "Split Into Two", abbr = "SPLIT", color = Palette.CloneBody, cd = 6f,
                exec = (p, g, lvl) => p.Mitosis(g, 1 + lvl) },
        };

        public static MoveDef Get(string id) => id != null && All.TryGetValue(id, out var m) ? m : null;
        public static bool Is(string id) => id != null && All.ContainsKey(id);
    }
}
