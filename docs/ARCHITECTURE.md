# Implemented Frame core architecture

This is the compatibility-first candidate on `refactor/compatibility-first-frame-core`,
from accepted UI baseline `bd2692cc16004c10d90c111f49ca23df1d0682a3`.
See [COMPATIBILITY_CORE.md](COMPATIBILITY_CORE.md) for the subclass spike,
framework API evidence, canary source snapshots and concrete boundaries.

## Identity and custody

The map actor remains a real `FramePawn : Pawn`. `FrameIdentity` recognizes
the cockpit comp as the explicit machine marker at shared execution boundaries.
The subclass keeps local pre-death/destruction custody, map exit, label, draft
persistence, control ticking and command filtering. Replacing those hooks would
add several global lifecycle interceptions. Exact-class XML comp injection by
some mods remains a potential boundary. Current RunAndGun delivers its comp through BasePawn inheritance, as observed live.

`CompPilotableMech` deep-owns the exact pilot in one `ThingOwner<Pawn>`.
Boarding stops independent pilot jobs, despawns the Pawn and restores map custody
in finally if transfer fails. Disembark cancels targeting before dropping the
same Pawn. Downing ejects through the comp; Kill/Destroy eject before destructive
base cleanup. Off-map failures transfer to the explicit `PilotRecovery` owner.
Map removal detaches nested pilots before vanilla recursive snapshots. World
exit retains free player pilots through their outer Frame; hostile world cleanup
keeps the existing explicit discard contract.

No pilot clone, proxy Pawn, copied skill tracker, saved machine HP or parallel
module identity is introduced. Ordinary map, world, cockpit and recovery owners
remain authoritative.

## Temporal simulation

The installed 1.6 API is `ThingOwner.DoTick()`. The cockpit invokes it once;
it runs ordinary Pawn.Tick and interval simulation, including comps, health,
abilities, inventory, genes, needs, skills, psychic entropy and age. Only the
exact contained pilot's independent JobTracker tick/interval are suppressed.
Pawn pathing/stances already require Spawned. The contained pilot remains
unspawned and pilot.Map remains null. Vanilla unspawned rules still apply:
entropy can recover, while NeedsPsyfocus is false outside map/caravan.

Loadout ticks each non-equipment physical owner once. A module comp ticks its
original Ability while installed; changing host runs the same vanilla tick once
to settle an expired cooldown timestamp. The module remains the deep owner.
Cooldown state is never rebuilt from a DefName.

## Action and order contracts

CanOperate means a physically valid machine/pilot pair. CanExecuteActions adds
service lock and physical stun. Player/hostile control, combat orders, intrinsic,
module and pilot abilities, ordinary Verb execution and melee retaliation use
that action contract. Service cancels in-flight action without ejecting the pilot.

The provider filter accepts generic drafted-only semantics, explicit Zenith
providers and the two stock rescue/capture carry exceptions. Unknown undrafted
providers still need an explicit contract because the native provider API does
not declare the resulting job. The ordered-job and StartJob boundaries accept
movement, combat, carry, exact owned abilities and explicitly declared
FrameJobExtension capabilities; human labor/social/survival remains blocked.
Equipment category/family policy also guards Pawn_EquipmentTracker.AddEquipment,
before its ownership mutation.

## Abilities

The pilot owns the original Ability, cooldown and psychic resources. The Frame
supplies the original Verb's physical caster and runs the original cast Job.
Logical self targets survive in job target C and resolve to the pilot at
activation. Target validation, AOE enumeration and targeting origin explicitly
use the Frame. All five old global spatial/control projection getters are gone.
Two stable vanilla Psycast method call sites adapt cast-VFX map access; ordinary
Pawn getters remain truthful.

Owner-based warmup/social motes, emitted-fleck abilities, world targeting,
Jump/ConsumeLeap and custom effects assuming a spawned logical owner are concrete
boundaries. Supported ordinary local abilities use native commands/jobs/effects.

The optional VEF adapter uses framework Caster/CasterPawn, StartAbilityJob,
IsEnabledForPawn and JobDriver_CastAbilityOnce.CompAbilities. The Frame does not
own a CompAbilities. VPE's generated-lambda transpiler was removed. Framework
warmup extensions which require the actor to own the logical component are
blocked while embarked; VPE's normal psycast warmup is in that unsupported set.

## Physical machine and service

Weapon stays solely in Pawn equipment. Head, Shoulder, Offhand, Utility and Power
have exact deep ThingOwners. Install/remove finally restores detached Things to
their original map, the Frame map or explicit Frame inventory custody. A
replacement failure preserves both incoming and outgoing identities and restores
the outgoing slot when possible. Exceptions propagate.

Reconstruction jobs bind one immutable BodyDef AllParts index in job.countQueue
and its material cost in job.count. The driver validates that exact missing part
and cost before reservation/work; invalidation fails safely instead of selecting
another part. Old queued debug jobs without this metadata are rejected.

Service preserves enqueue preference but skips temporarily inaccessible/reserved
orders. Blocked exact requests remain queued. UI distinguishes Queued, Blocked and
Servicing. Docking stays position-derived and gives the definition's fivefold
boarding/work multiplier; it never contains a Frame.

Real Pawn anatomy remains authoritative. Torso/Core destruction is fatal;
Head/Cockpit/Moving loss is disabling. Only resolved positive Cockpit injury
transfers trauma to the exact pilot. Human tending remains ordinary pilot health.

## Content authoring and presentation

The minimum BodyDef has exactly one Torso root, Core, Cockpit and Head Assembly,
plus the groups needed by its selected modules. ConfigErrors and construction
validation reject malformed chassis loudly without demanding one complete debug
anatomy. Overlay modules declare allowedChassisFamilies; visualFamily belongs to
the chassis. Non-overlay modules may remain unrestricted or opt into families.

New physical chassis initialize three livery colors from their own defaults once.
Saved/customized paint is preserved. Native render-tree layers and shared
material safety remain unchanged. Structural visibility covers the implemented
Torso/Head/Helmet/Shoulder rules; complete independent limb-disappearance art is
not implemented.

The accepted 620x520 Frame layout and 520x420 paint composition remain.
Popups capture the bounds of the GUI group actually drawing the Frame tab and
use native TabRect as fallback. Side placement and screen clipping use effective
UI coordinates. Portrait zoom is a small chassis authoring parameter, with the
Debug Frame's existing 0.43 retained. A UI overhaul replacing the tab altogether
cannot be supported by geometry alone.

## Packaging and validation

CoreOnly and WithVEF are explicit package modes. CoreOnly removes the adapter
load-folder claim and contains no optional DLL. WithVEF fails if the 1.6 framework
reference or resulting adapter is missing. A package mode marker and static
validation verify the choice. Core keeps Harmony as its only hard third-party
runtime dependency; no game, Harmony or test DLL is shipped.

Build/static/policy checks are the implementation loop. One final sequential
game acceptance exercises the completed candidate; one rerun is allowed after
a concrete failure and fix. Broad save/load, visual quality, actual mod-list
interaction and transport/world gameplay remain human QA. Historical suite
results in VALIDATION.md are evidence for their old commits only.
