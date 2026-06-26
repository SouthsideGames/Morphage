using UnityEngine;
using UnityEngine.InputSystem;

namespace Mutagen
{
    /// <summary>
    /// Bridge between the native UI Toolkit touch joystick and InputReader.
    /// Active = a touch device (or forced for editor testing) with a run in progress.
    /// Move = joystick output in game space (x right, y up), magnitude 0..1.
    /// </summary>
    public static class TouchInput
    {
        public static bool ForceTouch;          // editor toggle (test joystick with mouse)
        public static bool Active;              // set per-frame by the Game (touch device && playing)
        public static Vector2 Move;            // joystick vector, game space

        // UnityEngine.Device.* reflects the Device Simulator's simulated platform (the global
        // UnityEngine.Application does NOT) — so the simulator gets the real mobile layout too.
        public static bool IsTouchDevice =>
            ForceTouch || UnityEngine.Device.Application.isMobilePlatform
            || UnityEngine.Device.SystemInfo.deviceType == DeviceType.Handheld
            || Touchscreen.current != null;
    }
}
