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
        readonly HashSet<Enemy> _castHits = new(); // reused by Chain (avoids a per-cast alloc)
        public float quakeT, quakeR, quakeDmg;

        // modifiers / synergy state
        public bool multishot, stormborn, necrosis, clonesBuffed;
        public float damageReduction, dnaBonus, brambleCd;
        public float critChance, critMult = 2f, lifesteal; // Unstable Cells / Vampirism
        public float hasteMul = 1f, healPerKill; public int reviveCharges; // Adrenal Glands / Carnivore / Second Wind

        public void Heal(float v) { if (v > 0f) hp = Mathf.Min(maxHp, hp + v); }

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
        public int FreeMoveSlot() { for (int i = 0; i < 4; i++) if (moveSlots[i] == null) return i; return -1; }
        public float CdMul() => Mathf.Pow(0.88f, Stacks("arms")) * hasteMul
            * (Has("haste") && Has("reflexes") ? 0.92f : 1f);                       // +2 Arms / Adrenal Glands; Adrenaline synergy
        public float SpikeDamage() => (spikeDmg + (Has("thick") ? maxHp * 0.06f : 0f))
            * (Has("quills") && Has("carapace") ? 1.5f : 1f);                        // Thornmail synergy

        public void Hurt(float amount, Game game)
        {
            if (invuln > 0f) { game.AddFloater(x, y + r, "DODGE", Palette.Xp, 13f); return; }
            if (game.god) return;
            amount *= (1f - damageReduction);
            hp -= amount; hurtFlash = 0.18f; game.Shake(5f);
            game.hitstop = Mathf.Max(game.hitstop, 0.05f);
            game.stats.damageTaken += amount; Sfx.Hurt(); Haptics.Heavy();
            Vfx.Spawn("HitRed", x, y, 16f);
            game.AddFloater(x, y + r, "-" + Mathf.Round(amount), Palette.HurtRed, 15f);
            for (int i = 0; i < 6; i++)
            {
                float a = Fx.Rand(0f, TAU);
                game.AddParticle(x, y, Mathf.Cos(a) * 120f, Mathf.Sin(a) * 120f, .4f, Palette.HurtRed, 3f);
            }
            if (hp <= 0f)
            {
                if (reviveCharges > 0) // Second Wind: cheat death once
                {
                    reviveCharges--; hp = maxHp * 0.5f; invuln = 1.5f;
                    game.AddFloater(x, y + r, "SECOND WIND", Palette.Dna, 18f); game.Shake(8f);
                }
                else { hp = 0f; game.GameOver(); }
            }
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
            var lt = game.NearestEnemy(x, y, 99999f);
            if (lt != null) Vfx.Spawn("LaserImpact", lt.x, lt.y, 18f);
        }

        public void ProjAim(Game game, float dmg, Color color, float poison)
        {
            AimNearest(game);
            float a = Mathf.Atan2(facingY, facingX); const float sp = 560f;
            game.AddProjectile(x, y, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, true, dmg, color, 6f, 1.4f, poison);
            Vfx.Spawn("PoisonCloud", x, y, 26f);
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

        // Spore Burst: a 360° ring of short-range pellets around the player.
        public void Ring(Game game, int n, float dmg, Color color)
        {
            const float sp = 470f;
            for (int i = 0; i < n; i++)
            {
                float a = i / (float)n * TAU;
                game.AddProjectile(x, y, Mathf.Cos(a) * sp, Mathf.Sin(a) * sp, true, dmg, color, 5f, 1.0f);
            }
            Vfx.Spawn("Poof", x, y, 24f);
        }

        // Ground Spikes: damage everything inside a short forward segment (Beam-style projection).
        public void LineStrike(Game game, float range, float width, float dmg, Color color)
        {
            float dx0 = facingX, dy0 = facingY;
            for (int i = 0; i < 12; i++)
            {
                float ti = i / 11f, px = x + dx0 * range * ti, py = y + dy0 * range * ti;
                game.AddParticle(px, py, 0f, -70f, .3f, color, Fx.Rand(3f, 6f));
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i];
                float proj = (e.x - x) * dx0 + (e.y - y) * dy0;
                if (proj < 0f || proj > range) continue;
                float px = x + dx0 * proj, py = y + dy0 * proj;
                if (Rng.Dist2(e.x, e.y, px, py) < (e.r + width) * (e.r + width))
                    e.Hurt(dmg, game, dx0 * 6f, dy0 * 6f);
            }
            Vfx.Spawn("Explosion", x + dx0 * range * 0.6f, y + dy0 * range * 0.6f, 16f);
        }

        // Frost Breath: a forward cone that damages and slows (clone of Cone, no burn).
        public void FrostCone(Game game, float range, float half, float dmg, Color color)
        {
            float baseA = Mathf.Atan2(facingY, facingX);
            for (int i = 0; i < 14; i++)
            {
                float a = baseA + Fx.Rand(-half, half), s = Fx.Rand(120f, range * 2.2f);
                game.AddParticle(x + Mathf.Cos(baseA) * r, y + Mathf.Sin(baseA) * r, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.3f, .55f), color, Fx.Rand(3f, 6f));
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > range + e.r) continue;
                float da = Mathf.Atan2(Mathf.Sin(Mathf.Atan2(dy, dx) - baseA), Mathf.Cos(Mathf.Atan2(dy, dx) - baseA));
                if (Mathf.Abs(da) < half + 0.15f) { e.Hurt(dmg, game); e.ApplySlow(0.5f, 2f); }
            }
        }

        // Lightning Arc: a bolt that chains between the nearest foes, beaming + damaging each.
        public void Chain(Game game, int links, float dmg, float range, Color color)
        {
            var hit = _castHits; hit.Clear();
            float cx = x, cy = y;
            for (int l = 0; l < links; l++)
            {
                Enemy best = null; float bestD = range * range;
                var es = game.enemies;
                for (int i = 0; i < es.Count; i++)
                {
                    var e = es[i]; if (e.dead || hit.Contains(e)) continue;
                    float dd = Rng.Dist2(cx, cy, e.x, e.y);
                    if (dd < bestD) { bestD = dd; best = e; }
                }
                if (best == null) break;
                game.AddBeam(cx, cy, best.x, best.y, color);
                best.Hurt(dmg, game);
                hit.Add(best); cx = best.x; cy = best.y;
            }
        }

        // Tentacle Lash: strike ahead, dragging caught foes toward you (pull = knockback inward) + slow.
        public void Lash(Game game, float range, float dmg, Color color)
        {
            float baseA = Mathf.Atan2(facingY, facingX);
            for (int i = 0; i < 10; i++)
            {
                float ti = i / 9f;
                game.AddParticle(x + facingX * range * ti, y + facingY * range * ti, 0f, 0f, .25f, color, Fx.Rand(3f, 6f));
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                if (d > range + e.r) continue;
                float da = Mathf.Atan2(Mathf.Sin(Mathf.Atan2(dy, dx) - baseA), Mathf.Cos(Mathf.Atan2(dy, dx) - baseA));
                if (Mathf.Abs(da) < 0.7f)
                {
                    float pull = Mathf.Min(d - 20f, 90f); if (pull < 0f) pull = 0f;
                    e.Hurt(dmg, game, -dx / d * pull, -dy / d * pull);
                    e.ApplySlow(0.7f, 1f);
                }
            }
        }

        // Gravity Pulse: implode nearby foes inward (pull toward you) + damage + slow.
        public void GravityPulse(Game game, float radius, float dmg, Color color)
        {
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * TAU;
                game.AddParticle(x + Mathf.Cos(a) * radius, y + Mathf.Sin(a) * radius, -Mathf.Cos(a) * 180f, -Mathf.Sin(a) * 180f, .35f, color, 4f);
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                if (d > radius + e.r) continue;
                float pull = Mathf.Min(d, 70f);
                e.Hurt(dmg, game, -dx / d * pull, -dy / d * pull);
                e.ApplySlow(0.6f, 1.2f);
            }
            Vfx.Spawn("Poof", x, y, radius * 0.5f);
        }

        // Sonic Screech: radial shockwave — damage + knockback + slow.
        public void Screech(Game game, float radius, float dmg, Color color)
        {
            for (int i = 0; i < 22; i++)
            {
                float a = i / 22f * TAU;
                game.AddParticle(x, y, Mathf.Cos(a) * radius * 2.2f, Mathf.Sin(a) * radius * 2.2f, .3f, color, 4f);
            }
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i];
                if (Rng.Dist2(x, y, e.x, e.y) < (radius + e.r) * (radius + e.r))
                {
                    float dx = e.x - x, dy = e.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                    e.Hurt(dmg, game, dx / d * 14f, dy / d * 14f); e.ApplySlow(0.6f, 1.5f);
                }
            }
            Vfx.Spawn("Poof", x, y, radius * 0.5f);
            game.Shake(5f);
        }

        // Lingering hazard zone (Acid Pool / Gravity Well / Inferno), dropped on the nearest foe (else ahead).
        public void DropZone(Game game, float radius, float dps, float life, float pull, float slowMul, bool poison, Color color)
        {
            var t = game.NearestEnemy(x, y, 99999f);
            float zx = t != null ? t.x : x + facingX * 70f;
            float zy = t != null ? t.y : y + facingY * 70f;
            game.AddHazard(zx, zy, radius, dps, life, pull, slowMul, poison, color);
            Vfx.Spawn("Poof", zx, zy, radius * 0.4f);
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
            // tick move cooldowns (fixed slot order — deterministic; every live cooldown maps to an equipped move)
            for (int i = 0; i < 4; i++)
            {
                var id = moveSlots[i];
                if (id != null && moveCd.TryGetValue(id, out var cd) && cd > 0f) moveCd[id] = cd - dt;
            }

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
                    float spd = speed * (Has("wings") && Has("swift") ? 1.15f : 1f); // Afterburner synergy
                    x = Rng.Clamp(x + nx * spd * scale * dt, r, game.w - r);
                    y = Rng.Clamp(y + ny * spd * scale * dt, r, game.h - r);
                }
            }

            if (regen > 0f && hp < maxHp)
            {
                float rg = regen * (Has("regen") && Has("vitality") ? 1.6f : 1f); // Regenesis synergy
                hp = Mathf.Min(maxHp, hp + rg * dt);
                if (Fx.Chance(dt * 3f)) game.AddParticle(x + Fx.Rand(-10f, 10f), y + r, 0f, 30f, .5f, Palette.Regen, 2f);
            }

            if (game.autocast) for (int s = 0; s < 4; s++) UseMove(s, game);

            for (int i = 0; i < clones.Count; i++) clones[i].Update(dt, game);
            clones.RemoveAll(c => c.dead);
        }
    }
}
