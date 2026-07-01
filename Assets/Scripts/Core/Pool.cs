using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Minimal GameObject component pool. Avoids per-frame Instantiate/Destroy GC spikes
    /// for projectiles, particles, floaters, orbs and enemy views (carryover decision).
    /// </summary>
    public class Pool<T> where T : Component
    {
        readonly Func<T> _create;
        readonly Stack<T> _free = new();

        public Pool(Func<T> create, int prewarm = 0)
        {
            _create = create;
            for (int i = 0; i < prewarm; i++) Release(_create());
        }

        public T Get()
        {
            T t = _free.Count > 0 ? _free.Pop() : _create();
            t.gameObject.SetActive(true);
            return t;
        }

        public void Release(T t)
        {
            if (t == null) return;
            t.gameObject.SetActive(false);
            _free.Push(t);
        }
    }
}
