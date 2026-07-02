using UnityEngine;

namespace Mutagen.Net
{
    /// <summary>
    /// The complete input for one player on one simulation tick, quantized down to a tiny wire
    /// packet (4 bytes + the tick number). Both devices round-trip their LOCAL input through this
    /// before feeding the sim, so movement quantizes bit-for-bit identically on every device — a
    /// hard requirement for deterministic lockstep co-op. See docs/MULTIPLAYER.md §5.
    /// </summary>
    public struct TickInput
    {
        public int tick;
        public sbyte moveX, moveY;  // MoveVec.x / .y * 127, clamped to [-127, 127]
        public byte bits;           // bit0..3 = Move1..4 held this tick, bit4 = Dash
        public byte draft;          // level-up choice this tick (see Draft* constants below); 0 = none

        // ---- draft byte encoding (the only thing that travels for a level-up under Option B) ----
        public const byte DraftNone = 0;
        public const byte DraftPick1 = 1, DraftPick2 = 2, DraftPick3 = 3; // choose drafted option 1/2/3
        public const byte DraftReroll = 4;
        public const byte DraftReplace1 = 5, DraftReplace4 = 8;           // overwrite move slot 1..4
        public const byte DraftDiscard = 9;                              // decline the new move (loadout full)

        const byte BMove1 = 1, BMove2 = 2, BMove3 = 4, BMove4 = 8, BDash = 16;

        public bool Move(int slot) => (bits & (1 << slot)) != 0; // slot 0..3
        public bool Dash => (bits & BDash) != 0;
        public Vector2 MoveVec => new Vector2(moveX / 127f, moveY / 127f);

        /// <summary>Quantize a movement axis to a signed byte (the identical rounding on every device).</summary>
        public static sbyte Q(float v) => (sbyte)Mathf.Clamp(Mathf.RoundToInt(v * 127f), -127, 127);

        /// <summary>Pack this frame's local input into a packet.</summary>
        public static TickInput Local(int tick, Vector2 move, bool m1, bool m2, bool m3, bool m4, bool dash, byte draft = DraftNone)
        {
            byte bits = 0;
            if (m1) bits |= BMove1;
            if (m2) bits |= BMove2;
            if (m3) bits |= BMove3;
            if (m4) bits |= BMove4;
            if (dash) bits |= BDash;
            return new TickInput { tick = tick, moveX = Q(move.x), moveY = Q(move.y), bits = bits, draft = draft };
        }
    }
}
