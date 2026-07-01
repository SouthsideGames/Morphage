using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace Mutagen
{
    /// <summary>
    /// Rebindable keyboard controls (in-memory; reset on reload), ported from the prototype's
    /// DEFAULT_BINDS / BIND_DEFS. Movement arrow keys always work as a fallback (see InputReader).
    /// </summary>
    public static class Binds
    {
        public static readonly (string action, string label)[] Defs =
        {
            ("up", "Move Up"), ("down", "Move Down"), ("left", "Move Left"), ("right", "Move Right"),
            ("dash", "Dash / Dodge"), ("reroll", "Reroll Draft"),
            ("move1", "Move Slot 1"), ("move2", "Move Slot 2"), ("move3", "Move Slot 3"), ("move4", "Slot 4 / Discard"),
            ("mute", "Mute Sound"), ("debug", "Debug Panel"),
        };

        static readonly Dictionary<string, Key> Default = new()
        {
            { "up", Key.W }, { "down", Key.S }, { "left", Key.A }, { "right", Key.D },
            { "dash", Key.LeftShift }, { "reroll", Key.R },
            { "move1", Key.Digit1 }, { "move2", Key.Digit2 }, { "move3", Key.Digit3 }, { "move4", Key.Digit4 },
            { "mute", Key.M }, { "debug", Key.Backquote },
        };

        public static Dictionary<string, Key> Current = new(Default);
        public static string Rebinding; // action awaiting a key press, or null

        public static Key Get(string action) => Current.TryGetValue(action, out var k) ? k : Key.None;

        public static void Reset() { Current = new Dictionary<string, Key>(Default); Rebinding = null; }

        /// <summary>Assign a key to an action; if another action already uses it, swap.</summary>
        public static void Assign(string action, Key code)
        {
            Key old = Get(action);
            foreach (var a in new List<string>(Current.Keys))
                if (a != action && Current[a] == code) Current[a] = old;
            Current[action] = code;
        }

        public static string Label(Key k)
        {
            switch (k)
            {
                case Key.None: return "—";
                case Key.LeftShift: return "Shift"; case Key.RightShift: return "R Shift";
                case Key.LeftCtrl: return "Ctrl"; case Key.RightCtrl: return "R Ctrl";
                case Key.LeftAlt: return "Alt"; case Key.RightAlt: return "R Alt";
                case Key.Space: return "Space"; case Key.Backquote: return "`";
                case Key.Enter: return "Enter"; case Key.Escape: return "Esc"; case Key.Tab: return "Tab";
                case Key.UpArrow: return "↑"; case Key.DownArrow: return "↓"; case Key.LeftArrow: return "←"; case Key.RightArrow: return "→";
                case Key.Minus: return "-"; case Key.Equals: return "="; case Key.Comma: return ","; case Key.Period: return ".";
                case Key.Slash: return "/"; case Key.Semicolon: return ";"; case Key.Quote: return "'";
                case Key.LeftBracket: return "["; case Key.RightBracket: return "]"; case Key.Backslash: return "\\";
            }
            string s = k.ToString();
            if (s.StartsWith("Digit")) return s.Substring(5);
            if (s.StartsWith("Numpad")) return "Num" + s.Substring(6);
            return s; // letters report as "W", "R", etc.
        }

        public static bool IsShift(Key k) => k == Key.LeftShift || k == Key.RightShift;
    }
}
