# ZENITH FRAME: RIMWARD - Mecha Warfare

Experimental RimWorld 1.6 vertical MVP: one real humanoid mech Pawn, one real colonist pilot, explicit pilot ownership, manual movement/fire and safe ejection. Placeholder vanilla lancer art; no research or progression.

## Build and test

Windows prerequisites: .NET SDK 10 (global.json), .NET Framework 4.7.2 targeting pack and a local RimWorld 1.6 installation. No proprietary binaries or testing dependencies are distributed.

```powershell
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -Clean
./scripts/Test-Static.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project Tests/Policy -c Release
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
# Optional: repeat with all locally installed official expansions
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -WithInstalledExpansions
```

The build produces `artifacts/ZenithFrame`. Copy that folder into RimWorld's Mods directory and enable **ZENITH FRAME: RIMWARD - Mecha Warfare**. Core is sufficient; no Biotech, Harmony or third-party framework is required.

Live tests copy the game runtime and Core data into ignored `artifacts/runtime`, use an isolated save-data directory and start/stop only their own game processes. The development harness is a separate mod that is never included in the release package. Logs, save files and per-phase assertion reports remain in the disposable profile. Do not run two suites concurrently against the shared staged runtime. Graphical device initialization is required: `-nographics` breaks vanilla texture initialization on this build; tests run hidden with `-batchmode` and Direct3D.

## In-game validation

Use a disposable colony with the mod enabled and Dev Mode on.

1. Debug actions → ZENITH FRAME → Spawn debug Frame. Click a clear cell near a colonist.
2. Select the Frame → Board Frame → choose the colonist. Verify the colonist walks over, disappears from the ground and appears in the Frame's pilot inspection text.
3. Draft Frame → Move → select a reachable cell. Fire rifle → choose a target within 30 cells and line of sight. One command fires one burst; issue another command to fire again.
4. Save while embarked, return to the menu and load. Verify the same pilot is aboard and the Frame remains controllable. Disembark and verify the pilot returns.
5. Board again and disable the Frame (the damage-until-downed debug action may require repeated attempts on a mech; loss of both mechanical legs is deterministic). Verify the pilot is ejected alive. Repeat on a fresh Frame with the kill debug action.

Human validation is primarily visual: selection, menu usability, sprite, aiming feedback and whether movement/fire feel correct. Automated tests exercise the actual jobs, combat, ownership, down/death and fresh-process save/load.

See [architecture](docs/ARCHITECTURE.md), [baseline](docs/BASELINE.md), [project context](docs/PROJECT_CONTEXT.md) and [validation results](docs/VALIDATION.md).
