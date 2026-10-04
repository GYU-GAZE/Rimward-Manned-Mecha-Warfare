# Frame Body, Damage, and Pilot Trauma

## Authoritative health state

A humanoid Zenith Frame is still a `FramePawn`. Its body damage is stored only in the normal `Pawn_HealthTracker` against `ZF_HumanoidFrameBody`. The contained pilot remains a separate exact human `Pawn` with an independent normal health tracker. There is no Frame HP, pilot HP mirror, copied wound, or serialized disabled state.

The permanent body has 25 mechanical records:

- Torso with internal Core and internal Cockpit
- Head Assembly with Left Optic, Right Optic, and Sensor Array
- mirrored Arm, Forearm, Manipulator, and three Digit chains
- mirrored Leg, Lower Leg, and Foot chains

Every Zenith part is solid and has zero bleed rate. Vanilla capacity tags let the arm chains feed Manipulation, leg chains feed Moving, and optics and sensor feed Sight. These records affect health gameplay only and do not imply additional sprite layers.

Head, Shoulder, and Offhand modules remain separate physical equipment. Their configured `BodyPartGroupDef` mount controls whether derived effects are active, but chassis damage never reduces module item HP or deletes the installed Thing. Losing Head Assembly disables a Helmet projection while retaining the exact Helmet. Losing the tagged offhand mount similarly disables Offhand projections and localized Shield armor. Normal reconstruction restores those effects from the same installed objects without reinstallation.

## Death and disable contracts

`FrameDamageRules` derives state from the real body and health tracker.

A Frame dies when Torso or Core is destroyed. Explicit unusual Hediffs may retain their own `CauseDeathNow` behavior. Frame-only Harmony guards suppress vanilla accumulated-injury lethality and generic lethal required-capacities, so peripheral damage remains structurally recoverable while Torso and Core survive.

A living Frame becomes Downed when Head Assembly or Cockpit is destroyed, or when Moving becomes incapable. Its downing decision ignores pain shock. Manipulation and Sight may become incapable without killing or downing the chassis.

The existing real Downed state continues to drive weapon drop, emergency ejection, hacking, UI, and recovery. Empty mechanically healthy Frames remain standing but inert because operation still requires a valid pilot.

## Cockpit trauma

`Thing.TakeDamage` has a narrow Frame-gated postfix because `ThingComp.PostPostApplyDamage` does not receive the resolved `DamageResult`. The prefix records the exact pilot before Frame damage can trigger ejection. The postfix transfers trauma only when the returned result contains positive `Hediff_Injury` severity on the exact Cockpit record.

The development chassis defines `pilotTraumaFraction = 0.5`. Half of actual resolved Cockpit injury becomes a new normal `DamageInfo` against the exact pilot. The transfer retains damage definition, armor penetration, angle, instigator, weapon, source category, quality, guilt, filth, job-override, and cascade context where the public constructor permits. It does not specify a human hit part, so the normal human damage and apparel pipeline resolves the injury.

There is no additional random roll, damage threshold, penetration threshold, or weapon DefName list. Non-Cockpit injury never crosses this bridge.

## Contained pilot health and transitions

The cockpit calls only `pilot.health.HealthTick()` and `pilot.health.HealthTickInterval(1)` from its own comp tick, matching both health calls in the vanilla Pawn cadence. This advances Hediffs, interval Hediff givers, bleeding, BloodLoss, immunity, and health state without running contained jobs, pathing, think trees, or other full-Pawn behavior.

The existing narrow `Pawn_HealthTracker.MakeDowned` patch ejects a contained exact pilot immediately after their normal downing transition. A narrow Frame-pilot `Pawn.Kill` prefix ejects before vanilla corpse creation, preventing a corpse from being reinserted into the cockpit's `ThingOwner<Pawn>`. On a map, vanilla then creates the normal reachable corpse near the Frame.

An ejected downed hostile pilot remains hostile, spawned, and capturable. It is not added back to a raid Lord while downed or dead. The empty chassis stops immediately. Hacking accepts a living empty hostile or factionless chassis whether the chassis is mechanically Downed or healthy.

## Embarked self-tend

The player command exists only for a spawned player Frame with an operable exact pilot and tendable real wounds. It selects medicine solely from `pilot.inventory.innerContainer`; it never searches map stockpiles. No medicine is a valid target state.

Starting the command stops draft, movement, jobs, stances, active verbs, and pilot ability context before creating `ZF_FramePilotSelfTend`. Job target A is the Frame, target B is the exact carried `Medicine` or invalid, and target C is the exact contained pilot. These normal job targets persist across save/load.

The job continuously validates cockpit identity, health, and medicine ownership. Its duration uses the pilot's `MedicalTendSpeed`. Its work awards Medicine XP to the pilot, then calls `TendUtility.DoTend(pilot, pilot, medicine)`. A new player order interrupts it normally. Pilot replacement, disembark, pilot incapacity, Frame downing, or destruction invalidates or stops it safely.

## Recovery boundary

The development recovery command may remove existing injuries until the real Frame health tracker undowns the chassis, then reclaim the exact remembered Weapon. It never calls `RestorePart` and remains a Dev Mode fixture.

Production recovery now belongs to Construction. `WorkGiver_RepairFrame` reduces real `Hediff_Injury` severity and natural healing is disabled by `InjuryHealingFactor = 0`. `WorkGiver_ReconstructFrame` selects one missing common ancestor, carries the exact chassis-defined material stack, performs saved deterministic work, consumes material only on success, and calls `Pawn_HealthTracker.RestorePart`. Torso and Core remain fatal and cannot enter reconstruction. See `FRAME_SERVICE.md`.

## Focused manual QA

1. Spawn a fresh Debug Frame and inspect its Health tab for the full Zenith mechanical tree.
2. Board a healthy colonist and confirm the Frame tab shows machine capacities plus pilot health, consciousness, and bleeding.
3. Use `DEV: Damage Frame part` on one digit, manipulator, arm, foot, leg, optic, Head Assembly, Cockpit, Core, and Torso across fresh fixtures.
4. Confirm digit, manipulator, arm, foot, and optic losses degrade their normal capacities without an unrelated death.
5. Confirm both locomotion chains, Head Assembly, or Cockpit disable and eject; confirm Core or Torso destroys.
6. Target Cockpit below destruction and inspect the real wound on the exact contained pilot. Target another part and confirm no pilot trauma.
7. Let a bleeding pilot remain embarked and watch BloodLoss and consciousness change in the Frame tab.
8. Self-tend once with carried medicine and once without it. Confirm the Frame stops, no map medicine is consumed, and a new move order interrupts treatment.
9. Down a pilot through BloodLoss and confirm immediate ejection, normal rescue or capture, a healthy standing empty chassis, and Hack Frame availability for hostile ownership.
10. Kill a contained pilot and confirm one normal map corpse, an empty cockpit, and no duplicate or deleted pawn.
11. Save and reload with body damage, a bleeding contained pilot, and an active self-tend job.
