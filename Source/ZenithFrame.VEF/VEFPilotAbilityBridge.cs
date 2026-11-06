using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
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
            foreach (JobDef def in DefDatabase<JobDef>.AllDefsListForReading)
                if (typeof(JobDriver_CastAbilityOnce).IsAssignableFrom(def.driverClass))
                {
                    if (def.modExtensions == null) def.modExtensions = new List<DefModExtension>();
                    def.modExtensions.Add(new FrameJobExtension { capability = FrameJobCapability.Ability });
                }
            new Harmony("gyugaze.zenithframe.rimward.vef").PatchAll();
        }
    }

    public static class VEFFrameExecution
    {
        // The framework exposes a physical Caster separately from logical pawn.
        // Warmup extensions receive only Toil.actor; there is no separate owner
        // input. Such extensions cannot safely run on a Frame without opting
        // into an explicit API, so they are not silently executed.
        public static bool Supported(VEFAbility ability) => ability?.def != null &&
            ability.def.warmupMote == null && ability.def.warmupSound == null &&
            (ability.def.jobDef == null ||
             typeof(JobDriver_CastAbilityOnce).IsAssignableFrom(ability.def.jobDef.driverClass)) &&
            ability.AbilityModExtensions.All(extension =>
                extension.GetType().GetMethod(nameof(AbilityExtension_AbilityMod.WarmupToil))?.DeclaringType
                    == typeof(AbilityExtension_AbilityMod));

        public static bool Allowed(VEFAbility ability, out string reason)
        {
            reason = null;
            if (!FrameAbilityContext.TryGetContainingFrame(ability?.pawn, out FramePawn frame)) return true;
            if (!frame.Cockpit.CanExecuteActions) reason = "ZF_ActionUnavailable".Translate();
            else if (!Supported(ability)) reason = "ZF_VEFWarmupUnsupported".Translate();
            return reason == null;
        }

        public static void Bind(VEFAbility ability, FramePawn frame)
        { if (ability?.verb != null) ability.verb.caster = frame; }

        internal static Job Prepare(VEFAbility ability, FramePawn frame, GlobalTargetInfo[] targets)
        {
            CompAbilities comp = ability.pawn.GetComp<CompAbilities>();
            if (comp == null || !Allowed(ability, out _) || !ability.IsEnabledForPawn(out _)) return null;
            Bind(ability, frame);
            ability.ModifyTargets(ref targets);
            comp.currentlyCasting = ability;
            comp.currentlyCastingTargets = targets;
            LocalTargetInfo physicalTarget = frame;
            if (targets.Length != 0 && targets[0].IsMapTarget &&
                !ReferenceEquals(targets[0].Thing, ability.pawn)) physicalTarget = (LocalTargetInfo)targets[0];
            return JobMaker.MakeJob(ability.def.jobDef ?? VFE_DefOf_Abilities.VFEA_UseAbility, physicalTarget);
        }
    }

    [HarmonyPatch(typeof(FramePawn), nameof(FramePawn.GetGizmos))]
    internal static class Patch_FramePawn_GetGizmos_VEF
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> values, FramePawn __instance)
        {
            foreach (Gizmo value in values) yield return value;
            Pawn pilot = __instance.Cockpit?.Pilot;
            if (__instance.Cockpit?.CanTakePlayerOrders != true || pilot == null) yield break;
            CompAbilities comp = pilot.GetComp<CompAbilities>();
            if (comp == null) yield break;
            foreach (VEFAbility ability in comp.LearnedAbilities)
            {
                VEFFrameExecution.Bind(ability, __instance);
                if (ability.ShowGizmoOnPawn()) yield return ability.GetGizmo();
            }
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.ShowGizmoOnPawn))]
    internal static class Patch_VEFAbility_ShowGizmo_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref bool __result)
        {
            if (FrameAbilityContext.TryGetContainingFrame(__instance.pawn, out FramePawn frame))
                __result = frame.Cockpit.CanTakePlayerOrders && (frame.Drafted || __instance.def.showUndrafted) &&
                    __instance.AbilityModExtensions.All(extension => extension.ShowGizmoOnPawn(__instance.pawn));
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.IsEnabledForPawn))]
    internal static class Patch_VEFAbility_Enabled_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref bool __result, ref string reason)
        { if (!VEFFrameExecution.Allowed(__instance, out string blocked)) { __result = false; reason = blocked; } }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.Caster), MethodType.Getter)]
    internal static class Patch_VEFAbility_Caster_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref Thing __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance.pawn, out FramePawn frame)) __result = frame;
        }
    }
    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.CasterPawn), MethodType.Getter)]
    internal static class Patch_VEFAbility_CasterPawn_EmbarkedPilot
    {
        private static void Postfix(VEFAbility __instance, ref Pawn __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance.pawn, out FramePawn frame)) __result = frame;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.DoAction))]
    internal static class Patch_VEFAbility_DoAction_EmbarkedPilot
    {
        private static bool Prefix(VEFAbility __instance)
        {
            if (!VEFFrameExecution.Allowed(__instance, out _)) return false;
            FrameAbilityContext.BeginTargeting(__instance.pawn);
            return true;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.StartAbilityJob), new[] { typeof(GlobalTargetInfo[]) })]
    internal static class Patch_VEFAbility_StartAbilityJob_EmbarkedPilot
    {
        private static bool Prefix(VEFAbility __instance, GlobalTargetInfo[] targets)
        {
            if (!FrameAbilityContext.TryGetContainingFrame(__instance.pawn, out FramePawn frame)) return true;
            Job job = VEFFrameExecution.Prepare(__instance, frame, targets);
            if (job != null) frame.jobs.StartJob(job, JobCondition.InterruptForced);
            return false;
        }
    }

    [HarmonyPatch(typeof(VEFAbility), nameof(VEFAbility.Cast), new[] { typeof(GlobalTargetInfo[]) })]
    internal static class Patch_VEFAbility_Cast_ActionLock
    { private static bool Prefix(VEFAbility __instance) => VEFFrameExecution.Allowed(__instance, out _); }

    [HarmonyPatch(typeof(JobDriver_CastAbilityOnce), nameof(JobDriver_CastAbilityOnce.CompAbilities), MethodType.Getter)]
    internal static class Patch_VEFJobDriver_CompAbilities_EmbarkedPilot
    {
        private static void Postfix(JobDriver_CastAbilityOnce __instance, ref CompAbilities __result)
        {
            if (__result != null || !FrameIdentity.TryGet(__instance.GetActor(), out FramePawn frame)) return;
            Pawn pilot = frame.Cockpit?.Pilot;
            CompAbilities logical = pilot?.GetComp<CompAbilities>();
            if (logical?.currentlyCasting != null && VEFFrameExecution.Allowed(logical.currentlyCasting, out _))
                __result = logical;
        }
    }

    [HarmonyPatch(typeof(JobGiver_FramePilotAbility), "TryGiveJob")]
    internal static class Patch_FramePilotAbility_VEFHostileAI
    {
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null || !FrameIdentity.TryGet(pawn, out FramePawn frame) ||
                !frame.Cockpit.CanUseHostileCombatAI) return;
            CompAbilities comp = frame.Cockpit.Pilot?.GetComp<CompAbilities>();
            if (comp == null || comp.currentlyCasting != null) return;
            foreach (VEFAbility ability in comp.LearnedAbilities)
            {
                VEFFrameExecution.Bind(ability, frame);
                if (!ability.CanAutoCast || !VEFFrameExecution.Allowed(ability, out _) ||
                    !ability.IsEnabledForPawn(out _)) continue;
                IAttackTarget target = AttackTargetFinder.BestAttackTarget(frame,
                    TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                    candidate => ability.AICanUseOn(candidate) &&
                        ability.ValidateTarget(new LocalTargetInfo(candidate), false), 0f, ability.GetRangeForPawn());
                if (target?.Thing != null)
                {
                    __result = VEFFrameExecution.Prepare(ability, frame, new[] { new GlobalTargetInfo(target.Thing) });
                    if (__result != null) return;
                }
            }
        }
    }

    [HarmonyPatch(typeof(CompPilotableMech), nameof(CompPilotableMech.StopFrame))]
    internal static class Patch_Cockpit_StopFrame_VEFCleanup
    {
        private static void Postfix(CompPilotableMech __instance)
        {
            CompAbilities comp = __instance.Pilot?.GetComp<CompAbilities>();
            if (comp == null) return;
            comp.currentlyCasting = null;
            comp.currentlyCastingTargets = null;
            foreach (VEFAbility ability in comp.LearnedAbilities)
                if (ability.verb != null) ability.verb.caster = ability.pawn;
        }
    }
}

