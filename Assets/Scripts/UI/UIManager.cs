using System;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace Mutagen
{
    /// <summary>
    /// UI Toolkit front-end. Loads MutagenUI.uxml/.uss + a PanelSettings from Resources,
    /// builds a runtime UIDocument and binds it to the Game. Counterpart to the prototype UIManager.
    /// </summary>
    public class UIManager
    {
        readonly Game _game;
        VisualElement _root;

        VisualElement _hud, _debug, _startOverlay, _levelOverlay, _deathOverlay;
        VisualElement _codexOverlay, _codexList;
        VisualElement _galleryOverlay, _galleryList;
        Label _galleryEmpty;
        readonly System.Collections.Generic.List<RenderTexture> _portraits = new();
        VisualElement _hpFill, _xpFill, _muts, _cards;
        Label _hpTxt, _xpTxt, _lvlVal, _waveVal, _dnaVal;
        Label _dWave, _dLvl, _dKills, _dTime, _dDps, _dTaken, _dMuts, _dSeed, _dbgInfo, _dbgTitle;
        Label _banner, _endEyebrow, _endBig, _replaceTitle, _replaceSub;
        VisualElement _movebar, _replaceOverlay, _replaceSlots, _settingsOverlay, _bindList, _pauseOverlay;
        Button _pauseBtn;
        VisualElement _touchLayer, _joyBase, _joyKnob;
        bool _joyActive; int _joyPointerId = -1; Vector2 _joyStart;
        const float JoyRadius = 70f;
        Slider _volMaster, _volMusic, _volSfx;
        TextField _seedField;
        Button _soundToggle, _modeToggle, _autoToggle, _rerollBtn;
        bool _endless, _autocast;
        VisualElement[] _slots; // 4 move slots + 1 dash
        // Optional UXML templates (UI Builder-editable); each falls back to code-building if not loaded.
        VisualTreeAsset _moveSlotTemplate, _cardTemplate, _mutTagTemplate, _bindRowTemplate, _replaceSlotTemplate;
        readonly System.Collections.Generic.Dictionary<string, Texture2D> _iconCache = new();

        // Resources-load + cache a white icon texture (e.g. "Icons/Teeth"). Null key/miss returns null.
        Texture2D LoadIcon(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (_iconCache.TryGetValue(key, out var t)) return t;
            t = Resources.Load<Texture2D>(key);
            _iconCache[key] = t;
            return t;
        }

        // Passive modifiers aren't moves, so their icons live here (keyed by mutation id).
        static readonly System.Collections.Generic.Dictionary<string, string> ModifierIcon = new()
        {
            { "arms", "Icons/Fist" },
            { "regen", "Icons/Heart" },
            { "thick", "Icons/Shield" },
            { "wings", "Icons/Wing" },
            { "magnet", "Icons/Magnet" },
            { "power", "Icons/PowerCell" },
            { "carapace", "Icons/Overshield" },
            { "vitality", "Icons/Medpack" },
            { "reflexes", "Icons/Boost" },
            { "scavenger", "Icons/XpOrb" },
            { "toxicskin", "Icons/Mushroom" },
            { "glasscannon", "Icons/Skull" },
            { "crit", "Icons/Atom" },
            { "vampire", "Icons/DragonTooth" },
            { "haste", "Icons/Run" },
            { "bulk", "Icons/Paw" },
            { "swift", "Icons/Feather" },
            { "revive", "Icons/Halo" },
            { "quills", "Icons/Cactus" },
            { "carnivore", "Icons/Bone" },
        };

        static readonly System.Collections.Generic.Dictionary<string, Color> RarityColor = new()
        {
            { "common", new Color(158f/255f, 201f/255f, 189f/255f) },
            { "rare", new Color(74f/255f, 214f/255f, 255f/255f) },
            { "legendary", new Color(255f/255f, 210f/255f, 74f/255f) },
        };

        public UIManager(Game game)
        {
            _game = game;
            Build();
        }

        void Build()
        {
            var panel = Resources.Load<PanelSettings>("UI/MutagenPanel");
            var tree = Resources.Load<VisualTreeAsset>("UI/MutagenUI");
            var uss = Resources.Load<StyleSheet>("UI/MutagenUI");
            _moveSlotTemplate = Resources.Load<VisualTreeAsset>("UI/MoveSlot"); // optional; falls back to code
            _cardTemplate = Resources.Load<VisualTreeAsset>("UI/Card");
            _mutTagTemplate = Resources.Load<VisualTreeAsset>("UI/MutTag");
            _bindRowTemplate = Resources.Load<VisualTreeAsset>("UI/BindRow");
            _replaceSlotTemplate = Resources.Load<VisualTreeAsset>("UI/ReplaceSlot");
            if (panel == null || tree == null)
            {
                Debug.LogError("[MUTAGEN] UI assets missing. Run MUTAGEN → Generate Assets.");
                return;
            }

            // Force a large, orientation-independent UI scale at runtime (no asset regen needed).
            // match=0.5 => scale tracks screen AREA, so portrait and landscape read the same size.
            panel.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            panel.referenceResolution = new Vector2Int(1024, 576);
            panel.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            panel.match = 0.5f;

            EnsureEventSystem();

            // Prefer a UIDocument authored in the scene (a GameObject named "MutagenUI" with a UIDocument).
            // Authoring it in the scene is what lets you SEE and tweak the layout live in UI Builder + the
            // Game view at edit time. If none exists we create one in code (original bootstrap behaviour).
            UIDocument doc = null;
            foreach (var d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (d.gameObject.name == "MutagenUI") { doc = d; break; }
            if (doc == null)
            {
                var go = new GameObject("MutagenUI");
                go.transform.SetParent(_game.transform, false);
                go.SetActive(false);
                doc = go.AddComponent<UIDocument>();
                go.SetActive(true);
            }
            doc.panelSettings = panel;
            doc.visualTreeAsset = tree;

            _root = doc.rootVisualElement;
            if (uss != null && !_root.styleSheets.Contains(uss)) _root.styleSheets.Add(uss);

            // Inset the whole UI inside the device safe area (notch, home indicator, rounded corners).
            // Recompute on every layout change (rotation / device-simulator swap re-fires this).
            _root.RegisterCallback<GeometryChangedEvent>(_ => ApplySafeArea());

            VisualElement Q(string n) => _root.Q<VisualElement>(n);
            Label L(string n) => _root.Q<Label>(n);

            _hud = Q("hud"); _debug = Q("debug");
            _startOverlay = Q("startOverlay"); _levelOverlay = Q("levelOverlay"); _deathOverlay = Q("deathOverlay");
            _hpFill = Q("hpFill"); _xpFill = Q("xpFill"); _muts = Q("muts"); _cards = Q("cards");
            _hpTxt = L("hpTxt"); _xpTxt = L("xpTxt"); _lvlVal = L("lvlVal"); _waveVal = L("waveVal"); _dnaVal = L("dnaVal");
            _dWave = L("dWave"); _dLvl = L("dLvl"); _dKills = L("dKills"); _dTime = L("dTime"); _dDps = L("dDps"); _dTaken = L("dTaken");
            _dMuts = L("dMuts"); _dSeed = L("dSeed"); _dbgInfo = L("dbgInfo"); _dbgTitle = L("dbgTitle");
            _banner = L("banner");
            _endEyebrow = L("endEyebrow"); _endBig = L("endBig");
            _movebar = Q("movebar");
            _replaceOverlay = Q("replaceOverlay"); _replaceTitle = L("replaceTitle"); _replaceSub = L("replaceSub"); _replaceSlots = Q("replaceSlots");
            _settingsOverlay = Q("settingsOverlay"); _bindList = Q("bindList");
            _codexOverlay = Q("codexOverlay"); _codexList = Q("codexList");
            _galleryOverlay = Q("galleryOverlay"); _galleryList = Q("galleryList"); _galleryEmpty = L("galleryEmpty");
            _volMaster = _root.Q<Slider>("volMaster"); _volMusic = _root.Q<Slider>("volMusic"); _volSfx = _root.Q<Slider>("volSfx");
            _seedField = _root.Q<TextField>("seedField");
            _soundToggle = _root.Q<Button>("soundToggle");
            _modeToggle = _root.Q<Button>("modeToggle");
            _autoToggle = _root.Q<Button>("autoToggle");
            _rerollBtn = _root.Q<Button>("rerollBtn");
            _touchLayer = Q("touchLayer"); _joyBase = Q("joyBase"); _joyKnob = Q("joyKnob");
            BuildMoveBar();
            BuildJoystick();

            // ---- bindings ----
            _root.Q<Button>("beginBtn").clicked += () => _game.StartRun();
            _root.Q<Button>("replayBtn").clicked += () => _game.ReplaySeed();
            _root.Q<Button>("menuBtn").clicked += () => _game.GotoMenu();
            _rerollBtn.clicked += () => _game.Reroll();
            _soundToggle.clicked += () => { Sfx.Enabled = !Sfx.Enabled; SyncSound(); };
            _modeToggle.clicked += () =>
            {
                _endless = !_endless;
                _modeToggle.text = _endless ? "Endless" : "Campaign · 15 waves";
                if (_endless) _modeToggle.AddToClassList("on"); else _modeToggle.RemoveFromClassList("on");
            };
            if (_autoToggle != null) _autoToggle.clicked += () =>
            {
                _autocast = !_autocast;
                _autoToggle.text = "Auto-cast: " + (_autocast ? "On" : "Off");
                if (_autocast) _autoToggle.AddToClassList("on"); else _autoToggle.RemoveFromClassList("on");
            };
            var settingsBtn = _root.Q<Button>("settingsBtn");
            if (settingsBtn != null) settingsBtn.clicked += ShowSettings;
            var settingsDone = _root.Q<Button>("settingsDone");
            if (settingsDone != null) settingsDone.clicked += HideSettings;
            var codexBtn = _root.Q<Button>("codexBtn");
            if (codexBtn != null) codexBtn.clicked += ShowCodex;
            var pauseCodexBtn = _root.Q<Button>("pauseCodexBtn");
            if (pauseCodexBtn != null) pauseCodexBtn.clicked += ShowCodex;
            var codexDone = _root.Q<Button>("codexDone");
            if (codexDone != null) codexDone.clicked += HideCodex;
            var galleryBtn = _root.Q<Button>("galleryBtn");
            if (galleryBtn != null) galleryBtn.clicked += ShowGallery;
            var galleryDone = _root.Q<Button>("galleryDone");
            if (galleryDone != null) galleryDone.clicked += HideGallery;
            var bindReset = _root.Q<Button>("bindReset");
            if (bindReset != null) bindReset.clicked += () => { Binds.Reset(); RenderBinds(); };
            _pauseOverlay = Q("pauseOverlay"); _pauseBtn = _root.Q<Button>("pauseBtn");
            if (_pauseBtn != null) _pauseBtn.clicked += _game.TogglePause;
            var resumeBtn = _root.Q<Button>("resumeBtn");
            if (resumeBtn != null) resumeBtn.clicked += _game.TogglePause;
            var pauseMenuBtn = _root.Q<Button>("pauseMenuBtn");
            if (pauseMenuBtn != null) pauseMenuBtn.clicked += _game.GotoMenu;
            if (_volMaster != null) _volMaster.RegisterValueChangedCallback(e => Sfx.SetMaster(e.newValue / 100f));
            if (_volMusic != null) _volMusic.RegisterValueChangedCallback(e => Sfx.SetMusic(e.newValue / 100f));
            if (_volSfx != null) _volSfx.RegisterValueChangedCallback(e => Sfx.SetSfx(e.newValue / 100f));

            Hide(_replaceOverlay);
            Hide(_settingsOverlay);
            Hide(_codexOverlay);
            Hide(_galleryOverlay);
            Hide(_pauseOverlay);
            if (_pauseBtn != null) _pauseBtn.style.display = DisplayStyle.None;
            if (_banner != null) _banner.style.display = DisplayStyle.None;

            _root.Q<Button>("dbgGod").clicked += _game.DebugGod;
            _root.Q<Button>("dbgLvl").clicked += _game.DebugLevel;
            _root.Q<Button>("dbgMut").clicked += _game.DebugGrantMutation;
            _root.Q<Button>("dbgDna").clicked += _game.DebugDna;
            _root.Q<Button>("dbgKill").clicked += _game.DebugKillAll;
            _root.Q<Button>("dbgBoss").clicked += _game.DebugSpawnBoss;
            _root.Q<Button>("dbgSlow").clicked += _game.DebugSlow;
            _root.Q<Button>("dbgTouch").clicked += _game.DebugTouch;
            _root.Q<Button>("dbgChaser").clicked += () => _game.DebugSpawn("chaser");
            _root.Q<Button>("dbgFast").clicked += () => _game.DebugSpawn("fast");
            _root.Q<Button>("dbgTank").clicked += () => _game.DebugSpawn("tank");
            _root.Q<Button>("dbgSpitter").clicked += () => _game.DebugSpawn("spitter");
            _root.Q<Button>("dbgExploder").clicked += () => _game.DebugSpawn("exploder");

            // Make every full-screen panel scroll when its content is taller than the safe area.
            foreach (var ov in new[] { _startOverlay, _settingsOverlay, _codexOverlay, _galleryOverlay, _levelOverlay, _deathOverlay, _replaceOverlay, _pauseOverlay })
                WrapInScroll(ov);

            Hide(_levelOverlay); Hide(_deathOverlay); Hide(_debug);
            Show(_startOverlay);
            SyncSound();
        }

        // Reparent an overlay's children into a vertical ScrollView so tall content (e.g. the
        // settings keybind list) can scroll on small screens. Q<> lookups still resolve since the
        // elements remain descendants of the overlay.
        static void WrapInScroll(VisualElement overlay)
        {
            if (overlay == null || overlay.Q<ScrollView>(className: "oscroll") != null) return;
            var sv = new ScrollView(ScrollViewMode.Vertical);
            sv.AddToClassList("oscroll");
            sv.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            sv.verticalScrollerVisibility = ScrollerVisibility.Hidden; // drag/swipe to scroll, no visible bar
            var kids = new System.Collections.Generic.List<VisualElement>(overlay.Children());
            foreach (var c in kids) sv.Add(c); // Add() reparents into the scroll view's content
            overlay.Add(sv);
        }

        void EnsureEventSystem()
        {
            // Need exactly one EventSystem with an InputSystemUIInputModule that HAS its click
            // actions assigned — otherwise pointer events never reach UI Toolkit (keyboard still
            // works because we poll devices directly). Repair whatever's in the scene.
            var systems = UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            EventSystem es;
            if (systems.Length == 0)
                es = new GameObject("EventSystem").AddComponent<EventSystem>();
            else
            {
                es = systems[0];
                for (int i = 1; i < systems.Length; i++) UnityEngine.Object.Destroy(systems[i].gameObject);
            }

            var legacy = es.GetComponent<StandaloneInputModule>();
            if (legacy != null) UnityEngine.Object.Destroy(legacy);

            var module = es.GetComponent<InputSystemUIInputModule>();
            if (module == null) module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions(); // assigns + enables point/click actions on the live module
        }

        static void Show(VisualElement v) { if (v != null) v.style.display = DisplayStyle.Flex; }
        static void Hide(VisualElement v) { if (v != null) v.style.display = DisplayStyle.None; }

        // ---------------------------------------------------------------- HUD
        public void SyncHud()
        {
            var p = _game.player; if (p == null) return;
            _hpFill.style.width = Length.Percent(Rng.Clamp(p.hp / p.maxHp, 0f, 1f) * 100f);
            _hpTxt.text = $"{Mathf.Ceil(p.hp)} / {p.maxHp}";
            _xpFill.style.width = Length.Percent(Rng.Clamp(p.xp / p.xpNext, 0f, 1f) * 100f);
            _xpTxt.text = $"{Mathf.Round(p.xp)} / {p.xpNext}";
            _lvlVal.text = p.level.ToString();
            _waveVal.text = _game.wave.ToString();
            _dnaVal.text = Mathf.Round(p.dna).ToString();
        }

        // One owned-mutation chip from the UXML template, or built in code if it's missing.
        VisualElement MakeMutTag()
        {
            if (_mutTagTemplate != null)
            {
                var e = _mutTagTemplate.Instantiate().Q(className: "mtag");
                if (e != null) return e;
            }
            var tag = new VisualElement(); tag.AddToClassList("mtag");
            var dot = new VisualElement(); dot.AddToClassList("mtdot"); tag.Add(dot);
            var nm = new Label(); nm.AddToClassList("mtname"); tag.Add(nm);
            var star = new Label("★"); star.AddToClassList("star"); tag.Add(star);
            return tag;
        }

        public void RenderMuts()
        {
            if (_muts == null) return;
            _muts.Clear();
            var p = _game.player; if (p == null) return;
            foreach (var kv in p.mutations)
            {
                var def = _game.mutations.ById(kv.Key); if (def == null) continue;
                bool ev = p.evolved.Contains(kv.Key);
                var tag = MakeMutTag();
                if (ev) tag.AddToClassList("evolved"); else tag.RemoveFromClassList("evolved");
                var dot = tag.Q(className: "mtdot"); if (dot != null) dot.style.backgroundColor = def.color;
                var nm = tag.Q<Label>(className: "mtname"); if (nm != null) nm.text = def.displayName + (kv.Value > 1 ? $"  L{kv.Value}" : "");
                var star = tag.Q<Label>(className: "star"); if (star != null) star.style.display = ev ? DisplayStyle.Flex : DisplayStyle.None;
                _muts.Add(tag);
            }

            var syn = _game.ActiveSynergies(p);
            if (syn.Count > 0)
            {
                var wrap = new VisualElement(); wrap.AddToClassList("synwrap");
                foreach (var s in syn) { var chip = new Label("⚡ " + s); chip.AddToClassList("syn"); wrap.Add(chip); }
                _muts.Add(wrap);
            }
        }

        // Build one draft card from the UXML template (UI Builder-editable) or, if it's missing, in code.
        VisualElement MakeCard()
        {
            if (_cardTemplate != null)
            {
                var c = _cardTemplate.Instantiate().Q(className: "card");
                if (c != null) return c;
            }
            var card = new VisualElement(); card.AddToClassList("card");
            var top = new VisualElement(); top.AddToClassList("cardtop"); card.Add(top);
            var rar = new Label(); rar.AddToClassList("cardrar"); card.Add(rar);
            var tag = new Label(); tag.AddToClassList("tag"); card.Add(tag);
            var glyph = new VisualElement(); glyph.AddToClassList("glyph"); card.Add(glyph);
            var key = new Label(); key.AddToClassList("cardkey"); card.Add(key);
            var name = new Label(); name.AddToClassList("cardname"); card.Add(name);
            var desc = new Label(); desc.AddToClassList("carddesc"); card.Add(desc);
            var stk = new Label(); stk.AddToClassList("cardstk"); card.Add(stk);
            return card;
        }

        // ---------------------------------------------------------------- draft
        public void ShowDraft(System.Collections.Generic.List<MutationDef> options, Action<MutationDef> onPick)
        {
            _cards.Clear();
            for (int i = 0; i < options.Count; i++)
            {
                var def = options[i];
                var p = _game.player;
                bool isMove = !string.IsNullOrEmpty(def.move);
                int cur = p.Stacks(def.id);
                bool owned = isMove ? p.HasMove(def.move) : cur > 0;
                bool nearEvolve = def.maxStacks != 0 && cur + 1 == def.maxStacks;

                var card = MakeCard();

                var top = card.Q(className: "cardtop");
                if (top != null) top.style.backgroundColor = def.color;

                var rar = card.Q<Label>(className: "cardrar");
                if (rar != null)
                {
                    rar.text = (def.rarity ?? "common").ToUpper();
                    rar.style.color = RarityColor.TryGetValue(def.rarity ?? "common", out var rc) ? rc : Color.gray;
                    rar.style.unityTextAlign = TextAnchor.UpperRight;
                }

                var tag = card.Q<Label>(className: "tag");
                if (tag != null)
                {
                    tag.text = isMove ? "MOVE" : "MODIFIER";
                    tag.RemoveFromClassList("tagmove"); tag.RemoveFromClassList("tagmod");
                    tag.AddToClassList(isMove ? "tagmove" : "tagmod");
                }

                var glyph = card.Q(className: "glyph");
                if (glyph != null)
                {
                    glyph.style.backgroundColor = new Color(def.color.r, def.color.g, def.color.b, 0.16f);
                    if (def.icon != null) glyph.style.backgroundImage = new StyleBackground(def.icon);
                    else
                    {
                        string iconKey = isMove ? Moves.Get(def.move)?.icon
                                                : (ModifierIcon.TryGetValue(def.id, out var mk) ? mk : null);
                        var t = LoadIcon(iconKey);
                        if (t != null) { glyph.style.backgroundImage = new StyleBackground(t); glyph.style.unityBackgroundImageTintColor = def.color; }
                    }
                }

                var key = card.Q<Label>(className: "cardkey"); if (key != null) key.text = $"[{i + 1}]";
                var name = card.Q<Label>(className: "cardname"); if (name != null) name.text = def.displayName;
                var desc = card.Q<Label>(className: "carddesc"); if (desc != null) desc.text = def.description;
                var stk = card.Q<Label>(className: "cardstk");
                if (stk != null)
                {
                    stk.text = owned
                        ? $"Owned · L{cur} {(nearEvolve ? "→ EVOLVES NEXT!" : "→ level up")}"
                        : (isMove ? (p.FreeMoveSlot() >= 0 ? "New move" : "New move · loadout full") : "New modifier");
                    stk.style.color = nearEvolve ? Palette.FloaterBig : def.color;
                }

                var captured = def;
                card.RegisterCallback<ClickEvent>(_ => onPick(captured));
                _cards.Add(card);
            }
            UpdateReroll();
            Show(_levelOverlay);
        }

        public void HideDraft() => Hide(_levelOverlay);

        public void UpdateReroll()
        {
            if (_rerollBtn == null) return;
            _rerollBtn.text = $"↻ Reroll ({_game.rerolls})";
            if (_game.rerolls <= 0) _rerollBtn.AddToClassList("dim"); else _rerollBtn.RemoveFromClassList("dim");
        }

        public bool GetEndless() => _endless;

        // ---------------------------------------------------------------- end (death / victory)
        public void ShowEnd(bool victory)
        {
            var g = _game; var p = g.player; var s = g.stats;
            _dWave.text = g.wave.ToString();
            _dLvl.text = p.level.ToString();
            _dKills.text = s.kills.ToString();
            _dTime.text = Mathf.Floor(s.time) + "s";
            _dDps.text = Mathf.Round(s.damageDealt / Mathf.Max(s.time, 1f)).ToString();
            _dTaken.text = Mathf.Round(s.damageTaken).ToString();
            _dSeed.text = "seed " + g.seedText;
            if (_endEyebrow != null) _endEyebrow.text = victory ? "Specimen Ascended" : "Specimen Terminated";
            if (_endBig != null) { _endBig.text = victory ? "VICTORY" : "You Died"; _endBig.style.color = victory ? Palette.Dna : Palette.Ink; }

            var names = "";
            foreach (var kv in p.mutations)
            {
                var d = g.mutations.ById(kv.Key); if (d == null) continue;
                if (names.Length > 0) names += " · ";
                names += d.displayName + (kv.Value > 1 ? $" ×{kv.Value}" : "") + (p.evolved.Contains(kv.Key) ? "★" : "");
            }
            _dMuts.text = names.Length > 0 ? "Final form: " + names : "A sad, unmutated blob.";
            Show(_deathOverlay);
        }

        public void HideEnd() => Hide(_deathOverlay);

        public void UpdateBanner()
        {
            if (_banner == null) return;
            if (_game.banner.HasValue)
            {
                var b = _game.banner.Value;
                _banner.style.display = DisplayStyle.Flex;
                _banner.text = string.IsNullOrEmpty(b.sub) ? b.text : b.text + "\n" + b.sub;
                float a = Mathf.Clamp01(Mathf.Min(b.life, b.max - b.life) / 0.35f);
                var c = b.color; c.a = a; _banner.style.color = c;
            }
            else _banner.style.display = DisplayStyle.None;
        }

        // Build one slot from the UXML template (UI Builder-editable) or, if it's missing, in code.
        VisualElement MakeSlot()
        {
            if (_moveSlotTemplate != null)
            {
                var s = _moveSlotTemplate.Instantiate().Q(className: "mslot");
                if (s != null) return s; // detaches from its TemplateContainer when re-parented into the bar
            }
            var slot = new VisualElement(); slot.AddToClassList("mslot");
            var k = new Label(); k.AddToClassList("mk"); slot.Add(k);
            var lv = new Label(); lv.AddToClassList("ml"); slot.Add(lv);
            var g = new VisualElement(); g.AddToClassList("mglyph"); g.pickingMode = PickingMode.Ignore; slot.Add(g);
            var nm = new Label(); nm.AddToClassList("mn"); slot.Add(nm);
            var cool = new VisualElement(); cool.AddToClassList("mcool"); slot.Add(cool);
            return slot;
        }

        void BuildMoveBar()
        {
            if (_movebar == null) return;
            _movebar.Clear();
            _slots = new VisualElement[5];
            string[] keys = { "1", "2", "3", "4", "⇧" };
            for (int i = 0; i < 5; i++)
            {
                var s = MakeSlot();
                s.AddToClassList("mpos" + i); // lets the touch USS place each slot in a diamond
                if (i == 4) s.AddToClassList("dash");
                var k = s.Q<Label>(className: "mk"); if (k != null) k.text = keys[i];
                var nm = s.Q<Label>(className: "mn"); if (nm != null) nm.text = i == 4 ? "DASH" : "—";
                if (i == 4) // dash icon is static — set it once here
                {
                    var glyph = s.Q(className: "mglyph"); var dt = LoadIcon("Icons/Speed");
                    if (glyph != null && dt != null)
                    {
                        glyph.style.backgroundImage = new StyleBackground(dt);
                        glyph.style.unityBackgroundImageTintColor = new Color(124f/255f, 240f/255f, 196f/255f);
                        if (nm != null) nm.style.display = DisplayStyle.None;
                    }
                }
                int slot = i;
                s.RegisterCallback<ClickEvent>(_ =>
                {
                    if (_game.state != GameState.Playing || _game.player == null) return;
                    if (slot < 4) _game.player.UseMove(slot, _game); else _game.player.Dash(_game);
                });
                _movebar.Add(s); _slots[i] = s;
            }
        }

        public void UpdateMoveBar()
        {
            if (_slots == null) return;
            bool show = _game.state != GameState.Menu;
            _movebar.style.display = show ? DisplayStyle.Flex : DisplayStyle.None;
            var p = _game.player;
            if (!show || p == null) return;
            for (int i = 0; i < 4; i++)
            {
                var s = _slots[i];
                var nm = s.Q<Label>(className: "mn"); var lv = s.Q<Label>(className: "ml"); var cool = s.Q(className: "mcool");
                var glyph = s.Q(className: "mglyph");
                string id = p.moveSlots[i]; var mv = id != null ? Moves.Get(id) : null;
                if (mv == null)
                {
                    s.AddToClassList("empty");
                    if (nm != null) { nm.text = "—"; nm.style.display = DisplayStyle.Flex; }
                    if (glyph != null) glyph.style.backgroundImage = StyleKeyword.None;
                    if (lv != null) lv.text = ""; if (cool != null) cool.style.height = Length.Percent(0);
                    continue;
                }
                s.RemoveFromClassList("empty");
                var tex = glyph != null ? LoadIcon(mv.icon) : null;
                if (tex != null)
                {
                    glyph.style.backgroundImage = new StyleBackground(tex);
                    glyph.style.unityBackgroundImageTintColor = mv.color;
                    if (nm != null) nm.style.display = DisplayStyle.None;
                }
                else if (nm != null) { nm.text = mv.abbr; nm.style.color = mv.color; nm.style.display = DisplayStyle.Flex; }
                int l = p.moveLevel.TryGetValue(id, out int lvl) ? lvl : 1;
                if (lv != null) lv.text = l > 1 ? ("L" + l) : "";
                float frac = p.moveCd.TryGetValue(id, out float cd) ? Rng.Clamp(cd / mv.cd, 0f, 1f) : 0f;
                if (cool != null) cool.style.height = Length.Percent(frac * 100f);
            }
            var dcool = _slots[4].Q(className: "mcool");
            if (dcool != null) dcool.style.height = Length.Percent(Rng.Clamp(p.dashCd / (p.dashCdMax <= 0f ? 1.3f : p.dashCdMax), 0f, 1f) * 100f);
        }

        // ---------------------------------------------------------------- safe area
        Vector4 _safePad = new Vector4(-1, -1, -1, -1); // cache to avoid relayout churn

        void ApplySafeArea()
        {
            if (_root == null) return;
            float sw = Screen.width, sh = Screen.height;
            float panelW = _root.resolvedStyle.width, panelH = _root.resolvedStyle.height;
            if (sw <= 0f || sh <= 0f || panelW <= 0f || panelH <= 0f) return;

            var sa = Screen.safeArea;            // pixels, origin bottom-left
            float kx = panelW / sw, ky = panelH / sh;
            var pad = new Vector4(
                sa.xMin * kx,                    // left
                (sh - sa.yMax) * ky,             // top  (screen y is bottom-up)
                (sw - sa.xMax) * kx,             // right
                sa.yMin * ky);                   // bottom

            if ((pad - _safePad).sqrMagnitude < 0.25f) return; // unchanged → skip (prevents relayout loop)
            _safePad = pad;

            // Pad the gameplay layer + each overlay (not the root): overlay backgrounds stay
            // full-bleed behind the notch while their content insets into the safe area.
            foreach (var v in new[] { _hud, _debug, _levelOverlay, _deathOverlay, _replaceOverlay,
                                      _pauseOverlay, _settingsOverlay, _startOverlay })
            {
                if (v == null) continue;
                v.style.paddingLeft = pad.x;
                v.style.paddingTop = pad.y;
                v.style.paddingRight = pad.z;
                v.style.paddingBottom = pad.w;
            }
        }

        // ---------------------------------------------------------------- touch joystick
        void BuildJoystick()
        {
            if (_touchLayer == null) return;
            if (_joyBase != null) _joyBase.pickingMode = PickingMode.Ignore;
            if (_joyKnob != null) _joyKnob.pickingMode = PickingMode.Ignore;
            _touchLayer.RegisterCallback<PointerDownEvent>(OnJoyDown);
            _touchLayer.RegisterCallback<PointerMoveEvent>(OnJoyMove);
            _touchLayer.RegisterCallback<PointerUpEvent>(OnJoyUp);
            _touchLayer.RegisterCallback<PointerCaptureOutEvent>(_ => EndJoy());
        }

        void OnJoyDown(PointerDownEvent e)
        {
            if (_joyActive) return;
            _joyActive = true; _joyPointerId = e.pointerId;
            _joyStart = new Vector2(e.localPosition.x, e.localPosition.y);
            _touchLayer.CapturePointer(e.pointerId);
            if (_joyBase != null) { _joyBase.style.display = DisplayStyle.Flex; _joyBase.style.left = _joyStart.x - 75f; _joyBase.style.top = _joyStart.y - 75f; }
            SetKnob(Vector2.zero);
            TouchInput.Move = Vector2.zero;
            e.StopPropagation();
        }

        void OnJoyMove(PointerMoveEvent e)
        {
            if (!_joyActive || e.pointerId != _joyPointerId) return;
            Vector2 d = new Vector2(e.localPosition.x, e.localPosition.y) - _joyStart;
            Vector2 c = Vector2.ClampMagnitude(d, JoyRadius);
            SetKnob(c);
            TouchInput.Move = new Vector2(c.x / JoyRadius, -c.y / JoyRadius); // panel y is down → invert for game
        }

        void OnJoyUp(PointerUpEvent e) { if (e.pointerId == _joyPointerId) EndJoy(); }

        void EndJoy()
        {
            if (!_joyActive) return;
            _joyActive = false; TouchInput.Move = Vector2.zero;
            if (_joyBase != null) _joyBase.style.display = DisplayStyle.None;
            if (_joyPointerId >= 0 && _touchLayer != null) { _touchLayer.ReleasePointer(_joyPointerId); _joyPointerId = -1; }
        }

        void SetKnob(Vector2 offset)
        {
            if (_joyKnob == null) return;
            _joyKnob.style.left = 44f + offset.x;
            _joyKnob.style.top = 44f + offset.y;
        }

        public void SetTouchActive(bool active)
        {
            if (_touchLayer != null) _touchLayer.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;
            if (_movebar != null) { if (active) _movebar.AddToClassList("touch"); else _movebar.RemoveFromClassList("touch"); }
            if (_hud != null) { if (active) _hud.AddToClassList("touch"); else _hud.RemoveFromClassList("touch"); }
            // Persistent "mobile" flag on the root (independent of whether touch controls are live this
            // frame) so overlays/menus can lay out compactly on phones — see .mobile rules in the USS.
            if (_root != null)
            {
                if (TouchInput.IsTouchDevice) _root.AddToClassList("mobile"); else _root.RemoveFromClassList("mobile");
            }
            if (!active) EndJoy();
        }

        // One replace-overlay slot from the UXML template, or built in code if it's missing.
        VisualElement MakeReplaceSlot()
        {
            if (_replaceSlotTemplate != null)
            {
                var e = _replaceSlotTemplate.Instantiate().Q(className: "rslot");
                if (e != null) return e;
            }
            var el = new VisualElement(); el.AddToClassList("rslot");
            var k = new Label(); k.AddToClassList("rkey"); el.Add(k);
            var n = new Label(); n.AddToClassList("rname"); el.Add(n);
            var rl = new Label(); rl.AddToClassList("rlevel"); el.Add(rl);
            return el;
        }

        public void ShowReplace(MutationDef def, System.Action<int> onChoose)
        {
            var mv = Moves.Get(def.move);
            string newName = mv != null ? mv.name : def.displayName;
            if (_replaceTitle != null) _replaceTitle.text = "Learn " + newName + "?";
            if (_replaceSub != null) _replaceSub.text = "Loadout full. Overwrite a move, or discard the new one.";
            _replaceSlots.Clear();
            var p = _game.player;
            for (int i = 0; i < 4; i++)
            {
                string id = p.moveSlots[i]; var cur = Moves.Get(id);
                var el = MakeReplaceSlot(); el.RemoveFromClassList("discard");
                var k = el.Q<Label>(className: "rkey"); if (k != null) k.text = $"[{i + 1}] slot {i + 1}";
                var n = el.Q<Label>(className: "rname"); if (n != null) { n.text = cur != null ? cur.name : "—"; if (cur != null) n.style.color = cur.color; }
                int l = p.moveLevel.TryGetValue(id, out int lvl) ? lvl : 1;
                var rl = el.Q<Label>(className: "rlevel"); if (rl != null) rl.text = $"L{l} — replace with {newName}";
                int slot = i; el.RegisterCallback<ClickEvent>(_ => onChoose(slot));
                _replaceSlots.Add(el);
            }
            var dc = MakeReplaceSlot(); dc.AddToClassList("discard");
            var dk = dc.Q<Label>(className: "rkey"); if (dk != null) dk.text = "[Esc] discard";
            var dn = dc.Q<Label>(className: "rname"); if (dn != null) dn.text = "Don't learn";
            var dl = dc.Q<Label>(className: "rlevel"); if (dl != null) dl.text = $"Skip {newName}";
            dc.RegisterCallback<ClickEvent>(_ => onChoose(-1));
            _replaceSlots.Add(dc);
            Show(_replaceOverlay);
        }

        public void HideReplace() => Hide(_replaceOverlay);
        public bool GetAutocast() => _autocast;

        public void SetPauseVisible(bool v) { if (_pauseBtn != null) _pauseBtn.style.display = v ? DisplayStyle.Flex : DisplayStyle.None; }
        public void ShowPause() { Show(_pauseOverlay); _pauseOverlay?.BringToFront(); }
        public void HidePause() => Hide(_pauseOverlay);

        // ---------------------------------------------------------------- settings
        public void ShowSettings()
        {
            if (_volMaster != null) _volMaster.SetValueWithoutNotify(Sfx.MasterVol * 100f);
            if (_volMusic != null) _volMusic.SetValueWithoutNotify(Sfx.MusicVol * 100f);
            if (_volSfx != null) _volSfx.SetValueWithoutNotify(Sfx.SfxVol * 100f);
            RenderBinds();
            Show(_settingsOverlay);
            _settingsOverlay?.BringToFront(); // render above the menu
        }

        public void HideSettings() { Binds.Rebinding = null; Hide(_settingsOverlay); }

        // ---------------------------------------------------------------- synergy codex
        public void ShowCodex() { RenderCodex(); Show(_codexOverlay); _codexOverlay?.BringToFront(); }
        public void HideCodex() { Hide(_codexOverlay); }

        // List every synergy from the shared table; highlight the ones live in the current run.
        void RenderCodex()
        {
            if (_codexList == null) return;
            _codexList.Clear();
            var p = _game.player; // null on the main menu — then nothing is highlighted
            foreach (var d in Synergies.All)
            {
                var row = new VisualElement(); row.AddToClassList("codexrow");
                if (p != null && d.active(p)) row.AddToClassList("active");

                var head = new VisualElement(); head.AddToClassList("codexhead"); row.Add(head);
                var name = new Label("⚡ " + d.name); name.AddToClassList("synname"); head.Add(name);
                var cat = new Label(d.category); cat.AddToClassList("syncat"); head.Add(cat);

                var recipe = new Label(d.recipe); recipe.AddToClassList("synrecipe"); row.Add(recipe);
                var eff = new Label(d.effect); eff.AddToClassList("syneffect"); row.Add(eff);
                _codexList.Add(row);
            }
        }

        // ---------------------------------------------------------------- monster archive
        public void ShowGallery() { RenderGallery(); Show(_galleryOverlay); _galleryOverlay?.BringToFront(); }
        public void HideGallery() { Hide(_galleryOverlay); ReleasePortraits(); }

        void ReleasePortraits()
        {
            foreach (var rt in _portraits) if (rt != null) { rt.Release(); UnityEngine.Object.Destroy(rt); }
            _portraits.Clear();
        }

        // Load the archive and build one card per monster, each with a freshly-rendered portrait.
        void RenderGallery()
        {
            if (_galleryList == null) return;
            ReleasePortraits();
            _galleryList.Clear();

            var col = SaveSystem.LoadMonsters();
            bool any = col.monsters != null && col.monsters.Count > 0;
            if (_galleryEmpty != null) _galleryEmpty.style.display = any ? DisplayStyle.None : DisplayStyle.Flex;
            if (!any) return;

            foreach (var rec in col.monsters)
            {
                var card = new VisualElement(); card.AddToClassList("gcard");
                if (rec.won) card.AddToClassList("won");

                var pic = new VisualElement(); pic.AddToClassList("gpic");
                var rt = MonsterPortrait.Instance.Render(_game, rec, 160);
                _portraits.Add(rt);
                pic.style.backgroundImage = Background.FromRenderTexture(rt);
                card.Add(pic);

                var info = new VisualElement(); info.AddToClassList("ginfo");
                var name = new Label(rec.name); name.AddToClassList("gname"); info.Add(name);
                var outcome = new Label((rec.won ? "◆ Victory" : "✕ Fell") + "  ·  Wave " + rec.wave + "  ·  Lv " + rec.level);
                outcome.AddToClassList("goutcome"); if (rec.won) outcome.AddToClassList("won"); info.Add(outcome);
                var stat = new Label(rec.kills + " kills  ·  " + Mathf.RoundToInt(rec.timeSurvived) + "s  ·  " + Mathf.RoundToInt(rec.dps) + " DPS");
                stat.AddToClassList("gstat"); info.Add(stat);
                var moves = new Label(MoveSummary(rec)); moves.AddToClassList("gmut"); info.Add(moves);
                card.Add(info);

                _galleryList.Add(card);
            }
        }

        static string MoveSummary(MonsterRecord rec)
        {
            var list = new System.Collections.Generic.List<string>();
            if (rec.moveSlots != null)
                foreach (var m in rec.moveSlots) if (!string.IsNullOrEmpty(m)) list.Add(m);
            return "Moves: " + (list.Count > 0 ? string.Join(", ", list) : "—");
        }

        // One settings keybind row from the UXML template, or built in code if it's missing.
        VisualElement MakeBindRow()
        {
            if (_bindRowTemplate != null)
            {
                var e = _bindRowTemplate.Instantiate().Q(className: "bindrow");
                if (e != null) return e;
            }
            var row = new VisualElement(); row.AddToClassList("bindrow");
            var bl = new Label(); bl.AddToClassList("bl"); row.Add(bl);
            var btn = new Button(); btn.AddToClassList("bindkey"); row.Add(btn);
            return row;
        }

        public void RenderBinds()
        {
            if (_bindList == null) return;
            _bindList.Clear();
            foreach (var (action, label) in Binds.Defs)
            {
                var row = MakeBindRow();
                var bl = row.Q<Label>(className: "bl"); if (bl != null) bl.text = label;
                bool listening = Binds.Rebinding == action;
                var btn = row.Q<Button>(className: "bindkey");
                if (btn != null)
                {
                    if (listening) btn.AddToClassList("listening"); else btn.RemoveFromClassList("listening");
                    btn.text = listening ? "Press…" : Binds.Label(Binds.Get(action));
                    string a = action;
                    btn.clicked += () => { Binds.Rebinding = a; RenderBinds(); };
                }
                _bindList.Add(row);
            }
        }

        public void ShowStart() => Show(_startOverlay);
        public void HideStart() => Hide(_startOverlay);

        // ---------------------------------------------------------------- debug / misc
        public void SetDebugVisible(bool v) { if (v) Show(_debug); else Hide(_debug); }

        public void UpdateDebug()
        {
            if (_debug == null || _debug.style.display == DisplayStyle.None) return;
            if (_dbgTitle != null) _dbgTitle.text = "Debug · seed " + _game.seedText;
            if (_dbgInfo != null) _dbgInfo.text = _game.DebugInfo();
        }

        public void RefreshGodBtn()
        {
            var b = _root?.Q<Button>("dbgGod"); if (b != null) b.text = "God: " + (_game.god ? "on" : "off");
        }

        public void SyncSound()
        {
            if (_soundToggle == null) return;
            _soundToggle.text = Sfx.Enabled ? "Sound on" : "Sound off";
            if (Sfx.Enabled) _soundToggle.AddToClassList("on"); else _soundToggle.RemoveFromClassList("on");
        }

        public string GetSeedField() => _seedField != null ? (_seedField.value ?? "") : "";
        public void SetSeedField(string s) { if (_seedField != null) _seedField.value = s; }
    }
}
