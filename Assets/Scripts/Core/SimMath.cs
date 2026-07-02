using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Bit-deterministic math for the SIMULATION — required for cross-platform (iOS ↔ Android) lockstep.
    /// Platform math libraries implement sin/cos/atan/pow differently, so Mathf's transcendentals can
    /// differ in the last bits between an iPhone build and an Android build → lockstep desync. These
    /// replacements use only + − × ÷ (IEEE-754, bit-identical on all our targets), written one operation
    /// per statement so compilers can't fuse multiply+add differently per platform.
    ///
    /// RULES:
    ///  • Gameplay code must use SimMath.Sin / Cos / Atan2 / PowInt.
    ///  • Mathf.Sqrt / Abs / Floor / Ceil / Round / Min / Max / Clamp stay safe everywhere (exact IEEE ops).
    ///  • Cosmetic code (particles, rendering, audio) may keep Mathf freely.
    ///
    /// Accuracy ≈ 1e-6 — indistinguishable in play, but note the determinism-checksum BASELINE changes
    /// versus the old Mathf-based sim (expected, one time).
    /// </summary>
    public static class SimMath
    {
        public const float PI = 3.14159265f;
        public const float TAU = 6.28318531f;
        public const float HALF_PI = 1.57079633f;
        const float INV_TAU = 0.159154943f;

        // sin kernel — single-precision minimax coefficients on [-π/2, π/2]
        const float S3 = -1.6666654611e-1f;
        const float S5 = 8.3321608736e-3f;
        const float S7 = -1.9515295891e-4f;

        public static float Sin(float x)
        {
            // range-reduce to [-π, π)
            float t = x + PI;
            t = t * INV_TAU;
            float k = Mathf.Floor(t);
            float kt = k * TAU;
            float r = x - kt;
            // fold into [-π/2, π/2] (sin is symmetric about ±π/2)
            if (r > HALF_PI) r = PI - r;
            else if (r < -HALF_PI) r = -PI - r;
            // odd polynomial: sin(r) ≈ r + r·(S3·r² + S5·r⁴ + S7·r⁶)
            float r2 = r * r;
            float p = S7 * r2;
            p = p + S5;
            p = p * r2;
            p = p + S3;
            p = p * r2;
            p = p * r;
            return r + p;
        }

        public static float Cos(float x) => Sin(x + HALF_PI);

        // atan kernel — single-precision minimax coefficients on [0, 1]
        const float A1 = 0.99997726f;
        const float A3 = -0.33262347f;
        const float A5 = 0.19354346f;
        const float A7 = -0.11643287f;
        const float A9 = 0.05265332f;
        const float A11 = -0.01172120f;

        public static float Atan2(float y, float x)
        {
            if (x == 0f && y == 0f) return 0f;
            float ay = y < 0f ? -y : y;
            float ax = x < 0f ? -x : x;
            bool steep = ay > ax;                    // octant fold so the ratio is in [0, 1]
            float z = steep ? ax / ay : ay / ax;
            float z2 = z * z;
            float p = A11 * z2;
            p = p + A9;
            p = p * z2;
            p = p + A7;
            p = p * z2;
            p = p + A5;
            p = p * z2;
            p = p + A3;
            p = p * z2;
            p = p + A1;
            float a = p * z;
            if (steep) a = HALF_PI - a;
            if (x < 0f) a = PI - a;
            return y < 0f ? -a : a;
        }

        /// <summary>Deterministic b^n for small non-negative integer exponents (stack counts).</summary>
        public static float PowInt(float b, int n)
        {
            float r = 1f;
            for (int i = 0; i < n; i++) r = r * b;
            return r;
        }
    }
}
