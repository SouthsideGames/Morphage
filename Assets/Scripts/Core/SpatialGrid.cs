using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Uniform grid over the arena for enemy spatial queries — replaces the prototype's
    /// O(n) nearestEnemy / O(n*m) projectile loops. Rebuilt every fixed sim step.
    /// </summary>
    public class SpatialGrid
    {
        readonly float _cell;
        readonly int _cols, _rows;
        readonly float _w, _h;
        readonly List<Enemy>[] _cells;

        public SpatialGrid(float w, float h, float cell = 80f)
        {
            _w = w; _h = h; _cell = cell;
            _cols = Mathf.Max(1, Mathf.CeilToInt(w / cell));
            _rows = Mathf.Max(1, Mathf.CeilToInt(h / cell));
            _cells = new List<Enemy>[_cols * _rows];
            for (int i = 0; i < _cells.Length; i++) _cells[i] = new List<Enemy>(8);
        }

        int Cx(float x) => Mathf.Clamp(Mathf.FloorToInt(x / _cell), 0, _cols - 1);
        int Cy(float y) => Mathf.Clamp(Mathf.FloorToInt(y / _cell), 0, _rows - 1);

        public void Clear()
        {
            for (int i = 0; i < _cells.Length; i++) _cells[i].Clear();
        }

        public void Insert(Enemy e)
        {
            _cells[Cy(e.y) * _cols + Cx(e.x)].Add(e);
        }

        public void Rebuild(List<Enemy> enemies)
        {
            Clear();
            for (int i = 0; i < enemies.Count; i++) Insert(enemies[i]);
        }

        /// <summary>Nearest live enemy within range, or null. Mirrors Game.nearestEnemy.</summary>
        public Enemy Nearest(float x, float y, float range)
        {
            Enemy best = null;
            float bd = range * range;
            int minX = Cx(x - range), maxX = Cx(x + range);
            int minY = Cy(y - range), maxY = Cy(y + range);
            for (int gy = minY; gy <= maxY; gy++)
            for (int gx = minX; gx <= maxX; gx++)
            {
                var list = _cells[gy * _cols + gx];
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (e.dead) continue;
                    float d = Rng.Dist2(x, y, e.x, e.y);
                    if (d < bd) { bd = d; best = e; }
                }
            }
            return best;
        }

        /// <summary>Append enemies whose centre is within <paramref name="radius"/> into <paramref name="result"/>.</summary>
        public void QueryCircle(float x, float y, float radius, List<Enemy> result)
        {
            float r2 = radius * radius;
            int minX = Cx(x - radius), maxX = Cx(x + radius);
            int minY = Cy(y - radius), maxY = Cy(y + radius);
            for (int gy = minY; gy <= maxY; gy++)
            for (int gx = minX; gx <= maxX; gx++)
            {
                var list = _cells[gy * _cols + gx];
                for (int i = 0; i < list.Count; i++)
                {
                    var e = list[i];
                    if (!e.dead && Rng.Dist2(x, y, e.x, e.y) <= r2) result.Add(e);
                }
            }
        }
    }
}
