# MVP candidate validation

Executed 2026-09-27 on the user's installed RimWorld 1.6.4871 binary (runtime reports rev591; Version.txt reports rev590). Unity 2022.3.35f1, Windows, Direct3D 11. All game references were taken from that installation.

## Final results

| Layer | Result |
|---|---|
| Clean Release build | PASS, 0 warnings, 0 errors |
| Deterministic rebuild | PASS, identical DLL SHA-256 |
| Pure eligibility policy | PASS, 144 exhaustive boolean cases |
| XML, Core def references, package and source checks | PASS, 27 checks |
| Live Core-only suite | PASS, 138 assertions, 3 fresh processes, 2 save/load transitions |
| Live Core + all five installed expansions | PASS, 138 assertions, 3 fresh processes, 2 save/load transitions |
| Runtime log gates | PASS, no exceptions, config/XML/reference or asset-loading errors in the six final logs |

The 141 PASS lines per configuration include 138 assertions plus three phase-completion markers. Twenty board/disembark cycles are included per configuration; they are repetitions for regression coverage, not 20 distinct features. All six game processes exited with code 0.

Final profiles: `artifacts/final-core` and `artifacts/final-dlc`. The latter enabled Royalty, Ideology, Biotech, Anomaly and Odyssey. Both profiles used only official content, the production mod and the separate development harness. No existing user save or mod configuration was modified.

Tested production DLL SHA-256:

```text
8E509A617C803D089FFD97AD293F8758E7B5C2B877243CD38B8EFE20B82AFC43
```

Per-assertion reports are committed in `docs/test-results`. Full engine logs are included in the delivered validation evidence archive, not committed as game-generated output. A Steam API initialization message occurs when the disposable runtime runs outside Steam; offline game initialization and all tests succeeded. No claim is made about Steam/Workshop integration.

## Verified inside the real game

- Actual reservation/pathing/boarding job and single-pilot container ownership; no spawned cockpit pawn and no second object with the same pilot load ID.
- Null/self, foreign-faction, nonadjacent and occupied-cockpit rejection; no ownership mutation on rejection.
- Recursive map discovery of the unspawned pilot, faction coherence and no-pilot draft/move refusal.
- Deep-scribed pilot identity and relationship across process restarts; draft restoration with and without Biotech.
- Sole living player colonist embarked across longer than the normal game-over countdown.
- Real Goto job/pathfinder and real rifle projectile damage; single attack job completes, fire-at-will disabled.
- Normal disembark and 20 repeated transfer cycles per configuration.
- Real downing from removal of both mechanical legs, normal Kill, direct Destroy and off-map Destroy.
- Failed off-map disembark retains cockpit ownership; emergency rescue transfers ownership before destruction.
- Rescue owner survives another process restart and delivers the same pilot alive to the home map.

## Faults found and fixed during development

- Initial eligibility wrongly assumed spawned pawns had no ThingOwner; 1.6 uses map ownership.
- Vanilla removes/recreates the mech draft controller without Biotech; explicit saved draft intent fixes reload.
- Repeatedly setting FireAtWill=false cancels vanilla warmup even when already false; update only when true.
- Required Def metadata was caught by runtime validation and corrected.
- Harness fixes: do not combine autostart loading with quicktest; preserve native process handles; match FAIL only at line start; wait for arrival pods before arranging the arena; normalize expansion IDs and record known expansions to prevent duplicate activation; use deterministic leg-capacity loss instead of the randomized damage-until-downed helper.

## Audits and limits

No production references to Coventech, no reflection, no Harmony patches, no third-party runtime dependency, and no swallowed exceptions. The harness catches failures to write a report and exits nonzero. Historical project documentation mentions the old dependency only to explain its exclusion.

Legacy Test Mech.rar was not found locally and was not inspected. The supplied work-context document was read and preserved. No legacy implementation was copied.

Automated coverage does not validate human mouse interactions, final visual appearance or game feel. It does not establish compatibility with other mods or support for caravans, map abandonment, gravships, long-duration campaign balance, research or progression. The tested game version is the installed 1.6.4871 build only. Pilot needs/health/aging pause while stored. The manual procedure is in README.md.

The release package contains only About metadata, Frame defs and ZenithFrame.dll. The separate test mod and all proprietary binaries are excluded.
