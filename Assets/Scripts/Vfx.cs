using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Pooled Cartoon FX (JMO) one-shot effects. Each prefab key keeps a free-stack of reusable
    /// instances; finished effects are deactivated and returned instead of destroyed — so repeated
    /// hits/explosions don't churn Instantiate/Destroy GC. Decoupled from the JMO assembly: we
    /// neutralise each prefab's self-destruct by name (CFXR_Effect) + particle stop-action, so this
    /// compiles even without Cartoon FX installed.
    /// World units are sim pixels (arena ~960x600), so effects are scaled up from their authored size.
    /// </summary>
    public static class Vfx
    {
        static Transform _root;
        static readonly Dictionary<string, GameObject> _prefabs = new();
        static readonly Dictionary<string, Stack<VfxReturn>> _free = new();

        public static void Init(Transform root) { _root = root; _prefabs.Clear(); _free.Clear(); }

        /// <param name="key">prefab name under Resources/VFX (e.g. "Explosion")</param>
        /// <param name="scale">world-unit scale; CFXR prefabs are authored small, so scale up</param>
        public static void Spawn(string key, float x, float y, float scale = 24f)
        {
            if (_root == null) return;
            var inst = Acquire(key);
            if (inst == null) return;

            var t = inst.transform;
            t.position = new Vector3(x, y, 0f);
            t.localScale = Vector3.one * scale;
            inst.gameObject.SetActive(true);

            var ps = inst.GetComponent<ParticleSystem>();
            if (ps == null) ps = inst.GetComponentInChildren<ParticleSystem>(true);
            if (ps != null) { ps.Clear(true); ps.Play(true); }
            inst.Begin(4f); // return to the pool after the effect has finished
        }

        static VfxReturn Acquire(string key)
        {
            if (_free.TryGetValue(key, out var st))
                while (st.Count > 0) { var v = st.Pop(); if (v != null) return v; } // skip any destroyed

            if (!_prefabs.TryGetValue(key, out var prefab))
            {
                prefab = Resources.Load<GameObject>("VFX/" + key);
                _prefabs[key] = prefab; // cache misses too, so we don't reload every frame
            }
            if (prefab == null) return null;

            var go = Object.Instantiate(prefab, _root);
            // Stop the prefab from destroying/disabling itself so we can pool it.
            var cfxr = go.GetComponent("CFXR_Effect");
            if (cfxr is MonoBehaviour mb) Object.Destroy(mb);
            foreach (var p in go.GetComponentsInChildren<ParticleSystem>(true))
            { var main = p.main; main.stopAction = ParticleSystemStopAction.None; }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true)) r.sortingOrder = 30; // above bodies, below text

            var vr = go.AddComponent<VfxReturn>();
            vr.key = key;
            return vr;
        }

        internal static void Return(VfxReturn v)
        {
            if (v == null) return;
            v.gameObject.SetActive(false);
            if (!_free.TryGetValue(v.key, out var st)) { st = new Stack<VfxReturn>(); _free[v.key] = st; }
            st.Push(v);
        }
    }

    /// <summary>Per-instance lifetime timer: deactivates + returns the effect to its pool when done.
    /// Top-level (not nested) so runtime AddComponent works on every Unity version.</summary>
    public class VfxReturn : MonoBehaviour
    {
        public string key;
        float _t;
        public void Begin(float life) { _t = life; }
        void Update()
        {
            _t -= Time.unscaledDeltaTime;
            if (_t <= 0f) Vfx.Return(this);
        }
    }
}
