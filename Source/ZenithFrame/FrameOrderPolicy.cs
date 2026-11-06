using System;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    public interface IFrameOrderProvider { }
    public enum FrameJobCapability { None, Movement, Combat, Carry, Ability, PilotCare }
    public sealed class FrameJobExtension : DefModExtension
    { public FrameJobCapability capability; }

    public static class FrameOrderPolicy
    {
        private static readonly Func<FloatMenuOptionProvider, bool> Drafted =
            AccessTools.MethodDelegate<Func<FloatMenuOptionProvider, bool>>(
                AccessTools.PropertyGetter(typeof(FloatMenuOptionProvider), "Drafted"));
        private static readonly Func<FloatMenuOptionProvider, bool> Undrafted =
            AccessTools.MethodDelegate<Func<FloatMenuOptionProvider, bool>>(
                AccessTools.PropertyGetter(typeof(FloatMenuOptionProvider), "Undrafted"));

        // The 1.6 provider API has no declared resulting JobDef. Generic
        // drafted-only providers are accepted, with job authority below.
        // Undrafted actions need an explicit machine capability contract.
        public static bool ProviderAllowed(FloatMenuOptionProvider provider) =>
            provider is IFrameOrderProvider || (Drafted(provider) && !Undrafted(provider)) ||
            provider is FloatMenuOptionProvider_RescuePawn || provider is FloatMenuOptionProvider_CapturePawn;

        public static bool JobAllowed(FramePawn frame, Job job)
        {
            if (job?.def == null) return false;
            JobDef def = job.def;
            if (def == ZenithJobDefOf.ZF_FrameIdleJob || def == JobDefOf.Wait_Downed ||
                def == JobDefOf.Wait_MaintainPosture) return true;
            if (frame.Cockpit?.CanExecuteActions != true) return false;
            if (job.ability != null)
            {
                Pawn owner = job.ability.pawn;
                return (ReferenceEquals(owner, frame) || ReferenceEquals(owner, frame.Cockpit.Pilot)) &&
                    FramePilotAbilitySupport.ExecutionAllowed(job.ability).Accepted;
            }
            FrameJobCapability declared = def.GetModExtension<FrameJobExtension>()?.capability ?? FrameJobCapability.None;
            if (declared != FrameJobCapability.None) return true;
            Type driver = def.driverClass;
            return def == JobDefOf.Wait_Combat || def == JobDefOf.Rescue || def == JobDefOf.Capture ||
                def == JobDefOf.HaulToCell || def == JobDefOf.HaulToContainer ||
                def == ZenithJobDefOf.ZF_FramePilotSelfTend ||
                typeof(JobDriver_Goto).IsAssignableFrom(driver) ||
                typeof(JobDriver_AttackStatic).IsAssignableFrom(driver) ||
                typeof(JobDriver_AttackMelee).IsAssignableFrom(driver);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    internal static class Patch_FrameOrderedJob_Authority
    {
        private static bool Prefix(Pawn ___pawn, Job job, ref bool __result)
        {
            if (!FrameIdentity.TryGet(___pawn, out FramePawn frame) || FrameOrderPolicy.JobAllowed(frame, job)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_FrameStartJob_Authority
    {
        private static bool Prefix(Pawn ___pawn, Job newJob)
        {
            if (FrameIdentity.IsContainedPilot(___pawn)) return false;
            return !FrameIdentity.TryGet(___pawn, out FramePawn frame) || FrameOrderPolicy.JobAllowed(frame, newJob);
        }
    }

    [HarmonyPatch(typeof(Verb), nameof(Verb.TryStartCastOn),
        new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) })]
    internal static class Patch_FrameVerb_Authority
    {
        // Reject inactive machine execution before ordinary action-changing
        // prefixes consume a stopped job/stance or start their own cast.
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Verb __instance, ref bool __result)
        {
            if (!FrameIdentity.TryGet(__instance.CasterPawn, out FramePawn frame) || frame.Cockpit.CanExecuteActions) return true;
            __result = false;
            return false;
        }
    }
}

