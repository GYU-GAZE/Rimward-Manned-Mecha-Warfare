using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using VEF.Abilities;
using Verse;
using Verse.AI;
using VEFAbility = VEF.Abilities.Ability;

namespace ZenithFrame.VEFCompat
{
    [StaticConstructorOnStartup]
    public static class VEFBridgeBootstrap
    {
        static VEFBridgeBootstrap()
        {
            new Harmony("gyugaze.zenithframe.rimward.vef").PatchAll();
        }
    }

    [HarmonyPatch(typeof(FramePawn), nameof(FramePawn.GetGizmos))]
    internal static class Patch_FramePawn_GetGizmos_VEF
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, FramePawn __instance)
        {
            foreach (Gizmo value in values) yield return value;

            Pawn pilot = __instance.Cockpit?.Pilot;
            if (__instance.Faction != Faction.OfPlayer || !__instance.Spawned ||
                __instance.Cockpit?.CanTakePlayerOrders != true || pilot == null) yield break;

            CompAbilities comp = pilot.GetComp<CompAbilities>();
            if (comp == null) yield break;
            using (FrameAbilityContext.EnterTransient(pilot))
                foreach (Gizmo gizmo in comp.GetGizmos()) yield return gizmo;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.Caster), MethodType.Getter)]
    internal static class Patch_VEFAbility_Caster_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref Thing __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance?.pawn, out FramePawn frame))
                __result = frame;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.CasterPawn), MethodType.Getter)]
    internal static class Patch_VEFAbility_CasterPawn_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref Pawn __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance?.pawn, out FramePawn frame))
                __result = frame;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.DoAction))]
    internal static class Patch_VEFAbility_DoAction_EmbarkedPilot
    {
        private static void Prefix(VEFAbility __instance)
        {
            if (Event.current == null || Event.current.button != 1)
                FrameAbilityContext.BeginTargeting(__instance?.pawn);
        }
    }

    // VEF stores the cast on the owner's CompAbilities but its stock method
    // starts the job on owner.jobs. Preserve that state setup and start the
    // same configured job on the spawned operational Frame instead.
    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.StartAbilityJob), new[] { typeof(GlobalTargetInfo[]) })]
    internal static class Patch_VEFAbility_StartAbilityJob_EmbarkedPilot
    {
        private static bool Prefix(VEFAbility __instance, GlobalTargetInfo[] targets)
        {
            Pawn pilot = __instance?.pawn;
            if (!FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn frame)) return true;

            CompAbilities comp = pilot.GetComp<CompAbilities>();
            if (comp == null)
                throw new InvalidOperationException("ZENITH VEF: embarked pilot has no CompAbilities.");

            pilot.jobs?.EndCurrentJob(JobCondition.InterruptForced, false, true);
            comp.currentlyCasting = __instance;
            __instance.ModifyTargets(ref targets);
            comp.currentlyCastingTargets = targets;

            LocalTargetInfo executionTarget = LocalTargetInfo.Invalid;
            if (targets.Length != 0 && targets[0].IsMapTarget)
                executionTarget = ReferenceEquals(targets[0].Thing, pilot)
                    ? new LocalTargetInfo(frame)
                    : (LocalTargetInfo)targets[0];

            Job job = JobMaker.MakeJob(__instance.def.jobDef ?? VFE_DefOf_Abilities.VFEA_UseAbility,
                executionTarget);
            FrameAbilityContext.BeginCast(pilot);
            frame.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return false;
        }
    }

    // VEF and VPE warmup code ask the spawned toil actor for CompAbilities.
    // A Frame never owns that component; during this exact cast lookup return
    // the existing pilot component without adding or serializing a proxy.
    [HarmonyPatch]
    internal static class Patch_ThingWithComps_GetComp_VEFAbilityActor
    {
        private static System.Reflection.MethodBase TargetMethod() => AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.GetComp))
            .MakeGenericMethod(typeof(CompAbilities));

        private static void Postfix(ThingWithComps __instance, ref CompAbilities __result)
        {
            if (__result != null || !(__instance is FramePawn frame)) return;
            Pawn pilot = frame.Cockpit?.Pilot;
            if (!FrameAbilityContext.IsCasting(pilot) ||
                !FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn exactFrame) ||
                !ReferenceEquals(frame, exactFrame)) return;
            __result = pilot.GetComp<CompAbilities>();
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    internal static class Patch_VEFJobDriver_Cleanup_EmbarkedPilot
    {
        private static void Postfix(JobDriver __instance)
        {
            if (!(__instance is JobDriver_CastAbilityOnce) || !(__instance.GetActor() is FramePawn frame)) return;
            FrameAbilityContext.EndCast(frame.Cockpit?.Pilot);
        }
    }
}
