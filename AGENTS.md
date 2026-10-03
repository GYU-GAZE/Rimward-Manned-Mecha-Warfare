# AGENTS.md

## Project

ZENITH FRAME: RIMWARD - Mecha Warfare is a private RimWorld 1.6 mod about pilotable humanoid mecha.

Read `docs/PROJECT_CONTEXT.md` and `docs/ARCHITECTURE.md` before changing core behavior.

## Non-negotiable architecture

- `Frame / mecha = Pawn`
- pilot = separate Pawn explicitly stored inside the Frame
- chassis = machine identity and intrinsic properties
- add-ons = limited Weapon / Utility / Power customization
- normal Frames require a pilot
- no pilot means no Frame movement or combat, regardless of player, hostile or factionless ownership
- preserve explicit ownership and save/load safety
- a living pilot must have exactly one direct owner: map, spawned cockpit, world-Frame cockpit, direct world-pawn ownership or `PilotRecovery`
- map removal must detach nested pilots before vanilla recursively snapshots map pawns; never solve this by globally faking `Pawn.Map` or `Pawn.Position`
- pilot combat skills and experience remain owned by the cockpit pilot; Frames may consume the live skill contribution while operational but must never copy, own or serialize pilot skill progression
- Frame abilities belong to the Frame; pilot presence may enable operation, but ability ownership, effects and cooldowns remain machine state and must never be copied to the pilot
- pilot abilities and psycasts remain on the exact pilot; while embarked, the Frame may provide the spawned execution host but must never receive copied abilities, cooldowns, psychic resources, VEF `CompAbilities` or VPE state
- installed Frame add-ons are physical Things; the vanilla equipment tracker is the sole owner of the installed Weapon, and removal, downing, recovery, hacking, world transfer and destruction must preserve exact item identity without DefName replacement, duplicate ownership or silent fabrication
- loadout category eligibility is explicit through `FrameAddonExtension`; ordinary weapons and Utility/Power add-ons must never enter the Weapon slot merely because they have compatible RimWorld equipment metadata
- Utility and Power are physical Things in separate `CompFrameLoadout` ThingOwners; Weapon remains exclusively in `Pawn_EquipmentTracker`, and all three owners must participate in Verse ownership traversal without duplicate serialization
- addon power is a static derived budget: Power provides capacity, Utility consumes it, and insufficient power disables only the Utility effect; core Frame operation, Frame abilities and pilot abilities do not require or consume addon power
- Utility active state is never serialized; derive it from the exact installed Utility, exact installed Power and their `FrameAddonExtension` metadata

### Frames are NOT Biotech mechs

A Zenith Frame is not a Mechanitor-controlled RimWorld mechanoid.

Do not base normal Frame control on:

- Mechanitors
- Overseers
- command range
- bandwidth
- mech control groups
- `IsColonyMech` semantics

If vanilla routes Frames into those systems, fix/bridge the ordinary Pawn path instead.

A valid piloted Frame should use ordinary drafted-Pawn interaction wherever practical:

- pilot right-clicks Frame to board
- R drafts/undrafts
- right-click ground moves
- right-click hostile orders attack
- ordered attack continues against the chosen target
- multi-selected Frames accept group orders

Do not recreate basic Pawn controls as custom Move/Fire chess-piece buttons.

"Manual weapon" means the player selects the target. It does not mean one burst per click.

## Harmony

Harmony (`brrainz.harmony`) is an accepted runtime dependency when a narrow patch is needed to bridge Frames into vanilla Pawn behavior.

Keep the patch surface minimal and conditional on `FramePawn`.

Do not globally make Frames pretend to be colonists, player-controlled subhumans or Biotech colony mechs.

Avoid reflection unless there is no reasonable stable API.

## Clean-room rule

Do not depend on or copy Coventech internals.

Legacy Coventech-based code is behavioral/edge-case reference only.

## Scope

Do not implement future systems while fixing the MVP.

Before recovery/hacking/enemy Frames, the current vertical slice must pass:

- boarding/disembarking
- normal drafting
- normal right-click move/attack
- sustained ordered fire
- group move/attack
- save/load
- no pilot duplication/loss
- down/death ejection
- no Biotech command-range behavior

## Git

Never implement directly on `main`.

Use dedicated feature/fix branches. Do not force-push or rewrite published history. Do not merge to `main` unless explicitly requested.

Preserve working lifecycle code while changing the control layer.

## Tests

Compilation is not completion.

Run, where available:

```powershell
./scripts/Build.ps1 -RimWorldDir PATH -Clean
./scripts/Test-Static.ps1 -RimWorldDir PATH
dotnet run --project Tests/Policy -c Release
./scripts/Run-LiveTests.ps1 -RimWorldDir PATH
./scripts/Run-LiveTests.ps1 -RimWorldDir PATH -WithInstalledExpansions
./scripts/Run-AbilityTests.ps1 -RimWorldDir PATH
./scripts/Run-AbilityTests.ps1 -RimWorldDir PATH -WithInstalledExpansions
./scripts/Run-AbilityTests.ps1 -RimWorldDir PATH -WithInstalledExpansions -VEFDir VEF_PATH
./scripts/Run-AbilityTests.ps1 -RimWorldDir PATH -VEFDir VEF_PATH -VPEDir VPE_PATH
```

A manually discovered bug should become an automated regression test when practical.

Do not claim the current branch is validated until its own build/live suite has actually run. Historical results from `feat/mvp-pilot-lifecycle` do not validate later control/Harmony changes.
