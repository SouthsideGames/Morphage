# Morphage — Multiplayer Readiness & Plan

> Status: **online co-op WORKING CROSS-PLATFORM — verified iPhone ↔ Android (2026-07-02).** Two
> players connect by share code, each drives their own hero, full lockstep sync holds through
> combat + level-ups with no desync — including MIXED-PLATFORM matches (the SimMath deterministic
> math layer held with zero desyncs on first real test). Competitive last-one-standing rules.
> The core netcode is DONE; remaining work is polish and release logistics.
>
> Target design: **online co-op, 2 players, independent builds** (each player has
> their own HP, XP, level, move loadout, and mutation draft; enemies and waves are shared).

## Built so far (this session)
- **A2/A3 done:** owner attribution + the 2-hero sim (`Game.players[2]` + `localIndex`, `player` alias),
  gated behind a `Game.coop` mode. Determinism check stays green (single-player byte-identical).
- **Transport decision REVISED from §3 below:** using the modern **`com.unity.services.multiplayer`
  Sessions API** (create/join by code, seed carried as a session property) on top of **NGO
  (`com.unity.netcode.gameobjects`)** as the connection layer — NGO purely for the pipe + custom
  messaging, **no NetworkObjects** (so the lockstep design is intact). Not the raw-UTP+Relay+Lobby of §3.
- **Files:** `Net/CoopNet.cs` (sign-in, host/join, seed, NetworkManager bootstrap), `Net/CoopSync.cs`
  (per-tick input packets over `CustomMessagingManager`, auto-start), `Net/TickInput.cs` (input packet).
- **Working:** Stage 1 (connect + seed) ✅. Stage 2 **strict lockstep** ✅ — tick-indexed input buffers,
  `InputDelay=4`, redundant unreliable packets + resend-on-stall (no deadlock), both sims step identically
  (`_stepInput[2]` set in `Game.Tick`). Verified: two MPPM screens stay in sync through combat, no hang.
  Fixed one cosmetic-RNG-in-gameplay leak (Mitosis clone angle → seeded `Rng`).
- **Co-op draft (Option B) ✅** — non-blocking: level-up never pauses; the leveller gets a card panel + full
  invuln while choosing (both peers set the shield — it's sim state); the pick rides the `TickInput.draft`
  byte and both peers apply the identical option at the same tick. Single-player keeps its pause-to-pick.
  `mutations.Draft` is seeded, so both peers compute identical cards. (No reroll / loadout-full replace yet.)
- **Competitive finish ✅** — co-op is last-one-standing: first fall ends the run (`CoopEnd`), survivor = WINNER,
  fallen = DEFEATED (DRAW if same tick). Per-device W/L record in PlayerPrefs (`SaveSystem.CoopWins/Losses`).
  Menu cleaned up: real **Co-op** overlay (Host/Join by code); pause disabled in co-op.
- **Desync checksum ✅ (detection)** — every 30 ticks both peers exchange + compare a full-state hash and a
  component snapshot (`StateSnap`); on mismatch the log AND an on-screen readout say which part diverged first.
- **Match-config sync ✅** — the peers must run identical rules, so the host's session properties now carry:
  seed, autocast, and **arena w×h** (each device fits the arena to its own screen at startup — an iPhone and an
  iPad literally simulated different-sized arenas, which was the real-device desync; joiner now `ApplyArena`s
  the host's dimensions). Co-op also forces `endless = true` (last-one-standing) and locks the sim-mutating
  debug buttons while connected (each would desync). LESSON: any device-dependent init that feeds the sim
  (screen fit, saved settings) must be propagated from the host.
- **Disconnect handling ✅ (v1)** — peer drop (NGO disconnect callback or ~10s lockstep stall) ends the run as
  "Opponent Left" — the remaining player WINS (rage-quit = loss). Fresh host/join tears down the previous
  session (`TeardownPrevious`: leave session, NGO shutdown, re-register per-session message handlers).
- **Real-device verification ✅ (2026-07-02, iPhone + iPad):** no desync through live play after two final
  device-only fixes: (1) **arena size** propagated from host (each device screen-fits `w×h` at startup —
  the two devices literally simulated different arenas); (2) **touch move-bar taps** were calling
  `player.UseMove/Dash` directly, bypassing the input packets — now routed through `Game.QueueMove/QueueDash`
  (the queue is what rides the network). LESSON: every gameplay input MUST enter the sim via the tick-input
  queue; any direct sim call from UI is a device-only desync.
- **Cross-platform (iOS↔Android) determinism — SimMath layer ✅ VERIFIED (iPhone↔Android match, no desync).** Android release
  planned → mixed matches must survive different compiled binaries. Platform libm transcendentals (sin/cos/
  atan2/pow) differ in the last bits between Apple and Android toolchains → lockstep drift. Added
  `Core/SimMath.cs` (polynomial Sin/Cos/Atan2 + PowInt, plain IEEE arithmetic, one op per statement to block
  divergent multiply-add fusion) and swapped all 17 gameplay call sites (Player attack angles/projectiles,
  Enemy boss spread, Clone orbit, CdMul/dash Pow). Cosmetic Mathf uses left alone; Mathf.Sqrt/Floor/etc. are
  exact IEEE ops and stay. NOTE: solo determinism baseline hash CHANGES (one time). Residual cross-platform
  risk: compiler-level fusion on general sim arithmetic — verify with the desync detector on a real
  iPhone↔Android match; if it still drifts, next step is the resync fallback (or fixed-point).
- **Quick Match (random opponents) ✅ (untested).** `CoopNet.QuickMatchAsync` uses the Sessions quick-join:
  search public sessions ~6s → join one (adopting host's seed/settings/arena) or create one and wait. Friend
  games (`HostAsync`) are now `IsPrivate` — joinable by code only, invisible to quick-matchers. Sessions carry
  a `ver` property (`CoopNet.GameVer` — **bump on every sim-affecting release**); joiners refuse mismatched
  versions (guaranteed desync otherwise). Known edge: two players pressing Quick Match in the same ~6s window
  can both become waiting hosts (quick-join race) — self-resolves for later joiners; revisit if it bites.
- **Edge-case hardening pass ✅ (Tier 1).** Fixed: solo runs after an abandoned host/join/search (stale
  `coop`/`localIndex` cleared in `StartRun` + `CloseCoop`); quit-the-app win farming (disconnects classify
  own-link vs partner-left — only a verified partner departure in a truly started match, `_execTick ≥ 120`, awards
  a win; everything else ends "Connection Lost / NO RESULT"); zombie-lobby free wins (same started-gate);
  rematch honored only while still in-session and idle; Quick Match version FILTER (indexed `ver` property +
  `FilterOption` — mismatched builds never even match); join codes uppercased; slowmo locked in co-op.
  Tier 2/3 pass ✅: rematch button live-grays to "Opponent Left"; draft pick/reroll lock until the networked
  choice lands (double-tap guard); re-host notes the old code is cancelled; abyss backdrop behind the arena
  (letterbox polish); rotation/resize re-frames the camera; host wifi-drop detected via internetReachability
  (no free win). DEFERRED by design: adaptive input delay (tune from real-network telemetry; fixed 4 works)
  and the quick-match simultaneous-press race (self-heals; proper fix = Matchmaker service at real volume).
- **Still TODO (all non-blocking polish):** resync fallback (auto-recover — deprioritized: no desyncs observed
  on device); rematch UX without app restart; compact non-blocking card panel; co-op reroll; loadout-full
  replace flow; W/L record on menu; iPad letterboxes the host-shaped arena (cosmetic).

---

## 0. The one rule to honor while co-op is deferred

**All *gameplay* randomness must route through the seeded `Rng` (`Assets/Scripts/Core/Rng.cs`).
Only *cosmetic* randomness (particle scatter, floater jitter, screen shake, animation
phase) may use the unseeded `Fx` (`Assets/Scripts/Sim/Fx.cs`).**

This line already exists in the codebase — see the header comment in `Fx.cs`:

> "Non-seeded randomness for purely cosmetic effects … Kept OFF the seeded stream so
> decorative emission can't desync run composition / draft order … Gameplay draws use `Rng`."

If every new move/enemy/system honors this, the game stays deterministic-from-seed and
co-op stays cheap to add. If it doesn't, each violation becomes a desync bug to hunt down
later. It costs nothing to do right the first time.

**A gameplay dice-roll of probability `p` is:** `Rng.Next() < p`  (`Rng.Next()` returns a
float in `[0,1)`). Do **not** use `Fx.Chance(p)` / `UnityEngine.Random` for anything that
affects game state.

### Determinism cleanup already done
Three known gameplay rolls were moved onto the seeded stream, so the codebase is currently
fully in line with the rule:

- `Assets/Scripts/Sim/Enemy.cs` — crit roll (`Fx.Chance(pl.critChance)` → `Rng.Next() < pl.critChance`)
- `Assets/Scripts/Sim/Enemy.cs` — boss attack type (`Fx.Chance(0.5f)` → `Rng.Next() < 0.5f`)
- `Assets/Scripts/Sim/Player.cs` — move-cooldown tick now iterates `moveSlots` in fixed
  order instead of enumerating a `Dictionary` (whose order is not deterministic)

Side benefit: runs are now perfectly replayable from a seed — handy for testing and bug reports.

---

## 1. Why the foundation is co-op-friendly

- **Fixed 1/60s timestep** with no wall-clock reads inside the sim step (`Game.Step(dt)`);
  `Update()` only accumulates real time and drives fixed steps.
- **Input is queue-separated** from simulation (`_moveQueued[0..3]` / `_dashQueued` filled in
  `Update()`, consumed in `Step()`), so inputs are already a clean, serializable boundary.
- **Single seeded mulberry32 PRNG** (`Core/Rng.cs`, one `uint` of state) — trivially
  checksummable and resyncable.
- **Ordered sim collections** — enemies/projectiles/orbs/hazards are `List<T>` iterated by
  index (deterministic order).
- **Static, arena-centered camera** (no follow-cam to rework for two players).
- **Enemy spawns at arena edges**, not player-relative — both players see identical spawns.

---

## 2. Chosen netcode model — deterministic input-lockstep

Each device runs the **full identical sim** and exchanges **only the 2 players' inputs per
tick** (~4 bytes each), through an **input-delay buffer** (D ≈ 2–3 ticks to hide latency).
Robustness via a **periodic state checksum** (every ~30 ticks: hash `Rng.State` + per-player
`x,y,hp,xp,level,rerolls` + enemy count + a rolling hash of enemy `(x,y,hp)` + `wave/waveTimer`);
the host compares. On mismatch, the host sends a **full-state snapshot** and both resume from
the resync tick. **Desync is recoverable, not fatal.**

**Why this over host-authoritative snapshot streaming:** bandwidth would scale with entity
count (hundreds of enemies/projectiles), not player count — bad on mobile data. And sim
entities are plain C# with pooled sprite *views*, not `NetworkObject`s, so you'd hand-roll
serialization anyway — but pay it every tick instead of on two tiny input packets.

**Main open risk — cross-device float determinism.** IL2CPP codegen can differ across ARM
chips; a last-ULP drift can flip a comparison and snowball. The checksum + resync fallback
*contains* this. **Fixed-point (Q16.16) math is a contingency only** — decide from real-device
resync telemetry (Stage 2), do not do it pre-emptively.

---

## 3. Transport / services stack

Use **Unity Transport (UTP) directly** with a thin custom message protocol — **not** Netcode
for GameObjects (NGO replicates `NetworkObject`s, which this architecture doesn't use; it would
be pure overhead). **Relay** for NAT traversal (host allocates → join code); **Lobby** for
join-code exchange + seed/settings propagation. `com.unity.multiplayer.playmode` for testing
two virtual players in one editor.

Add to `Packages/manifest.json` (let Package Manager resolve exact U6 patch versions):

```json
"com.unity.transport": "2.4.0",
"com.unity.services.core": "1.14.0",
"com.unity.services.authentication": "3.4.0",
"com.unity.services.relay": "1.1.1",
"com.unity.services.lobby": "1.2.2",
"com.unity.multiplayer.playmode": "1.3.2"
```

One-time: link a Unity cloud project ID (Project Settings → Services) for Relay/Lobby/Auth.
Note: `com.unity.multiplayer.center` (already installed) is just the docs hub — harmless.

---

## 4. Sim refactor checklist (single → 2 players, independent builds)

Build as a **local loopback first** (both players driven locally, no networking) to de-risk.

- **Player storage** — `Game.player` (single field) → `Player[2]` + `localIndex` +
  `LocalPlayer`. Add `index` and `alive => hp > 0f` to `Player`. Construct both in `Reset()`
  with offset spawns; make `ReleaseAll()` loop.
- **Per-player Step** — replace the single-player block in `Game.Step()` with a fixed-order
  loop (index 0 then 1), each consuming its own input. `grid.Rebuild` stays shared.
- **Enemy targeting → nearest player** — add `Game.NearestPlayer(x,y)` (min `Rng.Dist2` over
  living players, low-index tie-break); use it at `Enemy.cs` (~line 130). Downstream uses of
  `p` (movement, `necrosis`, contact, reflect) follow automatically.
- **Owner attribution (largest edit — required).** `Enemy.Hurt` currently reads
  crit/Brittle/Plague/lifesteal from `game.player` (`Enemy.cs:54-63`), and `Projectile`
  carries no owner — so P0's projectile would use P1's stats. Add `Player owner` to
  `Projectile` and a `Player owner` param to `Game.AddProjectile`; change
  `Enemy.Hurt(amount, game, Player src, ...)` to use `src` (skip modifiers when `src == null`).
  Call sites: player move helpers pass `this`; clones pass their owner; `HandleProjectiles`
  passes `pr.owner`; enemy shots / `Explode` / `Hazard` pass `null`.
- **Enemy projectile & explosion collision → both players** — loop living players in
  `HandleProjectiles` (enemy branch) and `Explode`.
- **DNA orb attribution** — `DNAOrb.Update` returns the collecting player index (pull toward
  nearest living player); award XP to that player only.
- **Per-player draft — LIVE picking, no world pause (DECISION: Option B).** The sim must NOT halt for a
  level-up. When a player levels, show *them* a non-blocking card panel while combat keeps running for
  both; the levelling player keeps control (grant brief i-frames while the panel is open so they aren't
  punished for reading), and the other player is never interrupted. Implies: move `pendingLevels` and
  `rerolls` onto `Player`; replace the global `state == LevelUp` halt with a per-player `_drafting` flag;
  cards are tap/click-to-pick (keep 1–4 as ability keys during live play). Only the **choice** (option
  index / reroll / replace-slot) travels over the wire; both sims replay it in fixed player order so
  `MutationManager.Draft`'s seeded draws stay deterministic. `OnBossKilled` grants a reroll to both.
  NOTE: single-player keeps its existing full pause-to-pick — the live panel is a co-op-only behaviour.
- **Dual rendering** — `PlayerVisual[2]`; loop both in `Render()` and iterate both players'
  clones (Clone already stores its owner). Tint local vs. remote.
- **Game over** — run ends only when **both** players are dead (`Player.Hurt` death path →
  `Game.OnPlayerDowned`, which calls `GameOver()` only if all dead). Revive-on-touch optional.

---

## 5. Input & network plumbing (when networking begins)

- **`TickInput` struct** (`Assets/Scripts/Net/TickInput.cs`): `tick`, quantized `moveX/moveY`
  (`MoveVec*127`), a `bits` byte (Move1–4 + Dash), and a `draft` byte (none / pick 1–3 /
  reroll / replace-slot). **Always round-trip local input through `TickInput`** before feeding
  the sim, so both clients quantize identically. Move/dash bits come from the existing
  `_moveQueued`/`_dashQueued`.
- **Remote → players[1]** — `Game` holds `TickInput[2]`; local slot from `InputReader`, remote
  slot from the network. `InputReader` stays unchanged.
- **Delay buffer + tick gating** — restructure `Game.Tick()` so each `Step` advances only when
  **both** players' inputs for the current sim tick are present; otherwise stall that frame.
  Send the last K inputs per packet so one dropped datagram doesn't stall.
- **Draft over the wire** — both clients compute identical 3 options from the seeded `Rng`
  (options sorted by `MutationDef.order`), so only the **choice index / reroll / replace-slot**
  travel; the remote replays `Choose`/`Reroll`/`DoReplace`.
- **Bootstrap** — host: anon auth → Relay allocation → Lobby with join code, writing
  `seed` + `endless/autocast`. Client: enter code → read seed/settings → connect. Both
  `Rng.Set(...)` + 2-player `Reset()`, then a countdown handshake to start sim tick 0 together.
  `localIndex`: host = 0, client = 1.

---

## 6. UI work

- Two HP/XP/move panels: local player gets the full interactive move bar; remote player a
  compact read-only HP/XP/level strip. Parameterize `SyncHud`/`RenderMuts` by player index.
- No "waiting for partner" overlay — with live picking (Option B) neither player is ever blocked by the
  other's level-up. Each player's card panel is local and non-blocking, overlaid on the still-running game
  (smaller than the single-player full-screen draft so the fight stays visible); the levelling hero gets
  brief i-frames while it's open.
- Lobby/join-code screen: extend `startOverlay` with Host/Join, host code display, client code
  field, "waiting for player…".
- Disconnect (v1 scope): UTP disconnect event → "Opponent left" → continue-solo (freeze the
  peer's `Player`, flip the survivor's input-gate true) or quit. Reconnect is later scope.

---

## 7. Staged sequence

- **Stage 0 — determinism (✅ VERIFIED) + 2-players-on-one-device loopback.** The same-seed-twice
  checksum harness is built (`Game.Checksum` / `Game.RunDeterminismCheck`, debug-panel "Determinism"
  button) and **passes** — identical end-state hash across two 600-tick runs. Re-run it as a
  regression gate after each sim refactor (owner attribution, player array). Still to do here:
  the local 2-player loopback (`Game.player` → `Player[2]`), no networking yet.
- **Stage 1 — transport / lobby / relay.** Two virtual players connect by join code, log the
  same seed + distinct `localIndex`.
- **Stage 2 — lockstep + checksum.** Feed remote input into the second player; add periodic
  checksum compare. Test on two real devices (Wi-Fi + cellular); record resync frequency to
  decide whether fixed-point is ever needed.
- **Stage 3 — draft sync + resync fallback.** Full-state snapshot on checksum mismatch.
- **Stage 4 — UI + disconnect + polish.**

Testing harness throughout: **Multiplayer Play Mode** (two virtual players in one editor).

---

## Key files (reference)

| Area | File |
|------|------|
| Core loop / state / spawns / render | `Assets/Scripts/Game.cs` |
| Player sim, cooldowns, moves | `Assets/Scripts/Sim/Player.cs` |
| Enemy AI, targeting, damage (`Hurt`) | `Assets/Scripts/Sim/Enemy.cs` |
| Projectiles (needs `owner`) | `Assets/Scripts/Sim/Projectile.cs` |
| DNA orbs (needs per-player attribution) | `Assets/Scripts/Sim/DNAOrb.cs` |
| Seeded PRNG | `Assets/Scripts/Core/Rng.cs` |
| Cosmetic-only RNG (keep gameplay off this) | `Assets/Scripts/Sim/Fx.cs` |
| Draft / mutation selection | `Assets/Scripts/Data/MutationManager.cs` |
| HUD / draft / overlays | `Assets/Scripts/UI/UIManager.cs` |
| New netcode code goes here | `Assets/Scripts/Net/` (to be created) |

## Two easiest-to-underestimate items
1. **Owner attribution** (projectiles + `Enemy.Hurt` `src`) — touches many call sites and is
   required for correct independent-build damage.
2. Getting **Stage 0's same-seed-twice checksum green** before networking — every later stage
   depends on it.
