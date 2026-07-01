using UnityEngine;

namespace Mutagen
{
    /// <summary>Decorative particle. 1:1 with the prototype Particle (per-step 0.92 friction).</summary>
    public class Particle
    {
        public float x, y, vx, vy, life, max, size;
        public Color color;
        public ParticleView view;

        public Particle Set(float x, float y, float vx, float vy, float life, Color color, float size)
        {
            this.x = x; this.y = y; this.vx = vx; this.vy = vy;
            this.life = life; this.max = life; this.color = color; this.size = size;
            return this;
        }

        public void Update(float dt)
        {
            x += vx * dt; y += vy * dt;
            vx *= 0.92f; vy *= 0.92f;
            life -= dt;
        }

        public bool Dead => life <= 0f;
        public float Alpha => Rng.Clamp(life / max, 0f, 1f);
    }
}
