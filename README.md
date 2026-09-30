# ZENITH FRAME: RIMWARD - Mecha Warfare

Experimental RimWorld 1.6 vertical MVP for pilotable humanoid **Frames / mecha**.

The current target is deliberately small: one debug Frame, real player and hostile human pilots, explicit cockpit ownership, ordinary Pawn-style player input, vanilla hostile combat AI, save/load, safe pilot ejection, disabled-chassis hacking, and a Dev-only recovery proof. Placeholder vanilla lancer art is still used. There is no raid integration, final repair economy, research, progression, chassis library or final loadout UI yet.

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

`fix/pawn-first-controls` replaced that temporary control layer and passed the complete automated matrix. `feat/disabled-frame-recovery` proves that the same downed Frame identity persists through save/load, can be restored with a clearly development-only action, and reconnects to normal boarding and Pawn-first controls. `feat/disabled-frame-hacking` adds identity-preserving acquisition of hostile or factionless disabled Frames. `feat/hostile-frame-pilots` proves one deliberately spawned hostile human pilot inside the same cockpit owner, vanilla ranged/melee AI, save/load, downing/ejection, hacking, recovery and reuse by the player. Empty Frames remain inert regardless of faction. Exact evidence is recorded in `docs/VALIDATION.md`.

## Manual MVP validation after the automated suite is green

Use a disposable colony with Harmony + ZENITH FRAME enabled and Dev Mode on.

1. Debug actions -> ZENITH FRAME -> Spawn debug Frame.
2. Select a colonist, right-click the Frame, choose **Board Frame**. The pawn should walk to it and embark.
3. Select the piloted Frame and press **R**. It should draft/undraft like an ordinary pawn.
4. While drafted, right-click clear ground. The Frame should move normally. There must be no Mechanitor/command-range message and no custom **Move** gizmo.
5. Right-click a hostile target. The Frame should use the normal ordered attack flow and keep attacking that chosen target beyond one burst until the job ends or another order interrupts it. There must be no custom **Fire rifle** gizmo.
6. Press **B** while the Frame is drafted. The normal equipped-weapon targeting command should appear and fire the same rifle. Disembark and confirm the weapon command becomes unavailable.
7. Pilot two or more Frames, select them together, then issue one move order and one attack order. Every eligible selected Frame should receive the group command.
8. Save while pilots are embarked, return to the menu, load, and verify the same pilots remain inside.
9. Disembark and verify the pawns return normally.
10. Board again, down a Frame, and verify emergency ejection. Confirm the same chassis remains selectable, shows **Status: Disabled chassis (recoverable)**, refuses normal orders and cannot be boarded.
11. Select a colonist and right-click the player disabled Frame. **Rescue** and **Hack Frame** must not appear.
12. Down a hostile empty Frame. **Hack Frame** should appear while **Capture** and **Rescue** remain absent. Repeat with a factionless disabled Frame. Save and reload before hacking, then confirm the same chassis still has the action.
13. Issue **Hack Frame** from several cells away. The colonist should reserve it, walk into touch range and display work progress. Ownership must remain hostile until the work finishes.
14. After completion, confirm the same chassis belongs to the player but remains downed, empty, undrafted and inactive. Save and reload again and check the same identity.
15. With Dev Mode enabled, select it and use **DEV: Recover disabled chassis**. Confirm it becomes mobile but remains empty, undrafted and inactive.
16. Board the same recovered chassis again and repeat draft, movement, **B**, ranged right-click and forced-melee checks.
17. Verify **Hack Frame** is absent for operational hostile or factionless Frames, player-owned disabled Frames, neutral/friendly disabled Frames, pilot-containing Frames, dead Frames and destroyed Frames.
18. Place player-owned, hostile and factionless empty Frames beside adversaries and confirm they neither move nor attack or retaliate without a pilot.
19. Debug actions -> ZENITH FRAME -> **Spawn hostile piloted Frame**. Confirm it moves and attacks through normal hostile AI while refusing player orders.
20. Save and reload with the hostile pilot embarked. Down the Frame, verify that exact pilot ejects and the empty chassis immediately becomes inert, then run **Hack Frame** -> Dev recovery -> player boarding on the same chassis.

"Manual weapon" means the player chooses the target. It does not mean clicking fire again for every burst. A player Frame has no autonomous target-acquisition think branch; the internal firing gate is active only while its explicit player-forced attack job runs. Hostile target acquisition exists only while a valid hostile pilot operates the Frame.

See [architecture](docs/ARCHITECTURE.md), [baseline](docs/BASELINE.md), [project context](docs/PROJECT_CONTEXT.md) and [validation status](docs/VALIDATION.md).
