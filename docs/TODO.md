# Morphage — Working TODO / Handoff

Snapshot of where the project stands and what's left. Companion to [MULTIPLAYER.md](MULTIPLAYER.md).

## ✅ Done (recent session)
- **Build green & runs.** Fixed a stale-build NullReferenceException flood; hardened the per-frame UI path (`ui`-null guard in `Game.Update`, `_hpFill` guard in `UIManager.SyncHud`).
- **Determinism PROVEN.** `Game.Checksum()` + `Game.RunDeterminismCheck()` (debug-panel **Determinism** button) pass with identical hashes across two 600-tick runs. This validates lockstep multiplayer.
- **Mutation visuals** — every mutation shows on the creature: silhouette (arms, wings, tail, quills, carapace, stinger, ground-spikes), skin (thick/toxicskin/vitality/bulk/glasscannon tints, frost rim), accent (orbiting stat-trait motes), and idle move-tell nubs. See `PlayerVisual.cs`.
- **Synergies** — 17 total (7 real damage effects) via a single source of truth `Assets/Scripts/Data/Synergies.cs` (`Synergies.All`). `Game.ActiveSynergies` filters it for HUD chips; a **Synergies codex** UI renders the full table.
- **Monster archive** — persistent "Monsters" gallery with procedurally re-rendered portraits + the first save system. Files: `Data/MonsterRecord.cs`, `Core/SaveSystem.cs`, `MonsterPortrait.cs`, `Game.CaptureMonster`.
- **Settings** — sound / vibration / screen-shake / auto-cast / reduced-FX / left-handed / clear-archive, all persisted via `Core/GameSettings.cs` (PlayerPrefs). Keybind UI removed (mobile).
- **UI polish** — menu split into Game Mode + Settings panels, Oxanium font (loaded in code from `Resources/Fonts/Oxanium.ttf`), ~1.28× larger UI (panel ref 800×450), squarer buttons, seed-text color fix, decorative icons + subtitle removed.
- **AssetGenerator** paths fixed to target the live `Assets/Resources/{Mutations,Enemies,UI}` (was the dead `Assets/MUTAGEN/` tree).
- **Debug access** — `Tab` key (keyboard) or **triple-tap the MORPHAGE title** (touch) opens the debug panel.

## 🔜 To do

### 0. Finish verifying (DO FIRST — much of the above went in untested)
- [ ] Clean **Stop → recompile → Play** to be on final code (temp auto-run + diagnostic log were removed).
- [ ] Draft mutations → confirm creature **visuals** change.
- [ ] Trigger a synergy → **⚡ chip** shows; open the **Synergies** codex.
- [ ] Finish a run → **Monsters** gallery renders a **portrait** (riskiest: URP render-to-texture).
- [ ] Toggle each **setting** (esp. left-handed layout + reduced-FX).

### A. Multiplayer prerequisites (re-run the Determinism check after each)
- [~] **A2 — owner attribution. CODE DONE, determinism re-check PENDING.** Added `Player owner` to
  `Projectile` + `Game.AddProjectile` (default `null`); `Enemy.Hurt(amount, game, Player src, …)` now keys
  crit/synergy/lifesteal on `src` (skips all when `null`). Call sites per plan: player move helpers + player/clone
  projectiles pass the player; enemy shots / `Explode` / `Hazard` pass `null`; spike reflect passes the player.
  - **Determinism impact = none:** the harness runs a mutation-less player, so `pl.critChance > 0f` short-circuits
    before `Rng.Next()` and every synergy/lifesteal term is a no-op → end-state checksum is identical to before.
    Still must be re-run green in the editor to close the gate (see "How to run the Determinism check" below).
  - **Solo behaviour is neutral for all *player*-caused damage.** One non-player path changed by design (plan says
    `null`): enemy-exploder `Explode` no longer applies the player's crit/lifesteal to other enemies (a latent bug,
    now fixed).
  - **Hazard attribution added (refinement beyond the literal plan).** `Hazard`/`Game.AddHazard`/`DropZone` now carry
    an `owner`, so Acid Pool / Gravity Well / Inferno DoT ticks credit crit/synergy/lifesteal to the dropping player —
    keeps solo behaviour identical to pre-A2 *and* attributes correctly in 2P. Determinism-safe (harness drops no zones).

#### How to run the Determinism check
- **Fastest (editor already open):** focus the editor so it recompiles, open the debug panel (`Tab` / triple-tap the
  MORPHAGE title), click **Determinism**, read the console for `[MUTAGEN] Determinism PASS ✓`.
- **Headless / CI (editor must be CLOSED):** copy `HeadlessChecks.cs` (staged this session) into `Assets/Editor/`,
  then `Unity.exe -batchmode -projectPath "<proj>" -executeMethod MutagenEditor.HeadlessChecks.Determinism -logFile -`.
- [~] **A3 — `Game.player` → `Player[2]`, co-op as a game MODE. CODE DONE, needs compile + playtest.** Sim can run two
  heroes: `Game.players[2]` + `localIndex`, with `player` kept as an alias to the local hero so the whole UI is
  untouched. Gated behind `Game.coop` (**default off**): solo spawns exactly one centered hero (identical to pre-A3);
  co-op spawns two. Implemented: per-hero `Step` loop, `NearestPlayer` targeting (Enemy AI + DNA orbs), enemy
  projectiles/explosions hit any living hero, per-hero XP (orb credits its collector), dual `PlayerVisual`, both-dead
  game-over (`OnPlayerDowned`), Carnivore heal now credits the actual killer (`Enemy.Die(game, killer)`), checksum hashes
  every live hero.
  - **Co-op's hero 2 is a throwaway bot** (`Player.isBot` + `Game.BotMove`/`BotDraft`): approaches enemies, auto-casts,
    auto-picks its level-ups (first drafted option, no UI). Pure de-risk stand-in — deleted once real phone→phone input
    feeds hero 2. Only the local hero's draft opens the card UI.
  - **Determinism gate:** `SimulateHeadless` forces `coop = false`, so the check tests the shipping single-player sim.
    The PASS hash should return to the ORIGINAL `1DE638790E2C52AB` — a bonus proof that A3 left single-player byte-identical.
  - **OPEN: how to select the co-op mode.** It's a bot test, not real online co-op yet, so it doesn't belong on the main
    menu as "Co-op" prematurely. Need a toggle (debug button, or a temp menu option) to flip `Game.coop` on for testing.
  - **Known loopback rough edges (fine for the de-risk):** if the local hero dies first you can still click its move
    bar (harmless); the two heroes aren't visually tinted apart yet; no revive.

### Sync-debt (fix before networking, Stage 2)
- **Mitosis clone spawn angle uses cosmetic `Fx.Rand`, not seeded `Rng`** (`Player.Mitosis`). Harmless in solo and in
  the one-device loopback, but two networked devices would spawn clones at different angles → desync. Swap `Fx`→`Rng`.
  Worth a broader sweep for other `Fx`-in-gameplay before lockstep.

### B. Finish single-player
- [ ] Balance/content lock (stop churning mutations/enemies/synergy numbers before co-op multiplies the cost).
- [ ] Onboarding / tutorial.
- [ ] **Meta-progression** — persistent currency + between-runs unlock shop. Sets the save schema; do before co-op so it isn't retrofit into 2 players.

### C. Multiplayer proper (Stages 1–4 in MULTIPLAYER.md)
- [ ] Transport/lobby/relay (UTP + Relay + Lobby packages).
- [ ] Input-lockstep + periodic checksum.
- [ ] Draft sync + host-authoritative resync fallback.
- [ ] Co-op UI (two HUDs, waiting-draft overlay) + disconnect handling.

## ⚠️ Watch items
- **Gallery portraits** — URP `RenderTexture` via `RenderPipeline.SubmitRenderRequest`; if portraits are blank, fall back to an enabled camera + `targetTexture` (see `MonsterPortrait.Render`).
- **Glyphs** `▸ ❚❚ ↻ ◆ ✕` may render as boxes under Oxanium (Latin font). Swap to plain words if so.
- **Determinism harness scope** — the check runs an idle god-mode player, so it exercises spawn/enemy-AI/projectile/RNG determinism but NOT the draft path. Good enough for the lockstep assumption; extend later if needed.

## 📎 Reference
- [docs/MULTIPLAYER.md](MULTIPLAYER.md) — full co-op plan; Stage 0 determinism marked verified.
- Auto-memory (in the user's `.claude` dir): `mutation-overhaul`, `mutation-visuals`, `monster-archive`.
