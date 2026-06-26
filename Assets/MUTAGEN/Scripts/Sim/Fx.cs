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
    }
}
