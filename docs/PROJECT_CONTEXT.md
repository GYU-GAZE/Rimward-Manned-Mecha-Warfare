# ZENITH FRAME: RIMWARD — Mecha Warfare

## Project / Repository Identity

**Official project name:** `ZENITH FRAME: RIMWARD - Mecha Warfare`

**GitHub repository:** https://github.com/GYU-GAZE/Zenith-Frame---Rimward

**Repository:** `GYU-GAZE/Zenith-Frame---Rimward`

The GitHub repository is the canonical development repository for this mod. Use it for all implementation work, branches, commits, documentation and future pull requests.

The repository is currently at the beginning of development. Treat `main` as the stable baseline and perform implementation on dedicated feature branches.

The public-facing identity should be **ZENITH FRAME: RIMWARD - Mecha Warfare**. `Zenith Frame: Rimward` is the actual project/franchise title, while `Mecha Warfare` is the descriptive subtitle that communicates the mod's subject immediately. Do not call the project a framework in user-facing naming.

This is also **not intended to be marketed as a total RimWorld overhaul**. The mod adds a major new layer to warfare: humanoid mechs become common military assets used by factions across the planet, but ordinary RimWorld play remains valid. The player is not required to use mechs. Conventional infantry, dedicated anti-mecha squads, EMP weapons, anti-armor weapons, sabotage, hacking and other counterplay should remain viable approaches.

---

## 1. Project Goal

This project is a **RimWorld mod centered around humanoid mecha warfare and pilotable humanoid mechs**.

The intended fantasy is much closer to **Super Robot Wars Original Generations** than to a generic vehicle framework.

Reference points for the kind of design language intended:

- Gespenst
- Huckebein
- Wildraubtier
- Guarlion
- other humanoid military/mecha platforms with distinct combat roles and specializations

The goal is **not** to create a public generic vehicle framework.

The internal framework only needs to support this mod and its own content.

Do not design for third-party modders unless a future gameplay requirement explicitly demands it.

The long-term intention is for mechs to become a major new pillar of RimWorld combat without making them mandatory:

- player-controlled combatants frequently use humanoid mechs
- hostile factions can field pilots inside mechs
- enemy mechs can be disabled, recovered and potentially hacked
- different chassis fill different battlefield roles
- loadouts allow specialization
- pilots remain meaningful characters rather than being replaced by the machine
- a destroyed or disabled mech is a physical object/unit with strategic value
- infantry and non-mech combat should remain strategically relevant
- specialized anti-mecha squads should be able to threaten or defeat mechs with the right preparation
- the presence of mechs should change battlefield priorities without turning every non-mech pawn into obsolete filler

The mod should eventually make battles feel closer to a tactical mecha setting than normal RimWorld infantry combat.

Balance, progression, research, faction composition and large content variety are **future concerns**.

The first priority is proving the technical foundation.

---

## 2. Core Design Principle

The fundamental model should be:

```text
MECH = PAWN
PILOT = PAWN STORED INSIDE THE MECH
```

The mech should be a real RimWorld `Pawn`, not apparel worn by another pawn.

The pilot remains a separate real pawn and should be stored internally while embarked.

Conceptually:

```text
Pawn Mech
    |
    +-- Chassis identity / base stats
    |
    +-- CompPilotableMech
    |       |
    |       +-- Pilot Pawn
    |
    +-- CompMechLoadout
            |
            +-- Weapons
            +-- Utility / secondary equipment
            +-- Power source
```

The exact implementation may differ if RimWorld APIs make another representation cleaner, but this gameplay model should remain intact.

---

## 3. Chassis

The **chassis is the core identity of the mech**.

A chassis should define things that belong intrinsically to the machine itself, such as:

- structural durability / health
- armor
- movement speed
- body size
- base mass / carrying capability
- power requirements
- available loadout capacity / hardpoints
- inherent abilities if appropriate
- sprite / rendering identity
- chassis-specific restrictions or traits

The design should support multiple humanoid chassis with different battlefield identities.

Examples of future specialization:

- general-purpose frame
- scout / high-mobility frame
- heavy assault frame
- melee-focused frame
- artillery/support frame
- electronic warfare frame
- high-end prototype frame

Do **not** generalize the system for tanks, helicopters, cars, animals, ships, etc.

Those are outside the intended scope.

---

## 4. Add-ons / Loadout

The loadout system should stay intentionally small and readable.

Avoid Coventech-style slot explosion and giant UI layouts.

Do not create highly granular physical slots unless gameplay later proves they are necessary.

A simple logical model is preferable, for example:

```text
Weapon
Utility
Power
```

Potentially another category can be added later if there is a strong gameplay reason, but avoid speculative architecture.

A chassis may define capacities such as:

```text
Weapon slots: 2
Utility slots: 1
Power slots: 1
```

A heavier chassis may have more.

The purpose is to allow meaningful specialization without turning mech configuration into inventory-management hell.

---

## 5. Weapon Philosophy

Weapons should behave primarily like **weapons**, not autonomous turrets.

This is an important departure from Coventech.

Default expected interaction:

```text
select mech
choose weapon / attack command
choose target
mech fires
```

Where practical, use RimWorld's normal combat concepts such as:

- `Verb`
- `VerbProperties`
- projectiles
- range
- cooldown
- line of sight
- targeting
- armor penetration
- accuracy systems

The mech's weapons should feel like manually controlled pawn weapons.

### Automatic Fire

Automatic/turret-like behavior should **not** be the default weapon mechanic.

If desired later, it should be enabled through a specific add-on such as an autonomous fire-control system.

Example future concept:

```text
Utility Add-on:
Autonomous Fire Control System

Allows selected compatible weapons to independently acquire and fire on targets.
```

This turns autonomous fire into a build choice rather than a universal weapon rule.

---

## 6. Pilot Role

The mech and pilot must remain distinct entities.

The chassis should determine the physical capabilities of the machine.

The weapon should determine the physical capabilities of the weapon.

The pilot should influence how effectively the machine is used.

Possible future pilot influence:

- accuracy
- aim time
- melee effectiveness
- evasion
- ability cooldowns
- energy efficiency
- specialized piloting bonuses

Do not prematurely design a full custom pilot skill system.

Normal RimWorld skills can be bridged first where useful.

An excellent pilot in a mediocre chassis should still be meaningfully different from a poor pilot using the same chassis.

Likewise, capturing an enemy ace's mech should not automatically mean capturing the ace's combat proficiency.

---

## 7. Default Mech State

Normal mechs should require a pilot.

Default expectation:

```text
No pilot -> mech cannot operate normally
Pilot present -> mech can be controlled
```

Autonomous mechs or drone control can become a future specialization through explicit equipment or special chassis behavior.

Do not make autonomous operation part of the initial foundation.

---

## 8. Recovery, Disablement and Hacking

Battlefield recovery is intended to become an important part of the gameplay loop.

A hostile mech should not simply be equivalent to a normal corpse or dropped weapon.

Desired future loop:

```text
fight enemy mech
    ->
disable / down it
    ->
enemy pilot may be killed, incapacitated or ejected
    ->
mech remains on battlefield
    ->
player may attempt recovery / hacking
    ->
captured chassis can potentially be repaired and reused
```

This creates a meaningful distinction between:

- destroying a mech
- disabling a mech
- killing or incapacitating its pilot
- recovering the chassis

The previous mod already contains a hacking implementation that can be studied as behavioral reference.

---

## 9. The Attached Legacy `.rar`

A file named approximately:

```text
Test Mech.rar
```

will be attached to this Work task.

This archive is an **old vibecoded RimWorld mod/add-on** created earlier in the year with Gemini.

Its code quality is expected to be inconsistent.

Treat it as:

```text
BEHAVIORAL REFERENCE
EDGE-CASE DOCUMENTATION
OLD EXPERIMENTAL IMPLEMENTATION
```

Do **not** treat it as the architecture for the new project.

Do **not** blindly port or preserve its code structure.

### What the old project contains

The old project appears to include:

- hacking of hostile mechs
- emergency ejection when a mech dies or becomes downed
- protection against pilots/passengers disappearing when the mech dies
- pilot psycasts usable while inside a mech
- support for mech-owned abilities
- skill forwarding / inheritance from pilot to mech
- work around `Map` / `Position` assumptions for embarked pawns
- protection against game-over logic when the last living colonist is inside a mech
- protection against temporary-map cleanup while living pawns remain inside mechs
- mech repair behavior
- enemy mech pilot/passenger configuration
- a custom mech UI
- various Harmony patches and utility code

### Important legacy finding

The actual DLL packaged in the old mod corresponds to the older source project.

There is also another source directory/version in the archive that appears to be a later or abandoned experiment and was not the code actually shipped in the DLL.

When studying behavior, prioritize the source corresponding to the packaged DLL.

### Known ugly legacy patterns

The archive contains several hacks that should **not** automatically survive into the new implementation, such as:

- reflection-based discovery of internal Coventech containers
- global Harmony patches used to fake `Map` and `Position`
- targeting behavior discovered through overload/parameter inspection
- UI code with hardcoded physical slots
- text matching in float menus to identify Coventech actions
- swallowed exceptions
- code tightly coupled to Coventech internals
- potentially free repair behavior in the actually compiled version
- other vibecoded shortcuts

These are useful because they expose the edge cases we must account for.

They are not requirements for the new architecture.

---

## 10. Coventech Relationship

The new mod must be **independent of Coventech Mechsuit Framework**.

Do not make Coventech a dependency.

Do not reference Coventech assemblies in the new core.

Do not require Coventech XML definitions.

Do not design new code around Coventech classes such as its mech suit component, component tracker, boarding jobs, mech parts, or generation systems.

The previous archive depended heavily on Coventech and therefore contains calls to Coventech classes.

Those calls are evidence of desired behavior, not reusable architecture.

### Clean implementation rule

Implement the new system from scratch using RimWorld / Verse / Harmony APIs and the author's own gameplay requirements.

Do not copy Coventech internal source code into this project.

Studying observable behavior and using the author's own legacy add-on as reference is fine.

The new implementation should be structurally independent.

---

## 11. What the Old Add-on Already Taught Us

The old project already exposed several RimWorld edge cases that the new implementation must eventually handle.

Examples:

### Embarked pawn existence

A pawn stored inside another pawn may no longer be spawned on the map.

Some systems still need to understand that this pawn exists and is alive.

This affects things such as:

- game-over detection
- map cleanup
- abilities
- psycasts
- skill access
- faction logic
- world-pawn handling

### Mech death

The pilot must never silently disappear because the mech was destroyed.

Expected behavior:

```text
mech is downed or killed
    ->
pilot is safely removed from internal container
    ->
pilot is spawned/ejected onto the map when possible
    ->
appropriate damage/consequences may be applied
```

Passengers may be supported later only if actually needed.

The first version should focus on a single pilot.

### Psycasts / abilities

The old mod demonstrated that pawn abilities frequently assume:

- caster is spawned
- caster has a map
- caster has a position
- targeting begins from caster position

A proper bridge will be needed if embarked pilots are allowed to use abilities through the mech.

Do not begin with the most complex ability bridge.

This should come after the basic mech lifecycle is reliable.

### Enemy raids

Enemy pilots inside mechs introduce interactions with:

- faction
- AI
- Lords
- raid behavior
- death/downed state
- target selection
- map exit
- recovery

This should be implemented deliberately rather than assumed to work automatically.

---

## 12. Internal Components — Suggested Direction

Names are provisional.

A clean internal API could look conceptually like this:

```text
CompPilotableMech

Pilot
HasPilot

CanBePilotedBy(Pawn pawn)
TryBoard(Pawn pawn)
TryDisembark()
EmergencyEject()
```

And:

```text
CompMechLoadout

InstalledWeapons
InstalledUtilities
InstalledPowerSource

CanInstall(...)
Install(...)
Remove(...)
```

Avoid reflection where our own explicit API can solve the problem.

Because the framework only supports this mod, simplicity is more valuable than abstract extensibility.

---

## 13. Initial MVP

Do **not** start by recreating the full old mod.

Do **not** start with research trees, balance, many chassis, elaborate UI or dozens of weapons.

Create one ugly debug mech.

Example temporary concept:

```text
GYG_TestMech
```

Minimal required content:

- 1 humanoid mech chassis
- 1 pilot
- 1 manually controlled ranged weapon
- minimal boarding
- minimal disembarking
- minimal save/load support
- minimal downed/death ejection behavior

The first milestone must prove this loop:

```text
spawn test mech
    ->
pawn approaches mech
    ->
pawn boards as pilot
    ->
pilot is stored correctly
    ->
mech becomes player-controllable
    ->
mech can move
    ->
mech can manually attack using its weapon
    ->
save game
    ->
load game
    ->
pilot is still inside correctly
    ->
pilot can disembark correctly
    ->
board again
    ->
mech becomes downed
    ->
pilot is ejected correctly
    ->
repeat with mech death
    ->
pilot is not deleted or lost
```

If this works reliably, the architecture is considered proven enough to continue.

---

## 14. Recommended Development Order

After the MVP above, proceed incrementally.

Suggested order:

```text
1. Core mech pawn
2. Pilot container
3. Boarding
4. Disembarking
5. Draft/control behavior
6. One manual weapon
7. Save/load reliability
8. Downed/death emergency ejection
9. Recovery of disabled chassis
10. Hacking
11. Enemy pilots / hostile mechs
12. Game-over and map-retention edge cases
13. Pilot skill bridging
14. Mech abilities
15. Pilot abilities / psycasts
16. Loadout system
17. Power-source add-ons
18. Utility add-ons
19. Proper UI
20. Additional chassis/content
21. Progression/research
22. Balance
23. Faction-wide mech integration
```

Do not rigidly obey this order if implementation realities require small changes, but preserve the principle:

```text
prove lifecycle first
then gameplay systems
then content
then polish
```

---

## 14.5. Combined-Arms Principle

Mechs are intended to become common and dangerous military assets, but they must not be the only valid way to play.

The mod should support colonies that:

- invest heavily into their own mech forces
- field only a small number of elite Frames
- recover and reuse enemy machines
- specialize in disabling and hacking hostile Frames
- deliberately avoid using mechs and instead build dedicated anti-mecha infantry forces

Future anti-mecha options may include things such as:

- EMP / electronic disruption
- high-penetration weapons
- heavy anti-materiel weapons
- dedicated launchers
- sabotage
- hacking
- specialized close-range weapons
- targeting pilots or vulnerable systems where mechanically appropriate

These examples are future design space, not MVP requirements.

The important rule is:

```text
Mechs should reshape warfare.
They should not invalidate the rest of RimWorld warfare.
```

---

## 15. Explicit Non-Goals for Early Development

Do not implement these until the foundation is proven:

- public framework/API
- third-party mod support
- generic vehicle support
- large slot system
- giant mech customization UI
- passenger system unless needed by actual content
- complex heat simulation
- advanced power simulation
- custom pilot skill trees
- autonomous AI mechs
- automatic fire-control systems
- multiple chassis
- huge weapon catalog
- research balance
- faction balance
- Combat Extended compatibility
- multiplayer compatibility
- cross-mod compatibility matrix
- elaborate visual effects
- final art

Avoid speculative engineering.

If a system has no current gameplay requirement, leave it out.

---

## 16. UI Direction

The old custom UI may be studied to understand what information the author wanted visible.

Do not preserve its layout.

The old UI was considered too large and visually poor.

The new UI should prioritize clarity.

Likely useful information:

- chassis name
- pilot
- structural condition
- installed weapons
- utility equipment
- power source
- relevant mech stats
- embark/disembark controls
- later: recovery/hacking state

Avoid recreating Coventech's very large slot-heavy interface.

---

## 17. Source Control / Git Workflow

Use a dedicated feature branch.

Do not develop directly on `main`.

Before implementation:

- inspect repository state
- confirm RimWorld target version
- confirm required references
- establish a clean baseline
- create a dedicated feature branch

During implementation:

- create coherent commits at important milestones
- keep commits reviewable
- use descriptive commit messages
- avoid mixing unrelated refactors with feature work
- do not rewrite published history
- avoid giant one-shot implementation dumps

Suggested milestone commits might resemble:

```text
feat: add pilotable mech component and pilot container
feat: implement boarding and disembarking
feat: add manual mech weapon control
fix: preserve pilot state across save and load
feat: add emergency pilot ejection on mech disablement
```

The exact wording is not important.

The history should make major systems easy to inspect or revert.

---

## 18. Testing Philosophy

This project is being developed heavily through AI-assisted coding.

Therefore favor systems that can be tested with obvious outcomes.

Use RimWorld Dev Mode aggressively.

For every important feature, define a reproducible manual test.

Examples:

```text
TEST: Board mech
Expected:
- pilot leaves map
- pilot exists inside mech
- no duplicate pawn
- mech becomes controllable
```

```text
TEST: Save/load while boarded
Expected:
- pilot reference survives
- no duplicate pawn
- no world-pawn corruption
- mech remains controllable
```

```text
TEST: Kill mech
Expected:
- pilot is not destroyed with container
- pilot is spawned/ejected when possible
- save remains valid
```

Prefer one working vertical slice over large amounts of untested infrastructure.

---

## 19. Coding Philosophy

This project may be implemented primarily through vibecoding / AI-assisted development.

That is acceptable.

However, the codebase must be kept understandable enough that bugs can be isolated.

Priorities:

```text
explicit state
small components
minimal reflection
minimal global patches
clear ownership
clear lifecycle
few abstractions
reproducible tests
incremental commits
```

Avoid architecture created only because it "might be useful later."

Do not create generic factories, provider hierarchies, public APIs or extension systems unless a real mod feature needs them.

The framework is allowed to be opinionated because it belongs to one mod.

---

## 19.5. Zenith Frame RPG Heritage

The name **Zenith Frame** comes from an older original tabletop mecha RPG project by the author: **Zenith Frame RPG**.

The RimWorld mod does not need to reproduce that RPG's universe or mechanics literally, but it may deliberately reuse or reinterpret some of its terminology and design DNA.

Especially relevant legacy ideas include:

- humanoid machines called **Frames / Zenith Frames**
- clear separation between pilot identity and machine identity
- mass-production chassis alongside specialized / experimental machines
- progression through machine parts, equipment and recovered battlefield resources
- multiple technological / industrial philosophies rather than every chassis being a generic stat upgrade
- old lines such as **GENBU**, **OMEN** and **ANGEL**, which may later inspire technology grades, chassis families, research eras or other homage systems

Do not force these concepts into the MVP.

Treat them as a reservoir of naming, progression and setting ideas for later design work.

---

## 20. Long-Term Gameplay Vision

Once the foundation is mature, the mod should support a setting where humanoid mechs become central to faction warfare.

Possible future combat compositions:

```text
5 infantry
2 light humanoid mechs
1 command / specialist mech
```

or:

```text
several standardized general-purpose frames
1 high-end ace unit
support infantry
```

Different factions can eventually develop identities through:

- preferred chassis families
- different levels of mech technology
- standardized vs improvised loadouts
- specialist frames
- elite pilots
- recovered or stolen equipment
- autonomous systems where appropriate

A disabled enemy mech should create a tactical dilemma:

```text
destroy it now
or
risk recovering it later
```

The value of a battlefield mech wreck should be tangible.

This is one of the intended pillars of the mod.

---

## 21. Most Important Constraint

Do not attempt to build "the ultimate RimWorld mech framework."

Build the smallest stable system that supports **this specific humanoid-mech game**.

When choosing between:

```text
generic architecture that could support anything
```

and:

```text
simple architecture that cleanly supports the actual mod
```

prefer the second.

The intended end result is **ZENITH FRAME: RIMWARD - Mecha Warfare**: a RimWorld mod where humanoid mechs become a major part of planetary warfare through specialized chassis, modular loadouts, enemy mech pilots, battlefield recovery and meaningful anti-mecha counterplay.

Everything else is subordinate to making that loop reliable and fun.
