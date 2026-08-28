# Morphage — Handoff / Pick Up Here

**Project paused 2026-07-03** (last commit `02916d9f`) to ship another game. This doc was refreshed
2026-08-28 so you can restart cold without re-reading the code.

Companion docs: [MULTIPLAYER.md](MULTIPLAYER.md) (full co-op design + everything the netcode does),
[Assets/README.md](../Assets/README.md) (setup + how to add content).

---

## 1. Restart in five minutes

1. Open in **Unity 6000.3.9f1** (URP 2D). Working tree was clean at pause; branch `main`.
2. Press **Play** in any scene — the game self-bootstraps (`Bootstrap.cs`), no scene wiring needed.
   The generated data assets are committed (39 mutations, 6 enemies), so Play should just work. Only
   run **MUTAGEN → Generate Assets** if the game logs "No data assets found" or you edit the tables.
3. **Debug panel:** `Tab` in the editor, or triple-tap the MORPHAGE title on a device. On devices it
   is gated behind Developer Mode — type `southside-dev` into the seed field on the Game Mode panel
   and press Done (`Core/DevMode.cs`). Always enabled in the editor.
4. **Co-op testing in-editor:** Multiplayer Play Mode (two virtual players). Real testing needs two
   devices plus internet; sign-in is anonymous Unity Gaming Services, nothing to configure per device.

Project setting that bites if it ever resets: Player → *Active Input Handling* must include the
**Input System Package**.

---

## 2. Where the game actually stands

Short version: **it plays end to end, solo and online.** What is left is content lock, polish, and the
boring release work — not new systems.

| System | State |
|---|---|
| Core survivor loop (waves, campaign 15 + boss, endless) | Done, stable |
| 39 mutations — 19 moves + 20 modifiers, evolves, 4 swappable slots | Done, wants a balance pass |
| 17 synergies + in-game Synergies codex | Done |
| Every mutation visibly changes the creature (silhouette / skin / accent / move-tells) | Done |
| Mobile: touch joystick, move bar, pause, haptics, safe area, landscape | Done, device-tested |
| Monster archive (persistent gallery, procedural portraits) | Built — **portrait rendering never confirmed** |
| Settings + persistence (sound, vibration, shake, auto-cast, reduced FX, left-handed) | Done |
| Determinism (fixed timestep, seeded RNG, checksum harness, `SimMath`) | Done and **verified**, including iOS↔Android |
| **Online co-op — host/join by code, Quick Match, lockstep, desync detector, disconnects** | **Done, verified on real devices (iPhone↔iPad, iPhone↔Android)** |
| Meta-progression (Essence currency + unlock shop) | **Built 7/3, never playtested** ← newest, least proven |
| Onboarding / tutorial (first-run prompts) | Built, needs a device pass |
| Balance Lab (mass bot sim → CSVs + ranked report) | Built; use it for the balance pass |
| Release setup (bundle id, version, icons, store) | **Not started** |

### The newest thing — don't forget it exists

The final session (7/3) added **meta-progression and never tested it.** `Core/MetaProgress.cs`:

- A run banks **Essence** worth 20% of the DNA collected (`AwardRun`, called from `Game.cs:1123`).
- **Commons (18) are free; rares (18) cost 200; legendaries (3) cost 400.** The rule reads rarity off
  the mutation, so new content slots in automatically — no hand-maintained unlock list.
- Shop UI is the main-menu **Shop** button → `shopOverlay` (`UIManager.RenderShop`), plus an Essence
  readout on the menu. Saves to `morphage_meta.json` beside the monster archive.
- **Determinism guard:** locking mutations narrows the seeded draft pool, so `MetaProgress.Enforce`
  is **true only in solo play** — co-op and the headless/balance harnesses force the full pool. If you
  touch draft filtering, keep that rule or co-op desyncs.
- `Assets/Editor/MetaProgressTest.cs` asserts the pure math (currency conversion, prices).

**Never actually played:** earning Essence, the shop rendering with real data, buying something, and
whether a gated draft pool feels good. That is job #1 below.

---

## 3. When you come back — in this order

### First session back: prove what already exists (no new code)

- [ ] Clean recompile → Play a full solo run; confirm a clean console.
- [ ] Finish a run → Essence goes up → open **Shop** → buy something → start a run and confirm the
      purchase can now be drafted, and that a *locked* rare never shows up in a draft.
- [ ] Open **Monsters** → do portraits render, or come out blank? (URP render-to-texture is the risky
      part; the fallback plan is written in `MonsterPortrait.Render`.)
- [ ] Debug panel → **Determinism** check should PASS. Note the `SimMath` work moved the baseline hash
      once, on purpose — whatever it prints now is the new baseline.
- [ ] Two devices: host/join a co-op match and play it to a finish; confirm no desync readout.
- [ ] Fresh install / wiped data → does the **tutorial** fire correctly?

### After that: the real remaining work

1. **Balance and content lock.** Run the Balance Lab (debug panel → Balance, from the menu), read the
   three CSVs in `persistentDataPath`, kill the outliers — then stop changing numbers. Every tweak
   costs double now that co-op exists. Caveat: bots measure *relative* power (no dodging or aim), so
   use the data for outlier-hunting, not fine tuning.
2. **Tune the Essence economy.** `DnaToEssence = 0.2`, rare 200, legendary 400 are placeholder flat
   numbers. Decide how many runs unlocking the pool should take.
3. **Enemy AI depth pass.** Enemies mostly beeline at the nearest hero (spitters kite, boss
   telegraphs). Wanted: flanking chasers, spitters leading their shots, exploders feinting, elite
   sidesteps, pack behavior. **Any change must stay on seeded `Rng` and stay deterministic** or co-op
   breaks. The Balance Lab bot could learn to dash/kite at the same time, so its data better
   approximates human play.
4. **Release logistics (completely untouched).** `companyName` is still `DefaultCompany`, the bundle id
   is `com.DefaultCompany.Morphage`, `bundleVersion` is `00.00.01`, and the menu still reads
   "PROTOTYPE v0.2 · port". Needs: real identifiers, icons and launch screen, store listing, privacy
   answers (anonymous UGS auth creates a device-scoped id), and an Android build-target pass.
5. **Co-op polish backlog** (all non-blocking; details in MULTIPLAYER.md): rematch without an app
   restart, a compact non-blocking draft panel, co-op reroll, the loadout-full replace flow, W/L record
   on the menu, iPad letterboxing of the host-shaped arena, and the auto-resync fallback
   (deprioritized — no desyncs observed on device).

---

## 4. Rules that must not be broken

These cost real debugging time to learn. Breaking any of them breaks online co-op in ways that look
like random desyncs.

1. **Every gameplay input enters the sim through the tick queue** (`Game.QueueMove` / `QueueDash`). A
   UI button calling `player.UseMove` directly works fine solo and desyncs online.
2. **Gameplay randomness uses seeded `Rng`, never cosmetic `Fx.Rand`.** This already bit us once —
   Mitosis clone spawn angles.
3. **Gameplay trig/pow goes through `Core/SimMath.cs`**, not `Mathf` — platform math libraries differ
   in the last bits between iOS and Android. Cosmetic `Mathf` is fine; `Sqrt`/`Floor` are exact and fine.
4. **Anything device-dependent that feeds the sim must come from the host** (arena size, auto-cast, any
   setting). Each device fits the arena to its own screen — that was the real-device desync.
5. **Bump `CoopNet.GameVer` on every release that changes the sim.** Mismatched builds are filtered out
   of Quick Match; skipping the bump means guaranteed desyncs between versions.
6. **Unlock gating is solo-only** (`MetaProgress.Enforce`) — see §2.
7. **Re-run the Determinism check after any sim refactor.** It is the cheap regression gate.

---

## 5. Known issues / watch list

- **Gallery portraits** — never confirmed rendering; uses URP `SubmitRenderRequest`. Fallback is an
  enabled camera + `targetTexture` (`MonsterPortrait.Render`). It also renders all monsters (≤50)
  synchronously when the gallery opens — possible frame hitch.
- **Glyphs** `◇ ▸ ❚❚ ↻ ◆ ✕ ⚡ ✓` may render as boxes under Oxanium (a Latin font). Swap to plain words
  if they do.
- **Determinism harness scope** — it runs an idle god-mode player, so it covers spawns, enemy AI,
  projectiles and RNG, but **not the draft path**.
- **Quick Match race** — two players pressing it inside the same ~6s window can both become waiting
  hosts. Self-heals for later joiners; the proper fix is the Matchmaker service at real volume.
- **Co-op input delay** is a fixed 4 ticks. Adaptive delay was deferred until there is real network
  telemetry to tune against.

---

## 6. Code map

```
Assets/Scripts/
  Game.cs            state machine, fixed-step loop, waves, spawning, co-op glue, Balance Lab (~1570 ln)
  Bootstrap.cs       auto-spawns Game on Play
  InputReader.cs     input -> the tick queue
  Core/
    Rng.cs           seeded PRNG (all gameplay randomness)
    SimMath.cs       cross-platform deterministic Sin/Cos/Atan2/PowInt
    SaveSystem.cs    JSON saves: monster archive + meta-progression; co-op W/L in PlayerPrefs
    MetaProgress.cs  Essence currency + unlock rules            <- newest, untested
    GameSettings.cs  persisted settings        DevMode.cs   device debug gate
    Tutorial.cs      first-run prompts         BalanceSim.cs  bot-sim balance data
    Pool / SpatialGrid / TouchInput / Haptics / Binds
  Data/
    Moves.cs             move table (exec delegates -> Player helpers)
    MutationEffects.cs   modifier stat hooks
    MutationManager.cs   seeded draft + the unlock filter
    Synergies.cs         single source of truth for all 17 synergies
    MonsterRecord.cs / MutationDef.cs / EnemyDef.cs
  Sim/               Player, Enemy, Projectile, Clone, Hazard, DNAOrb, Beam, Particle, Floater, Palette, Fx
  Net/
    CoopNet.cs       UGS Sessions sign-in, host / join / quick-match, seed + match config
    CoopSync.cs      lockstep transport, tick input buffers, checksum + desync reporting
    TickInput.cs     the 4-byte wire packet (movement, buttons, draft choice)
  UI/UIManager.cs    every overlay, HUD, draft cards, codex, gallery, shop (~1374 ln)
  PlayerVisual.cs    the procedural creature    MonsterPortrait.cs  archive portraits
  Rendering.cs / Vfx.cs / Juice.cs / Sfx.cs

Assets/Editor/
  AssetGenerator.cs    <- canonical mutation + enemy tables; "MUTAGEN -> Generate Assets"
  MetaProgressTest.cs     IosPostBuild.cs
Assets/Resources/    generated Mutations/ + Enemies/, and UI (MutagenUI.uxml/.uss, templates, fonts)
```

**Adding content** touches all of: a row in `AssetGenerator.Mutations[]` → re-run Generate Assets;
then `Moves.cs` + a `Player` helper (for a move) or `MutationEffects.Apply` (for a modifier);
optionally a visual in `PlayerVisual.cs`. The rarity in that row also decides free-vs-shop
automatically. HUD and draft cards are data-driven — no UXML edits needed.

---

## 7. Reference

- [docs/MULTIPLAYER.md](MULTIPLAYER.md) — the deep dive: netcode model, everything built, every edge
  case fixed, the deferred list. Its §7 staged sequence is historical; all stages are done.
- Auto-memory (in the user's `.claude` directory): `mutation-overhaul`, `mutation-visuals`,
  `monster-archive`, `ui-architecture`.
- `prototype/mutant-arena.html` was the original behavioral source of truth. The Unity build has since
  evolved well past it (move loadout, co-op, meta-progression) — treat it as history, not a spec.
