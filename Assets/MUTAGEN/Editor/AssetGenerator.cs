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
        const string MutDir = "Assets/MUTAGEN/Resources/Mutations";
        const string EnemyDir = "Assets/MUTAGEN/Resources/Enemies";
        const string UiDir = "Assets/MUTAGEN/Resources/UI";

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
            // ---- MODIFIERS ----
            new object[]{ "arms",       8, "+2 Arms",        "#ffb86b", "common",    "", true, 4, "MODIFIER: extra limbs. All move cooldowns −12% per stack.", "Flailing Mass — Barrage & spreads fire extra shots" },
            new object[]{ "regen",      9, "Regeneration",   "#7cf0c4", "common",    "", true, 5, "MODIFIER: heal over time. +3 HP/sec.", "Symbiosis — regen surges, max HP grows" },
            new object[]{ "thick",     10, "Thick Hide",     "#4ad6a0", "common",    "", true, 5, "MODIFIER: max HP +40 and full heal. Buffs Tail Swipe reflect.", "Carapace — take 30% less damage" },
            new object[]{ "wings",     11, "Wings",          "#7cf0c4", "common",    "", true, 4, "MODIFIER: move speed +22%. Each stack shortens dash cooldown.", "Stormborn — dash cooldown halved" },
            new object[]{ "magnet",    12, "Magnet Sense",   "#4ad6ff", "common",    "", true, 4, "MODIFIER: DNA pickup range +80%.", "Apex Sense — DNA worth +25% XP" },
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
