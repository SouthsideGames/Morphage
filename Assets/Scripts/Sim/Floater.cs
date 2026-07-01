using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Floating damage number. Ported from the prototype Floater.
    /// NOTE: simulation runs in Unity's y-up space, so the prototype's vertical signs are
    /// flipped (rise = +y, gravity = -y). Callers spawn at y + r for "above the head".
    /// </summary>
    public class Floater
    {
        public float x, y, vx, vy, life, max, size;
        public string text;
        public Color color;
        public LabelView view;

        public Floater Set(float x, float y, string text, Color color, float size)
        {
            this.x = x; this.y = y; this.text = text; this.color = color; this.size = size;
            life = 0.7f; max = 0.7f; vy = 52f; vx = Rng.Rand(-16f, 16f);
            return this;
        }

        public void Update(float dt)
        {
            y += vy * dt; vy -= 140f * dt; x += vx * dt; life -= dt;
        }

        public bool Dead => life <= 0f;
        public float Alpha => Rng.Clamp(life / max, 0f, 1f);
    }
}
