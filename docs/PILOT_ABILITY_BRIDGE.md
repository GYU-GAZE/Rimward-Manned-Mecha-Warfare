# Pilot ability execution

The current model is FRAME-NATIVE EXECUTION / PILOT-OWNED STATE.
See [COMPATIBILITY_CORE.md](COMPATIBILITY_CORE.md) and
[ARCHITECTURE.md](ARCHITECTURE.md) for exact supported APIs and limitations.

The exact contained pilot owns the original vanilla/Royalty/VEF Ability, cooldown,
resources and modded state. The existing Verb binds to the physical Frame and
the original job runs through Frame.jobs. Self effects resolve to the pilot;
map-target effects use the Frame's physical origin. Cancellation restores the
caster and stops transient targeting before ownership changes.

There is no projected pilot.Map, Position, DrawPos, Drafted or player-control
getter. The pilot remains unspawned and cannot path or run independent jobs.
MapHeld/PositionHeld remain ordinary vanilla container APIs.

Vanilla local ability validation, AOE selection, CanCast/action lock and self
target resolution have narrow explicit adaptations. Two stable vanilla Psycast
call sites use an explicit execution map for VFX. Custom content effects which
assume their logical owner is spawned cannot be promised universal support.
Owner-based warmup/social motes, emitted-fleck, world targets and actor-moving
Jump/ConsumeLeap are explicitly unsupported while embarked.

VEF support is an optional framework adapter, built only with WithVEF. Caster,
CasterPawn, StartAbilityJob, IsEnabledForPawn and the stock driver's CompAbilities
getter are the framework surface. The original pilot CompAbilities ticks normally
through contained Pawn ticking; no Frame component proxy exists.

The VPE generated display-class/lambda transpiler no longer exists. VEF warmup
extensions receive Toil.actor without a separate logical-comp argument. VPE's
normal warmup requires actor.GetComp<CompAbilities> and therefore cannot safely
run while embarked. Such extension-backed warmups are disabled with a reason.
Their exact pilot state remains intact for ordinary disembarked use.

Commands, resources, cooldowns and save data are never cloned onto the Frame.
Unsupported is a stated boundary, not a signal to add per-content-mod patches.
