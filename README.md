# ZENITH FRAME: RIMWARD - Mecha Warfare

Experimental RimWorld 1.6 mod for pilotable humanoid Frames. The map actor is a
real Pawn; the exact real pilot is physically held by the cockpit. Physical
modules, equipment, health, jobs and native rendering provide the machine.

The current content is development proof: Debug Frame, weapons, Reactor/Weak
Cell, Helmet, Shoulder System, Mobility Booster, Shield, Beam Sword and Overdrive.
The Reactor is a static addon-capacity fixture, not final core power simulation.
Production chassis, research, general faction integration and balance are later
milestones.

Frames do not use Mechanitors, bandwidth, Overseers or command range. Harmony
is the core runtime dependency (brrainz.harmony); it is not bundled. DLC is
optional. VEF support is a separate explicit framework package mode.

## Build and implementation checks

```powershell
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -PackageMode CoreOnly -Clean
./scripts/Test-Static.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld'
dotnet run --project Tests/Policy -c Release
# Explicit optional framework package, using locally installed VEF
./scripts/Build.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -PackageMode WithVEF -Clean
```

The package is under artifacts/ZenithFrame. CoreOnly contains no adapter and makes
no adapter load-folder claim. WithVEF requires its reference and output DLL.
Neither mode includes game assemblies, Harmony, test assemblies or external mods.

## Completed-candidate live acceptance

Run once after the implementation is complete. A second launch is allowed only
after a concrete failure and fix. This uses a disposable runtime/profile and
does not modify normal saves or the user's active mod list.

```powershell
./scripts/Run-LiveTests.ps1 -RimWorldDir 'G:/SteamLibrary/steamapps/common/RimWorld' -WithVEF -WithInstalledTrackers -RunName 'architecture-unique-name'
```

WithInstalledTrackers loads installed Royalty/Biotech in that same process.
WithVEF loads the installed framework and its test probe in that same process.
Optional CanaryDir loads an available external canary without modifying Zenith.
Full/Smoke/phase/save-load/DLC/ability matrices are retired from the active loop.

See [VALIDATION.md](docs/VALIDATION.md) for current coverage and historical evidence.
Current results must come from the current candidate, not old matrix records.

## Compatibility architecture and boundaries

See [ARCHITECTURE.md](docs/ARCHITECTURE.md) and
[COMPATIBILITY_CORE.md](docs/COMPATIBILITY_CORE.md). Frame-native execution and
pilot-owned state are the contracts. Ordinary Pawn APIs are preferred, but exact
thingClass XML injection and humanlike-only mods can still exclude Frames.

The retained subclass preserves local lifecycle safety. The contained pilot
ticks through ordinary Pawn temporal simulation while independent jobs are
suppressed. No pilot spatial/control getter is faked. The VEF adapter contains
no VPE generated-lambda patch; owner-dependent warmup extensions are explicitly
unsupported while embarked.

## Human QA after installation

Use a disposable colony and the user's actual mod list.

- Board/disembark, draft, group movement, sustained ranged orders and unarmed melee.
- Long-duration embarked health, needs, gene/component state and ability cooldowns.
- Save/reload while embarked and after moving a cooldown-bearing module.
- Down/death ejection, off-map removal and exact pilot/item identity.
- Queued, blocked, active and cancelled module service; reconstruction target/cost.
- Frame-vs-Frame combat and desired movement/order canaries.
- Native Frame tab, paint controls, side popups and portrait fit with UI scaling.
