using UnityEngine;

namespace Mutagen
{
    /// <summary>
    /// Procedural creature rendering for the player. The root rotates to the facing direction.
    /// Mutation-driven visuals, in three tiers:
    ///  • Silhouette (outline geometry): arms, wings, tailswipe spike, quills, carapace shell,
    ///    stinger barb, ground-spikes.
    ///  • Skin (body re-skin): thick, toxicskin, vitality, bulk, glasscannon tints; frost rim + crystals;
    ///    firebreath glow; venom aura; laser eyes.
    ///  • Accent (orbiting motes): one colour-coded mote per abstract stat trait (see AccentIds).
    ///  • Move tells: a small organ nub per equipped attack move that has no bespoke feature (see MoveTellIds).
    /// Every mutation now leaves a persistent mark on the creature.
    /// </summary>
    public class PlayerVisual : MonoBehaviour
    {
        SpriteRenderer _wingL, _wingR, _glow, _aura, _thickRim, _spike, _body, _eyeL, _eyeR, _invuln;
        SpriteRenderer _shell, _stinger, _frostRim;  // carapace dome, stinger tail barb, icy rim
        SpriteRenderer[] _arms;            // 4 stacks x 2 sides
        SpriteRenderer[] _quills;          // radial spines (quills), density scales with stacks
        SpriteRenderer[] _fspikes;         // forward "ground spikes" eruption
        SpriteRenderer[] _frost;           // frost crystal shards
        SpriteRenderer[] _orbiters;        // accent-tier trait motes
        SpriteRenderer[] _moveTells;       // idle organ nubs for attack moves (≤4 slots)

        const int MaxQuills = 16;
        const int MaxFSpikes = 3;
        const int MaxFrost = 3;
        const int MaxOrbiters = 8;
        const int MaxMoveTells = 4;

        // Accent tier: each present modifier contributes one orbiting mote (parallel arrays).
        static readonly string[] AccentIds =
            { "regen", "magnet", "power", "crit", "vampire", "haste", "swift", "reflexes", "scavenger", "revive", "carnivore" };
        static readonly Color[] AccentCols =
            { Palette.Regen, Palette.Dna, Palette.AccentPower, Palette.AccentCrit, Palette.AccentVampire,
              Palette.AccentHaste, Palette.AccentHaste, Palette.AccentReflex, Palette.Dna, Palette.AccentRevive, Palette.AccentCarn };

        // Idle tell: attack moves lacking a bespoke feature get a small colour-coded organ nub.
        static readonly string[] MoveTellIds =
            { "bite", "barrage", "charge", "quake", "spore", "screech", "lightning", "tentacle", "gravity", "acid", "well", "inferno" };
        static readonly Color[] MoveTellCols =
            { Palette.MoveBite, Palette.Barrage, Palette.CloneShot, Palette.Quake, Palette.Poison, Palette.MoveScreech,
              Palette.SpitterShot, Palette.MoveTentacle, Palette.MoveGravity, Palette.Poison, Palette.MoveGravity, Palette.FireGlow };

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
            v._quills = new SpriteRenderer[MaxQuills];
            for (int i = 0; i < MaxQuills; i++) v._quills[i] = SR(go.transform, SpriteFactory.Box, -1);
            v._fspikes = new SpriteRenderer[MaxFSpikes];
            for (int i = 0; i < MaxFSpikes; i++) v._fspikes[i] = SR(go.transform, SpriteFactory.Box, -1);
            v._stinger = SR(go.transform, SpriteFactory.Box, -1);
            v._frostRim = SR(go.transform, SpriteFactory.Circle, -1);
            v._frost = new SpriteRenderer[MaxFrost];
            for (int i = 0; i < MaxFrost; i++) v._frost[i] = SR(go.transform, SpriteFactory.Box, -1);
            v._orbiters = new SpriteRenderer[MaxOrbiters];
            for (int i = 0; i < MaxOrbiters; i++) v._orbiters[i] = SR(go.transform, SpriteFactory.Circle, 3); // above body/eyes
            v._moveTells = new SpriteRenderer[MaxMoveTells];
            for (int i = 0; i < MaxMoveTells; i++) v._moveTells[i] = SR(go.transform, SpriteFactory.Circle, 2);
            v._shell = SR(go.transform, SpriteFactory.Circle, 1); // plating over the body, under eyes
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
            // Skin tier: start from thick/base, layer re-skin tints, then flash/alpha override.
            Color bodyCol = p.Has("thick") ? Palette.PlayerThick : Palette.PlayerBody;
            int tox = p.Stacks("toxicskin");
            if (tox > 0) bodyCol = Color.Lerp(bodyCol, Palette.ToxicSkin, Mathf.Min(0.55f, 0.22f + 0.11f * (tox - 1)));
            int vit = p.Stacks("vitality");
            if (vit > 0) bodyCol = Color.Lerp(bodyCol, Palette.Vital, Mathf.Min(0.40f, 0.16f * vit));
            int blk = p.Stacks("bulk");
            if (blk > 0) bodyCol = Color.Lerp(bodyCol, Palette.BulkTone, Mathf.Min(0.40f, 0.14f * blk));
            float bodyA = 1f;
            if (p.Has("glasscannon")) { bodyCol = Color.Lerp(bodyCol, Palette.GlassTint, 0.18f); bodyA = 0.72f; } // brittle, translucent
            if (flash) { bodyCol = Palette.HurtRed; bodyA = 1f; }
            bodyCol.a = bodyA;
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

            // Carapace — dorsal shell plating over the back; grows + darkens per stack.
            int shellStacks = p.Stacks("carapace");
            Toggle(_shell, shellStacks > 0);
            if (shellStacks > 0)
            {
                float grow = 1f + Mathf.Min(shellStacks, 4) * 0.06f;
                float alpha = 0.5f + Mathf.Min(shellStacks, 4) * 0.09f;
                var sc = Palette.Shell;
                Ellipse(_shell, new Vector2(-r * 0.35f, 0f), r * 1.5f * grow, r * 1.25f * grow, 0f,
                        new Color(sc.r, sc.g, sc.b, alpha));
            }

            // Quills — radial spines; count + length scale with stacks (max 3).
            int qStacks = p.Stacks("quills");
            int qCount = qStacks > 0 ? Mathf.Min(MaxQuills, 8 + (qStacks - 1) * 4) : 0;
            float qLen = 8f + (qStacks - 1) * 2f;
            for (int i = 0; i < MaxQuills; i++)
            {
                bool on = i < qCount;
                Toggle(_quills[i], on);
                if (!on) continue;
                float ang = i / (float)qCount * Mathf.PI * 2f;
                float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                Line(_quills[i], new Vector2(cx * r * 0.85f, cy * r * 0.85f),
                                 new Vector2(cx * (r + qLen), cy * (r + qLen)), 3f, Palette.Quill);
            }

            // Stinger — amber tail barb (distinct from the tailswipe spike).
            bool sting = p.HasMove("stinger");
            Toggle(_stinger, sting);
            if (sting) Line(_stinger, new Vector2(-r * 0.9f, -3f), new Vector2(-r - 14f, -9f), 3f, Palette.Stinger);

            // Ground Spikes — a row of forward eruption spikes (the leading edge).
            bool fspikes = p.HasMove("spikes");
            for (int i = 0; i < MaxFSpikes; i++)
            {
                Toggle(_fspikes[i], fspikes);
                if (!fspikes) continue;
                float yy = (i - 1) * 5f; // -5, 0, 5
                Line(_fspikes[i], new Vector2(r * 0.7f, yy), new Vector2(r + 11f, yy), 3f, Palette.Quill);
            }

            // Frost — icy rim + a few crystal shards (move).
            bool frost = p.HasMove("frost");
            Toggle(_frostRim, frost);
            if (frost)
            {
                var fr = Palette.FrostRim;
                Circle(_frostRim, 0f, 0f, r + 2f, new Color(fr.r, fr.g, fr.b, 0.6f));
            }
            for (int i = 0; i < MaxFrost; i++)
            {
                Toggle(_frost[i], frost);
                if (!frost) continue;
                float ang = i / (float)MaxFrost * Mathf.PI * 2f + 0.4f;
                float cx = Mathf.Cos(ang), cy = Mathf.Sin(ang);
                Line(_frost[i], new Vector2(cx * r * 0.6f, cy * r * 0.6f),
                                new Vector2(cx * (r + 6f), cy * (r + 6f)), 2.5f, Palette.FrostCrystal);
            }

            // Idle move tells: a small organ nub per equipped attack move without a bespoke feature,
            // fanned across the front rim (≤4 slots, so never crowded).
            int mtN = 0;
            for (int m = 0; m < MoveTellIds.Length; m++) if (p.HasMove(MoveTellIds[m])) mtN++;
            mtN = Mathf.Min(mtN, MaxMoveTells);
            int ti = 0;
            for (int m = 0; m < MoveTellIds.Length && ti < MaxMoveTells; m++)
            {
                if (!p.HasMove(MoveTellIds[m])) continue;
                float frac = mtN > 1 ? ti / (float)(mtN - 1) : 0.5f;
                float ang = Mathf.Lerp(-0.7f, 0.7f, frac); // front arc around +X (facing)
                Toggle(_moveTells[ti], true);
                Circle(_moveTells[ti], Mathf.Cos(ang) * r * 0.95f, Mathf.Sin(ang) * r * 0.95f, 2.4f, MoveTellCols[m]);
                ti++;
            }
            for (int i = ti; i < MaxMoveTells; i++) Toggle(_moveTells[i], false);

            // Accent tier: one orbiting mote per present stat trait, color-coded by mutation.
            int accN = 0;
            for (int a = 0; a < AccentIds.Length; a++) if (p.Has(AccentIds[a])) accN++;
            accN = Mathf.Min(accN, MaxOrbiters);
            float tt = Time.time;
            int oi = 0;
            for (int a = 0; a < AccentIds.Length && oi < MaxOrbiters; a++)
            {
                if (!p.Has(AccentIds[a])) continue;
                float ang = tt * 1.5f + oi / (float)Mathf.Max(1, accN) * Mathf.PI * 2f;
                float rad = r + 9f;
                float dotR = 1.6f + 0.4f * Mathf.Sin(tt * 6f + oi);
                Toggle(_orbiters[oi], true);
                Circle(_orbiters[oi], Mathf.Cos(ang) * rad, Mathf.Sin(ang) * rad, dotR, AccentCols[a]);
                oi++;
            }
            for (int i = oi; i < MaxOrbiters; i++) Toggle(_orbiters[i], false);

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
