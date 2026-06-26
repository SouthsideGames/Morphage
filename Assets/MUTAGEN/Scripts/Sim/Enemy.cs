using UnityEngine;

namespace Mutagen
{
    /// <summary>Data-driven enemy. v0.3: elite variants + telegraphed boss attacks (windup → spread/charge).</summary>
    public class Enemy
    {
        const float TAU = Mathf.PI * 2f;

        public string id;
        public EnemyDef cfg;
        public Color color;
        public float x, y, r, maxHp, hp, speed, dmg;
        public int xp;
        public bool dead;
        public float hitFlash, fireCd, poison, poisonT, poisonFloat, contactCd, t, scale;
        public string elite; // null | "tough" | "frenzied"

        // boss attack state
        public float attackCd, windup, aimX, aimY, dashTime, bdvx, bdvy;
        public string attackType;
        public bool bossDashing, finalBoss;

        public SpriteView view;

        public Enemy(EnemyDef cfg, float x, float y, float waveScale, string elite = null)
        {
            id = cfg.id; this.cfg = cfg; this.x = x; this.y = y; r = cfg.r; color = cfg.color;
            maxHp = cfg.hp * waveScale;
            speed = cfg.speed * (cfg.boss ? 1f : Rng.Lerp(1f, 1.25f, Rng.Clamp((waveScale - 1f) / 2f, 0f, 1f)));
            dmg = cfg.dmg * (cfg.boss ? 1f : waveScale);
            xp = cfg.xp;
            this.elite = elite;
            if (elite == "tough") { maxHp *= 2.5f; speed *= 0.8f; r += 4f; dmg *= 1.3f; xp *= 2; }
            else if (elite == "frenzied") { speed *= 1.45f; dmg *= 1.35f; xp *= 2; }
            hp = maxHp;
            dead = false; hitFlash = 0f; fireCd = Rng.Rand(1.2f, 2.4f);
            poison = 0f; poisonT = 0f; poisonFloat = 0f; contactCd = 0f; t = Fx.Rand(0f, TAU); scale = 0f;
            attackCd = Rng.Rand(2.2f, 3.2f); windup = 0f; attackType = null; aimX = 0f; aimY = 1f;
            bossDashing = false; dashTime = 0f; finalBoss = false;
        }

        public void Hurt(float amount, Game game, float kx = 0f, float ky = 0f, bool silent = false)
        {
            hp -= amount; hitFlash = 0.12f; game.stats.damageDealt += amount;
            if (kx != 0f || ky != 0f) { x += kx; y += ky; }
            if (!silent)
            {
                bool big = amount >= 28f;
                game.AddFloater(x, y + r, Mathf.Round(amount).ToString(),
                    big ? Palette.FloaterBig : Palette.FloaterWhite, big ? 20f : 14f);
                Sfx.Hit();
            }
            if (hp <= 0f && !dead) Die(game);
        }

        public void ApplyPoison(float dps) { poison = Mathf.Max(poison, dps); poisonT = Mathf.Max(poisonT, 3f); }

        public void Die(Game game)
        {
            dead = true; game.stats.kills++; game.Shake(cfg.boss ? 16f : 4f);
            game.hitstop = Mathf.Max(game.hitstop, cfg.boss ? 0.18f : 0.03f);
            Sfx.Death();
            if (cfg.boss) Haptics.Medium();
            int n = cfg.boss ? 60 : 12;
            for (int i = 0; i < n; i++)
            {
                float a = Fx.Rand(0f, TAU), s = Fx.Rand(40f, cfg.boss ? 340f : 200f);
                game.AddParticle(x, y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.3f, .8f), color, Fx.Rand(2f, cfg.boss ? 7f : 4f));
            }
            int drops = cfg.boss ? 18 : (id == "tank" ? 3 : 1);
            if (elite != null) drops += 2;
            for (int i = 0; i < drops; i++)
                game.AddOrb(x + Fx.Rand(-12f, 12f), y + Fx.Rand(-12f, 12f),
                    cfg.boss ? 6f : Mathf.Ceil((float)xp / (drops > 1 ? 2f : 1f)));
            if (cfg.explode) game.Explode(x, y, 70f, dmg * 2.2f, color);
            if (cfg.boss) game.OnBossKilled(this);
        }

        void StartAttack(float dx, float dy)
        {
            attackType = Fx.Chance(0.5f) ? "spread" : "charge"; // non-seeded, matches prototype Math.random
            windup = 0.8f;
            if (attackType == "charge") { aimX = dx; aimY = dy; }
        }

        void ExecuteAttack(Game game)
        {
            if (attackType == "spread")
            {
                for (int i = 0; i < 10; i++)
                {
                    float a = i / 10f * TAU + t;
                    game.AddProjectile(x, y, Mathf.Cos(a) * 230f, Mathf.Sin(a) * 230f, false, dmg * 0.5f, Palette.BossShot, 7f, 4f);
                }
                game.Shake(6f);
            }
            else { bdvx = aimX * 640f; bdvy = aimY * 640f; dashTime = 0.45f; bossDashing = true; game.Shake(5f); }
            attackType = null;
        }

        public void Update(float dt, Game game)
        {
            scale = Mathf.Min(1f, scale + dt * 5f); t += dt; if (hitFlash > 0f) hitFlash -= dt;
            var p = game.player;
            float dx = p.x - x, dy = p.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f; dx /= d; dy /= d;
            float slow = (poisonT > 0f && p.necrosis) ? 0.55f : 1f;

            if (poisonT > 0f)
            {
                poisonT -= dt; float pdmg = poison * dt; hp -= pdmg; game.stats.damageDealt += pdmg; poisonFloat -= dt;
                if (Fx.Chance(dt * 8f))
                    game.AddParticle(x + Fx.Rand(-6f, 6f), y + Fx.Rand(-6f, 6f), 0f, 20f, .4f, Palette.Poison, 2f);
                if (poisonFloat <= 0f)
                {
                    poisonFloat = 0.45f;
                    game.AddFloater(x + Fx.Rand(-8f, 8f), y + r, Mathf.Round(poison * 0.45f).ToString(), Palette.PoisonFloat, 12f);
                }
                if (hp <= 0f && !dead) { Die(game); return; }
            }

            if (cfg.boss)
            {
                if (bossDashing)
                {
                    x += bdvx * dt; y += bdvy * dt; bdvx *= 0.9f; bdvy *= 0.9f; dashTime -= dt;
                    if (dashTime <= 0f) bossDashing = false;
                }
                else if (windup > 0f)
                {
                    windup -= dt; x += dx * speed * 0.15f * dt; y += dy * speed * 0.15f * dt;
                    if (windup <= 0f) ExecuteAttack(game);
                }
                else
                {
                    x += dx * speed * slow * dt; y += dy * speed * slow * dt; attackCd -= dt;
                    if (attackCd <= 0f) { attackCd = Rng.Rand(2.6f, 3.8f); StartAttack(dx, dy); }
                }
            }
            else if (cfg.ranged)
            {
                const float want = 240f; float sp = speed * slow;
                if (d < want - 20f) { x -= dx * sp * dt; y -= dy * sp * dt; }
                else if (d > want + 40f) { x += dx * sp * dt; y += dy * sp * dt; }
                fireCd -= dt; if (fireCd <= 0f) { fireCd = Rng.Rand(1.6f, 2.6f); Shoot(game, dx, dy); }
            }
            else { x += dx * speed * slow * dt; y += dy * speed * slow * dt; }

            x = Rng.Clamp(x, r, game.w - r); y = Rng.Clamp(y, r, game.h - r);

            if (contactCd > 0f) contactCd -= dt;
            if (cfg.contact || cfg.boss || cfg.explode)
            {
                float rr = r + p.r;
                if (Rng.Dist2(x, y, p.x, p.y) < rr * rr && contactCd <= 0f)
                {
                    p.Hurt(dmg, game); contactCd = 0.5f;
                    if (p.HasMove("tailswipe")) Hurt(p.SpikeDamage(), game); // Tail Swipe reflect
                    if (cfg.explode) Die(game);
                }
            }
        }

        void Shoot(Game game, float dx, float dy)
        {
            const float sp = 210f;
            game.AddProjectile(x, y, dx * sp, dy * sp, false, 8f * (maxHp / 26f), Palette.SpitterShot, 6f, 4f);
        }
    }
}
