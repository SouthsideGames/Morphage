using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Non-seeded randomness for purely cosmetic effects (particle scatter, floater jitter,
    /// animation phase). Kept OFF the seeded stream so decorative emission can't desync
    /// run composition / draft order between seeded runs. Gameplay draws use <see cref="Rng"/>.
    /// </summary>
    public static class Fx
    {
        public static float Rand(float a, float b) => UnityEngine.Random.Range(a, b);
        public static bool Chance(float p) => UnityEngine.Random.value < p;
        public static Color Pick(Color[] arr) => arr[UnityEngine.Random.Range(0, arr.Length)];

        // Cached small-int strings — damage floaters fire on every hit, so avoid per-hit ToString() garbage.
        static readonly string[] _nums = BuildNums();
        static string[] BuildNums() { var a = new string[1000]; for (int i = 0; i < 1000; i++) a[i] = i.ToString(); return a; }
        public static string Num(float v) { int i = (int)(v + 0.5f); return (i >= 0 && i < 1000) ? _nums[i] : i.ToString(); }
    }
}
