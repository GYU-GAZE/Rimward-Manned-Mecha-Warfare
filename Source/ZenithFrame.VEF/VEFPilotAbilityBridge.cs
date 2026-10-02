using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
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
            if (targets.Length != 0)
            {
                if (ReferenceEquals(targets[0].Thing, pilot))
                    executionTarget = new LocalTargetInfo(frame);
                else if (targets[0].IsMapTarget)
                    executionTarget = (LocalTargetInfo)targets[0];
            }

            Job job = JobMaker.MakeJob(__instance.def.jobDef ?? VFE_DefOf_Abilities.VFEA_UseAbility,
                executionTarget);
            FrameAbilityContext.BeginCast(pilot);
            frame.jobs.StartJob(job, JobCondition.InterruptForced, null, false, true);
            return false;
        }
    }

    // VEF's cast driver asks its spawned actor for CompAbilities. A Frame never
    // owns that component; during this exact driver lookup return the existing
    // pilot component without adding or serializing a proxy. Patching the
    // closed ThingWithComps.GetComp<T> generic is unsafe on Mono because its
    // native body can be shared by unrelated T instantiations.
    [HarmonyPatch(typeof(JobDriver_CastAbilityOnce), nameof(JobDriver_CastAbilityOnce.CompAbilities), MethodType.Getter)]
    internal static class Patch_VEFJobDriver_CompAbilities_EmbarkedPilot
    {
        private static void Postfix(JobDriver_CastAbilityOnce __instance, ref CompAbilities __result)
        {
            if (__result != null || !(__instance.GetActor() is FramePawn frame)) return;
            Pawn pilot = frame.Cockpit?.Pilot;
            if (!FrameAbilityContext.IsCasting(pilot) ||
                !FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn exactFrame) ||
                !ReferenceEquals(frame, exactFrame)) return;
            __result = pilot.GetComp<CompAbilities>();
        }
    }

    // VPE's cast-bubble pre-init lambda bypasses JobDriver_CastAbilityOnce's
    // CompAbilities property and asks toil.actor directly. Redirect only that
    // concrete call site to the pilot's existing component. This stays in the
    // optional VEF assembly and prepares only when VPE is actually loaded.
    [HarmonyPatch]
    internal static class Patch_VPEPsycastWarmup_CompAbilities
    {
        private static readonly MethodInfo GetComp = AccessTools.Method(typeof(ThingWithComps), nameof(ThingWithComps.GetComp))
            .MakeGenericMethod(typeof(CompAbilities));
        private static readonly MethodInfo ResolveComp = AccessTools.Method(typeof(Patch_VPEPsycastWarmup_CompAbilities), nameof(Resolve));

        private static bool Prepare() => AccessTools.TypeByName(
            "VanillaPsycastsExpanded.AbilityExtension_Psycast+<>c__DisplayClass19_0") != null;

        private static MethodBase TargetMethod() => AccessTools.Method(AccessTools.TypeByName(
            "VanillaPsycastsExpanded.AbilityExtension_Psycast+<>c__DisplayClass19_0"), "<WarmupToil>b__0");

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(GetComp))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = ResolveComp;
                }
                yield return instruction;
            }
        }

        private static CompAbilities Resolve(ThingWithComps actor)
        {
            CompAbilities direct = actor?.GetComp<CompAbilities>();
            if (direct != null || !(actor is FramePawn frame)) return direct;
            Pawn pilot = frame.Cockpit?.Pilot;
            if (!FrameAbilityContext.IsCasting(pilot) ||
                !FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn exactFrame) ||
                !ReferenceEquals(frame, exactFrame)) return null;
            return pilot.GetComp<CompAbilities>();
        }
    }

    // Extend the core hostile Frame ability node only when VEF is present.
    // Selection and validation stay on the pilot's original VEF ability; the
    // returned ordinary VEF job is executed by the spawned Frame.
    [HarmonyPatch(typeof(JobGiver_FramePilotAbility), "TryGiveJob")]
    internal static class Patch_FramePilotAbility_VEFHostileAI
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || !(pawn is FramePawn frame) || frame.Cockpit?.CanUseHostileCombatAI != true)
                return;
            Pawn pilot = frame.Cockpit.Pilot;
            CompAbilities comp = pilot?.GetComp<CompAbilities>();
            if (comp == null || comp.currentlyCasting != null) return;

            using (FrameAbilityContext.EnterTransient(pilot))
            {
                foreach (VEFAbility ability in comp.LearnedAbilities)
                {
                    if (!ability.CanAutoCast || !ability.IsEnabledForPawn(out _)) continue;

                    IAttackTarget attackTarget = AttackTargetFinder.BestAttackTarget(frame,
                        TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                        target => target is Pawn && ability.AICanUseOn(target) &&
                            ability.ValidateTarget(new LocalTargetInfo(target), false),
                        0f, ability.def.range);
                    if (attackTarget?.Thing == null) continue;

                    GlobalTargetInfo[] targets = { new GlobalTargetInfo(attackTarget.Thing) };
                    comp.currentlyCasting = ability;
                    ability.ModifyTargets(ref targets);
                    comp.currentlyCastingTargets = targets;
                    FrameAbilityContext.BeginCast(pilot);
                    __result = JobMaker.MakeJob(ability.def.jobDef ?? VFE_DefOf_Abilities.VFEA_UseAbility,
                        new LocalTargetInfo(attackTarget.Thing));
                    return;
                }
            }
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
