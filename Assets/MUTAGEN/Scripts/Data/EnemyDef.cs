using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// One enemy archetype, inspector-editable. Mirrors a row of the prototype's ENEMY_TYPES.
    /// </summary>
    [CreateAssetMenu(menuName = "MUTAGEN/Enemy", fileName = "Enemy")]
    public class EnemyDef : ScriptableObject
    {
        public string id;
        public string displayName;
        public Color color = Color.red;
        public float r;        // radius (world units == prototype px)
        public float hp;
        public float speed;
        public float dmg;
        public int xp;

        [Header("Behaviour")]
        public bool contact;   // damages on touch
        public bool ranged;    // keeps distance and shoots
        public bool explode;   // detonates on contact/death (Bloater)
        public bool boss;      // APEX: ring-shot pattern, contact, big drops
    }
}
