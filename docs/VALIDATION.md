# MVP validation status

## Current branch: pawn-first control refactor

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

**No fresh local RimWorld run has been performed on this branch yet.**

Therefore the branch is not considered validated until the following are rerun against the user's installed RimWorld 1.6 build:

```powershell
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -Clean
./scripts/Test-Static.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project Tests/Policy -c Release
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -WithInstalledExpansions
```

The live suite now expects Harmony to be installed. `Run-LiveTests.ps1` auto-detects Steam Workshop item `2009463077` or accepts `-HarmonyDir PATH`.

### New control regressions that must pass

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

The pre-existing ownership, save/load, downing, death, direct destruction and recovery assertions remain in the same live suite.

---

## Historical validated baseline

The results below apply to the earlier commit line ending at `0f2d9676` on `feat/mvp-pilot-lifecycle`.

They **do not** prove that the current Harmony/pawn-first branch passes.

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
