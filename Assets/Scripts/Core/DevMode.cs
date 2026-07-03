using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Gates developer tools (debug panel, Balance Lab) on devices. Always available in the editor;
    /// on a device it must be armed with the "special call": type <see cref="Code"/> into the Game
    /// Mode seed field and press Done. Persists across restarts; type the code again to disarm.
    /// </summary>
    public static class DevMode
    {
        public const string Code = "BellaLove"; // the secret call — change to anything you like (matched case-insensitively)
        const string Key = "dev_mode";

        // Gated everywhere (editor included) behind the secret call so the editor matches devices.
        // The flag persists in PlayerPrefs, so you only type the code once per install.
        public static bool Enabled => PlayerPrefs.GetInt(Key, 0) == 1;

        /// <summary>Flip the device flag. Returns the new state.</summary>
        public static bool Toggle()
        {
            bool on = PlayerPrefs.GetInt(Key, 0) == 1;
            PlayerPrefs.SetInt(Key, on ? 0 : 1);
            PlayerPrefs.Save();
            return !on;
        }
    }
}
