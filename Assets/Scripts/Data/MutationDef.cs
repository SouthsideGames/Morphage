using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// One mutation, as an inspector-editable asset. Mirrors a row of the prototype's
    /// MutationManager.defs. The stat-changing effect lives in MutationEffects.Apply()
    /// keyed on <see cref="id"/> — closures can't be serialized into an asset.
    /// </summary>
    [CreateAssetMenu(menuName = "MUTAGEN/Mutation", fileName = "Mutation")]
    public class MutationDef : ScriptableObject
    {
        public string id;
        [Tooltip("Stable draft order — keeps seeded runs reproducible regardless of asset load order.")]
        public int order;
        public string displayName;
        public Color color = Color.magenta;
        [Tooltip("common | rare | legendary — drives draft weighting and the card label.")]
        public string rarity = "common";
        [Tooltip("If set, this draft option is a MOVE (granted to a loadout slot, keys 1-4); empty = passive MODIFIER.")]
        public string move = "";
        public bool active;            // (legacy v0.3 flag; unused in v0.4)
        public bool repeatable;
        public int maxStacks;          // 0 = unlimited (only meaningful when repeatable)
        [TextArea] public string description;
        [Tooltip("Shown when this mutation evolves at max stacks (empty = no evolution).")]
        public string evolveName;
        public Sprite icon;            // optional; UI falls back to a colour swatch
    }
}
