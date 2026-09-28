# Implemented MVP architecture

## Frame identity

`FramePawn : Pawn` is a real custom Pawn representing a humanoid pilotable mecha.

It is deliberately **not** a RimWorld/Biotech controllable mech. The debug Frame uses:

- `ToolUser` intelligence so it has ordinary Pawn equipment support
- custom non-organic `ZF_FrameMechanical` flesh type
- a vanilla lancer body/life-stage/graphic only as temporary placeholder content
- its own idle think trees instead of normal colonist work AI or mechanoid combat/work AI

Because `RaceProps.IsMechanoid` is false, Mechanitor, Overseer, bandwidth, command-range and mech-control-group semantics are not the Frame control model.

A debug rifle is created on first spawn and serializes as ordinary Pawn equipment.

## Pilot ownership

`CompPilotableMech : ThingComp, IThingHolder` owns exactly one `ThingOwner<Pawn>`.

That container is the sole source of pilot identity and is deep-scribed under `zenithPilot`. There is no second saved pilot object or speculative global pilot registry.

`JobDriver_BoardFrame` reserves the Frame, walks the pilot into touch range, revalidates, despawns the pawn and transfers it into the cockpit owner. Failed eligibility never mutates ownership. If insertion throws or fails after despawn, the pilot is restored to the original map when possible and the exception is not swallowed.

Disembarking uses Verse's normal `ThingOwner.TryDrop` contract.

## Pawn-first control model

With a valid pilot aboard, basic input is intended to feel like controlling a drafted colonist:

- select pilot -> right-click Frame -> **Board Frame**
- select piloted Frame -> **R** / normal draft command
- right-click ground -> normal drafted-Pawn movement order
- right-click hostile target -> normal ordered attack
- multi-select Frames -> ordinary group movement / attack behavior

The old temporary custom `Move` and `Fire rifle` gizmos were removed.

`Disembark` remains a Frame-specific gizmo. Draft/undraft is represented by a small bridge command using the ordinary `Pawn_DraftController`, vanilla draft hotkey, labels, sounds and grouping key. This bridge exists because vanilla only exposes `Pawn_DraftController.GetGizmos()` for its hard-coded player-controlled pawn categories.

The Frame's drafter is explicitly restored on spawn/load because a custom non-humanlike, non-mechanoid Pawn is outside the categories for which vanilla retains a draft controller automatically.

## Harmony control bridge

Harmony is a runtime dependency, but the patch surface is intentionally narrow. Frames are **not** reclassified as colonists or Biotech mechs.

Current patch points:

1. `Pawn.CanTakeOrder`
   - returns true for a valid operable `FramePawn`
   - this property is used by `FloatMenuContext` to decide which selected Pawns enter the ordinary right-click order pipeline

2. `FloatMenuOptionProvider.SelectedPawnValid`
   - for Frames, permits only the ordinary drafted movement and drafted attack providers
   - prevents the `CanTakeOrder` bridge from accidentally exposing colonist work/equip/haul/tend/rescue/etc. orders

3. `FloatMenuUtility.GetRangedAttackAction`
   - supplies the ordinary `AttackStatic` job for a Frame's equipped ranged weapon
   - omits Biotech command-range checks because Frames are not Biotech mechs

4. `FloatMenuUtility.GetMeleeAttackAction`
   - bridges Frames through the same reachability, violence, melee-verb and `AttackMelee` semantics used by ordinary Pawns
   - removes only the hard-coded vanilla controlled-pawn / Biotech command-range assumptions

5. `PawnAttackGizmoUtility.CanOrderPlayerPawn`
   - lets vanilla's own melee/squad attack gizmo logic recognize a valid piloted Frame
   - Frames still remain neither colonists nor Biotech mechs

`Pawn.IsPlayerControlled` is intentionally **not** patched. Keeping it false avoids globally pretending a Frame is a colonist/mech/subhuman and also avoids `AttackStatic`'s player-controlled automatic retarget behavior.

## Weapon behavior

The debug rifle uses a normal equipped `Verb_Shoot`, projectile, range, LOS, accuracy and cooldown. The debug Frame also has a simple built-in blunt `ZF_FrameStrike` tool so vanilla melee ordering has a real melee verb to use.

A player-issued attack uses RimWorld's ordinary `AttackStatic` job and is no longer capped to one burst. It continues against the chosen target until normal job termination/cancellation conditions apply.

`FireAtWill` is forced off. This is intentional:

- continuing to fire at the target the player ordered = manual targeting
- independently acquiring a new target = autonomous target acquisition

Only the second behavior is forbidden by default.

## Ejection and destruction safety

`Notify_Downed` ejects through the cockpit comp.

`FramePawn.Kill` and `FramePawn.Destroy` detach the pilot **before** entering vanilla destruction cleanup. Ordinary destruction therefore never owns a pilot by the time base cleanup runs.

If the Frame is off-map or a normal drop cannot occur, ownership transfers to `PilotRecovery`, an independently deep-scribed `GameComponent` owner. It retries safe delivery when a home map exists. If no safe owner can accept the pilot, destruction fails loudly instead of silently deleting the pawn.

The game-over bridge remains explicit ownership-based presence logic. It does not fake `Map` or `Position`.

## Deliberate MVP boundaries

- Stored pilots are not ticked; needs, injuries and aging pause while embarked.
- No hostile/neutral boarding or faction conversion yet.
- One fixed debug weapon only.
- No reload/ammunition, repair, power, research, loadout system, chassis variants or final art.
- No caravan, gravship, transport-pod or temporary-map lifecycle support yet.
- No Combat Extended, multiplayer or broad compatibility guarantee.
- No autonomous target acquisition.

## Patch and dependency audit

Production currently uses:

- Harmony as the single third-party runtime dependency
- five narrowly scoped Harmony patch points described above
- zero reflection
- zero Coventech references/dependencies
- no bundled Harmony DLL
- no proprietary RimWorld assemblies in the release

Harmony is referenced for compilation through `Lib.Harmony.Ref`. The release itself contains only ZENITH FRAME content and `ZenithFrame.dll`.

The pawn-first control refactor was validated in Core-only and all-installed-expansions configurations at source commit `74c2cf4c71a80cc691571c2051deeb25675a6b01`; see `docs/VALIDATION.md` for the exact environment, gates, binary hash and retained phase reports.
