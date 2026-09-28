# MVP validation status

## Validated disabled-chassis recovery foundation

Branch:

```text
feat/disabled-frame-recovery
```

Runtime-tested implementation commit:

```text
f7a4696c75a111d9b800ca9d32553eae8dc291cf
```

Commit `c99a6a7` then narrowed an overbroad static-test expression without changing production code, runtime tests or the staged DLL. The narrowed 49-check static suite and the 144-case policy suite both passed after that change.

Executed 2026-09-28 on RimWorld 1.6.4871 (runtime rev591; `Version.txt` rev590), Unity 2022.3.35f1 on Windows, with Harmony 2.4.2.0 from Workshop item `2009463077`.

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML/Core def/package/source/recovery checks | PASS, 49 checks |
| Live Core-only suite | PASS, 209 assertions, 4 fresh processes, 3 save/load transitions |
| Live Core + Royalty, Ideology, Biotech, Anomaly and Odyssey | PASS, 209 assertions, 4 fresh processes, 3 save/load transitions |
| Runtime log gates | PASS, no exception/config/XML/reference/asset-loading errors |

Tested DLL SHA-256:

```text
89882F30DB1A97688E7CE54B45D6E785D3202CCE4089D08A31E48D9085A4FF10
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
