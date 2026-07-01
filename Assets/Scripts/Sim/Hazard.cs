using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// A lingering ground zone that affects enemies inside its radius until its life runs out:
    /// ticks damage (every 0.25s), and optionally pulls toward its centre, slows, and applies poison.
    /// Powers Acid Pool / Gravity Well / Inferno. Pooled like <see cref="Projectile"/>.
    /// </summary>
    public class Hazard
    {
        public float x, y, r, dps, life, pull, slowMul;
        public bool poison, dead;
        public Color color;
        public SpriteView view;
        float _tick;

        public Hazard Set(float x, float y, float r, float dps, float life, float pull, float slowMul, bool poison, Color color)
        {
            this.x = x; this.y = y; this.r = r; this.dps = dps; this.life = life;
            this.pull = pull; this.slowMul = slowMul; this.poison = poison; this.color = color;
            dead = false; _tick = 0f;
            return this;
        }

        public void Update(float dt, Game game)
        {
            life -= dt;
            if (life <= 0f) { dead = true; return; }
            _tick -= dt;
            bool hit = _tick <= 0f; if (hit) _tick = 0.25f;
            var es = game.enemies;
            for (int i = 0; i < es.Count; i++)
            {
                var e = es[i]; if (e.dead) continue;
                float dx = e.x - x, dy = e.y - y, rr = r + e.r;
                if (dx * dx + dy * dy > rr * rr) continue;
                if (pull != 0f)
                {
                    float d = Mathf.Sqrt(dx * dx + dy * dy); if (d < 1f) d = 1f;
                    float k = Mathf.Min(d, 60f) * pull * dt;
                    e.x -= dx / d * k; e.y -= dy / d * k;
                }
                if (hit)
                {
                    e.Hurt(dps * 0.25f, game, 0f, 0f, true); // silent: no per-tick floater/Sfx spam
                    if (poison) e.ApplyPoison(dps);
                    if (slowMul < 1f) e.ApplySlow(slowMul, 0.4f);
                }
            }
        }

        public float Alpha => Mathf.Clamp01(life * 0.7f); // soft fade as it expires
    }
}
