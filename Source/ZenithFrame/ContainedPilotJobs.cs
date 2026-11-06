using HarmonyLib;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    // Pawn.DoTick owns temporal simulation, including ordinary Pawn comps.
    // Its JobTracker also runs for unspawned non-world Pawns. Containment is
    // an explicit physical boundary: no independent pilot AI/job driver runs.
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.JobTrackerTick))]
    internal static class Patch_ContainedPilot_JobTick
    {
        private static bool Prefix(Pawn ___pawn) => !FrameIdentity.IsContainedPilot(___pawn);
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.JobTrackerTickInterval))]
    internal static class Patch_ContainedPilot_JobInterval
    {
        private static bool Prefix(Pawn ___pawn) => !FrameIdentity.IsContainedPilot(___pawn);
    }
}
