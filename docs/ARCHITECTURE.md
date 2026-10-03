# Implemented MVP architecture

## Frame identity

`FramePawn : Pawn` is a real custom Pawn representing a humanoid pilotable mecha.

It is deliberately **not** a RimWorld/Biotech controllable mech. The debug Frame uses:

- `ToolUser` intelligence so it has ordinary Pawn equipment support
- custom non-organic `ZF_FrameMechanical` flesh type
- a vanilla lancer body/life-stage/graphic only as temporary placeholder content
- its own idle think trees instead of normal colonist work AI or mechanoid combat/work AI

Because `RaceProps.IsMechanoid` is false, Mechanitor, Overseer, bandwidth, command-range and mech-control-group semantics are not the Frame control model.

The loadout component installs the definition-provided default debug rifle exactly once. It serializes as ordinary Pawn equipment and is never regenerated merely because the Frame later becomes weaponless.

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

## Pilot combat skill bridge

The cockpit pilot remains the only owner of combat skills and experience. A Frame has no `SkillTracker`, copies no `SkillRecord`, and serializes no duplicate skill state. Replacing the pilot therefore changes combat performance immediately while preserving the same chassis and weapon.

`StatPart_PilotSkill` is attached to `ShootingAccuracyPawn` and `MeleeHitChance`. For an operational Frame it replaces the StatDef's ordinary `noSkillOffset` with the exact `SkillNeed.ValueFor(pilot)` contribution for Shooting or Melee, then leaves vanilla's existing stat post-process curve intact. This preserves chassis and equipment modifiers while excluding the pilot's capacities, traits, genes, ideology, apparel, equipment and hediffs. Empty or inoperable Frames receive no pilot contribution. Normal stat explanations identify the live pilot skill and level.

Ranged experience is awarded at the real `Verb_Shoot.WarmupComplete` execution point through the shared Frame shooting verb, using vanilla's hostile/non-hostile rate and `AdjustedFullCycleTime`. Melee experience is awarded by a narrow `Verb_MeleeAttack.TryCastShot` prefix at the equivalent real attack-execution boundary. Both routes call `pilot.skills.Learn`, leaving passion and learning behavior to vanilla. Player manual fire, Fire At Will, forced melee and hostile AI all teach the exact contained pilot. The chassis never learns and the pilot is never taught twice.

This milestone bridges only Shooting into ranged accuracy and Melee into melee hit chance. It does not add a custom Piloting skill or bridge melee dodge, aim/warmup time, weapon cooldown, movement, abilities, traits, capacities, genes, ideology or other human modifiers.

## Machine-owned Frame abilities

Frame abilities are intrinsic chassis state. The debug `ZF_DebugFrame` declares `ZF_DebugFrameOverdrive` in its normal `PawnKindDef.abilities` list, so vanilla `PawnComponentsUtility` creates `Pawn_AbilityTracker` and `PawnGenerator` grants exactly one ability instance. The Frame Pawn is the ability owner, caster, cooldown owner and effect owner. The contained pilot receives no ability, hediff or copied timer.

`Ability_FrameOverdrive` subclasses the Core `Ability` type and adds only one rule after `base.CanCast`: its pawn must be a `FramePawn` whose cockpit can operate. The rule is faction-independent. Empty, downed, dead or destroyed Frames cannot cast; a valid hostile pilot can operate the same machine ability without gaining player control.

Vanilla `Pawn_AbilityTracker.GetGizmos` hides commands for this deliberately non-colonist, non-Biotech classification. `FramePawn.GetGizmos` bridges only this visibility gap for a valid player-operated Frame by yielding the ability's ordinary `Command_Ability`. Vanilla still supplies its icon, tooltip, drafted disabled state, cooldown display, grouping and `CastAbilityOnThing` job. No parallel ability tracker, command surrogate or cooldown serialization exists.

Frame Overdrive is a development architecture proof. A vanilla `CompAbilityEffect_GiveHediff` self-applies `ZF_FrameOverdrive` for 300 ticks, and its hediff stage carries the provisional MoveSpeed offset. The vanilla ability owns the 900-tick cooldown. Both objects deep-save through the Frame, survive fresh-process load, remain on the chassis during disembark and pilot swaps, and follow the same Pawn through downing, hacking and recovery. Repeated generation, boarding and load cycles retain one ability instance.

Hostile operational Frames expose the same AI-usable ability to vanilla `JobGiver_AIFightEnemy`. The tested ThinkTree path naturally self-casts Overdrive, starts the machine cooldown and returns to ordinary combat without a custom ability AI layer.

These values and the debug presentation are provisional. Abilities owned by the contained pilot, including psycasts, remain a separate later milestone because the real caster Pawn is unspawned while embarked.

## Embarked pilot abilities and psycasts

Pilot abilities remain pilot state. The original vanilla or Royalty `Ability`, VEF `Ability`, cooldown, charge, `Pawn_PsychicEntropyTracker`, VEF `CompAbilities`, VPE implant and psyset stay on the exact contained Pawn. The Frame receives no copied tracker, component, ability or resource. Intrinsic Frame abilities such as Overdrive remain independent chassis state.

When an operational pilot is contained, `FramePawn.GetGizmos` forwards the original pilot commands beside the Frame's own commands. Targeting, range, LOS and warmup use the spawned Frame as the physical caster. Self effects and resource changes still resolve against the pilot. The original cast job runs on `Frame.jobs`, preserving the normal ability object and job driver while the pilot remains exclusively owned by the cockpit.

The core bridge supports RimWorld abilities and Royalty psycasts. It binds already-created verbs to the current Frame, preserves logical self targets in job target C, projects only the exact pilot's ability-time map/position/draw/draft queries and restores the pilot caster on cancellation or cockpit invalidation. Hostile operational Frames use pilot-owned AI-valid abilities through the Frame ThinkTree without exposing player gizmos.

VEF support is a separate conditionally loaded `ZenithFrame.VEF.dll`. It forwards the real configured `Command_Ability`, starts the real VEF job on the Frame and redirects the exact `JobDriver_CastAbilityOnce.CompAbilities` getter to the pilot component during that cast. The shared generic `ThingWithComps.GetComp<T>` body must never be patched because Mono can share that native body across unrelated component types. VPE needs one narrow optional transpiler for its generated warmup lambda, which bypasses the VEF driver property and directly asks the toil actor for `CompAbilities`.

VEF hostile AI evaluates each enabled autocast ability against targets inside that ability's own range and validator. Save/load, pilot replacement, disembark, downing and ejection keep every ability and resource with its original owner. See `docs/PILOT_ABILITY_BRIDGE.md` for the investigated runtime pipelines and compatibility boundary.

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

8. `MapDeiniter.Deinit`
   - detaches contained pilots before vanilla snapshots the recursively enumerated map-pawn list
   - routes player colonists to `PilotRecovery` and other pilots to direct world-pawn ownership
   - leaves the empty chassis to vanilla map-removal handling

9. `Verb_MeleeAttack.TryCastShot`
   - awards vanilla-formula melee experience to the exact operating pilot at real melee execution
   - leaves damage, hit resolution and every non-Frame verb unchanged

10. `Pawn_HealthTracker.MakeDowned`
   - captures the exact installed Frame Weapon immediately before vanilla incapacity handling may drop it
   - records only a shallow reference after downing and leaves every non-Frame pawn unchanged

`Pawn.IsPlayerControlled` is intentionally **not** patched. Keeping it false avoids globally pretending a Frame is a colonist/mech/subhuman and also avoids `AttackStatic`'s player-controlled automatic retarget behavior.

## Physical Frame loadout

`CompFrameLoadout` owns policy and mutation for the deliberately small add-on vocabulary `Weapon`, `Utility` and `Power`. The debug chassis has one explicit slot for each category. No generalized slot array or additional category exists.

An installed Weapon is a real `ThingWithComps` held only by the Frame's vanilla `Pawn_EquipmentTracker`. The loadout does not keep a second `ThingOwner`, deep-scribe a duplicate item or reconstruct equipment from `defName`. Installation and removal transfer the exact object through normal equipment/map ownership. Eligibility requires an explicit `FrameAddonExtension` whose category is `Weapon`; ordinary RimWorld weapons and test Utility/Power add-ons are rejected.

`CompProperties_FrameLoadout` declares capacity one, one optional default Weapon and development-only candidate Weapons. The default is initialized once for a newly created Frame. The saved `defaultInitialized` flag prevents respawn, recovery and save/load from replacing a removed, destroyed or intentionally absent Weapon.

Every loadout mutation stops Frame operation first. Current jobs, stance, active verbs, targeters and draft state are cleared before the equipment tracker changes, so neither the old verb nor a stale combat job can survive a swap. The normal `Command_VerbTarget`, right-click attack, Fire At Will and hostile AI pipelines then bind the newly installed weapon immediately. Pilot skill contribution stays independent of weapon physics.

Vanilla downing physically drops equipped weapons. A narrow Frame-only `Pawn_HealthTracker.MakeDowned` patch remembers the exact pre-downing Weapon as one shallow scribed reference because this custom nonhumanlike Pawn does not reliably retain vanilla `mindState.droppedWeapon`. Recovery may reclaim that object only while it still exists, is spawned on the same map and lies within two cells. If it was destroyed or removed, the slot remains empty; an identical nearby item is never substituted. Hacking preserves the same relation, world-owned Frames retain equipped items normally, and destruction leaves no duplicate or ownerless loadout object.

Utility and Power are physical Things held by two independent deep-scribed `ThingOwner<Thing>` containers inside `CompFrameLoadout`. The comp implements `IThingHolder`; a stable child holder exposes the second container so Verse traverses both owners during save/load, world ownership and map cleanup. Neither item is placed in Pawn equipment or mirrored as a DefName/ID field.

Power is a static addon budget. `AvailableAddonPower` comes from the installed Power extension, `RequiredAddonPower` comes from the installed Utility extension and `IsUtilityPowered` is derived on demand. There is no charge, drain, fuel, heat, recharge or tick loop. An over-budget Utility stays physically installed but its effect is inactive. Core operation, weapons, Overdrive and pilot abilities remain independent from addon power.

The development Mobility Booster consumes two points and contributes a local `ThingComp.GetStatOffset` of +1 MoveSpeed only when powered. The debug Reactor provides two points; the weak cell provides one. Utility state belongs to the chassis and survives pilot swaps, while the existing no-pilot operation gate remains absolute.

Weapon, Utility and Power defaults each have independent once-only initialization state. The debug Frame defaults to Rifle plus Reactor and no Utility. Removing the Reactor and saving never recreates it. Downing keeps Utility and Power installed while the weapon follows its exact drop/recovery lifecycle. Spawned destruction drops Utility and Power for salvage; off-map destruction destroys both exact objects after pilot safety is resolved. Multiple slots and additional effects remain deferred.

## Frame information UI

Every `FramePawn` exposes the native `ITab_Frame` inspect tab. The tab resolves `SelPawn` on every draw and builds a transient `FrameUiSnapshot` from the selected chassis, its cockpit, loadout, health, recovery and hacking components. It caches no selected Frame, pilot, item or presentation state, so selection changes and every lifecycle transition are visible immediately.

The compact scrollable panel shows chassis name and condition, the exact contained pilot, portrait, live Shooting and Melee skills, exact Weapon/Utility/Power objects, available and required addon power, powered state and counts of existing Frame and pilot abilities. Installed item rows open vanilla info cards. The panel summarizes abilities only; casting remains in the existing vanilla, Royalty, Ideology, VEF and VPE command pipelines.

`Inspect Pilot` opens a read-only dialog over the exact contained `Pawn`. It uses `CharacterCardUtility` and `HealthCardUtility`, preserving vanilla skills, passions, traits, health, ideology and Royalty presentation rules without copying state or temporarily spawning the pilot. Portraits come from `PortraitsCache` for the same Pawn object. Player-owned Frames may rename the chassis and disembark; hostile and factionless Frames remain read-only. Disembark delegates to `CompPilotableMech.TryDisembark`.

Frame naming uses normal `Pawn.Name` with a `NameSingle` value and is serialized by vanilla Pawn state. It is independent of pilot identity. Condition and integrity come from real Pawn health, recoverability comes from `CompFrameRecovery`, and hackability is derived from `CompFrameHacking`; the UI adds no health, ownership, loadout or power fields.

## Weapon behavior

The debug rifle uses normal equipped `Verb_Shoot`, projectile, range, LOS, accuracy and cooldown behavior. It has two narrowly gated verb entries because RimWorld's player and AI attack pipelines consult different weapon metadata:

- `Verb_FrameShot` is the manually targeted standard command used by a drafted player Frame on `Misc1` / **B**
- `Verb_HostileFrameShot` is the primary non-command verb used by vanilla hostile combat AI and by a drafted player Frame while its real `Pawn_DraftController.FireAtWill` state is enabled

The player right-click bridge binds the selected manual verb and marks the resulting `AttackStatic` job as player-forced. The primary execution verb services that exact job because RimWorld's attack driver consults the equipment primary verb while firing. Drafted player Frames also expose the vanilla Fire At Will state through `Misc6`, `TexCommand.FireAtWill` and `Pawn_DraftController`; the guarded `Wait_Combat` branch supplies normal nearby acquisition when the toggle is on. No Frame-specific Fire verb or chess-piece command exists. The debug Frame also has a simple built-in blunt `ZF_FrameStrike` tool so vanilla melee ordering has a real melee verb to use.

A player-issued attack uses RimWorld's ordinary `AttackStatic` job and is no longer capped to one burst. It continues against the chosen target until normal job termination/cancellation conditions apply.

Fire At Will off blocks natural ranged acquisition but leaves explicit right-click and **B** attacks intact. The toggle persists through save/load with draft intent. Every route remains gated by `CanTakePlayerOrders`, so a missing or invalid pilot immediately removes both automatic and explicit combat.

## Hostile piloted Frame operation

`CanOperate` describes machine and pilot state without assuming player ownership: the Frame and pilot must both be functional, the cockpit must contain that exact pilot, and their non-null factions must match. `CanTakePlayerOrders` adds player ownership and player-colonist requirements. `CanUseHostileCombatAI` instead requires an operational Frame whose faction is hostile to the player. Harmony UI/order bridges consume `CanTakePlayerOrders`; machine shutdown, ejection and melee execution boundaries consume `CanOperate`.

The development action **Spawn hostile piloted Frame** generates one hostile debug Frame and one real same-faction human pawn, then transfers that pawn into the existing cockpit `ThingOwner<Pawn>` through the same ownership-safe storage path used by normal boarding. The pilot is neither a fake stat record nor a duplicate world pawn. Faction mismatch, invalid pilot state or pilot loss prevents operation immediately.

The Frame think tree contains one conditional hostile branch before inert idle. It runs only while `CanUseHostileCombatAI` is true. A Lord-owned Frame first consumes its vanilla `ThinkNode_Duty`, allowing `AssaultColony` to approach distant colony targets; the existing `JobGiver_AIFightEnemies` remains the tactical fallback for target acquisition, ranged combat and melee. This AI represents the stored hostile pilot operating the machine. Faction alone never enables combat.

The colonist bar keeps rendering the contained player's portrait. Its selection lookup resolves that portrait through `holdingOwner` to the exact spawned player Frame, including vanilla shift-selection behavior. `FramePawn.DrawGUIOverlay` adds one `GenMapUI` label using `LabelShort`; ordinary Pawn UI does not label this non-humanlike, non-Biotech classification, so the override does not duplicate a vanilla label.

Downing ejects the exact hostile pilot through the ordinary cockpit lifecycle. The same chassis then becomes empty, inert, recoverably disabled and eligible for the existing Hack Frame job. Hacking, recovery and player boarding preserve that Frame's identity and transition it back into the ordinary player control path.

## Development enemy Frame raid lifecycle

`ZF_DevelopmentEnemyFrameRaid` is a zero-chance development incident backed by `IncidentWorker_RaidEnemy`. Its debug action generates exactly one ordinary human raider and one `FramePawn` containing a separately generated same-faction human pilot. The pilot enters the cockpit while both pawns are unspawned. Only the infantry and occupied Frame are passed to vanilla `EdgeWalkIn`, `ImmediateAttack` and `RaidStrategyWorker.MakeLords`, so the pilot is never duplicated as a spawned or Lord-owned pawn.

The cockpit remembers the operational Lord as a saved reference because RimWorld can remove an incapacitated Frame from `pawn.lord` before `ThingComp.Notify_Downed` runs. Ejection consumes that reference: the empty chassis is removed from raid control and the exact spawned pilot is added to the same Lord. Hacking explicitly removes any current hostile Lord membership before `SetFaction` converts the existing chassis.

Retreat remains driven by `LordJob_ExitMapBest` and `JobGiver_ExitMapBest`. RimWorld does not reliably assign an exit `PawnDuty` to this custom Pawn classification, so `FramePawn` supplies that narrow duty bridge only while its saved operational Lord is an exit Lord. The normal exit job passes the Frame into world-pawn handling without ejecting or duplicating the contained pilot. `FramePawn.ExitMap` clears the no-longer-needed Lord reference after the transfer. Empty or disabled Frames cannot enter this path because every bridge is gated by `CanUseHostileCombatAI`.

## Map and world ownership lifecycle

A living pilot has exactly one direct owner at every supported lifecycle point: a spawned map, a spawned Frame cockpit, a world-owned Frame cockpit, direct `WorldPawns` ownership or `PilotRecovery`. The runtime ownership audit enumerates all five locations, including cockpit contents nested inside world pawns.

Vanilla `MapDeiniter.PassPawnsToWorld` begins from `MapPawns.AllPawns`, whose recursive holder traversal includes a pilot inside a spawned Frame. The narrow `MapDeiniter.Deinit` prefix therefore prepares each spawned Frame before that snapshot. A free player colonist in a player Frame transfers atomically into `PilotRecovery`; a non-player pilot detaches into direct world-pawn ownership. Vanilla then passes the now-empty chassis through its normal map-removal path. This also clears saved raid-Lord state.

Ordinary `FramePawn.ExitMap` keeps the cockpit intact. If it contains a free humanlike player pilot, the outer world Frame is added to vanilla's `ForcefullyKeptPawns`, so world garbage collection cannot discard the colonist indirectly. Later off-map destruction of that Frame transfers the same pilot into `PilotRecovery`.

Hostile escaped Frames are not kept forever. If vanilla deliberately garbage-collects a hostile world Frame, the cockpit recognizes the active discard transaction and discards the nested hostile pilot with the chassis, leaving no second world-pawn entry or dangling owner.

The tested map-abandonment/removal core uses a legitimate secondary settlement map created through vanilla world-object and map-generation APIs, then `Game.DeinitAndRemoveMap`. The player pilot survives in recovery while the empty chassis becomes world-owned. Hostile Frame and pilot become separate world pawns. A pilot ejected immediately before shutdown and its disabled chassis also become separate world pawns. The user-facing abandonment confirmation wrapper is not part of the automated fixture.

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

`TryRecover` operates on the existing `FramePawn`. It restores missing-part common ancestors one at a time, then individual injuries largest-first only if still needed, stopping as soon as RimWorld's normal health evaluation clears the downed state. It neither destroys nor spawns a replacement Frame, saves no recovery state and performs no faction conversion. It delegates weapon recovery to `CompFrameLoadout`, which reclaims only the exact remembered nearby item and leaves the chassis weaponless when that object is gone.

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
- One functional slot for each of Weapon, Utility and Power; no multiweapon support or generated enemy loadouts.
- Static addon power affects only Utility. Chassis core power requirements, batteries, fuel, heat, charging and drain are deferred.
- The information UI displays the current physical loadout; mutation controls remain development-only.
- No final repair economy, reload/ammunition, power system, research, chassis variants or final art.
- No caravan boarding gameplay, gravship integration or transport-pod UI yet. The vanilla temporary-map removal core is covered only to preserve ownership safely.
- No Combat Extended, multiplayer or broad compatibility guarantee.
- No general raid generation, faction-wide hostile Frame spawning, frequency tuning, faction composition integration or enemy loadout generation. The current mixed raid is a deterministic Dev-only lifecycle fixture.
- One debug chassis ability and the pilot ability execution bridge are implemented. Ability loadouts, universal third-party compatibility and final ability balance remain deferred.

## Patch and dependency audit

Production currently uses:

- Harmony as the single third-party runtime dependency
- ten narrowly scoped Harmony patch points described above, including the Frame-only downing capture used for exact dropped-weapon recovery
- zero reflection
- zero Coventech references/dependencies
- no bundled Harmony DLL
- no proprietary RimWorld assemblies in the release

Harmony is referenced for compilation through `Lib.Harmony.Ref`. The release itself contains only ZENITH FRAME content and `ZenithFrame.dll`.

The pawn-first control refactor, disabled-chassis recovery, hacking, hostile-pilot lifecycle, development mixed-raid lifecycle, map/world ownership, pilot combat skill bridge, machine-owned Frame ability lifecycle, embarked pilot abilities and physical Frame Weapon loadout were validated in Core-only and all-installed-expansions configurations; see `docs/VALIDATION.md` for exact commits, environments, gates, binary hashes and retained phase reports.

## Humanoid Frame body and pilot trauma

`ZF_HumanoidFrameBody` replaces the temporary Lancer body with 25 mechanical records. Normal Pawn body-part and capacity code owns all chassis injury truth. Torso and internal Core are fatal. Head Assembly, internal Cockpit, and incapable Moving are disabling. Manipulation and Sight are performance capacities and are not death conditions.

Frame-only health patches suppress vanilla lethal injury accumulation, generic lethal required capacities, and pain-shock downing while preserving explicit Hediff death and Torso core-part death. `FrameDamageRules` centralizes the additional Core, Head, Cockpit, and Moving queries without serialized flags.

Resolved Cockpit injury transfers the chassis definition's fraction to the exact pilot through normal `Pawn.TakeDamage`. Cockpit health ticking calls only the contained pilot's `Pawn_HealthTracker.HealthTick`. Downed and Kill transitions eject the pilot immediately; vanilla then owns map downing, rescue, capture, medical care, and corpse creation.

Embarked self-tend is a Frame job with exact pilot and carried-medicine targets. The pilot supplies MedicalTendSpeed, Medicine XP, wound state, and normal `TendUtility`; map medicine is never searched. See `docs/FRAME_DAMAGE_MODEL.md` for the complete contract and QA sequence.
