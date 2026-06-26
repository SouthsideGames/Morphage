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

            var go = new GameObject("MutagenUI");
            go.transform.SetParent(_game.transform, false);
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = panel;
            doc.visualTreeAsset = tree;
            go.SetActive(true);

            _root = doc.rootVisualElement;
            if (uss != null) _root.styleSheets.Add(uss);

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

            Hide(_levelOverlay); Hide(_deathOverlay); Hide(_debug);
            Show(_startOverlay);
            SyncSound();
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

        public void RenderMuts()
        {
            if (_muts == null) return;
            _muts.Clear();
            var p = _game.player; if (p == null) return;
            foreach (var kv in p.mutations)
            {
                var def = _game.mutations.ById(kv.Key); if (def == null) continue;
                bool ev = p.evolved.Contains(kv.Key);
                var tag = new VisualElement(); tag.AddToClassList("mtag");
                if (ev) tag.AddToClassList("evolved");
                var dot = new VisualElement();
                dot.style.width = 8; dot.style.height = 8; dot.style.marginRight = 6;
                dot.style.backgroundColor = def.color;
                dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4;
                dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
                tag.Add(dot);
                tag.Add(new Label(def.displayName + (kv.Value > 1 ? $"  L{kv.Value}" : "")));
                if (ev) { var star = new Label("★"); star.AddToClassList("star"); tag.Add(star); }
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
                var card = new VisualElement(); card.AddToClassList("card");

                var top = new VisualElement(); top.AddToClassList("cardtop"); top.style.backgroundColor = def.color; card.Add(top);

                var rar = new Label((def.rarity ?? "common").ToUpper()); rar.AddToClassList("cardrar");
                rar.style.color = RarityColor.TryGetValue(def.rarity ?? "common", out var rc) ? rc : Color.gray;
                rar.style.unityTextAlign = TextAnchor.UpperRight; card.Add(rar);

                var tag = new Label(isMove ? "MOVE" : "MODIFIER"); tag.AddToClassList("tag");
                tag.AddToClassList(isMove ? "tagmove" : "tagmod"); card.Add(tag);

                var glyph = new VisualElement(); glyph.AddToClassList("glyph");
                glyph.style.backgroundColor = new Color(def.color.r, def.color.g, def.color.b, 0.16f);
                if (def.icon != null) glyph.style.backgroundImage = new StyleBackground(def.icon);
                card.Add(glyph);

                var key = new Label($"[{i + 1}]"); key.AddToClassList("cardkey"); card.Add(key);
                var name = new Label(def.displayName); name.AddToClassList("cardname"); card.Add(name);
                var desc = new Label(def.description); desc.AddToClassList("carddesc"); card.Add(desc);
                string stkText = owned
                    ? $"Owned · L{cur} {(nearEvolve ? "→ EVOLVES NEXT!" : "→ level up")}"
                    : (isMove ? (p.FreeMoveSlot() >= 0 ? "New move" : "New move · loadout full") : "New modifier");
                var stk = new Label(stkText);
                stk.AddToClassList("cardstk"); stk.style.color = nearEvolve ? Palette.FloaterBig : def.color; card.Add(stk);

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

        void BuildMoveBar()
        {
            if (_movebar == null) return;
            _movebar.Clear();
            _slots = new VisualElement[5];
            string[] keys = { "1", "2", "3", "4", "⇧" };
            for (int i = 0; i < 5; i++)
            {
                var s = new VisualElement(); s.AddToClassList("mslot");
                if (i == 4) s.AddToClassList("dash");
                var k = new Label(keys[i]); k.AddToClassList("mk"); s.Add(k);
                var lv = new Label(""); lv.AddToClassList("ml"); s.Add(lv);
                var nm = new Label(i == 4 ? "DASH" : "—"); nm.AddToClassList("mn"); s.Add(nm);
                var cool = new VisualElement(); cool.AddToClassList("mcool"); s.Add(cool);
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
                string id = p.moveSlots[i]; var mv = id != null ? Moves.Get(id) : null;
                if (mv == null) { s.AddToClassList("empty"); if (nm != null) nm.text = "—"; if (lv != null) lv.text = ""; if (cool != null) cool.style.height = Length.Percent(0); continue; }
                s.RemoveFromClassList("empty");
                if (nm != null) { nm.text = mv.abbr; nm.style.color = mv.color; }
                int l = p.moveLevel.TryGetValue(id, out int lvl) ? lvl : 1;
                if (lv != null) lv.text = l > 1 ? ("L" + l) : "";
                float frac = p.moveCd.TryGetValue(id, out float cd) ? Rng.Clamp(cd / mv.cd, 0f, 1f) : 0f;
                if (cool != null) cool.style.height = Length.Percent(frac * 100f);
            }
            var dcool = _slots[4].Q(className: "mcool");
            if (dcool != null) dcool.style.height = Length.Percent(Rng.Clamp(p.dashCd / (p.dashCdMax <= 0f ? 1.3f : p.dashCdMax), 0f, 1f) * 100f);
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
            if (!active) EndJoy();
        }

        public void ShowReplace(MutationDef def, System.Action<int> onChoose)
        {
            var mv = Moves.Get(def.move);
            string newName = mv != null ? mv.name : def.displayName;
            if (_replaceTitle != null) _replaceTitle.text = "Learn " + newName + "?";
            if (_replaceSub != null) _replaceSub.text = "Loadout full. Overwrite a move, or discard the new one.";
            _replaceSlots.Clear();
            var p = _game.player;
            for (int i = 1; i < 4; i++)
            {
                string id = p.moveSlots[i]; var cur = Moves.Get(id);
                var el = new VisualElement(); el.AddToClassList("rslot");
                var k = new Label($"[{i}] slot {i}"); k.AddToClassList("rkey"); el.Add(k);
                var n = new Label(cur != null ? cur.name : "—"); n.AddToClassList("rname"); if (cur != null) n.style.color = cur.color; el.Add(n);
                int l = p.moveLevel.TryGetValue(id, out int lvl) ? lvl : 1;
                var rl = new Label($"L{l} — replace with {newName}"); rl.AddToClassList("rlevel"); el.Add(rl);
                int slot = i; el.RegisterCallback<ClickEvent>(_ => onChoose(slot));
                _replaceSlots.Add(el);
            }
            var dc = new VisualElement(); dc.AddToClassList("rslot"); dc.AddToClassList("discard");
            var dk = new Label("[4] discard"); dk.AddToClassList("rkey"); dc.Add(dk);
            var dn = new Label("Don't learn"); dn.AddToClassList("rname"); dc.Add(dn);
            var dl = new Label($"Skip {newName}"); dl.AddToClassList("rlevel"); dc.Add(dl);
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

        public void RenderBinds()
        {
            if (_bindList == null) return;
            _bindList.Clear();
            foreach (var (action, label) in Binds.Defs)
            {
                var row = new VisualElement(); row.AddToClassList("bindrow");
                var bl = new Label(label); bl.AddToClassList("bl"); row.Add(bl);
                bool listening = Binds.Rebinding == action;
                var btn = new Button(); btn.AddToClassList("bindkey"); if (listening) btn.AddToClassList("listening");
                btn.text = listening ? "Press…" : Binds.Label(Binds.Get(action));
                string a = action;
                btn.clicked += () => { Binds.Rebinding = a; RenderBinds(); };
                row.Add(btn);
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
