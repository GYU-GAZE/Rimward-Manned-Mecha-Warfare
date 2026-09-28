# Baseline and constraints

Inspected 2026-09-27: origin/main `cc34af14c69e03194a2ee390e0375b65f911d15a`, clean, README only. No repository AGENTS.md or referenced project documents existed. The user's supplied AGENTS instructions and `Zenith_Frame_Rimward_Work_Context.md` were read before implementation. The latter is preserved in PROJECT_CONTEXT.md.

Installed game: RimWorld **1.6.4871 rev590**, Steam build 23969874. Discovered from Steam libraryfolders.vdf and appmanifest_294100.acf, verified with Version.txt. Managed references come from the installed game's RimWorldWin64_Data/Managed directory; proprietary binaries are never committed or packaged.

Local SDK: 10.0.102; .NET Framework 4.7.2 targeting pack is installed. Runtime target is net472 (Unity Mono), not .NET 10. No production NuGet dependencies.

Legacy Test Mech.rar was not found in the available attachments, Downloads, Documents, Desktop or accessible G: files. No legacy code has been imported. Known legacy hazards from the supplied context remain test requirements.

Testing alternatives investigated: [Pickle](https://github.com/RimWorks/Rimworld-Pickle) provides live Gherkin integration and requires a development patching library; [Quickstarts](https://github.com/RimWorks/Rimworld-Quickstarts) provides scenario bootstrap and smoke verification. For this small vertical slice, use vanilla `-quicktest` plus a separate development-only GameComponent harness. This allows real ticking, real jobs, real serialization and a fresh-process reload without adding runtime patches or testing dependencies to the shipped mod. The production package and harness are built/staged separately.
