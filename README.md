# ZENITH FRAME: RIMWARD - Mecha Warfare

Experimental RimWorld 1.6 vertical MVP for pilotable humanoid **Frames / mecha**.

The current target is deliberately small: one debug Frame, one real colonist pilot, explicit pilot ownership, ordinary Pawn-style combat input, save/load, and safe pilot ejection. Placeholder vanilla lancer art is still used. There is no research, progression, chassis library or final loadout UI yet.

## Important terminology

A Zenith Frame is **not** a RimWorld Biotech controllable mech.

Frames do not use Mechanitors, Overseers, command range, bandwidth or mech control groups. The debug Frame is a custom non-organic Pawn with its own mechanical flesh type. A valid piloted Frame is bridged into ordinary drafted-Pawn orders.

## Runtime dependency

The mod now depends on **Harmony**:

- package ID: `brrainz.harmony`
- Steam Workshop item: `2009463077`

Harmony is a shared RimWorld mod dependency. ZENITH FRAME does **not** bundle its own `0Harmony.dll`.

Biotech is not required.

## Build and automated tests

Windows prerequisites:

- .NET SDK selected by `global.json`
- .NET Framework 4.7.2 targeting pack
- a local RimWorld 1.6 installation
- Harmony installed for live-runtime tests

From the repository root:

```powershell
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -Clean
./scripts/Test-Static.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project Tests/Policy -c Release
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -WithInstalledExpansions
```

`Run-LiveTests.ps1` auto-detects the Steam Workshop Harmony install in the same Steam library. A different install can be supplied with `-HarmonyDir PATH`.

The build produces `artifacts/ZenithFrame`. The release folder contains this mod's data and `ZenithFrame.dll`; it does not copy RimWorld assemblies, Harmony DLLs or the development test harness.

Live tests use a disposable runtime/profile under ignored `artifacts` paths. They do not modify the user's normal save folder or mod configuration.

## Current validation status

The original lifecycle candidate on `feat/mvp-pilot-lifecycle` passed its automated lifecycle suite and then passed the user's manual save/load/ejection checks, but its temporary custom Draft/Move/Fire control layer failed the intended game feel.

`fix/pawn-first-controls` replaces that temporary control layer. **This refactor still requires a fresh local build and live-runtime test pass before it is considered validated.** Historical results in `docs/VALIDATION.md` are not evidence that the new Harmony control bridge already passes.

## Manual MVP validation after the automated suite is green

Use a disposable colony with Harmony + ZENITH FRAME enabled and Dev Mode on.

1. Debug actions -> ZENITH FRAME -> Spawn debug Frame.
2. Select a colonist, right-click the Frame, choose **Board Frame**. The pawn should walk to it and embark.
3. Select the piloted Frame and press **R**. It should draft/undraft like an ordinary pawn.
4. While drafted, right-click clear ground. The Frame should move normally. There must be no Mechanitor/command-range message and no custom **Move** gizmo.
5. Right-click a hostile target. The Frame should use the normal ordered attack flow and keep attacking that chosen target beyond one burst until the job ends or another order interrupts it. There must be no custom **Fire rifle** gizmo.
6. Pilot two or more Frames, select them together, then issue one move order and one attack order. Every eligible selected Frame should receive the group command.
7. Save while pilots are embarked, return to the menu, load, and verify the same pilots remain inside.
8. Disembark and verify the pawns return normally.
9. Board again, down a Frame, and verify emergency ejection. Repeat with a fresh Frame using kill/destruction.

"Manual weapon" means the player chooses the target. It does not mean clicking fire again for every burst. Fire-at-will remains disabled, so the Frame should not independently acquire a new target.

See [architecture](docs/ARCHITECTURE.md), [baseline](docs/BASELINE.md), [project context](docs/PROJECT_CONTEXT.md) and [validation status](docs/VALIDATION.md).
