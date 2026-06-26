using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// The mutating organism (v0.4). Pokémon-style 4-move loadout: keys 1-4 fire moveSlots,
    /// each with its own cooldown + a short global cooldown. No auto-attack. Dash + i-frames.
    /// Passive modifiers + move passive riders apply via MutationEffects.
    /// </summary>
    public class Player
    {
        const float TAU = Mathf.PI * 2f;

        public float x, y, r;
        public float baseSpeed, speed, maxHp, hp;
        public int level;
        public float xp, xpNext, dna;
        public float atkDmg, dnaRange, regen, spikeDmg, poisonDps;
        public Dictionary<string, int> mutations = new();
        public HashSet<string> evolved = new();
        public float facingX = 1f, facingY = 0f;
        public float hurtFlash;
        public List<Clone> clones = new();

        // moveset
        public string[] moveSlots = { "bite", null, null, null };
        public Dictionary<string, int> moveLevel = new() { { "bite", 1 } };
        public Dictionary<string, float> moveCd = new();
        public float gcd;

        // dash + charge + quake
        public float invuln, dashCd, dashCdMax = 1.3f, dashTime;
        public bool dashing;
        Vector2 _dashVel;
        public float chargeDmg; HashSet<Enemy> _chargeHits;
        public float quakeT, quakeR, quakeDmg;

        // modifiers / synergy state
        public bool multishot, stormborn, necrosis, clonesBuffed;
        public float damageReduction, dnaBonus, brambleCd;

        Vector2 _lastMove;
        public SpriteView view; // unused (renders via PlayerVisual); kept for pooling safety

        public Player(Game game)
        {
            x = game.w / 2f; y = game.h / 2f; r = 16f;
            baseSpeed = 185f; speed = 185f; maxHp = 100f; hp = 100f;
            level = 1; xp = 0f; xpNext = 8f; dna = 0f;
            atkDmg = 11f; dnaRange = 70f; regen = 0f; spikeDmg = 16f; poisonDps = 0f;
        }

        public bool Has(string id) => mutations.TryGetValue(id, out int s) && s > 0;
        public int Stacks(string id) => mutations.TryGetValue(id, out int s) ? s : 0;
        public bool HasMove(string id) { for (int i = 0; i < 4; i++) if (moveSlots[i] == id) return true; return false; }
        public int FreeMoveSlot() { for (int i = 1; i < 4; i++) if (moveSlots[i] == null) return i; return -1; }
        public float CdMul() => Mathf.Pow(0.88f, Stacks("arms"));         // +2 Arms: −12% cooldowns/stack
        public float SpikeDamage() => spikeDmg + (Has("thick") ? maxHp * 0.06f : 0f);

        public void Hurt(float amount, Game game)
        {
            if (invuln > 0f) { game.AddFloater(x, y + r, "DODGE", Palette.Xp, 13f); return; }
            if (game.god) return;
            amount *= (1f - damageReduction);
            hp -= amount; hurtFlash = 0.18f; game.Shake(5f);
            game.hitstop = Mathf.Max(game.hitstop, 0.05f);
            game.stats.damageTaken += amount; Sfx.Hurt(); Haptics.Heavy();
            game.AddFloater(x, y + r, "-" + Mathf.Round(amount), Palette.HurtRed, 15f);
            for (int i = 0; i < 6; i++)
            {
                float a = Fx.Rand(0f, TAU);
                game.AddParticle(x, y, Mathf.Cos(a) * 120f, Mathf.Sin(a) * 120f, .4f, Palette.HurtRed, 3f);
            }
            if (hp <= 0f) { hp = 0f; game.GameOver(); }
        }

        public void AddXp(float v, Game game)
        {
            v = Mathf.Round(v * (1f + dnaBonus));
            xp += v; dna += v; game.stats.dnaCollected += v;
            while (xp >= xpNext)
            {
                xp -= xpNext; level++;
                xpNext = Mathf.Floor(8f + level * 4f + level * (float)level * 0.6f);
                game.OnLevelUp();
            }
        }

        Enemy AimNearest(Game game)
        {
            var t = game.NearestEnemy(x, y, 99999f);
            if (t != null) { float dx = t.x - x, dy = t.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f; facingX = dx / d; facingY = dy / d; }
            return t;
        }
        void FaceAimOrMove(Game game)
        {
            if (_lastMove.sqrMagnitude > 0.0001f) { var m = _lastMove.normalized; facingX = m.x; facingY = m.y; }
            else AimNearest(game);
        }

        public void UseMove(int slot, Game game)
        {
            if (gcd > 0f) return;
            string id = moveSlots[slot]; if (id == null) return;
            if (moveCd.TryGetValue(id, out float cd) && cd > 0f) return;
            var mv = Moves.Get(id); if (mv == null) return;
            int lvl = moveLevel.TryGetValue(id, out int l) ? l : 1;
            if (mv.faceMove) FaceAimOrMove(game);
            mv.exec(this, game, lvl);
            moveCd[id] = mv.cd * CdMul() * (1f - 0.06f * (lvl - 1));
            gcd = 0.2f; Sfx.Cast();
        }

        // ---------------------------------------------------------------- move helpers
        public void MeleeArc(Game game, float range, float arc, float dmg, Color color)
        {
            float baseA = Mathf.Atan2(facingY, facingX);
            for (int i = 0; i < 10; i++)
            {
                float a = baseA + Fx.Rand(-arc / 2f, arc / 2f);
                game.AddParticle(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r, Mathf.Cos(a) * Fx.Rand(120f, 260f), Mathf.Sin(a) * Fx.Rand(120f, 260f), .25f, color, Fx.Rand(3f, 6f));
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > range + e.r) continue;
                float da = Mathf.Atan2(Mathf.Sin(Mathf.Atan2(dy, dx) - baseA), Mathf.Cos(Mathf.Atan2(dy, dx) - baseA));
                if (Mathf.Abs(da) < arc / 2f + 0.2f) e.Hurt(dmg, game, Mathf.Cos(baseA) * 8f, Mathf.Sin(baseA) * 8f);
            }
        }

        public void Cone(Game game, float range, float half, float dmg, bool burn)
        {
            float baseA = Mathf.Atan2(facingY, facingX);
            for (int i = 0; i < 14; i++)
            {
                float a = baseA + Fx.Rand(-half, half), s = Fx.Rand(120f, range * 2.2f);
                game.AddParticle(x + Mathf.Cos(baseA) * r, y + Mathf.Sin(baseA) * r, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.3f, .55f), Fx.Pick(Palette.Fire), Fx.Rand(3f, 7f));
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > range + e.r) continue;
                float da = Mathf.Atan2(Mathf.Sin(Mathf.Atan2(dy, dx) - baseA), Mathf.Cos(Mathf.Atan2(dy, dx) - baseA));
                if (Mathf.Abs(da) < half + 0.15f)
                {
                    float mult = (burn && Has("venom") && e.poisonT > 0f) ? 1.5f : 1f; // Wildfire
                    e.Hurt(dmg * mult, game, Mathf.Cos(baseA) * 6f, Mathf.Sin(baseA) * 6f);
                    if (burn) e.ApplyPoison(6f + atkDmg * 0.2f);
                }
            }
        }

        public void Beam(Game game, float dmg)
        {
            AimNearest(game);
            float dx0 = facingX, dy0 = facingY; const float len = 900f;
            game.AddBeam(x, y, x + dx0 * len, y + dy0 * len, Palette.Laser);
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i];
                float t = Rng.Clamp(((e.x - x) * dx0 + (e.y - y) * dy0) / len, 0f, 1f);
                float px = x + dx0 * len * t, py = y + dy0 * len * t;
                if (Rng.Dist2(e.x, e.y, px, py) < (e.r + 12f) * (e.r + 12f))
                {
                    e.Hurt(dmg, game);
                    if (poisonDps != 0f) e.ApplyPoison(poisonDps);
                }
            }
        }

        public void ProjAim(Game game, float dmg, Color color, float poison)
        {
            AimNearest(game);
            float a = Mathf.Atan2(facingY, facingX); const float sp = 560f;
            game.AddProjectile(x, y, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, true, dmg, color, 6f, 1.4f, poison);
        }

        public void Spread(Game game, int n, float dmg, Color color)
        {
            AimNearest(game);
            float baseA = Mathf.Atan2(facingY, facingX); const float sp = 520f, span = 0.5f;
            for (int i = 0; i < n; i++)
            {
                float a = baseA + (i - (n - 1) / 2f) * (span / Mathf.Max(1, n - 1));
                game.AddProjectile(x, y, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, true, dmg, color, 5f, 1.2f);
            }
        }

        public void Nova(Game game, float radius, float dmg, Color color, float knock)
        {
            for (int i = 0; i < 18; i++)
            {
                float a = i / 18f * TAU;
                game.AddParticle(x, y, Mathf.Cos(a) * radius * 2.4f, Mathf.Sin(a) * radius * 2.4f, .3f, color, 4f);
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i];
                if (Rng.Dist2(x, y, e.x, e.y) < (radius + e.r) * (radius + e.r))
                {
                    float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                    e.Hurt(dmg, game, dx / d * knock, dy / d * knock);
                }
            }
        }

        public void ChargeMove(Game game, float dmg)
        {
            FaceAimOrMove(game);
            _dashVel = new Vector2(facingX * 700f, facingY * 700f);
            dashTime = 0.22f; invuln = 0.24f; dashing = true; chargeDmg = dmg; _chargeHits = new HashSet<Enemy>();
            for (int i = 0; i < 10; i++)
                game.AddParticle(x, y, -facingX * Fx.Rand(20f, 90f), -facingY * Fx.Rand(20f, 90f), .3f, Palette.Xp, 4f);
        }

        public void QuakeMove(Game game, float radius, float dmg) { quakeT = 0.35f; quakeR = radius; quakeDmg = dmg; }

        public void Mitosis(Game game, int count)
        {
            for (int i = 0; i < count; i++) { var c = new Clone(this, Fx.Rand(0f, TAU)) { life = 8f }; clones.Add(c); }
            for (int i = 0; i < 16; i++) { float a = Fx.Rand(0f, TAU); game.AddParticle(x, y, Mathf.Cos(a) * 160f, Mathf.Sin(a) * 160f, .4f, Palette.CloneBody, 4f); }
        }

        public void Dash(Game game)
        {
            if (dashCd > 0f || dashing) return;
            float dx = facingX, dy = facingY;
            if (_lastMove.sqrMagnitude > 0.0001f) { var m = _lastMove.normalized; dx = m.x; dy = m.y; }
            _dashVel = new Vector2(dx * 640f, dy * 640f); dashTime = 0.14f; invuln = 0.22f; dashing = true; chargeDmg = 0f;
            dashCdMax = 1.3f * (stormborn ? 0.5f : 1f) * Mathf.Pow(0.9f, Stacks("wings"));
            dashCd = dashCdMax; Sfx.Dash(); Haptics.Light();
            for (int i = 0; i < 8; i++)
                game.AddParticle(x, y, -dx * Fx.Rand(20f, 80f), -dy * Fx.Rand(20f, 80f), .3f, Palette.Xp, 4f);
        }

        public void Update(float dt, Game game, Vector2 move)
        {
            _lastMove = move;
            if (hurtFlash > 0f) hurtFlash -= dt;
            if (invuln > 0f) invuln -= dt;
            if (dashCd > 0f) dashCd -= dt;
            if (brambleCd > 0f) brambleCd -= dt;
            if (gcd > 0f) gcd -= dt;
            // tick move cooldowns
            _cdKeys.Clear(); foreach (var kv in moveCd) _cdKeys.Add(kv.Key);
            foreach (var k in _cdKeys) if (moveCd[k] > 0f) moveCd[k] -= dt;

            if (quakeT > 0f) { quakeT -= dt; if (quakeT <= 0f) { Nova(game, quakeR, quakeDmg, Palette.Quake, 18f); game.Shake(8f); } }

            if (dashing)
            {
                x = Rng.Clamp(x + _dashVel.x * dt, r, game.w - r);
                y = Rng.Clamp(y + _dashVel.y * dt, r, game.h - r);
                _dashVel *= 0.85f; dashTime -= dt;
                if (Fx.Chance(dt * 30f)) game.AddParticle(x, y, 0f, 0f, .25f, Palette.Xp, 4f);
                if (chargeDmg > 0f)
                {
                    var es = game.enemies;
                    for (int i = 0; i < es.Count; i++)
                    {
                        var e = es[i];
                        if (!_chargeHits.Contains(e) && Rng.Dist2(x, y, e.x, e.y) < (r + e.r + 4f) * (r + e.r + 4f))
                        { _chargeHits.Add(e); e.Hurt(chargeDmg, game, facingX * 16f, facingY * 16f); }
                    }
                }
                if (dashTime <= 0f) { dashing = false; chargeDmg = 0f; _chargeHits = null; }
            }
            else
            {
                float m = move.magnitude;
                if (m > 0.0001f)
                {
                    float nx = move.x / m, ny = move.y / m;
                    facingX = nx; facingY = ny;
                    float scale = Mathf.Min(m, 1f);
                    x = Rng.Clamp(x + nx * speed * scale * dt, r, game.w - r);
                    y = Rng.Clamp(y + ny * speed * scale * dt, r, game.h - r);
                }
            }

            if (regen > 0f && hp < maxHp)
            {
                hp = Mathf.Min(maxHp, hp + regen * dt);
                if (Fx.Chance(dt * 3f)) game.AddParticle(x + Fx.Rand(-10f, 10f), y + r, 0f, 30f, .5f, Palette.Regen, 2f);
            }

            if (game.autocast) for (int s = 0; s < 4; s++) UseMove(s, game);

            for (int i = 0; i < clones.Count; i++) clones[i].Update(dt, game);
            clones.RemoveAll(c => c.dead);
        }

        static readonly List<string> _cdKeys = new();
    }
}
