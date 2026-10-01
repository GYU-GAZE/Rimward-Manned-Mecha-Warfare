# MVP validation status

## Validated machine-owned Frame ability lifecycle

Branch: `feat/frame-abilities`

Implementation HEAD: `979db9abc1747e10e772bc2ef5e533aa3d9e7734`

Feature commits: `b222b6a`, `28c283d`, `b48e9b3`, `66d9212`, `b56ea50`, `b73333b`, `25d55c4`, `979db9a`

Executed 2026-10-01 on RimWorld 1.6.4871 rev591 with rev590 reference assemblies, Harmony Workshop 2.4.2.0 metadata / 2.4.1.0 runtime assembly and .NET SDK 10.0.102.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive cases |
| XML/static/package/source/lifecycle checks | PASS, 109 checks |
| Live Core + Harmony | PASS, 543 assertions, 20 fresh processes, 19 save/load transitions |
| Live Core + Harmony + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 543 assertions, 20 fresh processes, 19 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors in either retained final run |

Final clean Release DLL SHA-256:

```text
359ED870708C4CF3A7EE839E19BCB7E889B52A20473F9C87C16FE2882DE0C0A8
```

The 20 phase reports from each final run are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

`ZF_DebugFrameOverdrive` is owned and cast by the Frame Pawn through the normal Core `Ability`, `Pawn_AbilityTracker`, `Command_Ability`, casting job, cooldown and hediff systems. The contained pilot only enables `Cockpit.CanOperate`; neither the ability, its 300-tick effect nor its 900-tick cooldown is copied to either tested pilot.

Fresh-process coverage preserves one exact ability instance, remaining cooldown and chassis effect through load, disembark, a second pilot, downing and recovery. The same ability follows a hostile chassis through ejection, hacking, recovery, player boarding and another load. Vanilla hostile combat AI naturally casts the self-buff, begins the machine cooldown and continues ordinary combat without a custom ability AI layer. The development values and presentation remain provisional.

### Frame ability manual QA checklist

1. Spawn one debug Frame and select it while empty; confirm Overdrive cannot be used
2. Board a player colonist
3. Draft the Frame with R
4. Confirm the ordinary Frame Overdrive ability command appears once
5. Record the Frame's normal MoveSpeed
6. Activate Frame Overdrive through its ability command
7. Confirm the chassis MoveSpeed increases
8. Confirm the ability enters cooldown
9. Attempt immediate reuse and confirm vanilla cooldown blocks it
10. Save while the effect and cooldown are active
11. Reload and confirm the same Frame and pilot retain the remaining machine state
12. Disembark before cooldown ends
13. Confirm the pilot owns no Overdrive ability or hediff
14. Board a second pilot into the same chassis
15. Confirm the same machine cooldown remains and the second pilot receives no copy
16. Wait for cooldown expiry and cast again
17. Down the Frame and confirm pilot ejection plus cast rejection
18. Recover the same chassis and confirm its one intrinsic ability and cooldown remain
19. Hack and recover a hostile Frame; confirm the captured chassis retains Overdrive
20. Spawn a hostile piloted Frame and confirm it naturally uses Overdrive during combat

Pilot-owned abilities and psycasts were not started. They remain the next canonical milestone.

## Validated pilot combat skill bridge

Branch: `feat/pilot-skill-bridging`

Implementation commits: `2da1981`, `1dc3b4b`, `f967c80`

Executed 2026-10-01 on RimWorld 1.6.4871 rev591 with rev590 reference assemblies and Harmony 2.4.2.0

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive cases |
| XML/static/package/source/lifecycle checks | PASS, 95 checks |
| Live Core + Harmony | PASS, 512 assertions, 16 fresh processes, 15 save/load transitions |
| Live Core + Harmony + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 512 assertions, 16 fresh processes, 15 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors in either final run |

Final clean Release DLL SHA-256:

```text
5DF0DCAA1224B9F62F0808923B65C4375E955548299C185B764E25BBCC00B910
```

The 16 phase reports from each uninterrupted final run are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

The live suite proves that Shooting 0/10/20 and Melee 0/10/20 produce the exact vanilla `SkillNeed` contributions through the same Frame and equipment; low/high/low pilot swaps restore the original value without chassis state; and normal stat explanations identify the live pilot contribution. Fresh-process save/load preserves Frame identity, pilot identity, levels, XP and effective stats.

Real player manual fire, Fire At Will, forced melee, hostile ranged AI and hostile melee AI award vanilla-formula experience to the exact contained pilot. The Frame has no `SkillTracker`, receives no duplicate experience and stores no copied skill state. Ejection removes the contribution immediately; recovery leaves the chassis empty; a different pilot defines both stats as soon as they board.

### Pilot skill manual QA checklist

1. Spawn one debug Frame and choose a low-Shooting colonist
2. Inspect `ShootingAccuracyPawn`, board the pilot and fire real ranged attacks
3. Confirm the pilot gains Shooting XP
4. Disembark and board a high-Shooting pilot into the same Frame
5. Confirm ranged accuracy rises without replacing the chassis or weapon
6. Repeat the swap with visibly different Melee levels and inspect `MeleeHitChance`
7. Perform a real forced melee attack and confirm the pilot gains Melee XP
8. Save and reload with the high-skill pilot embarked; confirm both values and XP persist
9. Down the Frame; confirm ejection removes the pilot contribution and leaves the chassis inert
10. Recover the chassis and board another pilot; confirm the new pilot immediately defines both values
11. Spawn the hostile piloted Frame or development mixed raid and confirm hostile combat uses and trains its exact contained pilot

Frame abilities were not started. They are the next canonical milestone after automated and human QA of this branch.

## Validated Frame map and world lifecycle

Branch: `feat/frame-map-world-lifecycle`

Implementation commit: `2fbe60e`

Runtime ownership-audit commit: `6a935da`

Executed 2026-10-01 on RimWorld 1.6.4871 rev591 with Harmony 2.4.2.0

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive cases |
| XML/static/package/source/lifecycle checks | PASS, 87 checks |
| Live Core + Harmony | PASS, 445 assertions, 14 fresh processes, 13 save/load transitions |
| Live Core + Harmony + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 445 assertions, 14 fresh processes, 13 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Final clean Release DLL SHA-256:

```text
30FC19156E1618BFDF6CBFE9A44E7F9DA258782F862B84A521845BFE4E5BFFBB
```

The 14 phase reports from each final uninterrupted run are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

The expanded suite proves:

- the last living colonist remains valid while exclusively owned by a spawned Frame cockpit and across a fresh-process load
- a hostile Frame retreat produces one world-owned Frame containing one exact pilot, with no second direct owner
- deliberate vanilla garbage collection of that hostile world Frame discards the nested hostile pilot without a dangling cockpit or world entry
- a player Frame exiting with a free humanlike player pilot is force-kept as a world pawn and survives a fresh-process load with exact cockpit ownership
- legitimate off-map destruction of that player Frame transfers the exact pilot atomically into `PilotRecovery`, which survives another fresh-process load
- vanilla-generated secondary-map removal sends a player pilot to recovery and its empty chassis to world ownership
- the same cleanup sends a hostile Frame and detached pilot into separate world-pawn ownership
- downing/ejection immediately before map shutdown leaves the pilot and disabled chassis as separate valid world pawns
- retained player, recovered or hacked chassis lose stale hostile Lord state during cleanup
- every audited pilot appears exactly once among spawned maps, spawned cockpit owners, world-Frame cockpit owners, direct world pawns and `PilotRecovery`

The automated abandonment fixture exercises the real cleanup core through a legitimate settlement map and `Game.DeinitAndRemoveMap`. It does not automate the user-facing abandonment confirmation dialog. General caravan gameplay, gravship integration and transport-pod UI remain outside this milestone.

## Validated development enemy Frame raid lifecycle

Branch:

```text
feat/enemy-frame-raid-lifecycle
```

Validation environment:

- RimWorld `1.6.4871 rev591`
- .NET SDK `8.0.414`
- Harmony Workshop item `2009463077`
- Core-only runtime
- Core plus all installed official expansions: Royalty, Ideology, Biotech, Anomaly and Odyssey

Automated gates:

| Gate | Result |
|---|---|
| Clean Release build against RimWorld `1.6.4871 rev590` reference assemblies | PASS |
| XML/Core def/package/source/recovery/hacking/hostile-AI/raid-Lord checks | PASS, 79 checks |
| Exhaustive pure boarding and operation policy combinations | PASS, 144 cases |
| Core-only live lifecycle, eleven fresh processes and ten save transitions | PASS, 397 assertions |
| All-installed-expansions live lifecycle, eleven fresh processes and ten save transitions | PASS, 398 assertions |

The eleven phase reports for each configuration are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

Final clean Release DLL SHA-256:

```text
9684E8F88D4A86FBBCE2DDD89D9906131C7BB925580B95A2DE04BB8A940A001F
```

The live suite proves:

- the zero-chance Dev incident is a real `IncidentWorker_RaidEnemy` path using vanilla `EdgeWalkIn`, `ImmediateAttack` and `RaidStrategyWorker.MakeLords`
- one ordinary infantry raider and one occupied hostile Frame share a vanilla `LordJob_AssaultColony`; the contained pilot is absent from direct Lord membership
- a fresh-process active-raid load restores the exact Frame, pilot, infantry, cockpit ownership and assault Lord membership
- the Lord-owned occupied Frame performs real hostile combat
- downing ejects the exact pilot, removes the empty chassis from Lord control and adds the pilot coherently to the same raid Lord
- the empty disabled Frame stays inert and cannot retreat itself
- hacking removes stale hostile Lord control, converts the same Frame and feeds that chassis through recovery, boarding and normal player reuse
- a second real mixed raid enters `LordJob_ExitMapBest`, moves the operational Frame through the vanilla exit-map job and saves mid-retreat
- a fresh-process retreat load safely exits the Frame through world-pawn handling while retaining exclusive ownership of the exact living pilot
- all earlier boarding, group orders, combat, save/load, ejection, off-map recovery, disabled recovery, hostile/factionless hacking, hostile AI and inertness assertions remain green

This path deliberately does not alter storyteller raid selection, pawn-group makers or general faction composition. The next milestone is map/world/cleanup edge cases around contained pilots and Frames.

## Validated single hostile-pilot Frame lifecycle

Branch:

```text
feat/hostile-frame-pilots
```

Runtime-tested implementation commit:

```text
26c05218a758b285c3b2b002a56064f16dc3825a
```

Executed 2026-09-30 on RimWorld 1.6.4871 (runtime rev591; `Version.txt` rev590), Unity 2022.3.35f1 on Windows, with Harmony 2.4.2.0 from Workshop item `2009463077`.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source/recovery/hacking/hostile-AI checks | PASS, 71 checks |
| Live Core-only suite | PASS, 356 assertions, 8 fresh processes, 7 save/load transitions |
| Live Core + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 356 assertions, 8 fresh processes, 7 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Final clean Release/deployed DLL SHA-256 (the live runner rebuilds the same commit into its disposable runtime):

```text
9CDC8EE7136C46BFD3F7E2EDE15FD094FE54F7519FFCD23C1C504EF31B92C26A
```

The eight final phase reports for each configuration are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

### Hostile-pilot behavior proven

- the debug action creates one hostile Frame and one real same-faction human pilot, then transfers that exact Pawn into the existing cockpit `ThingOwner`
- no duplicate pilot remains spawned or independently world-owned; exact Frame and pilot identities survive a fresh-process save/load
- faction mismatch is rejected without consuming the candidate pilot
- `CanOperate`, `CanTakePlayerOrders` and `CanUseHostileCombatAI` separate machine validity, player input and enemy AI eligibility
- the hostile operational Frame rejects player orders, drafting and hacking
- vanilla `JobGiver_AIFightEnemies` acquires a real player target, ordinary pathing closes weapon range and the AI-only rifle verb causes real projectile damage
- dropping the rifle makes the same piloted hostile Frame choose vanilla `AttackMelee` and cause real melee damage
- deterministic downing ejects the exact hostile pilot safely, leaves the same hostile Frame empty and recoverably disabled, and enables the existing hacking job
- the ejected empty hostile chassis remains stationary, runs only the inert Frame idle job and causes zero retaliatory melee damage for 360 ticks beside a player Pawn
- the real timed Hack Frame job converts that same chassis without recovering or boarding it
- Dev recovery preserves identity, the original player pilot boards, **R** drafting and **B** return, and ordinary right-click ranged and forced melee both cause real damage
- player manual fire and hostile AI fire use separately gated verbs; the player order binds its explicit manual verb while the primary execution verb is limited to hostile AI or that exact player-forced job
- all prior boarding, group control, save/load, ejection, Rescue/Capture exclusion, recovery, hostile hacking, factionless hacking and empty-Frame inertness regressions remain green

### Scope retained

This milestone deliberately spawns one development fixture. It does not add raids, faction composition, enemy Frame frequency, Lord integration, retreat/map exit, generated loadouts, multiple chassis or ace pilots. The next milestone is enemy Frame raid and faction integration.

---

## Validated disabled Frame hacking and empty-Frame inertness

Branch:

```text
feat/disabled-frame-hacking
```

Runtime-tested implementation commit:

```text
6931efe4e0d7766869524b595c41ceed96766be5
```

Executed 2026-09-30 on RimWorld 1.6.4871 (runtime rev591; `Version.txt` rev590), Unity 2022.3.35f1 on Windows, with Harmony 2.4.2.0 from Workshop item `2009463077`.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source/recovery/hacking checks | PASS, 63 checks |
| Live Core-only suite | PASS, 304 assertions, 7 fresh processes, 6 save/load transitions |
| Live Core + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 304 assertions, 7 fresh processes, 6 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Tested DLL SHA-256:

```text
C6353687BD67BE20233BB7B6E29DC9293046E9549356636A4822F877FA7FE9FA
```

The seven current phase reports for each configuration are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

### Hacking behavior proven

- **Hack Frame** appears through the normal right-click menu only for an eligible player colonist targeting a spawned, living, empty, downed hostile or factionless Frame
- operational hostile/factionless, player-owned disabled, neutral/friendly disabled, pilot-containing, dead and destroyed Frames are rejected
- the real `ZF_HackFrame` job reserves the target, paths into touch range, runs a visible 180-tick work delay and continuously fails if eligibility is lost
- ownership changes only when work completes, using `FramePawn.SetFaction(Faction.OfPlayer)` on the existing object
- the exact `GetUniqueLoadID()` survives a fresh-process save/load before hacking and a second fresh-process save/load after hacking
- faction conversion leaves the Frame downed, empty, undrafted and unable to take orders
- the existing recovery component restores mobility without replacing the Frame
- the existing boarding job accepts the same pilot after recovery
- the recovered hacked Frame regains ordinary draft, vanilla **B** / `Command_VerbTarget`, right-click ranged attack and forced melee behavior
- vanilla Capture, Rescue and autonomous rescue remain unavailable throughout the hostile disabled state
- a factionless disabled Frame survives a fresh-process save/load before the real hacking job, preserves its load ID and remains disabled after conversion
- player-owned, hostile and factionless empty Frames remain stationary and cause zero damage through 360 ticks beside adjacent adversaries
- the dedicated `ZF_FrameIdleJob` remains active during those observations, and the melee boundary blocks pilotless retaliation outside the idle driver

### Hacking manual checklist

- down a hostile or factionless empty Frame and confirm **Hack Frame** appears while **Capture** and **Rescue** do not
- issue **Hack Frame** from several cells away and confirm the colonist walks into touch range and displays work progress
- interrupt the colonist or invalidate the target during work and confirm ownership does not change
- complete the job and confirm the same chassis remains downed, empty and unusable but now belongs to the player
- save and reload both before and after hacking, checking that no duplicate Frame appears
- use the existing Dev recovery action, board a pilot, draft with **R**, target with **B**, attack by right-click and force melee
- place empty player-owned, hostile and factionless Frames beside enemies and confirm they remain stationary and never attack or retaliate

The 180-tick duration, lack of skill/stat scaling, absence of material cost and plain **Hack Frame** presentation are provisional MVP tuning. The identity-preserving faction conversion, strict eligibility, job reservation/pathing/revalidation and post-hack disabled state are durable lifecycle decisions.

---

## Validated disabled-chassis recovery foundation and control cleanup

Branch:

```text
feat/disabled-frame-recovery
```

Runtime-tested implementation commit:

```text
acbb9325b3c1ee32371aa15c0b9f951c144d512a
```

Executed 2026-09-29 on RimWorld 1.6.4871 (runtime rev591; `Version.txt` rev590), Unity 2022.3.35f1 on Windows, with Harmony 2.4.2.0 from Workshop item `2009463077`.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source/recovery checks | PASS, 51 checks |
| Live Core-only suite | PASS, 219 assertions, 4 fresh processes, 3 save/load transitions |
| Live Core + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 219 assertions, 4 fresh processes, 3 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Tested DLL SHA-256:

```text
AE685B5B6EA5BEB68C8310D12E04231DE820DA2700B3CF5934FAA24EBCB348BB
```

The four current phase reports for each configuration are retained in `docs/test-results/core` and `docs/test-results/installed-expansions`.

### Recovery behavior proven

- recoverable disablement is derived from the existing Frame Pawn being spawned, downed, alive, not destroyed and empty; no second saved disabled flag exists
- loss of both moving-limb-core parts ejects the exact pilot and leaves the same Frame on the map
- the disabled chassis is selectable, inspectable, rejects draft/move/ranged/melee/boarding orders and shows a concise recoverable status
- a save made while disabled reloads the same `GetUniqueLoadID()` and the same scribed Frame reference in a fresh process
- the Dev Mode-only recovery action repairs one missing part or injury at a time and stops when RimWorld's real health state becomes mobile
- unrelated remaining chassis loss is retained rather than silently full-healed
- the existing debug rifle dropped by vanilla incapacity is transferred back from the immediate recovery site; no replacement rifle or Frame is created
- recovery leaves the Frame empty, undrafted and unable to take orders until the same pilot boards through the existing job
- after reboarding, ordinary movement, ranged attack and forced melee all work again
- ordinary applied damage can also create a recoverable downed state, and a second recovery cycle preserves identity without duplicate pilot state
- truly dead or destroyed disabled Frames are rejected by the recovery API
- the same behavior passes with Biotech present without Mechanitor, Overseer, bandwidth, command-range or control-group semantics
- disabled player and hostile Frames are excluded from vanilla Rescue/Capture eligibility, autonomous `RescueDowned` work and their right-click options
- the equipped rifle exposes vanilla `Command_VerbTarget` on `Misc1` / `B` while piloted and drafted; the old custom Fire gizmo remains absent
- disembarking immediately makes the same weapon verb unavailable

### Recovery regression and manual checklist

- down a player Frame and confirm the pilot ejects, no **Rescue** option appears and no colonist starts rescue work
- down a hostile empty Frame and confirm neither **Capture** nor **Rescue** appears
- board and draft a recovered Frame, press **B**, select a target and confirm the normal weapon command fires the existing equipped rifle
- disembark and confirm **B** can no longer fire the Frame weapon
- save and reload once while disabled, then recover and reboard the exact same chassis

During the all-expansions run, random DLC pawn generation attached an off-map ex-spouse to a disposable test colonist. The fixture now clears random generated relationships before its first save. The repeated four-process DLC run completed without unresolved references or other gated log errors.

This is a technical lifecycle proof only. The command is explicitly development-only. No material cost, repair work, recovery bay, towing, hostile capture, hacking or faction conversion is implemented.

---

## Validated pawn-first control baseline

Branch:

```text
fix/pawn-first-controls
```

The lifecycle foundation inherited from `feat/mvp-pilot-lifecycle` was previously validated, but the control layer has since changed materially:

- Frame classification changed from vanilla `Mechanoid` flesh to custom `ZF_FrameMechanical`
- Harmony became a runtime dependency
- the temporary custom Move and Fire gizmos were removed
- pilot boarding moved into the normal pilot right-click flow
- Frames now enter the ordinary Pawn right-click order pipeline through narrow Harmony bridges
- ordered ranged attacks are sustained rather than capped to one burst
- automated tests now cover ordinary right-click controls and group orders

Validated source commit:

```text
74c2cf4c71a80cc691571c2051deeb25675a6b01
```

Executed 2026-09-28 on the user's installed RimWorld 1.6.4871 binary (runtime reports rev591; `Version.txt` reports rev590), Unity 2022.3.35f1 on Windows. Harmony 2.4.2.0 was loaded from Steam Workshop item `2009463077`.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source checks | PASS, 44 checks |
| Live Core-only suite | PASS, 161 assertions, 3 fresh processes, 2 save/load transitions |
| Live Core + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 161 assertions, 3 fresh processes, 2 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Tested DLL SHA-256:

```text
97870E2275D21B803C3A015CA6C50B301CC80C86F7FBBD9D53C63E93ECBDFF0A
```

The clean build was repeated after both live runs and produced the hash above. The tracked phase reports are in `docs/test-results/core` and `docs/test-results/installed-expansions`.

Reproduction commands:

```powershell
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -Clean
./scripts/Test-Static.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project Tests/Policy -c Release
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -WithInstalledExpansions
```

The live suite now expects Harmony to be installed. `Run-LiveTests.ps1` auto-detects Steam Workshop item `2009463077` or accepts `-HarmonyDir PATH`.

### Control regressions covered

The revised runtime harness checks, among other things:

- `FramePawn.RaceProps.IsMechanoid == false`
- a piloted Frame can enter `FloatMenuContext` through `CanTakeOrder`
- the Frame still does **not** report itself as vanilla `IsPlayerControlled` or `IsColonyMech`
- selecting a human pilot and right-clicking the Frame offers **Board Frame**
- the normal draft hotkey binding is present
- the temporary custom **Move** / **Fire rifle** gizmos are absent
- enabling Frame orders does not expose colonist equip/pickup/work actions
- ordinary right-click movement is available with no command-range rejection
- a valid melee attack alternative exists through normal `AttackMelee`
- when ranged and melee are both valid, hostile right-click still defaults to ranged using vanilla attack-option priority
- the vanilla melee attack gizmo is available when explicitly forcing melee
- the ranged attack remains active beyond the first burst
- one multiselect movement order reaches two piloted Frames
- both Frames physically move from that group order
- one multiselect attack order gives both Frames an `AttackStatic` job
- the group-issued attack causes real projectile damage
- unpiloted/disembarked Frames lose ordinary order eligibility

The pre-existing ownership, save/load, downing, death, direct destruction and pilot-recovery assertions pass in the same live suite.

---

## Historical validated baseline

The results below apply to the earlier commit line ending at `0f2d9676` on `feat/mvp-pilot-lifecycle`.

They do not replace or extend the current validation above.

Executed 2026-09-27 on the user's installed RimWorld 1.6.4871 binary (runtime reports rev591; Version.txt reports rev590), Unity 2022.3.35f1 on Windows.

| Layer | Historical result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Deterministic rebuild | PASS, identical DLL SHA-256 |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source checks | PASS, 27 checks |
| Live Core-only suite | PASS, 138 assertions, 3 fresh processes, 2 save/load transitions |
| Live Core + five installed expansions | PASS, 138 assertions, 3 fresh processes, 2 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Historical tested DLL SHA-256:

```text
8E509A617C803D089FFD97AD293F8758E7B5C2B877243CD38B8EFE20B82AFC43
```

That hash belongs to the old control candidate and must not be reported as the current branch binary.

### What the historical runtime suite established

The earlier suite established the lifecycle foundation:

- real reservation/pathing/boarding job
- exactly one pilot object with explicit cockpit ownership
- rejection of null/self/foreign/nonadjacent/second-pilot boarding without pawn loss
- deep-scribed pilot identity across fresh-process save/load
- embarked last colonist surviving the vanilla game-over countdown
- real pathfinding and real projectile damage
- repeated board/disembark ownership transfers
- deterministic downing by loss of both mechanical moving limbs
- ejection on down, kill and direct destroy
- off-map destruction transfer to `PilotRecovery`
- recovery-owner save/load and eventual delivery of the same pilot

The user's manual test independently confirmed:

- three simultaneous embarked pilots remained present
- save/load preserved all three
- mass disembark restored all three
- killing a Frame ejected its pilot alive
- an unpiloted Frame could not be operated

The same manual test rejected the old control UX because it required custom Draft/Move/Fire buttons, triggered Biotech command-range behavior on normal movement, limited fire to one burst and failed expected group attack behavior. Those failures are the reason the current branch exists.

## Limits

Even after automated tests pass, human validation is still required for:

- actual keyboard/mouse feel
- selection behavior in the real UI
- visual aiming/rendering feedback
- whether grouped control feels natural rather than merely functioning
- unexpected menu clutter not represented by automated fixtures

Compatibility with other mods, caravans, map abandonment, gravships, long campaigns, research/progression and balance remain outside the MVP.

Legacy `Test Mech.rar` was not available to the original Work runtime and was not used to implement the validated baseline.
## Validated raid/control manual-QA fixes

Branch: `feat/enemy-frame-raid-lifecycle`

Executed 2026-10-01 on RimWorld 1.6.4871 rev591 with Harmony 2.4.2.0

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive cases |
| XML/static/package/source checks | PASS, 82 checks |
| Live Core + Harmony | PASS, 403 assertions, 11 fresh processes, 10 save/load transitions |
| Live Core + Harmony + all installed official expansions | PASS, 403 assertions, 11 fresh processes, 10 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Final clean Release DLL SHA-256:

```text
97568BD25B0473012C156EE123355D3FD67C56301FF28A8CD331C83C9B7A7FA7
```

The regression suite now proves that an `AssaultColony` Frame consumes its real Lord duty, naturally moves from the raid edge and damages a colony target without test-assigned jobs, target injection or direct AI calls. It also proves Fire At Will on/off behavior, explicit attack preservation, cockpit-based colonist-bar selection and the Frame-specific `LabelShort` overlay while retaining every earlier lifecycle assertion.
