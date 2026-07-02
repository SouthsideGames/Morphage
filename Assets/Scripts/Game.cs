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
        public readonly Player[] players = new Player[2];
        public int localIndex = 0;                    // which hero this device drives (host = 0)
        public Player player => players[localIndex];  // the local hero — keeps existing UI/code working
        public readonly List<Enemy> enemies = new();
        public readonly List<Projectile> projectiles = new();
        public readonly List<DNAOrb> orbs = new();
        public readonly List<Particle> particles = new();
        public readonly List<Floater> floaters = new();
        public readonly List<Beam> beams = new();
        public readonly List<Hazard> hazards = new();
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
        public bool coop; // co-op game mode: spawn a second hero (a bot for now). Off = normal single-player.
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
        Pool<ParticleView> _particlePool;
        Pool<LabelView> _labelPool;
        Pool<BeamView> _beamPool;

        public static float ParticleScale = 1f; // <1 on mobile: fraction of cosmetic particles actually spawned
        public static float ParticleBase = 1f;  // platform baseline before the Reduced-Effects setting
        readonly Stack<Particle> _freeParticles = new();
        readonly Stack<Floater> _freeFloaters = new();
        readonly Stack<Projectile> _freeProjectiles = new();
        readonly Stack<DNAOrb> _freeOrbs = new();
        readonly Stack<Beam> _freeBeams = new();
        readonly Stack<Hazard> _freeHazards = new();
        readonly Stack<Enemy> _freeEnemies = new();

        Enemy NewEnemy(EnemyDef def, float x, float y, float scale, string elite = null)
        {
            var e = _freeEnemies.Count > 0 ? _freeEnemies.Pop() : new Enemy();
            return e.Set(def, x, y, scale, elite);
        }

        readonly List<Enemy> _hitBuf = new();
        const float MAX_ENEMY_R = 48f; // boss radius, for grid query padding

        InputReader _input = new();
        readonly bool[] _moveQueued = new bool[4];
        bool _dashQueued;
        float _accum;
        // co-op lockstep state
        readonly Net.TickInput[] _stepInput = new Net.TickInput[2];        // this tick's input per hero (index 0/1)
        readonly Dictionary<int, Net.TickInput> _localBuf = new();         // our sent inputs, keyed by tick (for redundancy)
        readonly List<Net.TickInput> _sendBatch = new();                   // reused scratch for a redundant send
        int _execTick, _stallFrames;
        byte _pendingDraftByte;      // co-op: the local hero's queued upgrade pick, sent with the next input
        const int InputDelay = 4;   // ticks of input pipeline (hides ~66ms of latency)
        const int Redundancy = 12;  // recent inputs repeated in each packet (survives dropped packets)
        readonly Dictionary<int, ulong> _localChecksums = new(); // co-op: our state hash at checksum ticks
        int _nextCompareTick;
        bool _desynced;
        const int ChecksumInterval = 30; // hash + compare full state every 30 ticks (~0.5s)
        Transform _viewRoot;
        Camera _cam;
        PlayerVisual[] _playerVisuals;
        Juice _juice;

        // ---------------------------------------------------------------- setup
        void Start()
        {
            // Mobile perf/UX: drive a steady 60 (mobile often defaults to 30) and keep the screen awake in-game.
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            ParticleBase = TouchInput.IsTouchDevice ? 0.55f : 1f; // thin out cosmetic particles on phones
            ParticleScale = ParticleBase;                          // GameSettings.Load() applies the Reduced-Effects setting

            // Fit the bounded arena to the device aspect so the dish fills the screen (no letterbox bands).
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (aspect >= 1f) w = Mathf.Clamp(h * aspect, 960f, 1500f);     // landscape: widen
            else h = Mathf.Clamp(w / Mathf.Max(0.01f, aspect), 600f, 1100f); // portrait: heighten

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
            Vfx.Init(_viewRoot);

            _spritePool = new Pool<SpriteView>(() => SpriteFactory.CreateSpriteView(_viewRoot), 96);
            _particlePool = new Pool<ParticleView>(() => SpriteFactory.CreateParticleView(_viewRoot), 256);
            _labelPool = new Pool<LabelView>(() => SpriteFactory.CreateLabel(_viewRoot), 16);
            _beamPool = new Pool<BeamView>(() => SpriteFactory.CreateBeam(_viewRoot), 4);
            _playerVisuals = new PlayerVisual[2];
            for (int i = 0; i < 2; i++) _playerVisuals[i] = PlayerVisual.Create(_viewRoot);

            SetupCamera();
            SetupFloor();
            _juice = Juice.Create(_viewRoot, _cam);

            var sfxSrc = gameObject.AddComponent<AudioSource>();
            var musicSrc = gameObject.AddComponent<AudioSource>();
            Sfx.Init(sfxSrc, musicSrc);
            GameSettings.Load();

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
            if (ui != null) { ui.SetTouchActive(touch); ui.SetPauseVisible(state == GameState.Playing && !coop); }

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
            if (ui != null)
            {
                if (player != null) ui.SyncHud();
                ui.UpdateBanner();
                ui.UpdateMoveBar();
                ui.UpdateDebug();
            }
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
            if (Net.CoopNet.Connected)
            {
                int start = _execTick;
                while (_accum >= FIXED && guard++ < 8)
                {
                    if (!Net.CoopSync.TryGetRemote(_execTick, out var remoteIn)) break; // stall: partner input not here yet
                    if (!_localBuf.TryGetValue(_execTick, out var localIn)) break;       // safety (always primed/sampled)
                    // sample local input for a future tick (pipelined) and broadcast it redundantly
                    SampleAndSend(_execTick + InputDelay, _input.MoveVec,
                        _moveQueued[0], _moveQueued[1], _moveQueued[2], _moveQueued[3], _dashQueued, _pendingDraftByte);
                    for (int s = 0; s < 4; s++) _moveQueued[s] = false;
                    _dashQueued = false; _pendingDraftByte = 0;
                    // assign this tick's inputs by hero index (identical on both peers), then step
                    _stepInput[localIndex] = localIn;
                    _stepInput[1 - localIndex] = remoteIn;
                    Step(FIXED);
                    if (_execTick % ChecksumInterval == 0) { ulong h = Checksum(); _localChecksums[_execTick] = h; Net.CoopSync.SendChecksum(_execTick, h); }
                    Net.CoopSync.RemoveRemote(_execTick);
                    _localBuf.Remove(_execTick - InputDelay - Redundancy);
                    _execTick++;
                    _accum -= FIXED;
                }
                if (_execTick == start && _accum >= FIXED)   // didn't advance = stalled on the partner
                {
                    ResendRecent();
                    if (++_stallFrames == 90)
                        Debug.LogWarning($"[MUTAGEN][net] stalled at tick {_execTick} (~1.5s) — waiting for partner (lag/packet loss)");
                }
                else _stallFrames = 0;
                if (_accum > FIXED * 30f) _accum = FIXED * 30f; // cap catch-up backlog
                CompareChecksums();
            }
            else
            {
                while (_accum >= FIXED && guard++ < 8) { Step(FIXED); _accum -= FIXED; }
                if (_accum > FIXED) _accum = 0f; // drop backlog rather than spiral
            }
        }

        void Step(float dt)
        {
            stats.time += dt;
            grid.Rebuild(enemies);               // for player/clone nearestEnemy this step

            // Per-hero update in fixed index order (deterministic). Networked: both heroes run from this
            // tick's pre-assigned inputs (_stepInput, set in Tick) — identical on both peers. Solo: local
            // input drives the local hero, bot AI drives the other.
            if (Net.CoopNet.Connected)
            {
                for (int i = 0; i < players.Length; i++)
                {
                    var p = players[i];
                    if (p == null || !p.alive) continue;
                    if (p.pendingDrafts > 0) p.invuln = Mathf.Max(p.invuln, 0.25f); // stay shielded the whole time you're choosing
                    var inp = _stepInput[i];
                    if (inp.draft != 0) ApplyCoopDraft(p, inp.draft);   // upgrade pick — same tick on both peers
                    p.Update(dt, this, inp.MoveVec);
                    for (int s = 0; s < 4; s++) if (inp.Move(s)) p.UseMove(s, this);
                    if (inp.Dash) p.Dash(this);
                }
            }
            else
            {
                for (int i = 0; i < players.Length; i++)
                {
                    var p = players[i];
                    if (p == null || !p.alive) continue;
                    if (p.isBot) { p.Update(dt, this, BotMove(p)); continue; }
                    p.Update(dt, this, _input.MoveVec);
                    for (int s = 0; s < 4; s++) if (_moveQueued[s]) { p.UseMove(s, this); _moveQueued[s] = false; }
                    if (_dashQueued) { p.Dash(this); _dashQueued = false; }
                }
                for (int s = 0; s < 4; s++) _moveQueued[s] = false; // drop any input not consumed (e.g. local hero down)
                _dashQueued = false;
            }

            UpdateWaves(dt);

            for (int i = 0; i < enemies.Count; i++) enemies[i].Update(dt, this);
            CullEnemies();

            grid.Rebuild(enemies);               // refresh after movement for projectile queries
            HandleProjectiles(dt);

            for (int i = 0; i < hazards.Count; i++) hazards[i].Update(dt, this);
            CullHazards();

            for (int i = 0; i < orbs.Count; i++)
            {
                var o = orbs[i];
                o.Update(dt, this);
                if (o.dead)
                {
                    if (o.collector != null) o.collector.AddXp(o.value, this);
                    Sfx.Pickup();
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
                            e.Hurt(pr.damage, this, pr.owner, pr.vx * 0.012f, pr.vy * 0.012f);
                            if (pr.poison != 0f) e.ApplyPoison(pr.poison);
                            pr.dead = true;
                            AddParticle(pr.x, pr.y, 0f, 0f, .2f, pr.color, 4f);
                            break;
                        }
                    }
                }
                else
                {
                    for (int pi = 0; pi < players.Length; pi++)
                    {
                        var pl = players[pi];
                        if (pl == null || !pl.alive) continue;
                        float rr = pr.r + pl.r;
                        if (Rng.Dist2(pr.x, pr.y, pl.x, pl.y) < rr * rr)
                        { pl.Hurt(pr.damage, this); pr.dead = true; break; }
                    }
                }
            }
            CullProjectiles();
        }

        public Enemy NearestEnemy(float x, float y, float range) => grid.Nearest(x, y, range);

        // Nearest living hero to a point (low-index tie-break). Null if every hero is down.
        public Player NearestPlayer(float x, float y)
        {
            Player best = null; float bestD = float.MaxValue;
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p == null || !p.alive) continue;
                float d = Rng.Dist2(x, y, p.x, p.y);
                if (d < bestD) { bestD = d; best = p; }
            }
            return best;
        }

        // The non-local hero (co-op partner), or null in single-player.
        public Player Partner()
        {
            for (int i = 0; i < players.Length; i++)
                if (i != localIndex && players[i] != null) return players[i];
            return null;
        }

        // Throwaway bot AI for the local loopback test: approach the nearest enemy, back off if too
        // close. Deterministic (no cosmetic RNG) so it never affects the determinism gate.
        Vector2 BotMove(Player p)
        {
            var e = NearestEnemy(p.x, p.y, 99999f);
            if (e == null) return Vector2.zero;
            float dx = e.x - p.x, dy = e.y - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
            if (d < 1f) return Vector2.zero;
            float nx = dx / d, ny = dy / d;
            float ideal = e.r + 34f;                            // sit just outside the enemy, in melee range
            if (d > ideal + 22f) return new Vector2(nx, ny);    // too far: close in
            if (d < ideal - 22f) return new Vector2(-nx, -ny);  // too close: ease back
            return new Vector2(-ny, nx);                        // in the band: orbit rather than jitter toward/away
        }

        public void Explode(float x, float y, float radius, float dmg, Color color)
        {
            Shake(8f);
            Vfx.Spawn("Explosion", x, y, radius * 0.6f);
            for (int i = 0; i < 24; i++)
            {
                float a = Fx.Rand(0f, Mathf.PI * 2f), s = Fx.Rand(60f, 300f);
                AddParticle(x, y, Mathf.Cos(a) * s, Mathf.Sin(a) * s, Fx.Rand(.3f, .6f), color, Fx.Rand(3f, 6f));
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                var e = enemies[i];
                if (Rng.Dist2(x, y, e.x, e.y) < (radius + e.r) * (radius + e.r)) e.Hurt(dmg, this, null);
            }
            for (int pi = 0; pi < players.Length; pi++)
            {
                var pl = players[pi];
                if (pl == null || !pl.alive) continue;
                if (Rng.Dist2(x, y, pl.x, pl.y) < (radius + pl.r) * (radius + pl.r))
                    pl.Hurt(dmg * 0.5f, this);
            }
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
            enemies.Add(NewEnemy(def, x, y, scale, elite));
        }

        void SpawnBoss()
        {
            if (_bossDef == null) return;
            var e = NewEnemy(_bossDef, w / 2f, -40f, 1f + (wave / 5f - 1f) * 0.6f);
            if (finalBossPending) { e.finalBoss = true; finalBossPending = false; }
            enemies.Add(e); bossAlive = true; Sfx.Boss(); Shake(12f);
            Vfx.Spawn("BossExplosion", e.x, e.y, 80f);
        }

        public void OnBossKilled(Enemy boss)
        {
            bossAlive = false; rerolls++; ui.UpdateReroll();
            if (boss != null && boss.finalBoss && !endless) Win();
        }

        // ---------------------------------------------------------------- level up / draft
        public void OnLevelUp(Player p)
        {
            if (coop)
            {
                if (p.isBot && !Net.CoopNet.Connected) { BotDraft(p); return; } // solo loopback bot auto-picks
                p.pendingDrafts++;
                if (p.draftOptions == null) p.draftOptions = mutations.Draft(p, 3); // deterministic on both peers
                p.invuln = Mathf.Max(p.invuln, 1f);              // grace while picking — set on BOTH peers (it's sim state)
                if (p.index == localIndex && p.pendingDrafts == 1) ShowLocalCoopDraft(p); // panel is UI-only (local)
                return;
            }
            if (p.isBot) { BotDraft(p); return; }               // bot auto-picks; never blocks on the UI draft
            pendingLevels++; if (state == GameState.Playing) OpenDraft();
        }

        void ShowLocalCoopDraft(Player p) =>
            ui.ShowDraft(p.draftOptions, def => SetLocalDraftPick(p.draftOptions.IndexOf(def)));

        // The local hero clicked a co-op upgrade card; queue the choice into the next input packet so it
        // applies on the same tick on both peers.
        public void SetLocalDraftPick(int idx) { if (idx >= 0) _pendingDraftByte = (byte)(idx + 1); }

        // Apply a co-op upgrade pick to hero p — driven by the TickInput.draft byte, so it runs identically
        // on both peers at the same tick.
        void ApplyCoopDraft(Player p, byte draft)
        {
            if (p.draftOptions == null || p.pendingDrafts <= 0) return;
            int idx = draft - 1;
            if (idx < 0 || idx >= p.draftOptions.Count) return;
            ApplyMutation(p, p.draftOptions[idx]);
            p.pendingDrafts--;
            p.draftOptions = p.pendingDrafts > 0 ? mutations.Draft(p, 3) : null;
            if (p.index == localIndex)
            {
                if (p.draftOptions != null) ShowLocalCoopDraft(p); else ui.HideDraft();
                ui.RenderMuts();
            }
        }

        // Grant a drafted mutation to hero p (shared by co-op picks and the loopback bot).
        void ApplyMutation(Player p, MutationDef def)
        {
            if (!string.IsNullOrEmpty(def.move))
            {
                if (p.HasMove(def.move))
                {
                    int lvl = (p.moveLevel[def.move] += 1);
                    p.mutations[def.move] = lvl;
                    MutationEffects.Apply(def.move, p);
                    if (def.maxStacks != 0 && lvl == def.maxStacks && !p.evolved.Contains(def.move))
                    { p.evolved.Add(def.move); MutationEffects.Evolve(def.move, p); }
                }
                else
                {
                    int slot = p.FreeMoveSlot();
                    if (slot >= 0) { p.moveSlots[slot] = def.move; p.moveLevel[def.move] = 1; p.mutations[def.move] = 1; MutationEffects.Apply(def.move, p); }
                    // loadout full: pick is skipped (no co-op replace flow yet)
                }
            }
            else mutations.Pick(def, p);
        }

        // A loopback bot resolves its pick immediately (first drafted option), no UI.
        void BotDraft(Player p)
        {
            var opts = mutations.Draft(p, 3);
            if (opts.Count > 0) ApplyMutation(p, opts[0]);
        }

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
                if (_input.Move1) DoReplace(0);
                else if (_input.Move2) DoReplace(1);
                else if (_input.Move3) DoReplace(2);
                else if (_input.Move4) DoReplace(3);
                else if (_input.Pause) DoReplace(-1); // Esc = discard the new move
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
            if (idx >= 0 && idx <= 3)
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
                Vfx.Spawn("Poof", player.x, player.y, 34f);
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
            if (p == null) return s;
            foreach (var d in Synergies.All) if (d.active(p)) s.Add(d.name);
            return s;
        }

        const float TAU = Mathf.PI * 2f;

        // ---------------------------------------------------------------- determinism harness
        // FNV-1a hash over the gameplay-critical state (NOT cosmetics). Used to verify the sim is
        // reproducible from a seed — the load-bearing assumption for lockstep multiplayer + a daily
        // seed challenge. Also the basis of the runtime desync checksum in the netcode plan.
        public ulong Checksum()
        {
            ulong h = 1469598103934665603UL;
            void Mix(long v) { h = (h ^ (ulong)v) * 1099511628211UL; }
            void MixF(float f) { Mix(System.BitConverter.SingleToInt32Bits(f)); }
            Mix(Rng.State);
            Mix(wave); MixF(waveTimer);
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                if (p != null) { MixF(p.x); MixF(p.y); MixF(p.hp); MixF(p.xp); Mix(p.level); }
            }
            Mix(enemies.Count);
            for (int i = 0; i < enemies.Count; i++) { var e = enemies[i]; MixF(e.x); MixF(e.y); MixF(e.hp); }
            Mix(projectiles.Count);
            for (int i = 0; i < projectiles.Count; i++) { var p = projectiles[i]; MixF(p.x); MixF(p.y); }
            return h;
        }

        // Run the sim twice from the same seed with identical (idle) input and compare the end-state
        // hash. God mode keeps the idle player alive (no death/draft side effects), so this isolates
        // spawn/enemy-AI/projectile/RNG determinism. Destructive: resets any in-progress run.
        public void RunDeterminismCheck(int ticks = 600)
        {
            uint seed = Rng.SeedToInt("determinism-probe");
            ulong a = SimulateHeadless(seed, ticks);
            ulong b = SimulateHeadless(seed, ticks);
            Debug.Log($"[MUTAGEN] Determinism {(a == b ? "PASS ✓" : "FAIL ✗")} after {ticks} ticks — {a:X16} vs {b:X16}");
            GotoMenu();
        }

        ulong SimulateHeadless(uint seed, int ticks)
        {
            Rng.Set(seed);
            bool prevGod = god, prevAuto = autocast, prevCoop = coop;
            coop = false;                 // the gate always tests the shipping single-player sim
            Reset();
            god = true; autocast = false; state = GameState.Playing;
            for (int i = 0; i < ticks; i++)
            {
                for (int s = 0; s < 4; s++) _moveQueued[s] = false;
                _dashQueued = false;
                Step(FIXED);
            }
            ulong h = Checksum();
            god = prevGod; autocast = prevAuto; coop = prevCoop;
            return h;
        }

        // ---------------------------------------------------------------- run lifecycle
        // A hero hit 0 HP. Play their death burst; the run only ends once every hero is down.
        public void OnPlayerDowned(Player p)
        {
            if (state == GameState.Dead || state == GameState.Won) return; // run already ended
            Explode(p.x, p.y, 40f, 0f, Palette.Ink);
            if (coop) { CoopEnd(); return; }  // competitive co-op: the first fall ends the run
            GameOver();                        // solo: one hero, so this ends it
        }

        // Co-op is competitive: the first hero to fall loses, the survivor wins. Runs deterministically on
        // both peers at the same tick; each device then shows its own result + updates its win/loss tally.
        void CoopEnd()
        {
            state = GameState.Dead; Sfx.Over();
            Player winner = null;
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null && players[i].alive) { winner = players[i]; break; }
            bool draw = winner == null;                     // both fell on the same tick
            bool localWon = !draw && winner.index == localIndex;
            CaptureMonster(localWon);
            if (!draw) SaveSystem.AddCoopResult(localWon);
            ui.HideDraft();
            ui.ShowCoopEnd(localWon, draw);
        }

        public void GameOver()
        {
            if (state == GameState.Dead || state == GameState.Won) return;
            state = GameState.Dead; Sfx.Over();
            CaptureMonster(false);
            ui.ShowEnd(false);
        }

        public void Win()
        {
            if (state == GameState.Dead || state == GameState.Won) return;
            state = GameState.Won; Sfx.Win();
            SetBanner("VICTORY", null, Palette.Dna, 3f);
            CaptureMonster(true);
            ui.ShowEnd(true);
        }

        // Snapshot the current creature (genome + run stats) into the persistent monster archive.
        void CaptureMonster(bool won)
        {
            var p = player; if (p == null) return;
            var rec = new MonsterRecord
            {
                won = won,
                wave = wave,
                level = p.level,
                kills = stats.kills,
                timeSurvived = stats.time,
                dps = stats.time > 1f ? stats.damageDealt / stats.time : stats.damageDealt,
                damageTaken = stats.damageTaken,
                seed = seedText ?? "",
                stamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                maxHp = p.maxHp,
                radius = p.r,
            };
            for (int i = 0; i < 4; i++) rec.moveSlots[i] = p.moveSlots[i] ?? "";
            var ids = new List<string>(); var stk = new List<int>();
            foreach (var kv in p.mutations) { ids.Add(kv.Key); stk.Add(kv.Value); }
            rec.mutIds = ids.ToArray(); rec.mutStacks = stk.ToArray();
            rec.evolved = new List<string>(p.evolved).ToArray();
            rec.name = MonsterNameFor(p);
            SaveSystem.AddMonster(rec);
        }

        // Flavour name from the creature's most defining mutations.
        static string MonsterNameFor(Player p)
        {
            string adj =
                p.Has("glasscannon") ? "Brittle" :
                p.Has("vampire") ? "Bloodthirsty" :
                (p.Has("toxicskin") || p.HasMove("venom")) ? "Venomous" :
                p.HasMove("frost") ? "Frozen" :
                p.Has("bulk") ? "Massive" :
                (p.Has("thick") || p.Has("carapace")) ? "Armored" :
                p.Has("wings") ? "Winged" :
                p.Stacks("arms") >= 2 ? "Many-Armed" :
                p.Has("quills") ? "Spiny" :
                p.Has("crit") ? "Unstable" : "Feral";
            string noun =
                p.HasMove("mitosis") ? "Swarm" :
                p.HasMove("laser") ? "Watcher" :
                (p.HasMove("gravity") || p.HasMove("well")) ? "Devourer" :
                (p.HasMove("inferno") || p.HasMove("firebreath")) ? "Drake" :
                p.HasMove("quake") ? "Titan" :
                p.HasMove("tentacle") ? "Horror" :
                p.HasMove("lightning") ? "Tempest" :
                p.evolved.Count > 0 ? "Behemoth" : "Specimen";
            return adj + " " + noun;
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

        // Both networked peers call this once connected: same seed → identical run. localIndex is
        // already set (host 0 / client 1); the non-local hero is driven by remote input in Step().
        public void StartCoopRun()
        {
            coop = true;
            Rng.Set(Rng.SeedToInt(seedText));
            Reset();
            // Prime the input pipeline: the first InputDelay ticks run neutral input on both sides, so
            // real input flows with a fixed delay and both sims stay tick-for-tick identical.
            _execTick = 0; _stallFrames = 0; _pendingDraftByte = 0; _nextCompareTick = 0; _desynced = false;
            _localBuf.Clear(); _localChecksums.Clear(); Net.CoopSync.ResetBuffer();
            for (int t = 0; t < InputDelay; t++) SampleAndSend(t, Vector2.zero, false, false, false, false, false);
            ui.HideStart(); ui.HideEnd(); ui.HidePause();
            SetBanner("CO-OP · WAVE 1", null, Palette.Dna, 1.8f);
            state = GameState.Playing;
        }

        // Buffer a local input for a tick and broadcast a redundant window of recent inputs.
        void SampleAndSend(int tick, Vector2 move, bool m1, bool m2, bool m3, bool m4, bool dash, byte draft = 0)
        {
            _localBuf[tick] = Net.TickInput.Local(tick, move, m1, m2, m3, m4, dash, draft);
            _sendBatch.Clear();
            for (int t = Mathf.Max(0, tick - Redundancy + 1); t <= tick; t++)
                if (_localBuf.TryGetValue(t, out var b)) _sendBatch.Add(b);
            Net.CoopSync.SendBatch(_sendBatch);
        }

        // While stalled (waiting on the partner), re-broadcast our newest inputs in case they were dropped.
        void ResendRecent()
        {
            int newest = _execTick + InputDelay - 1;
            _sendBatch.Clear();
            for (int t = Mathf.Max(0, newest - Redundancy + 1); t <= newest; t++)
                if (_localBuf.TryGetValue(t, out var b)) _sendBatch.Add(b);
            if (_sendBatch.Count > 0) Net.CoopSync.SendBatch(_sendBatch);
        }

        // Compare our state hash against the partner's at each checksum tick; flag the first divergence.
        void CompareChecksums()
        {
            while (_localChecksums.TryGetValue(_nextCompareTick, out var local) &&
                   Net.CoopSync.TryGetRemoteChecksum(_nextCompareTick, out var remote))
            {
                if (local != remote && !_desynced)
                {
                    _desynced = true;
                    Debug.LogError($"[MUTAGEN][net] DESYNC at tick {_nextCompareTick}: local {local:X16} vs peer {remote:X16}");
                    SetBanner("DESYNC", "games drifted out of sync", Palette.HurtRed, 6f);
                }
                _localChecksums.Remove(_nextCompareTick);
                Net.CoopSync.RemoveRemoteChecksum(_nextCompareTick);
                _nextCompareTick += ChecksumInterval;
            }
        }
        public void GotoMenu() { ReleaseAll(); state = GameState.Menu; _paused = false; ui.HideEnd(); ui.HideDraft(); ui.HideReplace(); ui.HidePause(); ui.ShowStart(); }

        public void TogglePause()
        {
            if (state != GameState.Playing || coop) return; // no pausing in co-op — it would freeze/desync the shared sim
            _paused = !_paused;
            if (_paused) ui.ShowPause(); else ui.HidePause();
        }

        void Reset()
        {
            ReleaseAll();
            int heroCount = coop ? 2 : 1;
            for (int i = 0; i < players.Length; i++)
            {
                if (i >= heroCount) { players[i] = null; continue; }       // solo: only hero 0 exists
                var p = new Player(this) { index = i, isBot = coop && i != localIndex };
                if (coop) p.x = w / 2f + (i == 0 ? -46f : 46f);            // co-op: nudge apart so they don't overlap
                players[i] = p;
            }
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
            // Cosmetic only (non-seeded): drop a fraction on mobile to cut GameObject churn.
            if (ParticleScale < 1f && UnityEngine.Random.value > ParticleScale) return;
            var p = _freeParticles.Count > 0 ? _freeParticles.Pop() : new Particle();
            particles.Add(p.Set(x, y, vx, vy, life, color, size));
        }

        public void AddFloater(float x, float y, string text, Color color, float size)
        {
            var f = _freeFloaters.Count > 0 ? _freeFloaters.Pop() : new Floater();
            floaters.Add(f.Set(x, y, text, color, size));
        }

        public void AddProjectile(float x, float y, float vx, float vy, bool playerOwned, float damage, Color color, float r = 5f, float life = 3f, float poison = 0f, Player owner = null)
        {
            var pr = _freeProjectiles.Count > 0 ? _freeProjectiles.Pop() : new Projectile();
            projectiles.Add(pr.Set(x, y, vx, vy, playerOwned, damage, color, r, life, poison, owner));
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

        public void AddHazard(float x, float y, float r, float dps, float life, float pull, float slowMul, bool poison, Color color, Player owner = null)
        {
            var h = _freeHazards.Count > 0 ? _freeHazards.Pop() : new Hazard();
            hazards.Add(h.Set(x, y, r, dps, life, pull, slowMul, poison, color, owner));
        }

        // ---------------------------------------------------------------- culling (release sim object + view)
        void CullEnemies()
        {
            for (int i = enemies.Count - 1; i >= 0; i--)
            {
                var e = enemies[i];
                if (e.dead) { ReleaseView(ref e.view); enemies.RemoveAt(i); _freeEnemies.Push(e); }
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
                if (particles[i].Dead) { var p = particles[i]; if (p.view != null) { _particlePool.Release(p.view); p.view = null; } particles.RemoveAt(i); _freeParticles.Push(p); }
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
        void CullHazards()
        {
            for (int i = hazards.Count - 1; i >= 0; i--)
                if (hazards[i].dead) { var h = hazards[i]; ReleaseView(ref h.view); hazards.RemoveAt(i); _freeHazards.Push(h); }
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
            foreach (var e in enemies) { ReleaseView(ref e.view); _freeEnemies.Push(e); }
            enemies.Clear();
            foreach (var p in projectiles) { ReleaseView(ref p.view); _freeProjectiles.Push(p); }
            projectiles.Clear();
            foreach (var o in orbs) { ReleaseView(ref o.view); _freeOrbs.Push(o); }
            orbs.Clear();
            foreach (var p in particles) { if (p.view != null) { _particlePool.Release(p.view); p.view = null; } _freeParticles.Push(p); }
            particles.Clear();
            foreach (var f in floaters) { if (f.view != null) { _labelPool.Release(f.view); f.view = null; } _freeFloaters.Push(f); }
            floaters.Clear();
            foreach (var b in beams) { if (b.view != null) { _beamPool.Release(b.view); b.view = null; } _freeBeams.Push(b); }
            beams.Clear();
            foreach (var h in hazards) { ReleaseView(ref h.view); _freeHazards.Push(h); }
            hazards.Clear();
            for (int i = 0; i < players.Length; i++)
                if (players[i] != null) ReleaseView(ref players[i].view);
        }

        // ---------------------------------------------------------------- render (sync views)
        void Render()
        {
            // Camera is static (set in SetupCamera); Feel's MMCameraShaker owns all camera shake.
            if (state == GameState.Menu) { for (int i = 0; i < 2; i++) _playerVisuals[i].Sync(null, false); return; }

            // hazard zones (soft translucent pools, drawn under everything else)
            for (int i = 0; i < hazards.Count; i++)
            {
                var hz = hazards[i]; EnsureView(ref hz.view); hz.view.HideBar();
                var c = hz.color; c.a = 0.16f * hz.Alpha;
                hz.view.Set(hz.x, hz.y, hz.r, c, 0.10f * hz.Alpha);
            }
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
            // particles (lightweight single-renderer views)
            for (int i = 0; i < particles.Count; i++)
            {
                var pt = particles[i];
                if (pt.view == null) pt.view = _particlePool.Get();
                float a = pt.Alpha;
                Color c = pt.color; c.a = a;
                pt.view.Set(pt.x, pt.y, Mathf.Max(0.01f, pt.size * a), c);
            }
            // heroes (procedural creatures) + their clones. The partner hero is tinted blue so you can tell them apart.
            for (int i = 0; i < players.Length; i++)
            {
                var p = players[i];
                Color? tint = (p != null && p.index != localIndex) ? (Color?)new Color(0.45f, 0.7f, 1f) : null;
                _playerVisuals[i].Sync(p, p != null && p.alive, tint);
                if (p == null) continue;
                foreach (var c in p.clones) { EnsureView(ref c.view); c.view.HideBar(); c.view.Set(c.x, c.y, c.r, Palette.CloneBody, 0.4f); }
            }
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
        public void Shake(float a) { if (GameSettings.ScreenShake && _juice != null) _juice.Shake(a); }
        public void ToggleDebug() { debug = !debug; ui.SetDebugVisible(debug); }

        public void DebugGod() { god = !god; ui.RefreshGodBtn(); }
        public void DebugLevel() { if (player != null) player.AddXp(player.xpNext - player.xp + 1f, this); }
        public void DebugDna() { if (player != null) player.AddXp(200f, this); }
        public void DebugSlow() { slowmo = !slowmo; }
        // Toggle co-op mode from the debug panel. Takes effect on the NEXT run (Reset spawns hero 2).
        public void DebugCoop() { coop = !coop; ui.RefreshCoopBtn(); }
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
