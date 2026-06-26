using System.Text.RegularExpressions;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Seeded RNG ported 1:1 from the prototype (mulberry32 + FNV-1a string hash).
    /// All gameplay randomness routes through here so seeded runs are reproducible.
    /// Prototype reference: mulberry32 / hashStr / seedToInt / RNG in mutant-arena.html.
    /// </summary>
    public static class Rng
    {
        static uint _state;

        public static void Set(uint seed) => _state = seed;

        // mulberry32 — bit-for-bit match with the JS version (uint arithmetic == Math.imul truncation).
        public static float Next()
        {
            _state += 0x6D2B79F5u;
            uint a = _state;
            uint t = (a ^ (a >> 15)) * (1u | a);
            t = (t + ((t ^ (t >> 7)) * (61u | t))) ^ t;
            return (float)((t ^ (t >> 14)) / 4294967296.0);
        }

        public static uint HashStr(string s)
        {
            uint h = 2166136261u;
            for (int i = 0; i < s.Length; i++)
            {
                h ^= s[i];
                h *= 16777619u;
            }
            return h;
        }

        public static uint SeedToInt(string t)
        {
            if (Regex.IsMatch(t, @"^\d+$") && double.TryParse(t, out double d))
                return unchecked((uint)((long)(d % 4294967296.0)));
            return HashStr(t);
        }

        // ---- helpers mirroring the prototype's rand/randi/dist2/clamp/lerp ----
        public static float Rand(float a, float b) => a + Next() * (b - a);
        public static int RandI(int a, int b) => Mathf.FloorToInt(Rand(a, b + 1));
        public static float Dist2(float ax, float ay, float bx, float by)
        {
            float dx = ax - bx, dy = ay - by;
            return dx * dx + dy * dy;
        }
        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }
}
