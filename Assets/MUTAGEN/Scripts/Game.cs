using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mutagen
{
    public enum GameState { Menu, Playing, LevelUp, Dead, Won, Replace }

    public struct Banner { public string text, sub; public Color color; public float life, max; }

    public class RunStats
    {
        public int kills;
        public float damageDealt, damageTaken, dnaCollected, time;
    }

    /// <summary>
    /// Central game loop / state machine — the Unity counterpart of the prototype Game class.
    /// Runs a fixed-timestep simulation; pooled sprite views are synced each rendered frame.
    /// </summary>
    public class Game : MonoBehaviour
    {
        public const float FIXED = 1f / 60f;
        public const int WIN_WAVE = 15;
        public float w = 960f, h = 600f;

        public GameState state = GameState.Menu;
        public Player player;
        public readonly List<Enemy> enemies = new();
        public readonly List<Projectile> projectiles = new();
        public readonly List<DNAOrb> orbs = new();
        public readonly List<Particle> particles = new();
        public readonly List<Floater> floaters = new();
        public readonly List<Beam> beams = new();
        public RunStats stats = new();

        public bool god, debug, slowmo;
        public float hitstop, fps = 60f;
        public string seedText = "";
        public int wave = 1;

        public SpatialGrid grid;
        public MutationManager mutations;
        public UIManager ui;

        // wave state
        float waveTimer = 22f, spawnCd, intermission;
        bool bossAlive, pendingBoss, finalBossPending;
        readonly HashSet<int> bossesSpawned = new();
        int pendingLevels;
        List<MutationDef> draftOptions;

        // v0.3 / v0.4
        public bool endless, autocast;
        public int rerolls = 3;
        public Banner? banner;
        MutationDef _pendingReplace;
        float _draftGuard; // brief input lockout when a draft/forget panel opens (prevents stray attack-key picks)
        bool _paused;

        public void SetBanner(string text, string sub, Color color, float life)
            => banner = new Banner { text = text, sub = sub, color = color, life = life, max = life };

        // data
        Dictionary<string, EnemyDef> _enemyById = new();
        EnemyDef _bossDef;

        // pools
        Pool<SpriteView> _spritePool;
        Pool<LabelView> _labelPool;
        Pool<BeamView> _beamPool;
        readonly Stack<Particle> _freeParticles = new();
        readonly Stack<Floater> _freeFloaters = new();
        readonly Stack<Projectile> _freeProjectiles = new();
        readonly Stack<DNAOrb> _freeOrbs = new();
        readonly Stack<Beam> _freeBeams = new();

        readonly List<Enemy> _hitBuf = new();
        const float MAX_ENEMY_R = 48f; // boss radius, for grid query padding

        InputReader _input = new();
        readonly bool[] _moveQueued = new bool[4];
        bool _dashQueued;
        float _accum;
        Transform _viewRoot;
        Camera _cam;
        PlayerVisual _playerVisual;
        Juice _juice;

        // ---------------------------------------------------------------- setup
        void Start()
        {
            var mutDefs = Resources.LoadAll<MutationDef>("Mutations");
            var enemyDefs = Resources.LoadAll<EnemyDef>("Enemies");
            if (mutDefs.Length == 0 || enemyDefs.Length == 0)
            {
                Debug.LogError("[MUTAGEN] No data assets found. Run menu: MUTAGEN → Generate Assets, then press Play.");
                enabled = false;
                return;
            }
            mutations = new MutationManager(mutDefs);
            foreach (var e in enemyDefs) _enemyById[e.id] = e;
            _enemyById.TryGetValue("boss", out _bossDef);

            grid = new SpatialGrid(w, h);
            _viewRoot = new GameObject("Views").transform;
            _viewRoot.SetParent(transform, false);

            _spritePool = new Pool<SpriteView>(() => SpriteFactory.CreateSpriteView(_viewRoot), 64);
            _labelPool = new Pool<LabelView>(() => SpriteFactory.CreateLabel(_viewRoot), 16);
            _beamPool = new Pool<BeamView>(() => SpriteFactory.CreateBeam(_viewRoot), 4);
            _playerVisual = PlayerVisual.Create(_viewRoot);

            SetupCamera();
            SetupFloor();
            _juice = Juice.Create(_viewRoot, _cam);

            var sfxSrc = gameObject.AddComponent<AudioSource>();
            var musicSrc = gameObject.AddComponent<AudioSource>();
            Sfx.Init(sfxSrc, musicSrc);

            ui = new UIManager(this);
            ui.ShowStart();
            state = GameState.Menu;
        }

        void SetupCamera()
        {
            _cam = Camera.main; // reuse the scene's Main Camera if present
            if (_cam == null)
            {
                var go = new GameObject("MutagenCamera");
                go.transform.SetParent(transform, false);
                _cam = go.AddComponent<Camera>();
                _cam.tag = "MainCamera";
            }
            _cam.orthographic = true;
            // Frame the whole bounded arena (faithful to v0.2) regardless of window aspect.
            float aspect = _cam.aspect > 0.01f ? _cam.aspect : 16f / 9f;
            _cam.orthographicSize = Mathf.Max(h / 2f, (w / 2f) / aspect);
            _cam.transform.position = new Vector3(w / 2f, h / 2f, -10f);
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = Palette.Abyss;
        }

        void SetupFloor()
        {
            var go = new GameObject("Floor");
            go.transform.SetParent(_viewRoot, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = SpriteFactory.MakeFloor((int)w, (int)h);
            sr.sortingOrder = -100;
            go.transform.position = new Vector3(w / 2f, h / 2f, 1f);
        }

        // ---------------------------------------------------------------- loop
        void Update()
        {
            bool touch = ui != null && TouchInput.IsTouchDevice && state == GameState.Playing && !_paused;
            TouchInput.Active = touch;
            if (ui != null) { ui.SetTouchActive(touch); ui.SetPauseVisible(state == GameState.Playing); }

            _input.Poll();
            Sfx.Tick(Time.deltaTime);
            if (_draftGuard > 0f) _draftGuard -= Time.deltaTime;
            if (_input.Pause && state == GameState.Playing) TogglePause();

            // Rebinding: swallow input until a key is pressed, then assign (Esc cancels).
            if (Binds.Rebinding != null)
            {
                if (_input.CapturedKey != UnityEngine.InputSystem.Key.None)
                {
                    if (_input.CapturedKey != UnityEngine.InputSystem.Key.Escape)
                        Binds.Assign(Binds.Rebinding, _input.CapturedKey);
                    Binds.Rebinding = null;
                    ui.RenderBinds();
                }
                return;
            }

            if (_input.ToggleDebug) ToggleDebug();
            if (_input.ToggleMute) { Sfx.Enabled = !Sfx.Enabled; ui.SyncSound(); }

            switch (state)
            {
                case GameState.Menu:    if (_input.StartPressed) StartRun(); break;
                case GameState.LevelUp:
                case GameState.Replace: HandleDraftInput(); break;
                case GameState.Playing:
                    if (_input.Move1) _moveQueued[0] = true;
                    if (_input.Move2) _moveQueued[1] = true;
                    if (_input.Move3) _moveQueued[2] = true;
                    if (_input.Move4) _moveQueued[3] = true;
                    if (_input.DashPressed) _dashQueued = true;
                    break;
            }

            Tick();
            Render();
            if (player != null) ui.SyncHud();
            ui.UpdateBanner();
            ui.UpdateMoveBar();
            ui.UpdateDebug();
        }

        void Tick()
        {
            float realDt = Mathf.Min(Time.deltaTime, 0.25f);
            fps = Rng.Lerp(fps, 1f / Mathf.Max(realDt, 1e-4f), 0.1f);
            if (banner.HasValue) { var b = banner.Value; b.life -= realDt; banner = b.life <= 0f ? (Banner?)null : b; }
            if (state != GameState.Playing || _paused) return;

            float scaled = slowmo ? realDt * 0.3f : realDt;
            if (hitstop > 0f) { hitstop = Mathf.Max(0f, hitstop - realDt); scaled = 0f; }

            _accum += scaled;
            int guard = 0;
            while (_accum >= FIXED && guard++ < 8) { Step(FIXED); _accum -= FIXED; }
            if (_accum > FIXED) _accum = 0f; // drop backlog rather than spiral
        }

        void Step(float dt)
        {
            stats.time += dt;
            grid.Rebuild(enemies);               // for player/clone nearestEnemy this step

            player.Update(dt, this, _input.MoveVec);
            for (int s = 0; s < 4; s++) if (_moveQueued[s]) { player.UseMove(s, this); _moveQueued[s] = false; }
            if (_dashQueued) { player.Dash(this); _dashQueued = false; }

            UpdateWaves(dt);

            for (int i = 0; i < enemies.Count; i++) enemies[i].Update(dt, this);
            CullEnemies();

            grid.Rebuild(enemies);               // refresh after movement for projectile queries
            HandleProjectiles(dt);

            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                o.Update(dt, player);
                if (o.dead)
                {
                    player.AddXp(o.value, this); Sfx.Pickup();
                    AddParticle(o.x, o.y, 0f, 40f, .4f, Palette.Dna, 4f);
                }
            }
            CullOrbs();

            for (int i = 0; i < particles.Count; i++) particles[i].Update(dt);
            CullParticles();
            for (int i = 0; i < floaters.Count; i++) floaters[i].Update(dt);
            CullFloaters();
            for (int i = 0; i < beams.Count; i++) beams[i].life -= dt;
            CullBeams();
        }

        // ---------------------------------------------------------------- projectiles / collisions
        void HandleProjectiles(float dt)
        {
            for (int i = 0; i < projectiles.Count; i++)
            {
                var pr = projectiles[i];
                pr.Update(dt, w, h);
                if (pr.dead) continue;

                if (pr.playerOwned)
                {
                    _hitBuf.Clear();
                    grid.QueryCircle(pr.x, pr.y, pr.r + MAX_ENEMY_R, _hitBuf);
                    for (int k = 0; k < _hitBuf.Count; k++)
                    {
                        var e = _hitBuf[k];
                        if (e.dead) continue;
                        float rr = pr.r + e.r;
                        if (Rng.Dist2(pr.x, pr.y, e.x, e.y) < rr * rr)
                        {
                            e.Hurt(pr.damage, this, pr.vx * 0.012f, pr.vy * 0.012f);
                            if (pr.poison != 0f) e.ApplyPoison(pr.poison);
                            pr.dead = true;
                            AddParticle(pr.x, pr.y, 0f, 0f, .2f, pr.color, 4f);
                            break;
                        }
                    }
                }
                else
                {
                    float rr = pr.r + player.r;
                    if (Rng.Dist2(pr.x, pr.y, player.x, player.y) < rr * rr)
                    {
                        player.Hurt(pr.damage, this); pr.dead = true;
                    }
                }
            }
            CullProjectiles();
        }

        public Enemy NearestEnemy(float x, float y, float range) => grid.Nearest(x, y, range);

        public void Explode(float x, float y, float radius, float dmg, Color color)
        {
            Shake(8f);
            for (int i = 0; i < 24; i++)
            {
                float a = Fx.Rand(0f, Mathf.PI * 2f), s = Fx.Rand(60f, 300f);
                AddParticle(x, y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.3f, .6f), color, Fx.Rand(3f, 6f));
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (Rng.Dist2(x, y, e.x, e.y) < (radius + e.r) * (radius + e.r)) e.Hurt(dmg, this);
            }
            if (Rng.Dist2(x, y, player.x, player.y) < (radius + player.r) * (radius + player.r))
                player.Hurt(dmg * 0.5f, this);
        }

        // ---------------------------------------------------------------- waves / spawning
        public float WaveScale => 1f + (wave - 1) * 0.18f;

        void UpdateWaves(float dt)
        {
            if (intermission > 0f)
            {
                intermission -= dt;
                if (intermission <= 0f && pendingBoss) { pendingBoss = false; SpawnBoss(); }
                return;
            }
            waveTimer -= dt; if (waveTimer <= 0f) { AdvanceWave(); return; }
            spawnCd -= dt;
            int target = 5 + wave * 2;
            float interval = Mathf.Max(0.32f, 1.5f - wave * 0.07f);
            if (spawnCd <= 0f && enemies.Count < target)
            {
                spawnCd = interval;
                int batch = 1 + (wave > 6 ? 1 : 0);
                for (int i = 0; i < batch; i++) SpawnEnemy();
            }
        }

        void AdvanceWave()
        {
            wave++; waveTimer = 22f;
            bool boss = wave % 5 == 0;
            intermission = boss ? 3.0f : 2.0f;
            if (boss)
            {
                pendingBoss = true;
                if (!endless && wave >= WIN_WAVE) finalBossPending = true;
                SetBanner("WAVE " + wave, (!endless && wave >= WIN_WAVE) ? "◆ FINAL BOSS ◆" : "◆ BOSS INCOMING ◆", Palette.BossBar, 2.6f);
            }
            else SetBanner("WAVE " + wave, null, Palette.Dna, 1.8f);
        }

        void EdgeSpawn(out float x, out float y)
        {
            int side = Rng.RandI(0, 3);
            if (side == 0) { x = Rng.Rand(0f, w); y = -20f; }
            else if (side == 1) { x = w + 20f; y = Rng.Rand(0f, h); }
            else if (side == 2) { x = Rng.Rand(0f, w); y = h + 20f; }
            else { x = -20f; y = Rng.Rand(0f, h); }
        }

        void SpawnEnemy()
        {
            EdgeSpawn(out float x, out float y);
            int wv = wave;
            // weights mirror the prototype spawnEnemy table exactly
            (string id, float wt)[] weights =
            {
                ("chaser", 5f),
                ("fast", wv >= 2 ? 4f : 1f),
                ("tank", wv >= 3 ? 2.5f : 0.3f),
                ("spitter", wv >= 2 ? 2.5f : 0.5f),
                ("exploder", wv >= 4 ? 2.5f : 0.2f),
            };
            float total = 0f; foreach (var ww in weights) total += ww.wt;
            float r = Rng.Rand(0f, total); string type = "chaser";
            foreach (var ww in weights) { r -= ww.wt; if (r <= 0f) { type = ww.id; break; } }
            string elite = null;
            if (wv >= 3) { float chance = 0.05f + wv * 0.008f; if (Rng.Next() < chance) elite = Rng.Next() < 0.5f ? "tough" : "frenzied"; }
            SpawnType(type, x, y, WaveScale, elite);
        }

        void SpawnType(string id, float x, float y, float scale, string elite = null)
        {
            if (!_enemyById.TryGetValue(id, out var def)) return;
            enemies.Add(new Enemy(def, x, y, scale, elite));
        }

        void SpawnBoss()
        {
            if (_bossDef == null) return;
            var e = new Enemy(_bossDef, w / 2f, -40f, 1f + (wave / 5f - 1f) * 0.6f);
            if (finalBossPending) { e.finalBoss = true; finalBossPending = false; }
            enemies.Add(e); bossAlive = true; Sfx.Boss(); Shake(12f);
        }

        public void OnBossKilled(Enemy boss)
        {
            bossAlive = false; rerolls++; ui.UpdateReroll();
            if (boss != null && boss.finalBoss && !endless) Win();
        }

        // ---------------------------------------------------------------- level up / draft
        public void OnLevelUp() { pendingLevels++; if (state == GameState.Playing) OpenDraft(); }

        void OpenDraft()
        {
            state = GameState.LevelUp;
            draftOptions = mutations.Draft(player, 3);
            _draftGuard = 0.25f;
            Sfx.LevelUp();
            ui.ShowDraft(draftOptions, Choose);
        }

        void HandleDraftInput()
        {
            if (state == GameState.Replace)
            {
                if (_input.Move1) DoReplace(1);
                else if (_input.Move2) DoReplace(2);
                else if (_input.Move3) DoReplace(3);
                else if (_input.Move4) DoReplace(-1); // discard
                return;
            }
            if (draftOptions == null) return;
            int pick = _input.Move1 ? 0 : _input.Move2 ? 1 : _input.Move3 ? 2 : -1;
            if (pick >= 0 && pick < draftOptions.Count) { Choose(draftOptions[pick]); return; }
            if (_input.Reroll) Reroll();
        }

        public void Reroll()
        {
            if (rerolls <= 0 || state != GameState.LevelUp) return;
            rerolls--;
            draftOptions = mutations.Draft(player, 3);
            _draftGuard = 0.2f;
            ui.ShowDraft(draftOptions, Choose); Sfx.Pickup();
        }

        public void Choose(MutationDef def)
        {
            if (state != GameState.LevelUp || _draftGuard > 0f) return;
            if (!string.IsNullOrEmpty(def.move))
            {
                if (player.HasMove(def.move))
                {
                    int lvl = (player.moveLevel[def.move] += 1);
                    player.mutations[def.move] = lvl;
                    MutationEffects.Apply(def.move, player);
                    bool ev = def.maxStacks != 0 && lvl == def.maxStacks && !player.evolved.Contains(def.move);
                    if (ev) { player.evolved.Add(def.move); MutationEffects.Evolve(def.move, player); }
                    AfterPick(def, ev, ev ? def.evolveName : null);
                }
                else
                {
                    int slot = player.FreeMoveSlot();
                    if (slot >= 0) { LearnInto(def, slot); AfterPick(def, false, null); }
                    else BeginReplace(def);
                }
            }
            else
            {
                var res = mutations.Pick(def, player);
                AfterPick(def, res.evolved, res.name);
            }
        }

        void LearnInto(MutationDef def, int slot)
        {
            player.moveSlots[slot] = def.move;
            player.moveLevel[def.move] = 1;
            player.mutations[def.move] = 1;
            MutationEffects.Apply(def.move, player);
        }

        void BeginReplace(MutationDef def)
        {
            _pendingReplace = def; state = GameState.Replace; _draftGuard = 0.25f;
            ui.HideDraft(); ui.ShowReplace(def, DoReplace);
        }

        public void DoReplace(int idx)
        {
            if (state != GameState.Replace || _draftGuard > 0f) return;
            var def = _pendingReplace; _pendingReplace = null; ui.HideReplace();
            if (idx >= 1 && idx <= 3)
            {
                string old = player.moveSlots[idx];
                if (old != null)
                {
                    player.mutations.Remove(old); player.moveLevel.Remove(old);
                    player.evolved.Remove(old); player.moveCd.Remove(old);
                }
                LearnInto(def, idx);
            }
            AfterPick(def, false, null);
        }

        void AfterPick(MutationDef def, bool evolved, string name)
        {
            for (int i = 0; i < 26; i++)
            {
                float a = Fx.Rand(0f, TAU), s = Fx.Rand(60f, 260f);
                AddParticle(player.x, player.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.4f, .8f), def.color, Fx.Rand(2f, 5f));
            }
            Shake(6f);
            if (evolved)
            {
                Sfx.Evolve(); Haptics.Success(); SetBanner("EVOLVED", name, Palette.FloaterBig, 2.6f);
                for (int i = 0; i < 30; i++)
                {
                    float a = Fx.Rand(0f, TAU), s = Fx.Rand(80f, 300f);
                    AddParticle(player.x, player.y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.5f, .9f), Palette.FloaterBig, Fx.Rand(3f, 6f));
                }
            }
            else { Sfx.Mutate(); Haptics.Selection(); }
            ui.HideDraft(); ui.RenderMuts();
            pendingLevels--;
            if (pendingLevels > 0) OpenDraft();
            else state = GameState.Playing;
        }

        public List<string> ActiveSynergies(Player p)
        {
            var s = new List<string>();
            if (p.HasMove("firebreath") && p.HasMove("venom")) s.Add("Wildfire");
            if (p.HasMove("laser") && p.Has("arms")) s.Add("Overcharge");
            if (p.HasMove("tailswipe") && p.Has("thick")) s.Add("Bramble Plate");
            return s;
        }

        const float TAU = Mathf.PI * 2f;

        // ---------------------------------------------------------------- run lifecycle
        public void GameOver()
        {
            if (state == GameState.Dead || state == GameState.Won) return;
            state = GameState.Dead; Sfx.Over();
            Explode(player.x, player.y, 40f, 0f, Palette.Ink);
            ui.ShowEnd(false);
        }

        public void Win()
        {
            if (state == GameState.Dead || state == GameState.Won) return;
            state = GameState.Won; Sfx.Win();
            SetBanner("VICTORY", null, Palette.Dna, 3f);
            ui.ShowEnd(true);
        }

        public void StartRun(string seed = null)
        {
            if (seed == null)
            {
                string v = ui.GetSeedField().Trim();
                seed = string.IsNullOrEmpty(v) ? ((uint)(Fx.Rand(0f, 1f) * 4294967296.0)).ToString() : v;
            }
            seedText = seed;
            Rng.Set(Rng.SeedToInt(seed));
            ui.SetSeedField(seed);
            endless = ui.GetEndless();
            autocast = ui.GetAutocast();
            Reset();
            ui.HideStart(); ui.HideEnd(); ui.HidePause();
            SetBanner(endless ? "ENDLESS" : "WAVE 1", null, Palette.Dna, 1.6f);
            state = GameState.Playing;
        }

        public void ReplaySeed() => StartRun(seedText);
        public void GotoMenu() { ReleaseAll(); state = GameState.Menu; _paused = false; ui.HideEnd(); ui.HideDraft(); ui.HideReplace(); ui.HidePause(); ui.ShowStart(); }

        public void TogglePause()
        {
            if (state != GameState.Playing) return;
            _paused = !_paused;
            if (_paused) ui.ShowPause(); else ui.HidePause();
        }

        void Reset()
        {
            ReleaseAll();
            player = new Player(this);
            wave = 1; waveTimer = 22f; spawnCd = 0f; intermission = 0f;
            pendingBoss = false; finalBossPending = false;
            bossAlive = false; bossesSpawned.Clear();
            rerolls = 3; banner = null; _paused = false;
            stats = new RunStats();
            pendingLevels = 0; god = false; slowmo = false; _accum = 0f; _dashQueued = false;
            for (int i = 0; i < 4; i++) _moveQueued[i] = false;
            _pendingReplace = null;
            ui.RefreshGodBtn(); ui.RenderMuts();
        }

        // ---------------------------------------------------------------- spawn helpers (allocate + pool)
        public void AddParticle(float x, float y, float vx, float vy, float life, Color color, float size)
        {
            var p = _freeParticles.Count > 0 ? _freeParticles.Pop() : new Particle();
            particles.Add(p.Set(x, y, vx, vy, life, color, size));
        }

        public void AddFloater(float x, float y, string text, Color color, float size)
        {
            var f = _freeFloaters.Count > 0 ? _freeFloaters.Pop() : new Floater();
            floaters.Add(f.Set(x, y, text, color, size));
        }

        public void AddProjectile(float x, float y, float vx, float vy, bool playerOwned, float damage, Color color, float r = 5f, float life = 3f, float poison = 0f)
        {
            var pr = _freeProjectiles.Count > 0 ? _freeProjectiles.Pop() : new Projectile();
            projectiles.Add(pr.Set(x, y, vx, vy, playerOwned, damage, color, r, life, poison));
        }

        public void AddOrb(float x, float y, float value)
        {
            var o = _freeOrbs.Count > 0 ? _freeOrbs.Pop() : new DNAOrb();
            orbs.Add(o.Set(x, y, value));
        }

        public void AddBeam(float x1, float y1, float x2, float y2, Color color)
        {
            var b = _freeBeams.Count > 0 ? _freeBeams.Pop() : new Beam();
            beams.Add(b.Set(x1, y1, x2, y2, color));
        }

        // ---------------------------------------------------------------- culling (release sim object + view)
        void CullEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.dead) { ReleaseView(ref e.view); enemies.RemoveAt(i); }
            }
        }
        void CullProjectiles()
        {
            for (int i = projectiles.Count - 1; i >= 0; i--)
                if (projectiles[i].dead) { var p = projectiles[i]; ReleaseView(ref p.view); projectiles.RemoveAt(i); _freeProjectiles.Push(p); }
        }
        void CullOrbs()
        {
            for (int i = orbs.Count - 1; i >= 0; i--)
                if (orbs[i].dead) { var o = orbs[i]; ReleaseView(ref o.view); orbs.RemoveAt(i); _freeOrbs.Push(o); }
        }
        void CullParticles()
        {
            for (int i = particles.Count - 1; i >= 0; i--)
                if (particles[i].Dead) { var p = particles[i]; ReleaseView(ref p.view); particles.RemoveAt(i); _freeParticles.Push(p); }
        }
        void CullFloaters()
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
                if (floaters[i].Dead) { var f = floaters[i]; if (f.view != null) { _labelPool.Release(f.view); f.view = null; } floaters.RemoveAt(i); _freeFloaters.Push(f); }
        }
        void CullBeams()
        {
            for (int i = beams.Count - 1; i >= 0; i--)
                if (beams[i].Dead) { var b = beams[i]; if (b.view != null) { _beamPool.Release(b.view); b.view = null; } beams.RemoveAt(i); _freeBeams.Push(b); }
        }

        void ReleaseView(ref SpriteView v)
        {
            if (v == null) return;
            v.HideBar();
            _spritePool.Release(v);
            v = null;
        }

        void ReleaseAll()
        {
            foreach (var e in enemies) ReleaseView(ref e.view);
            enemies.Clear();
            foreach (var p in projectiles) { ReleaseView(ref p.view); _freeProjectiles.Push(p); }
            projectiles.Clear();
            foreach (var o in orbs) { ReleaseView(ref o.view); _freeOrbs.Push(o); }
            orbs.Clear();
            foreach (var p in particles) { ReleaseView(ref p.view); _freeParticles.Push(p); }
            particles.Clear();
            foreach (var f in floaters) { if (f.view != null) { _labelPool.Release(f.view); f.view = null; } _freeFloaters.Push(f); }
            floaters.Clear();
            foreach (var b in beams) { if (b.view != null) { _beamPool.Release(b.view); b.view = null; } _freeBeams.Push(b); }
            beams.Clear();
            if (player != null) ReleaseView(ref player.view);
        }

        // ---------------------------------------------------------------- render (sync views)
        void Render()
        {
            // Camera is static (set in SetupCamera); Feel's MMCameraShaker owns all camera shake.
            if (state == GameState.Menu) { _playerVisual.Sync(null, false); return; }

            // orbs
            for (int i = 0; i < orbs.Count; i++) { var o = orbs[i]; EnsureView(ref o.view); o.view.HideBar(); o.view.Set(o.x, o.y, o.PulseR, Palette.Dna, 0.5f); }
            // beams
            for (int i = 0; i < beams.Count; i++) { var b = beams[i]; EnsureBeam(b); b.view.Set(b.x1, b.y1, b.x2, b.y2, b.color, b.Alpha); }
            // enemies
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i]; EnsureView(ref e.view);
                bool telegraph = e.cfg.boss && e.windup > 0f;
                Color col = e.hitFlash > 0f ? Color.white
                    : telegraph ? Color.Lerp(e.color, Color.red, 0.5f + 0.4f * Mathf.Sin(e.t * 30f))
                    : e.color;
                float rad = e.r * Mathf.Max(0.001f, e.scale);
                float glowA = telegraph ? 0.9f : (e.elite != null ? 0.6f : 0.35f);
                e.view.Set(e.x, e.y, rad, col, glowA);
                if (e.elite != null) e.view.glow.color = new Color(1f, 0.82f, 0.29f, 0.6f); // gold elite halo
                if ((e.cfg.boss || e.r >= 18f || e.elite != null) && e.hp < e.maxHp)
                    e.view.ShowBar(e.hp / e.maxHp, rad, e.cfg.boss ? Palette.BossBar : (e.elite != null ? Palette.FloaterBig : Palette.EnemyBar));
                else e.view.HideBar();
            }
            // projectiles
            for (int i = 0; i < projectiles.Count; i++) { var p = projectiles[i]; EnsureView(ref p.view); p.view.HideBar(); p.view.Set(p.x, p.y, p.r, p.color, 0.6f); }
            // particles
            for (int i = 0; i < particles.Count; i++)
            {
                var pt = particles[i]; EnsureView(ref pt.view); pt.view.HideBar();
                float a = pt.Alpha;
                Color c = pt.color; c.a = a;
                pt.view.Set(pt.x, pt.y, Mathf.Max(0.01f, pt.size * a), c, 0f);
            }
            // player (procedural creature)
            _playerVisual.Sync(player, player != null);
            // clones
            if (player != null)
                foreach (var c in player.clones) { EnsureView(ref c.view); c.view.HideBar(); c.view.Set(c.x, c.y, c.r, Palette.CloneBody, 0.4f); }
            // floaters
            for (int i = 0; i < floaters.Count; i++)
            {
                var f = floaters[i]; EnsureLabel(f);
                f.view.Set(f.x, f.y, f.text, f.color, f.size, f.Alpha);
            }
        }

        void EnsureView(ref SpriteView v) { if (v == null) v = _spritePool.Get(); }
        void EnsureLabel(Floater f) { if (f.view == null) f.view = _labelPool.Get(); }
        void EnsureBeam(Beam b) { if (b.view == null) b.view = _beamPool.Get(); }

        // ---------------------------------------------------------------- misc / debug
        public void Shake(float a) { if (_juice != null) _juice.Shake(a); }
        public void ToggleDebug() { debug = !debug; ui.SetDebugVisible(debug); }

        public void DebugGod() { god = !god; ui.RefreshGodBtn(); }
        public void DebugLevel() { if (player != null) player.AddXp(player.xpNext - player.xp + 1f, this); }
        public void DebugDna() { if (player != null) player.AddXp(200f, this); }
        public void DebugSlow() { slowmo = !slowmo; }
        public void DebugTouch() { TouchInput.ForceTouch = !TouchInput.ForceTouch; }
        public void DebugKillAll() { foreach (var e in new List<Enemy>(enemies)) if (!e.dead) e.Die(this); }
        public void DebugSpawnBoss() { if (state == GameState.Playing) SpawnBoss(); }
        public void DebugSpawn(string id)
        {
            if (state != GameState.Playing) return;
            EdgeSpawn(out float x, out float y);
            SpawnType(id, x, y, WaveScale);
        }
        public void DebugGrantMutation()
        {
            if (player == null) return;
            var opts = mutations.Available(player);
            if (opts.Count == 0) return;
            var def = opts[Rng.RandI(0, opts.Count - 1)];
            if (!string.IsNullOrEmpty(def.move))
            {
                if (!player.HasMove(def.move)) { int slot = player.FreeMoveSlot(); if (slot < 0) return; LearnInto(def, slot); }
                else { int lvl = (player.moveLevel[def.move] += 1); player.mutations[def.move] = lvl; MutationEffects.Apply(def.move, player); }
            }
            else mutations.Pick(def, player);
            ui.RenderMuts(); Sfx.Mutate();
        }

        public string DebugInfo() =>
            $"fps {Mathf.Round(fps)}   wave {wave}\nenemies {enemies.Count}  proj {projectiles.Count}\nparts {particles.Count}  orbs {orbs.Count}";
        public bool BossAlive => bossAlive;
    }
}
