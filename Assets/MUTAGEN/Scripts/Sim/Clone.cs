using UnityEngine;

namespace Mutagen
{
    /// <summary>Orbiting clone from the Mitosis move. v0.4: temporary (life-limited) + buffed by Hive Mind.</summary>
    public class Clone
    {
        public Player player;
        public float angle, x, y, atkCd;
        public float life = -1f;   // -1 = permanent; mitosis sets a timer
        public bool dead;
        public SpriteView view;

        public float r => player.clonesBuffed ? 12f : 9f;

        public Clone(Player player, float angle)
        {
            this.player = player; this.angle = angle; x = player.x; y = player.y; atkCd = 0f;
        }

        public void Update(float dt, Game game)
        {
            if (life >= 0f) { life -= dt; if (life <= 0f) { dead = true; return; } }
            angle += dt * 1.4f;
            const float orbit = 46f;
            float tx = player.x + Mathf.Cos(angle) * orbit, ty = player.y + Mathf.Sin(angle) * orbit;
            x = Rng.Lerp(x, tx, Rng.Clamp(dt * 6f, 0f, 1f));
            y = Rng.Lerp(y, ty, Rng.Clamp(dt * 6f, 0f, 1f));
            atkCd -= dt;
            var t = game.NearestEnemy(x, y, 160f);
            if (t != null && atkCd <= 0f)
            {
                atkCd = player.clonesBuffed ? 0.6f : 0.85f;
                float dx = t.x - x, dy = t.y - y, d = Mathf.Sqrt(dx * dx + dy * dy); if (d == 0f) d = 1f;
                const float sp = 480f;
                float dmg = player.atkDmg * (player.clonesBuffed ? 0.6f : 0.45f);
                game.AddProjectile(x, y, dx / d * sp, dy / d * sp, true, dmg, Palette.CloneShot, 4f, 1f, player.poisonDps);
            }
        }
    }
}
