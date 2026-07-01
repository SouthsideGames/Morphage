using UnityEngine;

namespace Mutagen
{
    /// <summary>DNA pickup with magnet pull toward the player. 1:1 with the prototype DNAOrb.</summary>
    public class DNAOrb
    {
        public float x, y, value, r, t, vx, vy;
        public bool dead;
        public SpriteView view;

        const float TAU = Mathf.PI * 2f;

        public DNAOrb Set(float x, float y, float value)
        {
            this.x = x; this.y = y; this.value = value; r = 5f; dead = false;
            t = Rng.Rand(0f, TAU); vx = Rng.Rand(-40f, 40f); vy = Rng.Rand(-40f, 40f);
            return this;
        }

        public void Update(float dt, Player player)
        {
            t += dt * 4f; vx *= 0.9f; vy *= 0.9f; x += vx * dt; y += vy * dt;
            float range = player.dnaRange, d2 = Rng.Dist2(x, y, player.x, player.y);
            if (d2 < range * range)
            {
                float d = Mathf.Sqrt(d2); if (d == 0f) d = 1f;
                float pull = Rng.Lerp(380f, 60f, Rng.Clamp(d / range, 0f, 1f));
                x += (player.x - x) / d * pull * dt;
                y += (player.y - y) / d * pull * dt;
            }
            float rr = player.r + 8f;
            if (d2 < rr * rr) dead = true;
        }

        public float PulseR => r * (1f + Mathf.Sin(t) * 0.18f);
    }
}
