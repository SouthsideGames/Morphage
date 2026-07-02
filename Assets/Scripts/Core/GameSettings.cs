using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Player options persisted via PlayerPrefs and applied to Sfx / Haptics / the sim.
    /// Load() runs once at startup (after Sfx.Init); the Set* helpers write-through so
    /// changes stick between sessions.
    /// </summary>
    public static class GameSettings
    {
        public static bool ScreenShake = true;    // read by Game.Shake
        public static bool ReducedEffects = false; // fewer cosmetic particles (perf)
        public static bool LeftHanded = false;     // mirror touch controls (joystick right, moves left)

        public static void Load()
        {
            // Only force-mute if the player disabled sound; leaving the default 'on' avoids
            // kicking off menu music here (the Sfx.Enabled setter starts music when set true).
            if (PlayerPrefs.GetInt("opt_sound", 1) == 0) Sfx.Enabled = false;
            Sfx.SetMaster(PlayerPrefs.GetFloat("opt_master", 0.7f));
            Sfx.SetMusic(PlayerPrefs.GetFloat("opt_music", 0.5f));
            Sfx.SetSfx(PlayerPrefs.GetFloat("opt_sfx", 0.8f));
            Haptics.Enabled = PlayerPrefs.GetInt("opt_haptics", 1) == 1;
            ScreenShake = PlayerPrefs.GetInt("opt_shake", 1) == 1;
            ReducedEffects = PlayerPrefs.GetInt("opt_reducefx", 0) == 1;
            LeftHanded = PlayerPrefs.GetInt("opt_lefthand", 0) == 1;
            ApplyEffects();
        }

        // Scale cosmetic particle density off the platform baseline (Game sets ParticleBase).
        public static void ApplyEffects() => Game.ParticleScale = Game.ParticleBase * (ReducedEffects ? 0.4f : 1f);

        public static void SetSound(bool v)  { Sfx.Enabled = v; PlayerPrefs.SetInt("opt_sound", v ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetMaster(float v) { Sfx.SetMaster(v); PlayerPrefs.SetFloat("opt_master", v); PlayerPrefs.Save(); }
        public static void SetMusic(float v)  { Sfx.SetMusic(v); PlayerPrefs.SetFloat("opt_music", v); PlayerPrefs.Save(); }
        public static void SetSfx(float v)    { Sfx.SetSfx(v); PlayerPrefs.SetFloat("opt_sfx", v); PlayerPrefs.Save(); }
        public static void SetHaptics(bool v) { Haptics.Enabled = v; PlayerPrefs.SetInt("opt_haptics", v ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetShake(bool v)   { ScreenShake = v; PlayerPrefs.SetInt("opt_shake", v ? 1 : 0); PlayerPrefs.Save(); }
        public static void SetReducedEffects(bool v) { ReducedEffects = v; PlayerPrefs.SetInt("opt_reducefx", v ? 1 : 0); PlayerPrefs.Save(); ApplyEffects(); }
        public static void SetLeftHanded(bool v)     { LeftHanded = v; PlayerPrefs.SetInt("opt_lefthand", v ? 1 : 0); PlayerPrefs.Save(); }
    }
}
