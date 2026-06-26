# MORPHAGE — Unity port of `mutant-arena.html`

Faithful Unity 6 / URP-2D port of the MUTAGEN prototype. The HTML
(`/prototype/mutant-arena.html`) is the behavioral source of truth; this code
mirrors its numbers. I (the "port system") keep them in sync: you send updated
HTML, I diff it against the snapshot and bring the deltas across.

## First-time setup (one click)

There are **no committed `.asset` files** — they're generated so GUIDs are always
correct and the numbers stay in code for easy HTML syncing.

1. Open the project in Unity (6000.3.x).
2. Menu: **MUTAGEN → Generate Assets** (creates mutation/enemy data + the UI PanelSettings).
3. Press **Play**. The game self-bootstraps in any scene — no scene wiring needed.

If Play logs "No data assets found", you skipped step 2.

> **Project setting:** Edit → Project Settings → Player → *Active Input Handling*
> must include the **Input System Package** (New or Both). The port uses it directly.

## Controls (v0.4 — move loadout)

- **Move:** WASD / arrows / left stick.
- **Moves:** keys **1-4** fire your loadout slots (each its own cooldown + short GCD). No auto-attack.
  Offensive mutations are MOVES (Bite is the starter in slot 1); passives are MODIFIERS.
- **Dash:** Shift / left shoulder — i-frames; cooldown shortened by Wings/Stormborn.
- **Draft:** 1 / 2 / 3 or click. Re-drafting a move levels it (max 3 → evolves). A 5th move opens a
  **forget** prompt (keys 1-3 overwrite a slot, 4 = discard).  **Reroll:** R / button (+1 per boss).
- **Menu:** Campaign (15 waves → final boss → Victory) / Endless, and **Auto-cast** (fire all off-cooldown moves).
- **Debug:** `` ` ``.  **Mute:** M.

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

```
Assets/MUTAGEN/
  Scripts/
    Core/      Rng, Pool, SpatialGrid
    Data/      MutationDef, EnemyDef (ScriptableObjects), MutationEffects, MutationManager
    Sim/       Player, Clone, Enemy, Projectile, DNAOrb, Particle, Floater, Beam, Palette, Fx
    UI/        UIManager (UI Toolkit)
    Game.cs        state machine, fixed-timestep loop, waves, spawning, pooling, render-sync
    InputReader.cs single-stick + ability input
    Sfx.cs         synthesized SFX (no audio assets)
    Rendering.cs   runtime circle-sprite factory + pooled views
    Bootstrap.cs   auto-spawns Game on Play
  Editor/      AssetGenerator.cs  ← canonical numbers + asset generation (the sync point)
  Resources/
    Mutations/ Enemies/   generated SO assets
    UI/        MutagenUI.uxml/.uss (authored) + generated PanelSettings/theme
```

## How to add / change a mutation

1. **Data:** add a row to `AssetGenerator.Mutations` (id, order, name, color, repeatable,
   maxStacks, description), then re-run **MUTAGEN → Generate Assets**.
2. **Effect:** add a `case "id":` in `MutationEffects.Apply` (stat tweaks) and/or runtime
   behavior in `Player` (for active abilities like fire/laser).
   Enemies work the same way via `AssetGenerator.Enemies` + behavior flags.

The HUD and draft cards are fully data-driven — no UXML changes needed.

## Deviations from the prototype (intentional)

1. **Move loadout (v0.4):** Combat is a 4-slot move loadout (keys 1-4), no auto-attack. Moves live in
   a code table (`Moves.cs`, exec delegates → `Player` helpers); draftable moves/modifiers are SOs.
2. **Settings (menu → ⚙):** rebindable keys (`Binds.cs`; click a key, press the new one) and an audio
   mixer — Master/Music/SFX sliders + ambient music (drone + sparse melody). Binds are in-memory (reset on reload).
2. **Bounded 960×600 arena, camera frames it.** v0.2 is `arena == screen`; every number is
   tuned to that box. The notes' "follow camera / open world" is a design *evolution* — switch
   the camera when the HTML evolves.
3. **Visuals.** Floor grid + border and the procedural creature (body, glow, eyes, per-stack arms,
   wings, spiked tail, thick rim, poison aura, fire/laser tint) are ported (`PlayerVisual.cs`,
   `SpriteFactory.MakeFloor`). Enemies are still glowing circles + health bars (their prototype art
   is also circles). All gameplay numbers are exact.
4. **Determinism scope.** Seeded RNG drives run composition (spawn type/side, draft, enemy fire
   timing); cosmetic randomness (particle scatter) is off the seeded stream. This matches the
   prototype's actual guarantee ("seeds fix spawn composition + draft order"). The fixed
   timestep is in place; full frame-exact determinism (single seeded stream for *all* draws) is
   a later tightening.

## Deferred (ponytail-flagged, add when needed)

- View interpolation (sim runs 60 Hz; add if high-refresh stutter shows).
- asmdefs for faster incremental compiles (currently Assembly-CSharp).
- Real sprite art + bloom glow (URP 2D Volume) instead of generated circles + halo.
