using UnityEngine;

namespace Mutagen
{
    /// <summary>Named colours from the prototype's CSS / inline hex literals.</summary>
    public static class Palette
    {
        static Color H(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }

        public static readonly Color Dna        = H("#5ff5b0");
        public static readonly Color Mut        = H("#ff3d9a");
        public static readonly Color Xp         = H("#7cf0c4");
        public static readonly Color Hp         = H("#ff5a6e");
        public static readonly Color Ink        = H("#dff5ee");

        public static readonly Color PlayerShot = H("#9dff5c");
        public static readonly Color CloneShot  = H("#7cf0c4");
        public static readonly Color Poison     = H("#9dff5c");
        public static readonly Color Laser      = H("#5ff5b0");

        public static readonly Color SpitterShot= H("#4ad6ff");
        public static readonly Color BossShot   = H("#ff7a7a");

        public static readonly Color FloaterBig = H("#ffd24a");
        public static readonly Color FloaterWhite = Color.white;
        public static readonly Color PoisonFloat= H("#9dff5c");
        public static readonly Color HurtRed    = H("#ff5a6e");
        public static readonly Color Regen      = H("#7cf0c4");

        // Fire breath particle colours (prototype picks one at random per particle).
        public static readonly Color[] Fire = { H("#ffd24a"), H("#ff8a3c"), H("#ff5a2a") };

        // v0.4 move colours.
        public static readonly Color Barrage = H("#ffd24a");
        public static readonly Color Quake   = H("#caa14a");

        // Entity body / glow colours.
        public static readonly Color PlayerBody  = H("#dff5ee");
        public static readonly Color PlayerThick = H("#8fe6c0");
        public static readonly Color FireGlow    = H("#ff7a3c");
        public static readonly Color CloneBody   = H("#bdeedd");
        public static readonly Color BossBar     = H("#ff3d3d");
        public static readonly Color EnemyBar    = H("#ff8a5c");

        // Floor.
        public static readonly Color Floor = H("#0e211d");
        public static readonly Color Abyss = H("#0b1a18");
        public static readonly Color FloorEdge = H("#07110f");
        public static readonly Color GridLine = new(95f/255f, 245f/255f, 176f/255f, 0.045f);
        public static readonly Color Border   = new(95f/255f, 245f/255f, 176f/255f, 0.18f);

        // Creature features (from the prototype player.draw).
        public static readonly Color LaserEye = H("#ff3d6a");
        public static readonly Color DarkEye  = H("#06140f");
        public static readonly Color Arm      = H("#ffb86b");
        public static readonly Color Wing     = new(124f/255f, 240f/255f, 196f/255f, 0.55f);
        public static readonly Color Spike    = H("#ff9ccb");
        public static readonly Color ThickRim = H("#4ad6a0");
        public static readonly Color PoisonAura = new(157f/255f, 255f/255f, 92f/255f, 0.35f);
        public static readonly Color Quill    = H("#ff9ccb"); // radial spines (quills) + forward ground-spikes
        public static readonly Color Shell    = H("#9ad6c0"); // carapace plating
        public static readonly Color Stinger  = H("#ffd24a"); // tail barb

        // Skin-tier re-skins (body tint per mutation) + frost detailing.
        public static readonly Color ToxicSkin    = H("#9dff5c");
        public static readonly Color Vital        = H("#7cf0a0");
        public static readonly Color BulkTone     = H("#9c8cff");
        public static readonly Color GlassTint    = H("#ff8a99");
        public static readonly Color FrostRim     = H("#4ad6ff");
        public static readonly Color FrostCrystal = H("#bfeaff");

        // Accent-tier orbiting motes (one per abstract stat trait).
        public static readonly Color AccentPower   = H("#ff8a3c");
        public static readonly Color AccentCrit    = H("#ffd24a");
        public static readonly Color AccentVampire = H("#c23a52");
        public static readonly Color AccentHaste   = H("#4ad6ff");
        public static readonly Color AccentReflex  = H("#6ba8ff");
        public static readonly Color AccentRevive  = H("#ffe08a");
        public static readonly Color AccentCarn    = H("#ff6a5c");

        // Idle-tell nubs for equipped attack moves that lack a bespoke feature (def colours).
        public static readonly Color MoveBite     = H("#c9d8d0");
        public static readonly Color MoveScreech  = H("#b8c6ff");
        public static readonly Color MoveTentacle = H("#cf6fa6");
        public static readonly Color MoveGravity  = H("#9b8cff");
    }
}
