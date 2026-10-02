# Embarked Pilot Ability Bridge

## Durable ownership rule

Pilot abilities remain owned by the exact pilot. The cockpit Frame supplies only the spawned operational body and spatial context while that pilot is embarked.

The bridge must never copy a vanilla `Ability`, a VEF `Ability`, a `Pawn_PsychicEntropyTracker`, a VEF `CompAbilities`, a VPE hediff, a psyset, or a cooldown onto the Frame. Machine abilities such as `ZF_DebugFrameOverdrive` remain independently owned by the chassis.

## Investigated runtime

The design below was derived from the assemblies and definitions installed for the validated RimWorld 1.6 runtime rather than from assumed APIs.

| Component | Installed identity |
| --- | --- |
| RimWorld | `1.6.4871 rev590` |
| Vanilla Expanded Framework | package `OskarPotocki.VanillaFactionsExpanded.Core`; `VEF.dll` assembly `1.1.7.0`, file `1.1.9.0`; SHA-256 `E7C26859386E62D681B0F6E2E8B155DBCB589BCA51DD0EF1B9297D314D315F94` |
| Vanilla Psycasts Expanded | package `VanillaExpanded.VPsycastsE`; `VanillaPsycastsExpanded.dll` assembly/file `1.1.0.0`; SHA-256 `0FCA12E85B2258F8B5D86832BEEEF5151048F1B82798A465448EF55016DE69C9` |

### Holder discovery

The authoritative relationship already exists in Verse:

```text
pilot.holdingOwner
  -> ThingOwner<Pawn>
  -> CompPilotableMech
  -> FramePawn
```

`MapHeld`, `PositionHeld`, and `SpawnedParentOrMe` can follow this chain. `FrameAbilityContext.TryGetOperationalFrame` therefore resolves the host from ownership on demand and does not maintain a pilot-to-Frame dictionary.

Resolution succeeds only when:

- the holder owner is `CompPilotableMech`
- `cockpit.Pilot` is the exact same Pawn
- the parent is a spawned `FramePawn`
- the Frame and cockpit are operational

World Frames, empty Frames, stale holder relationships, downed Frames, destroyed Frames, and recovery-owned pilots do not produce an operational host.

## RimWorld Ability pipeline

### Ownership and persistence

- `Pawn_AbilityTracker.abilities` deep-scribes each `RimWorld.Ability` with the tracker Pawn as constructor owner.
- `Ability.pawn` is the owner.
- cooldown fields and charges are stored inside that same `Ability` instance.
- `Psycast` consumes `ability.pawn.psychicEntropy` and never needs a Frame-side tracker.

### Gizmos

- `Pawn_AbilityTracker.GetGizmos` filters on the Pawn's player-control and draft classification.
- each accepted ability calls its own `GetGizmos`.
- `Ability.GetGizmos` constructs the configured `AbilityDef.gizmoClass`.
- `Psycast.GetGizmos` constructs the real `Command_Psycast`.
- `Pawn_PsychicEntropyTracker.GetGizmo` owns the real psychic status gizmo.

The Frame forwards the pilot's original commands and tracker gizmo. It does not replace them with `Command_Action`. Visibility uses the Frame's draft state, while command ownership stays on the pilot.

### Targeting and range

- `Command_Ability.ProcessInput` begins targeting with `ability.verb`.
- `Targeter` validates `ITargetingSource.Caster`, its selected state, map, verb availability, range, LOS, overlays, and target validation.
- `Ability.ConstantCaster` normally returns `ability.pawn`; its `VerbTracker` consumes that property as the caster.
- self casts explicitly queue `ability.pawn` as target.

For an embarked operation, `Ability.ConstantCaster` resolves to the Frame. The targeter, range, LOS, rings, and verb origin consequently use the spawned Frame. `Ability.pawn` remains the pilot, so a self target is still the pilot.

### Job and warmup

- `Ability.QueueCastingJob` creates a normal ability `Job` containing the original ability and verb.
- vanilla normally submits it through `ability.pawn.jobs`.
- `JobDriver_CastAbility` runs the verb and calls the original ability.
- several visual paths still read `ability.pawn.Map`, `Position`, or `DrawPos` directly.

The bridge redirects only the original pilot-owned ability job to `Frame.jobs`. The job still refers to the original ability. During targeting and the lifetime of that cast job, narrowly scoped spatial access projects the exact pilot's `Map`, `Position`, and `DrawPos` from the exact operational Frame. It never reports `pilot.Spawned = true`.

### Resources and completion

- `Psycast.Activate` changes `ability.pawn.psychicEntropy`.
- `Ability.PreActivate` changes cooldown/charges on the same ability.
- effects continue to receive the original ability and logical targets.

The bridge does not intercept or reproduce these resource operations.

## Royalty Psycast pipeline

Royalty `Psycast` is a `RimWorld.Ability`, so it uses the same command, targeter, verb, job, and spatial bridge.

- owner: pilot `Psycast` instance
- range/LOS/visual origin: Frame through the operational caster and scoped spatial context
- self target: pilot
- entropy and psyfocus: pilot `Pawn_PsychicEntropyTracker`
- cooldown: pilot ability
- status UI and desired psyfocus: pilot tracker gizmo
- save/load: pilot tracker and ability serialization

Royalty remains optional. The core bridge references only types present in Core's game assembly and performs Royalty-specific work only when those game objects exist.

## VEF Ability pipeline

### Ownership and persistence

- `VEF.Abilities.CompAbilities` stores `LearnedAbilities` on the pilot.
- `GiveAbility` creates the configured ability class, assigns `ability.pawn` and `ability.holder` to the pilot, and initializes its `Verb_CastAbility`.
- `Ability.cooldown`, targets, extensions, and mod state stay on that exact object.
- `CompAbilities.PostExposeData` deep-scribes learned abilities and restores their pilot owner.

### Gizmos and targeting

- `CompAbilities.GetGizmos` calls `Ability.ShowGizmoOnPawn` and then the configured `AbilityDef.gizmoClass` through `Ability.GetGizmo`.
- the VEF command retains `ability.DoAction`, descriptions, autocast, overlays, and cooldown display.
- `Ability.DoTargeting` uses the VEF ability itself as `ITargetingSource`.
- VEF directly reads `ability.pawn.Position` and `ability.pawn.Map` in range, LOS, targeting overlays, AoE enumeration, cast effects, and warmup.

The optional adapter returns the Frame from VEF's targeting-source `Caster`/`CasterPawn` while keeping `ability.pawn` unchanged. It forwards the real configured VEF command. The same scoped spatial projection covers the direct position/map reads proven in the installed VEF assembly.

### Job and warmup

- `Ability.StartAbilityJob` records `currentlyCasting` and targets on the pilot's `CompAbilities`, then normally starts the job on `pilot.jobs`.
- `JobDriver_CastAbilityOnce` normally fetches `CompAbilities` from its spawned job actor.
- warmup callbacks use the toil actor for aim/sounds and the ability owner for several VEF effects.

The adapter starts the normal VEF job on the Frame and makes that driver's `CompAbilities` lookup resolve to the pilot's existing component. No component is added to the Frame. The Frame supplies the spawned actor, pathing, stance, aim pie, and interruption lifecycle; the pilot component continues to supply the original ability and targets.

## VPE Psycast pipeline

VPE 1.6 uses VEF abilities and adds behavior through the existing pilot objects.

- `AbilityExtension_Psycast.IsEnabledForPawn` reads the original pilot's VPE hediff and psychic tracker.
- `AbilityExtension_Psycast.Cast` calls `Hediff_PsycastAbilities.UseAbility`, consuming the original pilot's psyfocus and entropy.
- `Command_Ability_Psycast` is selected through the VEF ability def's configured gizmo class and displays costs from the original pilot.
- `CompAbilities_GetGizmos_Patch` filters actual VEF commands through the pilot's active psyset and adds the real psyset controls.
- `Pawn_EntropyTracker_GetGizmo_Prefix` replaces the vanilla tracker gizmo with the real VPE `PsychicStatusGizmo`.
- `Hediff_PsycastAbilities` owns levels, points, paths, psysets, channeling state, and progression on the pilot.

The VEF adapter is therefore also the VPE cast bridge. VPE commands, psyset controls, status gizmo, hediff, progression, costs, and save data are forwarded from the pilot without a VPE-specific copy or replacement UI.

## Optional dependency strategy

Zenith Frame's core assembly has no VEF or VPE reference. A separate `ZenithFrame.VEF.dll` is built against the installed VEF API and placed under a load folder guarded by:

```xml
IfModActive="OskarPotocki.VanillaFactionsExpanded.Core"
```

The optional assembly contains the narrow VEF job and gizmo integration. VPE needs no hard reference because its public behavior arrives through VEF and the pilot's normal tracker/hediff. `About.xml` does not require either mod.

This gives four deliberate states:

- Core without Royalty, VEF, or VPE: core bridge loads and ordinary pilot abilities work
- official DLC stack without VEF/VPE: vanilla and Royalty path
- VEF without VPE: VEF adapter and generic VEF abilities
- VEF plus VPE and Royalty: same VEF adapter with real VPE commands and state

## Scoped spatial compatibility rule

Installed VEF and vanilla code repeatedly read owner `Map`, `Position`, and `DrawPos` directly. There is no shared public spatial-source property that reaches all of those paths. A narrowly scoped getter bridge is therefore evidence-driven and permitted only under these conditions:

- the Pawn is the exact current cockpit pilot
- the host is the exact spawned operational Frame
- an ability command is being evaluated, targeting is active for that pilot ability, or the Frame is running that pilot ability's cast job
- only `Map`, `Position`, `DrawPos`, and draft-state queries needed by supported ability pipelines are projected
- `Spawned` is never changed or faked
- the pilot is never removed, spawned, or assigned a fake position
- the context is cleared on target cancellation, job completion, disembark, ejection, invalid cockpit state, downing, death, or destruction

Ordinary contained Pawns and all non-ability simulation continue to observe normal Verse state.

## Cancellation and lifecycle

The relationship is revalidated whenever context is used. Invalidating the cockpit cancels targeting and the Frame's cast job before ownership changes. Downing, destruction, ejection, disembark, map removal, and recovery therefore leave the original ability and resource state on the pilot and the independent machine ability state on the Frame.

No local-map cast is allowed for a world/off-map Frame.

## Compatibility contract

Expected to work:

- abilities implemented through the supported RimWorld `Ability` pipeline
- Royalty psycasts implemented through that pipeline
- abilities implemented through the installed VEF ability pipeline
- VPE psycasts using VEF and VPE's normal extensions, commands, trackers, and hediff state

May need later mod-specific support:

- systems bypassing both RimWorld and VEF ability APIs
- custom code requiring `pawn.Spawned == true`
- custom code reading or mutating unbridged Pawn state outside an ability operation
- bespoke casting managers or UI that never use the shared command/targeter/job paths

No universal compatibility claim is made. Production code contains no per-DefName psycast table and no copied Frame equivalents.
