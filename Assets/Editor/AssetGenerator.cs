using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Mutagen.EditorTools
{
    /// <summary>
    /// Generates every data + UI asset the runtime loads, from the canonical tables below.
    /// This is the PORT-SYNC POINT: when the HTML prototype's numbers change, edit these
    /// tables and re-run "MUTAGEN → Generate Assets". Numbers mirror mutant-arena.html v0.2.
    /// </summary>
    public static class AssetGenerator
    {
        // Live Resources layout (the project was reorganized out of the old Assets/MUTAGEN/ tree).
        const string MutDir = "Assets/Resources/Mutations";
        const string EnemyDir = "Assets/Resources/Enemies";
        const string UiDir = "Assets/Resources/UI";

        // id, order, name, colorHex, rarity, move, repeatable, maxStacks, description, evolveName
        // move != "" => offensive MOVE (loadout slot); "" => passive MODIFIER. (v0.4)
        static readonly object[][] Mutations =
        {
            // ---- MOVES ----
            new object[]{ "firebreath", 0, "Fire Breath",    "#ff8a3c", "rare",      "firebreath", true, 3, "MOVE: cone of flame ahead. Burns. Synergy w/ Venom: hits poisoned foes harder.", "Wildfire — a wider, fiercer cone" },
            new object[]{ "laser",      1, "Laser Eyes",     "#ff3d6a", "legendary", "laser",      true, 3, "MOVE: piercing beam at nearest foe. Synergy w/ +2 Arms: faster cooldowns.", "Death Ray — devastating beam" },
            new object[]{ "venom",      2, "Venom Spit",     "#9dff5c", "rare",      "venom",      true, 3, "MOVE: a glob that applies corrosive damage-over-time.", "Necrosis — poisoned enemies are slowed" },
            new object[]{ "tailswipe",  3, "Tail Swipe",     "#ff9ccb", "rare",      "tailswipe",  true, 3, "MOVE: 360° tail slam with knockback. Also reflects contact damage.", "Bramble Lord — bigger sweep, harder reflect" },
            new object[]{ "barrage",    4, "Barrage",        "#ffd24a", "common",    "barrage",    true, 3, "MOVE: spray of pellets at the nearest foe. More pellets per level.", "Suppression — a wall of pellets" },
            new object[]{ "charge",     5, "Charge",         "#7cf0c4", "common",    "charge",     true, 3, "MOVE: ram forward, damaging everything you hit. Brief invulnerability.", "Juggernaut — a longer, deadlier ram" },
            new object[]{ "quake",      6, "Quake",          "#caa14a", "rare",      "quake",      true, 3, "MOVE: brief wind-up, then a heavy shockwave around you.", "Cataclysm — a wider, crushing quake" },
            new object[]{ "mitosis",    7, "Split Into Two", "#bdeedd", "rare",      "mitosis",    true, 3, "MOVE: split off temporary clones that orbit and fire for you.", "Hive Mind — more, sturdier clones" },
            new object[]{ "spore",     20, "Spore Burst",    "#9dff5c", "common",    "spore",      true, 3, "MOVE: bursts a 360° ring of spores around you.", "Bloom — a denser, wider ring" },
            new object[]{ "stinger",   21, "Stinger Volley", "#ffd24a", "common",    "stinger",    true, 3, "MOVE: a wide volley of barbs at the nearest foe.", "Hailstorm — more barbs, faster" },
            new object[]{ "spikes",    22, "Ground Spikes",  "#ff9ccb", "rare",      "spikes",     true, 3, "MOVE: erupts a line of spikes straight ahead.", "Impaler — longer, wider spikes" },
            new object[]{ "frost",     23, "Frost Breath",   "#4ad6ff", "rare",      "frost",      true, 3, "MOVE: a chilling cone that damages and slows.", "Permafrost — a wider, deeper freeze" },
            new object[]{ "screech",   24, "Sonic Screech",  "#b8c6ff", "rare",      "screech",    true, 3, "MOVE: a shockwave that knocks back and slows.", "Resonance — a far wider scream" },
            new object[]{ "lightning", 25, "Lightning Arc",  "#4ad6ff", "rare",      "lightning",  true, 3, "MOVE: a bolt that arcs between nearby foes.", "Tesla — more arcs, longer reach" },
            new object[]{ "tentacle",  26, "Tentacle Lash",  "#cf6fa6", "common",    "tentacle",   true, 3, "MOVE: lash ahead, dragging foes toward you.", "Maw — a longer, stronger pull" },
            new object[]{ "gravity",   27, "Gravity Pulse",  "#9b8cff", "rare",      "gravity",    true, 3, "MOVE: implodes nearby foes inward, crushing them.", "Singularity — a wider, deadlier collapse" },
            new object[]{ "acid",      38, "Acid Pool",      "#9dff5c", "rare",      "acid",       true, 3, "MOVE: splashes a lingering acid pool that melts and poisons foes.", "Caustic Mire — a wider, deadlier pool" },
            new object[]{ "well",      39, "Gravity Well",   "#9b8cff", "rare",      "well",       true, 3, "MOVE: opens a vortex that drags foes in and grinds them down.", "Singularity — a stronger, wider pull" },
            new object[]{ "inferno",   40, "Inferno",        "#ff8a3c", "rare",      "inferno",    true, 3, "MOVE: ignites a blazing zone that burns everything inside.", "Firestorm — a vast, raging blaze" },
            // ---- MODIFIERS ----
            new object[]{ "arms",       8, "+2 Arms",        "#ffb86b", "common",    "", true, 4, "MODIFIER: extra limbs. All move cooldowns −12% per stack.", "Flailing Mass — Barrage & spreads fire extra shots" },
            new object[]{ "regen",      9, "Regeneration",   "#7cf0c4", "common",    "", true, 5, "MODIFIER: heal over time. +3 HP/sec.", "Symbiosis — regen surges, max HP grows" },
            new object[]{ "thick",     10, "Thick Hide",     "#4ad6a0", "common",    "", true, 5, "MODIFIER: max HP +40 and full heal. Buffs Tail Swipe reflect.", "Carapace — take 30% less damage" },
            new object[]{ "wings",     11, "Wings",          "#7cf0c4", "common",    "", true, 4, "MODIFIER: move speed +22%. Each stack shortens dash cooldown.", "Stormborn — dash cooldown halved" },
            new object[]{ "magnet",    12, "Magnet Sense",   "#4ad6ff", "common",    "", true, 4, "MODIFIER: DNA pickup range +80%.", "Apex Sense — DNA worth +25% XP" },
            new object[]{ "power",     13, "Mutagen Surge",  "#ff6b9d", "rare",      "", true, 5, "MODIFIER: raw damage +2 per stack — boosts every move.", "Overdrive — a final surge of power" },
            new object[]{ "carapace",  14, "Carapace",       "#9ad6c0", "common",    "", true, 4, "MODIFIER: take 6% less damage per stack.", "Living Shell — an even tougher hide" },
            new object[]{ "vitality",  15, "Vitality",       "#7cf0a0", "common",    "", true, 3, "MODIFIER: max HP +20% and full heal.", "Hardy Cells — regenerate faster" },
            new object[]{ "reflexes",  16, "Reflexes",       "#7cf0c4", "common",    "", true, 4, "MODIFIER: dash cooldown −15% per stack.", "Blink — dash recharges far quicker" },
            new object[]{ "scavenger", 17, "Scavenger",      "#ffd24a", "common",    "", true, 4, "MODIFIER: DNA & XP gain +15% per stack.", "Apex Forager — pickups travel from afar" },
            new object[]{ "toxicskin", 18, "Toxic Skin",     "#9dff5c", "rare",      "", true, 4, "MODIFIER: +4 poison damage. Synergy w/ Venom & Laser.", "Virulent — far more potent toxins" },
            new object[]{ "glasscannon",19,"Glass Cannon",   "#ff5a6e", "legendary", "", false, 1, "MODIFIER: +6 damage, but max HP −20%. High risk.", "" },
            new object[]{ "crit",      30, "Unstable Cells", "#ffd24a", "rare",      "", true, 4, "MODIFIER: +8% chance to land a 2× critical hit per stack.", "Meltdown — critical hits strike even harder" },
            new object[]{ "vampire",   31, "Vampirism",      "#ff5a6e", "rare",      "", true, 4, "MODIFIER: heal 6% of the damage you deal per stack.", "Bloodthirst — drain even more life" },
            new object[]{ "haste",     32, "Adrenal Glands", "#7cf0c4", "common",    "", true, 4, "MODIFIER: move cooldowns −10% per stack.", "Overclock — even faster reflexes" },
            new object[]{ "bulk",      33, "Bulk",           "#9b8cff", "common",    "", true, 3, "MODIFIER: +30 max HP and a bigger body — but a bigger target.", "Behemoth — a tougher hide too" },
            new object[]{ "swift",     34, "Swift Fins",     "#7cf0c4", "common",    "", true, 3, "MODIFIER: move speed +12% per stack.", "Blur — speed plus quicker dashes" },
            new object[]{ "revive",    35, "Second Wind",    "#ffd24a", "legendary", "", false, 1, "MODIFIER: cheat death once, reviving at 50% HP.", "" },
            new object[]{ "quills",    36, "Quills",         "#ff9ccb", "common",    "", true, 3, "MODIFIER: grow spines that reflect contact damage.", "Spineback — far sharper spines" },
            new object[]{ "carnivore", 37, "Carnivore",      "#ff5a6e", "rare",      "", true, 4, "MODIFIER: heal 2 HP per kill per stack.", "Devour — feast harder on kills" },
        };

        // id, name, colorHex, r, hp, speed, dmg, xp, contact, ranged, explode, boss
        static readonly object[][] Enemies =
        {
            new object[]{ "chaser",   "Chaser",   "#ff8a5c", 14f, 22f,  78f,  8f,  3, true,  false, false, false },
            new object[]{ "fast",     "Sprinter", "#ffd24a", 10f, 12f,  150f, 6f,  3, true,  false, false, false },
            new object[]{ "tank",     "Brute",    "#9b8cff", 22f, 80f,  42f,  14f, 7, true,  false, false, false },
            new object[]{ "spitter",  "Spitter",  "#4ad6ff", 13f, 26f,  55f,  0f,  5, false, true,  false, false },
            new object[]{ "exploder", "Bloater",  "#ff5d8a", 16f, 20f,  88f,  6f,  5, false, false, true,  false },
            new object[]{ "boss",     "APEX",     "#ff3d3d", 46f, 900f, 46f,  24f, 60, false, false, false, true },
        };

        [MenuItem("MUTAGEN/Generate Assets")]
        public static void Generate()
        {
            EnsureDir(MutDir); EnsureDir(EnemyDir); EnsureDir(UiDir);
            ClearAssets(MutDir); ClearAssets(EnemyDir); // wipe stale defs (ids change between versions)

            foreach (var m in Mutations)
            {
                var d = ScriptableObject.CreateInstance<MutationDef>();
                d.id = (string)m[0]; d.order = (int)m[1]; d.displayName = (string)m[2];
                d.color = Hex((string)m[3]); d.rarity = (string)m[4]; d.move = (string)m[5];
                d.repeatable = (bool)m[6]; d.maxStacks = (int)m[7];
                d.description = (string)m[8]; d.evolveName = (string)m[9];
                Recreate($"{MutDir}/{d.id}.asset", d);
            }

            foreach (var e in Enemies)
            {
                var d = ScriptableObject.CreateInstance<EnemyDef>();
                d.id = (string)e[0]; d.displayName = (string)e[1]; d.color = Hex((string)e[2]);
                d.r = (float)e[3]; d.hp = (float)e[4]; d.speed = (float)e[5]; d.dmg = (float)e[6]; d.xp = (int)e[7];
                d.contact = (bool)e[8]; d.ranged = (bool)e[9]; d.explode = (bool)e[10]; d.boss = (bool)e[11];
                Recreate($"{EnemyDir}/{d.id}.asset", d);
            }

            GeneratePanel();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[MUTAGEN] Generated mutation/enemy data + UI PanelSettings. Press Play.");
        }

        static void GeneratePanel()
        {
            // Default runtime theme as a .tss source file, then a PanelSettings that references it.
            string tssPath = $"{UiDir}/MutagenTheme.tss";
            if (!File.Exists(tssPath))
            {
                File.WriteAllText(tssPath, "@import url(\"unity-theme://default\");\n");
                AssetDatabase.ImportAsset(tssPath);
            }
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(tssPath);

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.themeStyleSheet = theme;
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1024, 576); // smaller ref => larger on-screen UI (runtime also overrides this)
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
            Recreate($"{UiDir}/MutagenPanel.asset", ps);
        }

        static void Recreate(string path, Object asset)
        {
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(asset, path);
        }

        static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }

        static void ClearAssets(string dir)
        {
            foreach (var f in Directory.GetFiles(dir, "*.asset")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
        }

        static Color Hex(string s) { ColorUtility.TryParseHtmlString(s, out var c); return c; }
    }
}
