using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Procedural creature rendering for the player, ported from the prototype's player.draw:
    /// body + glow, poison aura, thick rim, eyes (laser-tinted), arms (per +2 Arms stack),
    /// wings, spiked tail. The root rotates to the facing direction (features are symmetric).
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        SpriteRenderer _wingL, _wingR, _glow, _aura, _thickRim, _spike, _body, _eyeL, _eyeR, _invuln;
        SpriteRenderer[] _arms; // 4 stacks x 2 sides

        public static PlayerVisual Create(Transform parent)
        {
            var go = new GameObject("PlayerVisual");
            go.transform.SetParent(parent, false);
            var v = go.AddComponent<PlayerVisual>();
            v._wingL = SR(go.transform, SpriteFactory.Circle, -3);
            v._wingR = SR(go.transform, SpriteFactory.Circle, -3);
            v._glow = SR(go.transform, SpriteFactory.Circle, -2);
            v._aura = SR(go.transform, SpriteFactory.Circle, -2);
            v._spike = SR(go.transform, SpriteFactory.Box, -1);
            v._thickRim = SR(go.transform, SpriteFactory.Circle, -1);
            v._body = SR(go.transform, SpriteFactory.Circle, 0);
            v._arms = new SpriteRenderer[8];
            for (int i = 0; i < 8; i++) v._arms[i] = SR(go.transform, SpriteFactory.Box, 1);
            v._eyeL = SR(go.transform, SpriteFactory.Circle, 2);
            v._eyeR = SR(go.transform, SpriteFactory.Circle, 2);
            v._invuln = SR(go.transform, SpriteFactory.Circle, -4); // dash i-frame shield (behind)
            return v;
        }

        static SpriteRenderer SR(Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject("part");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        public void Sync(Player p, bool show)
        {
            if (gameObject.activeSelf != show) gameObject.SetActive(show);
            if (!show || p == null) return;

            transform.position = new Vector3(p.x, p.y, 0f);
            transform.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(p.facingY, p.facingX) * Mathf.Rad2Deg);
            float r = p.r;

            bool flash = p.hurtFlash > 0f;
            Color bodyCol = flash ? Palette.HurtRed : (p.Has("thick") ? Palette.PlayerThick : Palette.PlayerBody);
            Circle(_body, 0, 0, r, bodyCol);

            Color g = p.HasMove("firebreath") ? Palette.FireGlow : Palette.Dna;
            Circle(_glow, 0, 0, r * 1.7f, new Color(g.r, g.g, g.b, 0.5f));

            Toggle(_aura, p.HasMove("venom")); if (p.HasMove("venom")) Circle(_aura, 0, 0, r * 0.7f, Palette.PoisonAura);
            Toggle(_thickRim, p.Has("thick")); if (p.Has("thick")) Circle(_thickRim, 0, 0, r + 2.5f, Palette.ThickRim);

            Color eye = p.HasMove("laser") ? Palette.LaserEye : Palette.DarkEye;
            Circle(_eyeL, r * 0.45f, -4f, 3.2f, eye);
            Circle(_eyeR, r * 0.45f, 4f, 3.2f, eye);

            int arms = p.Stacks("arms");
            for (int s = 0; s < 4; s++)
            {
                float off = 6f + s * 6f;
                for (int side = 0; side < 2; side++)
                {
                    var a = _arms[s * 2 + side];
                    bool on = s < arms;
                    Toggle(a, on);
                    if (!on) continue;
                    float sgn = side == 0 ? -1f : 1f;
                    Line(a, new Vector2(2f, sgn * off * 0.4f), new Vector2(r + 10f, sgn * (off + 6f)), 4f, Palette.Arm);
                }
            }

            bool wings = p.Has("wings");
            Toggle(_wingL, wings); Toggle(_wingR, wings);
            if (wings)
            {
                Ellipse(_wingL, new Vector2(-12f, -13f), 34f, 16f, -28f, Palette.Wing);
                Ellipse(_wingR, new Vector2(-12f, 13f), 34f, 16f, 28f, Palette.Wing);
            }

            bool spikes = p.HasMove("tailswipe");
            Toggle(_spike, spikes);
            if (spikes) Line(_spike, new Vector2(-r, 0f), new Vector2(-r - 20f, 0f), 6f, Palette.Spike);

            bool inv = p.invuln > 0f;
            Toggle(_invuln, inv);
            if (inv)
            {
                float a = 0.25f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 25f));
                Circle(_invuln, 0f, 0f, r + 6f, new Color(Palette.Xp.r, Palette.Xp.g, Palette.Xp.b, a));
            }
        }

        static void Toggle(SpriteRenderer sr, bool on) { if (sr.gameObject.activeSelf != on) sr.gameObject.SetActive(on); }

        static void Circle(SpriteRenderer sr, float lx, float ly, float radius, Color c)
        {
            sr.transform.localPosition = new Vector3(lx, ly, 0f);
            sr.transform.localRotation = Quaternion.identity;
            sr.transform.localScale = new Vector3(radius * 2f, radius * 2f, 1f);
            sr.color = c;
        }

        static void Ellipse(SpriteRenderer sr, Vector2 pos, float w, float h, float angle, Color c)
        {
            sr.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            sr.transform.localRotation = Quaternion.Euler(0, 0, angle);
            sr.transform.localScale = new Vector3(w, h, 1f);
            sr.color = c;
        }

        static void Line(SpriteRenderer sr, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 mid = (a + b) * 0.5f, d = b - a;
            float len = d.magnitude, ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            sr.transform.localPosition = new Vector3(mid.x, mid.y, 0f);
            sr.transform.localRotation = Quaternion.Euler(0, 0, ang);
            sr.transform.localScale = new Vector3(len, thickness, 1f);
            sr.color = c;
        }
    }
}
