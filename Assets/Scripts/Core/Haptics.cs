using UnityEngine;
using Lofelt.NiceVibrations;

namespace Mutagen
{
    /// <summary>
    /// Thin wrapper over Nice Vibrations (bundled with Feel). Mobile-only; no-ops in editor/desktop.
    /// Kept tasteful — fired on impactful moments, not every enemy hit.
    /// </summary>
    public static class Haptics
    {
        public static bool Enabled = true;

        static void Play(HapticPatterns.PresetType p)
        {
            if (!Enabled || !Application.isMobilePlatform) return;
            try { HapticPatterns.PlayPreset(p); } catch { /* device without haptics */ }
        }

        public static void Light()     => Play(HapticPatterns.PresetType.LightImpact);
        public static void Medium()    => Play(HapticPatterns.PresetType.MediumImpact);
        public static void Heavy()     => Play(HapticPatterns.PresetType.HeavyImpact);
        public static void Success()   => Play(HapticPatterns.PresetType.Success);
        public static void Selection() => Play(HapticPatterns.PresetType.Selection);
    }
}
