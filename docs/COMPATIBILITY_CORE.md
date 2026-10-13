# Compatibility-first Frame core

Baseline: `feat/frame-proper-ui` at `bd2692cc16004c10d90c111f49ca23df1d0682a3`.
Candidate branch: `refactor/compatibility-first-frame-core`.

## Physical Pawn identity decision

Keep `FramePawn : Pawn`. The maintained RunAndGun Continued source was inspected at
`MemeGoddess/RunAndGun` commit `f1dc88033d15ae0c0ee4c8d0fc19e5c4b92afc83`.
Its `Patches/RunAndGun.xml` still selects `ThingDef[thingClass = "Pawn"]/comps`.
Its generic runtime uses Pawn.GetGizmos, Drafted, FireAtWill, equipment, Goto,
attack verbs and ordinary stances. A Frame satisfies the runtime surfaces but
does not receive its injected comp. This is a STRUCTURAL LIMITATION, not a
Zenith-specific compatibility patch opportunity.

Complete subclass responsibility inventory:

| Override/API | Responsibility | Decision |
| --- | --- | --- |
| Kill | Eject exact pilot and salvage modules before vanilla death | Keep local override |
| Destroy | Resolve pilot custody before destructive cleanup, including off-map | Keep local override |
| ExitMap | Retain player-pilot world Frame, clear operational Lord | Keep local override |
| DrawGUIOverlay | Label physical nonhuman Frame | Keep local override |
| ExposeData | Draft/FireAtWill intent vanilla does not preserve for this classification | Keep existing state |
| SpawnSetup | Restore real drafter and saved control intent | Keep existing override |
| Tick | Operational invalidation and hostile Lord exit duty before Pawn simulation | Keep local override |
| SetDrafted | Validate player order authority before changing real drafter | Keep direct API |
| GetGizmos | Filter duplicate dev ability commands and expose ordinary commands | Keep local override |
| Cockpit/Loadout/Recovery/Hacking/Service/Livery accessors | Access exact authoritative comps | Keep convenience APIs |

Death must eject before base Pawn.Kill, not in a post-destruction comp callback.
ExitMap has no equivalent ThingComp callback carrying its caravan semantics.
Replacing these overrides would introduce at least four global lifecycle/UI
interceptions plus migrate control and command filtering. There is no lifecycle
or patch-risk benefit sufficient to justify the rewrite. `FrameIdentity` uses
the cockpit comp as the explicit marker at new shared boundaries; the subclass
continues to provide the typed physical actor. No proxy, global Frame registry,
or duplicate machine state is introduced.

## Contained temporal simulation

Installed RimWorld `1.6.4871` exposes `ThingOwner.DoTick()` (the current equivalent
of older `ThingOwnerTick`). It calls each exact contained Thing.DoTick. For Pawn
this runs normal ThingComps, Pawn.Tick and scheduled TickInterval, including
health, abilities, inventory, needs, genes, skills, psychic entropy and age.
Pawn.GetChildHolders includes inventory/carry/equipment/apparel but not its
ThingComp holders. The cockpit therefore invokes its own owner once in CompTick;
loadout invokes each of its five non-equipment owners once. Equipment follows
vanilla Pawn holder ticking.

Pawn pathing, stances and native verbs already require Spawned. Pawn job ticking
does not: it runs for unspawned non-world Pawns. Two conditional JobTracker
prefixes suppress that independent driver/think-tree work only for the exact
pilot directly held by its cockpit. Boarding stops existing pilot jobs before
despawn. No tracker is manually simulated and the pilot's Map remains null.

The exact module owns its Ability. A ticking module runs ordinary AbilityTick
while installed; rebinding to a new Frame performs that same tick once so an
expired saved cooldown timestamp also clears vanilla's internal cooldown flag.
Unbound modules retain their timestamp and are not executed with a null caster.

## Ability execution and action authority

The exact pilot owns its Ability, cooldown, entropy, skills and component state.
The existing Ability Verb is bound to the Frame; its original GetJob result runs
on Frame.jobs. Logical self targets are retained in target C and restored only
at activation. Target validation, AOE enumeration and hostile AOE selection use
the physical actor explicitly. The stock commands, Ability objects and job
drivers remain in use. Disembark cancels targeting and restores caster bindings
before dropping the pilot.

Removed all five projected Pawn/Thing getters: Map, Position, DrawPos, Drafted,
IsColonistPlayerControlled. Removed transient projection stacks and casting
registries. Ordinary pilot.Map remains null and ordinary Spawned remains false.
Two stable vanilla Psycast method bodies (Activate and ApplyEffects) adapt their
direct Map calls for cast/immune VFX through ExecutionMap. This is a narrow
vanilla API call-site adaptation, not a Pawn property patch. No private or
compiler-generated content-mod method is patched.

Vanilla owner-based warmupMote/social bubble, emitted-fleck and world-target
abilities, and actor-moving Jump/ConsumeLeap verbs, are explicitly unsupported
while embarked. Custom effects which assume ability.pawn is spawned also remain
a compatibility boundary. Their logical state is retained; the command reports
the limitation rather than temporarily spawning or impersonating the pilot.

VEF retains its own logical pawn and uses its separate Caster/CasterPawn API for
physical execution. StartAbilityJob is adapted to the Frame, and the framework
JobDriver_CastAbilityOnce.CompAbilities getter resolves the exact pilot comp.
The Frame does not acquire a CompAbilities. Framework jobs declare an Ability
capability to the core order policy. No per-content-mod patch exists.

Removed the VPE generated-lambda transpiler entirely. Extensions overriding
the framework WarmupToil API, and owner-based warmup mote/sound definitions, are
rejected while embarked. The VPE psycast extension falls into this boundary:
its warmup expects toil.actor.GetComp<CompAbilities>, but the actor is a Frame
and the comp belongs to the pilot. VEF currently offers no separate logical
component argument to that extension hook. Consequently VPE's normal warmup
psycasts are UNSUPPORTED while embarked; they retain their original state and
work according to VPE's ordinary rules after disembark. This deliberate boundary
replaces fragile fake support.

CanOperate describes a valid physical machine/pilot pair. CanExecuteActions
adds service lock and physical stun. Orders, player/hostile AI, ability CanCast,
activation, module casting, Verb.TryStartCastOn and melee retaliation consume
that shared rule. Service preserves the pilot and cancels in-flight execution.

## Order provider contract

Generic drafted-only providers now pass the semantic provider gate. Existing
undrafted Zenith providers implement IFrameOrderProvider; stock Rescue/Capture
are the two explicit carry exceptions because their API declares no capability.
This removes the previous closed list of concrete movement/attack providers.
Protected Drafted/Undrafted metadata is read through cached Harmony delegates
to those stable properties; this is reflection and is not described as zero
reflection. Unknown undrafted providers still need an explicit contract.

The authoritative ordered-job and StartJob boundaries admit normal movement,
combat, carry/rescue/capture, exact owned Ability jobs, pilot self-care and
JobDefs declaring a FrameJobExtension capability. Human labor/social/survival
jobs remain rejected. An arbitrary menu action cannot be safely previewed to
discover its job without executing it; unknown custom jobs need the small
explicit capability extension. Achtung-style force-work stays blocked while
normal drafted movement/combat jobs can pass. This is a STRUCTURAL LIMITATION
for unrestricted force-work, and LIKELY NATURAL for movement/formation paths
which use the normal job/drafter APIs; no live Achtung compatibility is claimed.

## Compatibility evidence references

- [RunAndGun Continued source](https://github.com/MemeGoddess/RunAndGun/tree/f1dc88033d15ae0c0ee4c8d0fc19e5c4b92afc83)
- Achtung source snapshot: `pardeike/Achtung2` at `69b48943fbe90549cbb3dcf5b73b12f73259ed1d`
- RimHUD source snapshot: `Jaxe-Dev/RimHUD` at `6f1dbfc3ba1196ab081ee4d382101ddd6b23badb`

Zenith participates in ordinary Pawn execution pipelines where practical.
There is no universal compatibility claim. Named content-mod patches are not
the default; supported framework adapters may use stable framework APIs.

## Authoring, geometry and release modes

A chassis body has one Torso corePart/root, one internal Core, one Cockpit and one
Head Assembly. It also contains the body-part groups used by installed/default
modules. ConfigErrors and creation validation reject missing/duplicate anchors.
This does not prescribe the rest of one exact debug anatomy.

A chassis authors visualFamily and portraitZoom. Overlay modules declare
allowedChassisFamilies; plain modules can be unrestricted or explicitly tagged.
Current Debug Helmet/Shoulder use debug-testframe. Paint initializes once from
chassis defaults; load/customization never reinitializes it.

The tab captures its actual GUI group bounds while drawing; native TabRect is
the fallback. Popups prefer right, then left, then reduce width when a viable
side still exists, and finally clamp when neither side can accommodate the
window. Effective UI coordinates account for UI scaling. Extreme effective
resolutions still cannot fit two complete windows; overlap can be unavoidable.
Portrait fit is chassis-authored; Debug keeps its existing 0.43 zoom.

Build.ps1 has explicit CoreOnly and WithVEF modes. CoreOnly removes the optional
load-folder claim. WithVEF requires the reference and verifies the resulting
adapter. PackageMode.txt and Test-Static validate that declared choice. Core has
no VEF assembly dependency. No external mod, test DLL, game DLL or Harmony DLL is
included in the package.

## Combat classification and art boundaries

The hostile fallback no longer sets humanlikesOnly=true. It uses normal hostility,
threat and auto-targetable semantics, including other Frame Pawns. Normal weapon
verbs/jobs/stances and unarmed melee remain physical Pawn execution paths.

Installed StunHandler behavior: non-flesh Frames receive EMP stun. Because they
are neither IsMechanoid nor IsDrone and have no CompStunnable adaptation override,
EMP adaptation does not apply. Repeated EMP can extend the stun interval and
sustain a lock. This anti-machine vulnerability is retained for the later Balance
milestone; no EMP numbers or race classification changed. NerveStun also considers
these non-mechanoid Pawns eligible; with Anomaly active its separate adaptation
branch treats non-mechanoid/non-drone Pawns as adaptable. Mechanitor shockwave
eligibility is different and does not apply to the Frame classification.

Visual scale 3.5 remains independent of Debug baseBodySize 1.5 and health scale 2.
It does not enlarge the Pawn's occupied grid, door traversal or melee adjacency.
Vanilla cover/LOS and mechanical target-size calculations keep their ordinary
Pawn inputs; the sprite can visually overhang those cells and cover. Targeting
readability and door clipping need human QA and later art/balance decisions.
No large-body pathing framework is introduced.

Native render nodes implement current torso/head/helmet/shoulder structural
visibility. Full independent arm/leg disappearance artwork is not implemented.
Debug module stats, overlapping mobility buffs and static addon power are
development fixtures, not final production balance.

## Canary classifications

| Canary | Classification | Evidence and boundary |
| --- | --- | --- |
| Current RunAndGun Continued | STRUCTURAL LIMITATION | Exact thingClass Pawn XML injection omits FramePawn comp; runtime otherwise uses ordinary Goto/Verb/stance/drafter APIs |
| Achtung | LIKELY NATURAL for drafted movement; STRUCTURAL LIMITATION for human work | Current Controller/Colonist use ordinary PawnCanGoto, drafting, jobs and stances; force-work remains deliberately forbidden; not live validated |
| VEF | Supported framework surface; runtime proof is required for a NATURAL claim | Exact pilot CompAbilities plus explicit physical Caster/job adapter; no Frame proxy |
| VPE through VEF | UNSUPPORTED while embarked for owner-dependent warmup | Stable WarmupToil gives only physical actor, VPE needs actor-owned CompAbilities; generated patch removed |
| RimHUD/native inspect family | LIKELY NATURAL for retained native tab flow | Current InspectPaneTabs calls tab.DoTabGUI and adjusts pane geometry; captured bounds follow actual drawing; not live validated with RimHUD |

The framework adapter cannot guarantee arbitrary custom Ability subclasses or
extensions which assume their logical owner is spawned. The transaction boundary
restores detached surviving Things after exceptions; it cannot resurrect an exact
Thing that arbitrary external code deliberately destroys. Old debug reconstruction
jobs without bound part metadata fail safely. Broad old-save compatibility is a
human regression task, not a reason to retain unsafe architecture.
