using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    // Logical state never leaves the owner. Only the existing Verb and Job use
    // the physical machine. No Pawn property is projected or globally faked.
    public static class FrameAbilityContext
    {
        private static Pawn targetingPilot;

        public static bool TryGetContainingFrame(Pawn pilot, out FramePawn frame)
        {
            frame = null;
            if (!FrameIdentity.IsContainedPilot(pilot)) return false;
            frame = ((CompPilotableMech)pilot.holdingOwner.Owner).Frame as FramePawn;
            return frame != null;
        }

        public static bool TryGetOperationalFrame(Pawn pilot, out FramePawn frame) =>
            TryGetContainingFrame(pilot, out frame) && frame.Cockpit.CanExecuteActions;

        public static bool BindVanillaAbility(Ability ability)
        {
            if (ability?.verb == null || !TryGetOperationalFrame(ability.pawn, out FramePawn frame)) return false;
            ability.verb.caster = frame;
            return true;
        }

        public static void BeginTargeting(Pawn pilot)
        {
            if (TryGetOperationalFrame(pilot, out _)) targetingPilot = pilot;
        }

        public static void EndTargeting() => targetingPilot = null;
        public static bool IsTargeting(Pawn pilot) => ReferenceEquals(targetingPilot, pilot);
        public static bool IsCasting(Pawn pilot) => TryGetContainingFrame(pilot, out FramePawn frame) &&
            ReferenceEquals(frame.CurJob?.ability?.pawn, pilot);

        public static void Cancel(Pawn pilot)
        {
            if (pilot == null) return;
            if (pilot.abilities != null)
                foreach (Ability ability in pilot.abilities.AllAbilitiesForReading)
                    if (ability?.verb != null && FrameIdentity.IsFrame(ability.verb.CasterPawn))
                        ability.verb.caster = pilot;
            if (ReferenceEquals(targetingPilot, pilot))
            {
                targetingPilot = null;
                if (Find.Targeter?.IsTargeting == true) Find.Targeter.StopTargeting();
            }
        }

        // Spatial presentation inside the two stable Psycast methods only.
        // Ordinary callers of pilot.Map still observe null.
        public static Map ExecutionMap(Thing owner) =>
            owner is Pawn pilot && TryGetContainingFrame(pilot, out FramePawn frame) ? frame.Map : owner.Map;
    }

    public static class FramePilotAbilitySupport
    {
        public static bool Supported(Ability ability) => ability?.def != null &&
            !ability.def.targetWorldCell && ability.def.warmupMote == null &&
            ability.def.WarmupMoteSocialSymbol == null && ability.def.emittedFleck == null &&
            !(ability.verb is Verb_CastAbilityJump) && !(ability.verb is Verb_CastAbilityConsumeLeap);

        public static AcceptanceReport ExecutionAllowed(Ability ability)
        {
            Pawn owner = ability?.pawn;
            if (FrameAbilityContext.TryGetContainingFrame(owner, out FramePawn frame))
            {
                if (!frame.Cockpit.CanExecuteActions) return "ZF_ActionUnavailable".Translate();
                if (!Supported(ability)) return "ZF_AbilityUnsupportedEmbarked".Translate();
            }
            else if (FrameIdentity.IsFrame(owner) && FrameIdentity.Cockpit(owner).CanExecuteActions != true)
                return "ZF_ActionUnavailable".Translate();
            return true;
        }

        internal static IEnumerable<LocalTargetInfo> AffectedTargets(Ability ability, FramePawn frame, LocalTargetInfo target)
        {
            if (!ability.def.HasAreaOfEffect || !ability.def.canUseAoeToGetTargets)
            { yield return target; yield break; }
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(target.Cell, frame.Map,
                ability.def.EffectRadius, true))
                if (ValidAffected(ability, thing)) yield return thing;
        }

        internal static bool ValidAffected(Ability ability, Thing thing) =>
            ability.verb.targetParams.CanTarget(thing) && !thing.Fogged() &&
            ability.EffectComps.All(effect => effect.Valid(new LocalTargetInfo(thing), false));
    }

    internal static class PilotAbilityGizmos
    {
        internal static IEnumerable<Gizmo> VanillaFor(FramePawn frame)
        {
            Pawn pilot = frame.Cockpit?.Pilot;
            if (pilot?.abilities == null || !frame.Cockpit.CanTakePlayerOrders) yield break;
            foreach (Ability ability in pilot.abilities.AllAbilitiesForReading)
            {
                FrameAbilityContext.BindVanillaAbility(ability);
                if ((!frame.Drafted && !ability.def.displayGizmoWhileUndrafted) ||
                    (frame.Drafted && !ability.def.showWhenDrafted) || !ability.GizmosVisible()) continue;
                foreach (Command command in ability.GetGizmos()) yield return command;
                foreach (Gizmo extra in ability.GetGizmosExtra()) yield return extra;
            }
            if (pilot.HasPsylink && pilot.psychicEntropy != null) yield return pilot.psychicEntropy.GetGizmo();
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.ConstantCaster), MethodType.Getter)]
    internal static class Patch_Ability_ConstantCaster_EmbarkedPilot
    {
        private static void Postfix(Ability __instance, ref Thing __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance?.pawn, out FramePawn frame)) __result = frame;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast), MethodType.Getter)]
    internal static class Patch_Ability_CanCast_FrameExecution
    {
        private static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            AcceptanceReport allowed = FramePilotAbilitySupport.ExecutionAllowed(__instance);
            if (!allowed.Accepted) __result = allowed;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmoDisabled))]
    internal static class Patch_Ability_GizmoDisabled_EmbarkedPilot
    {
        private static bool Prefix(Ability __instance, ref bool __result, out string reason)
        {
            reason = null;
            if (!FrameAbilityContext.TryGetContainingFrame(__instance.pawn, out FramePawn frame)) return true;
            AcceptanceReport allowed = FramePilotAbilitySupport.ExecutionAllowed(__instance);
            if (!allowed.Accepted) reason = allowed.Reason;
            else if (!__instance.CanCast.Accepted) reason = __instance.CanCast.Reason ?? "ZF_ActionUnavailable".Translate();
            else if (__instance.EffectComps.Any(effect => effect.GizmoDisabled(out _)))
            {
                foreach (CompAbilityEffect effect in __instance.EffectComps)
                    if (effect.GizmoDisabled(out reason)) break;
            }
            else if (!frame.Drafted && __instance.def.disableGizmoWhileUndrafted)
                reason = "AbilityDisabledUndrafted".Translate();
            else if (__instance.pawn.Downed || __instance.pawn.Deathresting || __instance.pawn.DevelopmentalStage.Baby())
                reason = "ZF_ActionUnavailable".Translate();
            else if (__instance.def.casterMustBeCapableOfViolence && __instance.pawn.WorkTagIsDisabled(WorkTags.Violent))
                reason = "IsIncapableOfViolence".Translate(__instance.pawn.LabelShort, __instance.pawn);
            else if (frame.jobs.AllJobs().Any(job => job.ability == __instance))
                reason = "AbilityAlreadyQueued".Translate();
            else
            {
                AcceptanceReport lord = frame.Cockpit.OperationalLord?.AbilityAllowed(__instance) ?? true;
                if (!lord.Accepted) reason = lord.Reason;
            }
            __result = reason != null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    internal static class Patch_PawnJobTracker_TryTakeOrderedJob_PilotAbility
    {
        private static bool Prefix(Pawn ___pawn, Job job, JobTag? tag, bool requestQueueing, ref bool __result)
        {
            if (!FrameAbilityContext.TryGetContainingFrame(___pawn, out FramePawn frame)) return true;
            __result = false;
            if (job?.ability?.pawn == ___pawn && FramePilotAbilitySupport.ExecutionAllowed(job.ability).Accepted &&
                FrameAbilityContext.BindVanillaAbility(job.ability))
                __result = frame.jobs.TryTakeOrderedJob(job, tag, requestQueueing);
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetJob))]
    internal static class Patch_Ability_GetJob_EmbarkedSelfTarget
    {
        private static void Postfix(Ability __instance, LocalTargetInfo target, Job __result)
        {
            if (__result == null || !FrameAbilityContext.TryGetOperationalFrame(__instance.pawn, out FramePawn frame)) return;
            FrameAbilityContext.BindVanillaAbility(__instance);
            if (ReferenceEquals(target.Thing, __instance.pawn))
            { __result.targetA = frame; __result.targetC = __instance.pawn; }
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.Activate), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    internal static class Patch_Ability_Activate_FrameExecution
    {
        private static bool Prefix(Ability __instance, ref LocalTargetInfo target, ref bool __result)
        {
            if (!FramePilotAbilitySupport.ExecutionAllowed(__instance).Accepted) { __result = false; return false; }
            if (FrameAbilityContext.TryGetOperationalFrame(__instance.pawn, out FramePawn frame) &&
                ReferenceEquals(target.Thing, frame) &&
                (!__instance.def.targetRequired || (frame.CurJob?.ability == __instance &&
                 ReferenceEquals(frame.CurJob.targetC.Thing, __instance.pawn)))) target = __instance.pawn;
            return true;
        }
    }

    [HarmonyPatch(typeof(Verb_CastAbility), nameof(Verb_CastAbility.ValidateTarget))]
    internal static class Patch_VerbCastAbility_ValidateTarget_PhysicalFrame
    {
        private static bool Prefix(Verb_CastAbility __instance, LocalTargetInfo target, bool showMessages, ref bool __result)
        {
            Ability ability = __instance.ability;
            if (!FrameAbilityContext.TryGetContainingFrame(ability?.pawn, out FramePawn frame)) return true;
            __result = false;
            if (!FramePilotAbilitySupport.ExecutionAllowed(ability).Accepted) return false;
            FrameAbilityContext.BindVanillaAbility(ability);
            bool self = ReferenceEquals(target.Thing, ability.pawn) && !ability.def.targetRequired;
            if (!self && (__instance.EffectiveRange > 0f ? !__instance.CanHitTarget(target) :
                !frame.CanReach(target, PathEndMode.Touch, frame.NormalMaxDanger()))) return false;
            __result = __instance.IsApplicableTo(target, showMessages) &&
                ability.EffectComps.All(effect => effect.Valid(target, showMessages));
            return false;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetAffectedTargets))]
    internal static class Patch_Ability_AffectedTargets_PhysicalFrame
    {
        private static void Postfix(Ability __instance, LocalTargetInfo target, ref IEnumerable<LocalTargetInfo> __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance.pawn, out FramePawn frame))
                __result = FramePilotAbilitySupport.AffectedTargets(__instance, frame, target);
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.AIGetAOETarget))]
    internal static class Patch_Ability_AOETarget_PhysicalFrame
    {
        private static bool Prefix(Ability __instance, ref LocalTargetInfo __result)
        {
            if (!FrameAbilityContext.TryGetContainingFrame(__instance.pawn, out FramePawn frame)) return true;
            __result = LocalTargetInfo.Invalid;
            if (!frame.Cockpit.CanExecuteActions || !__instance.def.ai_SearchAOEForTargets) return false;
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(frame.Position, frame.Map,
                __instance.verb.EffectiveRange, true))
                if (FramePilotAbilitySupport.ValidAffected(__instance, thing) &&
                    __instance.EffectComps.All(effect => effect.AICanTargetNow(thing)))
                { __result = thing; break; }
            return false;
        }
    }

    [HarmonyPatch(typeof(Targeter), nameof(Targeter.BeginTargeting),
        new[] { typeof(ITargetingSource), typeof(ITargetingSource), typeof(bool),
            typeof(Func<LocalTargetInfo, ITargetingSource>), typeof(Action), typeof(bool) })]
    internal static class Patch_Targeter_BeginTargeting_PilotAbility
    {
        private static void Postfix(ITargetingSource source)
        {
            if (source is Verb_CastAbility verb) FrameAbilityContext.BeginTargeting(verb.ability?.pawn);
        }
    }
    [HarmonyPatch(typeof(Targeter), nameof(Targeter.StopTargeting))]
    internal static class Patch_Targeter_StopTargeting_PilotAbility
    { private static void Postfix() => FrameAbilityContext.EndTargeting(); }

    // The public Psycast implementation directly reads owner.Map for cast VFX.
    // Adapt those call sites to an explicit execution map; owner getters remain
    // truthful. No content-mod or compiler-generated method is targeted.
    [HarmonyPatch]
    internal static class Patch_Psycast_ExecutionMap
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(Psycast), nameof(Psycast.Activate),
                new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
            yield return AccessTools.Method(typeof(Psycast), "ApplyEffects",
                new[] { typeof(IEnumerable<CompAbilityEffect>), typeof(LocalTargetInfo), typeof(LocalTargetInfo) });
        }
        private static bool Prefix(Psycast __instance) => FramePilotAbilitySupport.ExecutionAllowed(__instance).Accepted;
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo map = AccessTools.PropertyGetter(typeof(Thing), nameof(Thing.Map));
            MethodInfo explicitMap = AccessTools.Method(typeof(FrameAbilityContext), nameof(FrameAbilityContext.ExecutionMap));
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(map)) { instruction.opcode = OpCodes.Call; instruction.operand = explicitMap; }
                yield return instruction;
            }
        }
    }

    public sealed class JobGiver_FramePilotAbility : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!FrameIdentity.TryGet(pawn, out FramePawn frame) || !frame.Cockpit.CanUseHostileCombatAI) return null;
            foreach (Ability module in frame.Loadout.ActiveModuleAbilities())
                if (module.def.aiCanUse && module.CanCast.Accepted && module.AICanTargetNow(frame))
                    return module.GetJob(frame, frame);
            Pawn pilot = frame.Cockpit.Pilot;
            if (pilot?.abilities == null || frame.CurJob?.ability != null) return null;
            IAttackTarget enemy = AttackTargetFinder.BestAttackTarget(frame,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable, null, 0f, 65f);
            if (enemy?.Thing == null) return null;
            foreach (Ability ability in pilot.abilities.AllAbilitiesForReading)
            {
                FrameAbilityContext.BindVanillaAbility(ability);
                if (ability.def.aiCanUse && ability.CanCast.Accepted && ability.AICanTargetNow(enemy.Thing))
                    return ability.GetJob(enemy.Thing, enemy.Thing);
            }
            return null;
        }
    }
}

