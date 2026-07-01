using UnityEngine;

namespace Mutagen
{
    /// <summary>Transient laser beam line (Laser Eyes). Ported from the prototype's beams array.</summary>
    public class Beam
    {
        public float x1, y1, x2, y2, life;
        public Color color;
        public BeamView view;

        public Beam Set(float x1, float y1, float x2, float y2, Color color)
        {
            this.x1 = x1; this.y1 = y1; this.x2 = x2; this.y2 = y2;
            this.color = color; life = 0.18f;
            return this;
        }

        public bool Dead => life <= 0f;
        public float Alpha => Rng.Clamp(life / 0.18f, 0f, 1f);
    }
}
