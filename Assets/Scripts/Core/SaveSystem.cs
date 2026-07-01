using System.IO;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Minimal JSON persistence to <c>Application.persistentDataPath</c>. Currently the monster
    /// archive; this is the shared foundation for future meta-progression / settings saves too.
    /// </summary>
    public static class SaveSystem
    {
        const int MaxMonsters = 50;
        static string MonsterPath => Path.Combine(Application.persistentDataPath, "morphage_monsters.json");

        public static MonsterCollection LoadMonsters()
        {
            try
            {
                if (File.Exists(MonsterPath))
                {
                    var c = JsonUtility.FromJson<MonsterCollection>(File.ReadAllText(MonsterPath));
                    if (c?.monsters != null) return c;
                }
            }
            catch (System.Exception e) { Debug.LogWarning($"[MUTAGEN] Load monsters failed: {e.Message}"); }
            return new MonsterCollection();
        }

        public static void SaveMonsters(MonsterCollection c)
        {
            try
            {
                if (c.monsters.Count > MaxMonsters)
                    c.monsters.RemoveRange(MaxMonsters, c.monsters.Count - MaxMonsters);
                File.WriteAllText(MonsterPath, JsonUtility.ToJson(c));
            }
            catch (System.Exception e) { Debug.LogWarning($"[MUTAGEN] Save monsters failed: {e.Message}"); }
        }

        /// <summary>Append a new monster (newest first) and persist.</summary>
        public static void AddMonster(MonsterRecord rec)
        {
            var c = LoadMonsters();
            c.monsters.Insert(0, rec);
            SaveMonsters(c);
        }
    }
}
