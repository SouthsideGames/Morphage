using UnityEngine;
using UnityEngine.Rendering;

namespace Mutagen
{
    /// <summary>
    /// Renders a saved monster's genome into a RenderTexture by reusing <see cref="PlayerVisual"/>
    /// with a dedicated off-screen orthographic camera parked far from the arena. Renders on demand
    /// (the camera is disabled and driven via URP render requests), so many portraits can be produced
    /// sequentially from one rig. Callers own the returned RenderTexture and must release it.
    /// </summary>
    public class MonsterPortrait
    {
        static MonsterPortrait _inst;
        public static MonsterPortrait Instance => _inst ??= new MonsterPortrait();

        const float FarX = 100000f, FarY = 100000f; // parked well outside the arena view
        Camera _cam;
        PlayerVisual _visual;
        Player _p;

        void EnsureRig(Game game)
        {
            if (_cam != null) return;
            var root = new GameObject("MonsterPortraitRig");
            UnityEngine.Object.DontDestroyOnLoad(root);
            root.transform.position = new Vector3(FarX, FarY, 0f);

            var camGo = new GameObject("PortraitCamera");
            camGo.transform.SetParent(root.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = 42f;              // frames body + limbs/quills/glow
            _cam.transform.localPosition = new Vector3(0f, 0f, -10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.03f, 0.06f, 0.05f, 1f); // consistent dark backdrop
            _cam.cullingMask = ~0;
            _cam.enabled = false;                     // rendered only via SubmitRenderRequest

            _visual = PlayerVisual.Create(root.transform);
            _p = new Player(game);
        }

        public RenderTexture Render(Game game, MonsterRecord rec, int size)
        {
            EnsureRig(game);

            // Rebuild the throwaway player from the record so PlayerVisual reads the right state.
            for (int i = 0; i < 4; i++)
                _p.moveSlots[i] = (rec.moveSlots != null && i < rec.moveSlots.Length && !string.IsNullOrEmpty(rec.moveSlots[i]))
                    ? rec.moveSlots[i] : null;
            _p.mutations.Clear();
            if (rec.mutIds != null)
                for (int i = 0; i < rec.mutIds.Length; i++)
                    _p.mutations[rec.mutIds[i]] = (rec.mutStacks != null && i < rec.mutStacks.Length) ? rec.mutStacks[i] : 1;
            _p.maxHp = rec.maxHp; _p.hp = rec.maxHp;
            _p.r = rec.radius <= 0f ? 16f : rec.radius;
            _p.hurtFlash = 0f; _p.invuln = 0f;
            _p.facingX = 1f; _p.facingY = 0f;
            _p.x = FarX; _p.y = FarY;

            _visual.Sync(_p, true);

            var rt = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32);
            var req = new RenderPipeline.StandardRequest { destination = rt };
            if (RenderPipeline.SupportsRenderRequest(_cam, req))
                RenderPipeline.SubmitRenderRequest(_cam, req);
            else { _cam.targetTexture = rt; _cam.Render(); _cam.targetTexture = null; } // built-in fallback

            _visual.Sync(_p, false); // hide the rig again until the next portrait
            return rt;
        }
    }
}
