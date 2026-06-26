using UnityEngine;

namespace Mutagen
{
    /// <summary>Bullet for both player and enemies. 1:1 with the prototype Projectile.</summary>
    public class Projectile
    {
        public float x, y, vx, vy, r, damage, life, poison;
        public Color color;
        public bool playerOwned;
        public bool dead;
        public SpriteView view;

        public Projectile Set(float x, float y, float vx, float vy,
            bool playerOwned, float damage, Color color, float r = 5f, float life = 3f, float poison = 0f)
        {
            this.x = x; this.y = y; this.vx = vx; this.vy = vy;
            this.playerOwned = playerOwned; this.damage = damage; this.color = color;
            this.r = r; this.life = life; this.poison = poison; dead = false;
            return this;
        }

        public void Update(float dt, float w, float h)
        {
            x += vx * dt; y += vy * dt; life -= dt;
            if (life <= 0f) dead = true;
            if (x < -30f || x > w + 30f || y < -30f || y > h + 30f) dead = true;
        }
    }
}
