using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// First-run onboarding: a short chain of contextual prompts during the first solo run
    /// (move → attack → dash → collect DNA → send-off), each completed by DOING the action.
    /// Purely observational — it reads player actions and never touches the sim, so it cannot
    /// affect determinism, seeded replays, or co-op. Shown once per device (PlayerPrefs).
    /// </summary>
    public class Tutorial
    {
        const string DoneKey = "tut_done";

        public bool Active { get; private set; }

        int _step;                 // 0 move · 1 attack · 2 dash · 3 collect · 4 outro
        float _moveHeld, _outro;
        bool _moved, _attacked, _dashed, _collected;
        bool _touch;

        public void BeginIfFirstRun(bool touch)
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) { Active = false; return; }
            _touch = touch;
            _step = 0; _moveHeld = 0f; _outro = 0f;
            _moved = _attacked = _dashed = _collected = false;
            Active = true;
        }

        public void Stop() => Active = false;

        // ---- action hooks (safe to call from anywhere; ignored when inactive) ----
        public void OnMoved(float dt) { if (Active) { _moveHeld += dt; if (_moveHeld > 0.7f) _moved = true; } }
        public void OnMoveUsed() { if (Active) _attacked = true; }
        public void OnDashed() { if (Active) _dashed = true; }
        public void OnOrbCollected() { if (Active) _collected = true; }

        /// <summary>Advance and return the current prompt (null = show nothing). Call once per rendered frame.</summary>
        public string Tick(float dt)
        {
            if (!Active) return null;
            if (_step == 0 && _moved) _step = 1;
            if (_step == 1 && _attacked) _step = 2;
            if (_step == 2 && _dashed) _step = 3;
            if (_step == 3 && _collected) { _step = 4; _outro = 3.5f; }
            if (_step == 4)
            {
                _outro -= dt;
                if (_outro <= 0f)
                {
                    Active = false;
                    PlayerPrefs.SetInt(DoneKey, 1); PlayerPrefs.Save();
                    return null;
                }
                return "Survive the waves. Evolve. Become monstrous.";
            }
            switch (_step)
            {
                case 0: return _touch ? "Drag on the left side of the screen to move" : "Move with W A S D";
                case 1: return _touch ? "Tap the glowing button to attack" : "Press 1 to attack";
                case 2: return _touch ? "Tap the glowing button to dash" : "Press SHIFT to dash";
                case 3: return "Grab the DNA your prey drops";
                default: return null;
            }
        }

        /// <summary>Move-bar slot to pulse for the current step (-1 none, 0-3 = moves, 4 = dash).</summary>
        public int HighlightSlot => !Active ? -1 : _step == 1 ? 0 : _step == 2 ? 4 : -1;
    }
}
