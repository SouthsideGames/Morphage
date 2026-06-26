using UnityEngine;
using UnityEngine.InputSystem;

namespace Mutagen
{
    /// <summary>
    /// Input for one frame (v0.4), driven by rebindable <see cref="Binds"/>. Movement is a Vector2
    /// (arrow keys always work as a fallback). Keys 1-4 fire move slots / pick cards. Shift = dash.
    /// <see cref="CapturedKey"/> reports any key pressed this frame (used by the rebind UI).
    /// </summary>
    public class InputReader
    {
        public Vector2 MoveVec;
        public bool DashPressed, Reroll, Pause;
        public bool Move1, Move2, Move3, Move4;
        public bool ToggleDebug, ToggleMute, StartPressed;
        public Key CapturedKey;

        const float Dead = 0.3f;

        static bool Held(Keyboard kb, Key k) => kb != null && k != Key.None && kb[k].isPressed;
        static bool Pressed(Keyboard kb, Key k) => kb != null && k != Key.None && kb[k].wasPressedThisFrame;

        public void Poll()
        {
            var kb = Keyboard.current;
            var gp = Gamepad.current;

            CapturedKey = Key.None;
            if (kb != null)
                foreach (var key in kb.allKeys)
                    if (key.wasPressedThisFrame) { CapturedKey = key.keyCode; break; }

            Vector2 mv = Vector2.zero;
            float x = ((Held(kb, Binds.Get("right")) || Held(kb, Key.RightArrow)) ? 1f : 0f)
                    - ((Held(kb, Binds.Get("left")) || Held(kb, Key.LeftArrow)) ? 1f : 0f);
            float y = ((Held(kb, Binds.Get("up")) || Held(kb, Key.UpArrow)) ? 1f : 0f)
                    - ((Held(kb, Binds.Get("down")) || Held(kb, Key.DownArrow)) ? 1f : 0f);
            if (x != 0f || y != 0f) mv = new Vector2(x, y);
            if (gp != null) { Vector2 s = gp.leftStick.ReadValue(); if (s.sqrMagnitude > Dead * Dead) mv = s; }
            if (TouchInput.Active) mv = TouchInput.Move; // on-screen joystick overrides on touch devices
            MoveVec = mv;

            Move1 = Pressed(kb, Binds.Get("move1"));
            Move2 = Pressed(kb, Binds.Get("move2"));
            Move3 = Pressed(kb, Binds.Get("move3"));
            Move4 = Pressed(kb, Binds.Get("move4"));
            if (gp != null)
            {
                if (gp.buttonSouth.wasPressedThisFrame) Move1 = true;
                if (gp.buttonEast.wasPressedThisFrame) Move2 = true;
                if (gp.buttonWest.wasPressedThisFrame) Move3 = true;
                if (gp.buttonNorth.wasPressedThisFrame) Move4 = true;
            }

            Key dashKey = Binds.Get("dash");
            DashPressed = Pressed(kb, dashKey)
                || (Binds.IsShift(dashKey) && (Pressed(kb, Key.LeftShift) || Pressed(kb, Key.RightShift)))
                || (gp != null && gp.leftShoulder.wasPressedThisFrame);
            Reroll = Pressed(kb, Binds.Get("reroll"));
            Pause = (kb != null && kb.escapeKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
            ToggleDebug = Pressed(kb, Binds.Get("debug"));
            ToggleMute = Pressed(kb, Binds.Get("mute"));
            StartPressed = (kb != null && kb.enterKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame);
        }
    }
}
