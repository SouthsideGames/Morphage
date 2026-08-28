# MORPHAGE — mobile monster-evolution survivor (Unity 6 / URP-2D)

> **Coming back to this project? Start with [docs/TODO.md](../docs/TODO.md)** — current status,
> what's verified, and what to do first. This file is the setup + content-authoring reference.

Started as a port of the `prototype/mutant-arena.html` prototype and has since grown well past it
(4-slot move loadout, synergies, monster archive, meta-progression, online co-op). The HTML is
history now, not a spec — the Unity code is the source of truth.

## First-time setup

1. Open the project in Unity (**6000.3.9f1**).
2. Press **Play**. The game self-bootstraps in any scene — no scene wiring needed.
3. Only if Play logs "No data assets found" (or you edited the data tables): menu
   **MUTAGEN → Generate Assets**, which regenerates the mutation/enemy data + UI PanelSettings
   into `Assets/Resources/`.

The generated `.asset` files **are committed** now; the canonical numbers still live in code
(`Assets/Editor/AssetGenerator.cs`) so the generator can rebuild them at any time.

> **Project setting:** Edit → Project Settings → Player → *Active Input Handling*
> must include the **Input System Package** (New or Both). The port uses it directly.

## Controls (v0.4 — move loadout)

- **Move:** WASD / arrows / left stick.
- **Moves:** keys **1-4** fire your loadout slots (each its own cooldown + short GCD). No auto-attack.
  Offensive mutations are MOVES (Bite is the starter in slot 1); passives are MODIFIERS.
- **Dash:** Shift / left shoulder — i-frames; cooldown shortened by Wings/Stormborn.
- **Draft:** 1 / 2 / 3 or click. Re-drafting a move levels it (max 3 → evolves). A 5th move opens a
  **forget** prompt (keys 1-3 overwrite a slot, 4 = discard).  **Reroll:** R / button (+1 per boss).
- **Menu:** Game Mode (Campaign — 15 waves → final boss → Victory — or Endless, plus **Host / Join /
  Quick Match** for online co-op), Settings, Synergies codex, Monsters archive, and the unlock **Shop**.
- **Debug:** `Tab` (or triple-tap the title on a device; gated by Developer Mode there).  **Mute:** M.

## Mobile / touch controls (native UI Toolkit)

Touch controls are built natively in UI Toolkit (no uGUI — avoids the pointer contention that broke
the earlier Suriyun attempt). On a touch device during a run:
- **Floating joystick** (left half): press anywhere on the left, drag to move; recenters each touch.
  `TouchInput` (static bridge) feeds `InputReader.MoveVec`.
- **Move buttons** (bottom-right): the move bar repositions + enlarges (`.movebar.touch`); tap slots 1-4 / dash.
- Device detection: `Application.isMobilePlatform || Touchscreen.current != null`. Orientation: **landscape-only**.
- **Pause** button (top-right) → Resume / Main Menu, freezes the sim (Esc also, on desktop).
- **Haptics** (`Haptics.cs`, Nice Vibrations, mobile-only): hurt/dash/evolve/pick/boss-death.

**Test in-editor:** Debug panel (`` ` ``) → **Touch UI** toggles force-touch so you can drag the joystick
with the mouse in the **Game view** (the UITK pointer path is identical for mouse and touch). Note: UI Toolkit
clicks don't register in the Device **Simulator** — test in the Game view or on-device.

**To build:** switch the active Build Target to iOS / Android in Build Settings (heavy reimport), then
build/sign in Xcode / Android Studio. Player orientation is already landscape.

## Project layout

> The old `Assets/MUTAGEN/` tree no longer exists. Code lives in `Assets/Scripts/`, generated data in
> `Assets/Resources/`, editor tools in `Assets/Editor/`.

```
Assets/Scripts/
  Core/      Rng, SimMath, SaveSystem, MetaProgress, GameSettings, DevMode, Tutorial,
             BalanceSim, Pool, SpatialGrid, TouchInput, Haptics, Binds
  Data/      MutationDef, EnemyDef (ScriptableObjects), Moves, MutationEffects,
             MutationManager, Synergies, MonsterRecord
  Sim/       Player, Clone, Enemy, Projectile, Hazard, DNAOrb, Particle, Floater, Beam, Palette, Fx
  Net/       CoopNet (sessions/host/join), CoopSync (lockstep), TickInput (wire packet)
  UI/        UIManager (UI Toolkit — HUD, overlays, draft, codex, gallery, shop)
  Game.cs        state machine, fixed-timestep loop, waves, spawning, pooling, render-sync
  InputReader.cs movement + ability input, routed through the tick queue
  Sfx.cs         synthesized SFX (no audio assets)
  Rendering.cs   runtime circle-sprite factory + pooled views
  PlayerVisual.cs / MonsterPortrait.cs / Vfx.cs / Juice.cs
  Bootstrap.cs   auto-spawns Game on Play

Assets/Editor/    AssetGenerator.cs  ← canonical numbers + asset generation
                  MetaProgressTest.cs, IosPostBuild.cs
Assets/Resources/ Mutations/ Enemies/   generated SO assets (committed)
                  UI/  MutagenUI.uxml/.uss + per-item templates, fonts, PanelSettings
```

Full annotated map, including which files are newest and least tested:
[docs/TODO.md §6](../docs/TODO.md).

## How to add / change a mutation

1. **Data:** add a row to `AssetGenerator.Mutations` (id, order, name, color, **rarity**, move,
   repeatable, maxStacks, description, evolveName), then re-run **MUTAGEN → Generate Assets**.
   Rarity also decides shop gating: commons are free, rares/legendaries must be unlocked.
2. **Effect:** for a move, add an entry in `Data/Moves.cs` + an attack helper in `Sim/Player.cs`.
   For a modifier, add a `case "id":` in `MutationEffects.Apply` (and `Evolve`).
   Enemies work the same way via `AssetGenerator.Enemies` + behavior flags.
3. **Visual (optional but wanted):** procedural body part in `PlayerVisual.cs` — silhouette, skin
   tint, accent mote, or move-tell nub depending on the mutation type.

The HUD and draft cards are fully data-driven — no UXML changes needed.

⚠️ Anything that affects the simulation must stay deterministic (seeded `Rng`, `SimMath` for trig)
or online co-op desyncs. See [docs/TODO.md §4](../docs/TODO.md).

## Design notes / how it grew past the prototype

1. **Move loadout.** Combat is a 4-slot move loadout (no auto-attack). Moves live in a code table
   (`Moves.cs`, exec delegates → `Player` helpers); draftable moves/modifiers are ScriptableObjects.
   19 moves + 20 modifiers, with evolves and 17 synergies (`Data/Synergies.cs`).
2. **Settings** are mobile-shaped: sound, vibration, screen shake, auto-cast, reduced FX, left-handed
   layout, clear archive — all persisted (`Core/GameSettings.cs`). The old rebindable-keys UI was
   removed; `Binds.cs` remains for desktop defaults.
3. **Arena is screen-fit per device**, not a fixed 960×600 box, and the camera frames it. In co-op the
   joiner adopts the **host's** arena size — two differently-shaped screens would otherwise simulate
   different arenas (this was a real desync).
4. **Visuals** are fully procedural: floor grid + border, and a creature where every one of the 39
   mutations leaves a persistent mark (silhouette geometry, skin tint, orbiting accent motes, move-tell
   nubs) — see `PlayerVisual.cs`. Enemies are still glowing circles + health bars.
5. **Determinism is now a hard requirement, not a nicety** — it's what makes lockstep co-op work.
   Seeded `Rng` drives everything in the sim; cosmetic randomness (`Fx`) is off that stream; gameplay
   trig/pow goes through `Core/SimMath.cs` so iOS and Android agree bit-for-bit. Verified same-seed
   and cross-platform. The rules to honor are listed in [docs/TODO.md §4](../docs/TODO.md).

## Deferred (add when needed)

- View interpolation (sim runs 60 Hz; add if high-refresh stutter shows).
- asmdefs for faster incremental compiles (currently Assembly-CSharp).
- Real sprite art + bloom glow (URP 2D Volume) instead of generated circles + halo.
