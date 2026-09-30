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

1. `HealthAIUtility.CanRescueNow`
   - rejects every `FramePawn` from vanilla rescue/capture semantics
   - also blocks autonomous `WorkGiver_RescueDowned` jobs while leaving ordinary Pawns unchanged

2. `Pawn.CanTakeOrder`
   - returns true only for an operational player-owned `FramePawn` with a valid player colonist pilot
   - this property is used by `FloatMenuContext` to decide which selected Pawns enter the ordinary right-click order pipeline

3. `Pawn_MeleeVerbs.TryMeleeAttack`
   - rejects melee execution for a Frame whose cockpit cannot operate
   - closes vanilla retaliation paths that can invoke melee outside the current idle `JobDriver`

4. `FloatMenuOptionProvider.SelectedPawnValid`
   - for Frames, permits only the ordinary drafted movement and drafted attack providers
   - prevents the `CanTakeOrder` bridge from accidentally exposing colonist work/equip/haul/tend/rescue/etc. orders

5. `FloatMenuUtility.GetRangedAttackAction`
   - supplies the ordinary `AttackStatic` job for a Frame's equipped ranged weapon
   - omits Biotech command-range checks because Frames are not Biotech mechs

6. `FloatMenuUtility.GetMeleeAttackAction`
   - bridges Frames through the same reachability, violence, melee-verb and `AttackMelee` semantics used by ordinary Pawns
   - removes only the hard-coded vanilla controlled-pawn / Biotech command-range assumptions

7. `PawnAttackGizmoUtility.CanOrderPlayerPawn`
   - lets vanilla's own melee/squad attack gizmo logic recognize a valid piloted Frame
   - Frames still remain neither colonists nor Biotech mechs

`Pawn.IsPlayerControlled` is intentionally **not** patched. Keeping it false avoids globally pretending a Frame is a colonist/mech/subhuman and also avoids `AttackStatic`'s player-controlled automatic retarget behavior.

## Weapon behavior

The debug rifle uses normal equipped `Verb_Shoot`, projectile, range, LOS, accuracy and cooldown behavior. It has two narrowly gated verb entries because RimWorld's player and AI attack pipelines consult different weapon metadata:

- `Verb_FrameShot` is the manually targeted standard command used by a drafted player Frame on `Misc1` / **B**
- `Verb_HostileFrameShot` is the primary non-command verb used by vanilla hostile combat AI and is unavailable without a valid operational hostile cockpit

The player right-click bridge binds the selected manual verb and marks the resulting `AttackStatic` job as player-forced. The primary execution verb is allowed to service that exact job because RimWorld's attack driver consults the equipment primary verb while firing. It receives no player command and cannot initiate a player target on its own. No Frame-specific Fire gizmo exists. The debug Frame also has a simple built-in blunt `ZF_FrameStrike` tool so vanilla melee ordering has a real melee verb to use.

A player-issued attack uses RimWorld's ordinary `AttackStatic` job and is no longer capped to one burst. It continues against the chosen target until normal job termination/cancellation conditions apply.

For player Frames, the internal firing permission is enabled only while executing an explicit player-forced `AttackStatic` job bound to `Verb_FrameShot`, then returns to off. This is intentional:

- continuing to fire at the target the player ordered = manual targeting
- independently acquiring a new target = autonomous target acquisition

Only the second behavior is forbidden by default. Player Frames have no autonomous combat branch in their think tree.

## Hostile piloted Frame operation

`CanOperate` describes machine and pilot state without assuming player ownership: the Frame and pilot must both be functional, the cockpit must contain that exact pilot, and their non-null factions must match. `CanTakePlayerOrders` adds player ownership and player-colonist requirements. `CanUseHostileCombatAI` instead requires an operational Frame whose faction is hostile to the player. Harmony UI/order bridges consume `CanTakePlayerOrders`; machine shutdown, ejection and melee execution boundaries consume `CanOperate`.

The development action **Spawn hostile piloted Frame** generates one hostile debug Frame and one real same-faction human pawn, then transfers that pawn into the existing cockpit `ThingOwner<Pawn>` through the same ownership-safe storage path used by normal boarding. The pilot is neither a fake stat record nor a duplicate world pawn. Faction mismatch, invalid pilot state or pilot loss prevents operation immediately.

The Frame think tree contains one conditional hostile branch before inert idle. It runs only while `CanUseHostileCombatAI` is true and delegates target acquisition, movement, ranged combat and melee to vanilla `JobGiver_AIFightEnemies`, normal pathing, `AttackStatic`, `AttackMelee` and weapon verbs. This AI represents the stored hostile pilot operating the machine. Faction alone never enables combat.

Downing ejects the exact hostile pilot through the ordinary cockpit lifecycle. The same chassis then becomes empty, inert, recoverably disabled and eligible for the existing Hack Frame job. Hacking, recovery and player boarding preserve that Frame's identity and transition it back into the ordinary player control path.

## Ejection and destruction safety

`Notify_Downed` ejects through the cockpit comp.

`FramePawn.Kill` and `FramePawn.Destroy` detach the pilot **before** entering vanilla destruction cleanup. Ordinary destruction therefore never owns a pilot by the time base cleanup runs.

If the Frame is off-map or a normal drop cannot occur, ownership transfers to `PilotRecovery`, an independently deep-scribed `GameComponent` owner. It retries safe delivery when a home map exists. If no safe owner can accept the pilot, destruction fails loudly instead of silently deleting the pawn.

The game-over bridge remains explicit ownership-based presence logic. It does not fake `Map` or `Position`.

## Disabled-chassis recovery

`CompFrameRecovery` is a focused Frame-only lifecycle component. It does not save a parallel `isDisabled` flag. `IsRecoverablyDisabled` is derived from the existing Pawn and cockpit state:

- the Frame is spawned and downed
- it is alive and not destroyed
- its cockpit exists and contains no pilot

The existing downed notification remains responsible for ejecting the pilot. While downed, the same `CanOperate`, `CanBoard`, draft and Harmony order gates keep the chassis inactive. A narrow Frame-only Harmony gate also prevents vanilla Rescue/Capture actions and autonomous rescue jobs from treating the chassis as an injured or imprisonable biological Pawn. The component contributes only a concise inspect status and, in Dev Mode for a player Frame, **DEV: Recover disabled chassis**.

`TryRecover` operates on the existing `FramePawn`. It restores missing-part common ancestors one at a time, then individual injuries largest-first only if still needed, stopping as soon as RimWorld's normal health evaluation clears the downed state. It neither destroys nor spawns a replacement Frame, saves no recovery state and performs no faction conversion. If vanilla dropped the fixed debug rifle during incapacity, recovery transfers that exact nearby item back into the equipment owner; it does not create another weapon.

Successful recovery leaves the chassis unpiloted, undrafted and inactive. A pilot must board again through the normal boarding job before Pawn-first controls become available. Dead and destroyed Frames fail recovery.

This is deliberately a technical development action, not the final repair economy. Costs, work, facilities and towing remain future systems.

## Disabled hostile Frame hacking

`CompFrameHacking` owns the acquisition eligibility and final ownership transition. A target must be the existing spawned, living, empty, downed Frame represented by `CompFrameRecovery.IsRecoverablyDisabled`. Its current faction must be either null or hostile to the player. Player-owned and neutral/friendly faction-owned Frames are never acquisition targets. The actor must be a spawned, player-controlled human colonist on the same map who is alive, mobile, mentally available, free and able to reserve and reach the chassis.

`FloatMenuOptionProvider_HackFrame` adds **Hack Frame** to that colonist's ordinary right-click flow. `JobDriver_HackFrame` reserves the target, uses normal touch-range pathing, performs a visible 180-tick delay and rechecks the complete eligibility predicate throughout the job. If any condition changes, the job becomes incompletable without changing faction.

Successful completion calls the normal `FramePawn.SetFaction(Faction.OfPlayer)` API on the same object. It creates no replacement Frame and saves no separate hacked flag. The chassis remains downed, empty, undrafted and inactive, then proceeds through the existing recovery and boarding systems.

## Empty Frame inertness

An empty or otherwise inoperable Frame receives `ZF_FrameIdleJob`, driven by a minimal delay-only `JobDriver_FrameIdle`. It does not use vanilla `JobDefOf.Wait`, whose wait driver can scan adjacent hostiles and initiate opportunistic melee.

The melee execution boundary also rejects `Pawn_MeleeVerbs.TryMeleeAttack` when the cockpit cannot operate. This covers retaliation paths invoked outside the current job driver. The invariant is faction-independent: player-owned, hostile and factionless Frames cannot move, acquire targets, attack or retaliate without a valid pilot.

The 180-tick duration and absence of hacking skill, stat, resource or tool requirements are provisional MVP tuning. The same-object ownership conversion and disabled post-hack state are lifecycle constraints.

## Deliberate MVP boundaries

- Stored pilots are not ticked; needs, injuries and aging pause while embarked.
- No hostile boarding UI. A focused internal installation path exists only for the deliberate hostile debug fixture.
- One fixed debug weapon only.
- No final repair economy, reload/ammunition, power, research, loadout system, chassis variants or final art.
- No caravan, gravship, transport-pod or temporary-map lifecycle support yet.
- No Combat Extended, multiplayer or broad compatibility guarantee.
- No raid generation, faction-wide hostile Frame spawning, Lord integration, retreat/exit behavior or enemy loadout generation.

## Patch and dependency audit

Production currently uses:

- Harmony as the single third-party runtime dependency
- seven narrowly scoped Harmony patch points described above
- zero reflection
- zero Coventech references/dependencies
- no bundled Harmony DLL
- no proprietary RimWorld assemblies in the release

Harmony is referenced for compilation through `Lib.Harmony.Ref`. The release itself contains only ZENITH FRAME content and `ZenithFrame.dll`.

The pawn-first control refactor, disabled-chassis recovery, hacking and single hostile-pilot lifecycle were validated in Core-only and all-installed-expansions configurations; see `docs/VALIDATION.md` for exact commits, environments, gates, binary hashes and retained phase reports.
