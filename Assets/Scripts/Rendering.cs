using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Builds the runtime-generated circle sprite (no PNG assets) and the pooled view objects.
    /// Rendering is intentionally simple: the prototype's procedural body parts (arms, wings,
    /// spikes, laser eyes) are cosmetic and deferred — see README "Deviations".
    /// ponytail: generated sprites instead of imported art; swap in real sprites later.
    /// </summary>
    public static class SpriteFactory
    {
        static Sprite _circle;
        static Sprite _box;
        static Font _font;

        const int TEX = 64;

        public static Sprite Circle => _circle ??= MakeCircle();
        public static Sprite Box => _box ??= MakeBox();
        public static Font Font => _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        static Sprite MakeCircle()
        {
            var tex = new Texture2D(TEX, TEX, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = (TEX - 1) * 0.5f, rad = TEX * 0.5f;
            var px = new Color32[TEX * TEX];
            for (int y = 0; y < TEX; y++)
            for (int x = 0; x < TEX; x++)
            {
                float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
                float a = Mathf.Clamp01((rad - d) / 1.5f); // soft 1.5px edge
                px[y * TEX + x] = new Color(1, 1, 1, a);
            }
            tex.SetPixels32(px);
            tex.Apply();
            // PPU = TEX so the sprite is exactly 1 world unit in diameter; scale by 2*r to get radius r.
            return Sprite.Create(tex, new Rect(0, 0, TEX, TEX), new Vector2(0.5f, 0.5f), TEX);
        }

        /// <summary>Bakes the prototype's drawFloor look (radial gradient + grid + border) into one sprite.</summary>
        public static Sprite MakeFloor(int w, int h)
        {
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            float cx = w * 0.5f, cy = h * 0.5f, maxR = w * 0.7f;
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float t = Mathf.Clamp01(Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) / maxR);
                Color c = Color.Lerp(Palette.Floor, Palette.FloorEdge, t);
                if (x % 48 == 0 || y % 48 == 0) c = Color.Lerp(c, Palette.GridLine, 0.5f); // faint grid
                if (x < 2 || x >= w - 2 || y < 2 || y >= h - 2) c = Color.Lerp(c, Palette.Border, 0.7f);
                c.a = 1f;
                px[y * w + x] = c;
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 1f); // PPU 1 => w x h units
        }

        static Sprite MakeBox()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }

        public static SpriteView CreateSpriteView(Transform parent)
        {
            var go = new GameObject("View");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<SpriteView>();

            v.glow = AddSprite(go.transform, Circle, -1);
            v.body = AddSprite(go.transform, Circle, 0);
            v.barBg = AddSprite(go.transform, Box, 2);
            v.barFill = AddSprite(go.transform, Box, 3);
            v.barBg.gameObject.SetActive(false);
            v.barFill.gameObject.SetActive(false);
            return v;
        }

        // Lightweight view for decorative particles: a single SpriteRenderer (no glow/bars).
        // Particles are the most numerous on-screen object, so this is ¼ the renderers of a SpriteView.
        public static ParticleView CreateParticleView(Transform parent)
        {
            var go = new GameObject("P");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = Circle;
            sr.sortingOrder = 0;
            var v = go.AddComponent<ParticleView>();
            v.sr = sr;
            return v;
        }

        public static LabelView CreateLabel(Transform parent)
        {
            var go = new GameObject("Floater");
            go.transform.SetParent(parent, false);
            var tm = go.AddComponent<TextMesh>();
            tm.font = Font;
            tm.fontSize = 48;
            tm.characterSize = 0.32f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = Font.material;
            mr.sortingOrder = 50;
            var v = go.AddComponent<LabelView>();
            v.text = tm;
            return v;
        }

        public static BeamView CreateBeam(Transform parent)
        {
            var go = new GameObject("Beam");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<BeamView>();
            v.core = AddSprite(go.transform, Box, 40);
            v.glow = AddSprite(go.transform, Box, 39);
            return v;
        }

        static SpriteRenderer AddSprite(Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject("S");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }
    }

    /// <summary>A pooled circle view: body + soft glow halo + optional health bar.</summary>
    public class SpriteView : MonoBehaviour
    {
        public SpriteRenderer body, glow, barBg, barFill;

        /// <param name="radius">world-unit radius</param>
        public void Set(float x, float y, float radius, Color color, float glowAlpha = 0.35f)
        {
            transform.position = new Vector3(x, y, 0f);
            float d = radius * 2f;
            body.transform.localScale = new Vector3(d, d, 1f);
            body.color = color;
            glow.transform.localScale = new Vector3(d * 1.7f, d * 1.7f, 1f);
            glow.color = new Color(color.r, color.g, color.b, glowAlpha);
        }

        public void ShowBar(float frac, float radius, Color fill)
        {
            barBg.gameObject.SetActive(true);
            barFill.gameObject.SetActive(true);
            float w = radius * 2.1f, h = 4f, yOff = radius + 12f;
            barBg.transform.localPosition = new Vector3(0, yOff, 0);
            barBg.transform.localScale = new Vector3(w, h, 1);
            barBg.color = new Color(0, 0, 0, 0.5f);
            frac = Mathf.Clamp01(frac);
            barFill.transform.localPosition = new Vector3(-w * 0.5f + w * frac * 0.5f, yOff, 0);
            barFill.transform.localScale = new Vector3(w * frac, h, 1);
            barFill.color = fill;
        }

        public void HideBar()
        {
            if (barBg.gameObject.activeSelf) barBg.gameObject.SetActive(false);
            if (barFill.gameObject.activeSelf) barFill.gameObject.SetActive(false);
        }
    }

    /// <summary>One-renderer particle view (body only). Set scales by radius like SpriteView.</summary>
    public class ParticleView : MonoBehaviour
    {
        public SpriteRenderer sr;
        public void Set(float x, float y, float radius, Color color)
        {
            transform.position = new Vector3(x, y, 0f);
            float d = radius * 2f;
            transform.localScale = new Vector3(d, d, 1f);
            sr.color = color;
        }
    }

    public class LabelView : MonoBehaviour
    {
        public TextMesh text;
        public void Set(float x, float y, string s, Color color, float size, float alpha)
        {
            transform.position = new Vector3(x, y, -1f);
            text.text = s;
            text.characterSize = size * 0.02f;
            var c = color; c.a = alpha; text.color = c;
        }
    }

    public class BeamView : MonoBehaviour
    {
        public SpriteRenderer core, glow;
        public void Set(float x1, float y1, float x2, float y2, Color color, float alpha)
        {
            float dx = x2 - x1, dy = y2 - y1;
            float len = Mathf.Sqrt(dx * dx + dy * dy);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
            transform.position = new Vector3((x1 + x2) * 0.5f, (y1 + y2) * 0.5f, -0.5f);
            transform.rotation = Quaternion.Euler(0, 0, ang);
            glow.transform.localScale = new Vector3(len, 6f, 1f);
            glow.color = new Color(color.r, color.g, color.b, alpha);
            core.transform.localScale = new Vector3(len, 2f, 1f);
            core.color = new Color(1, 1, 1, alpha);
        }
    }
}
