using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    // An ability remains owned by the contained pilot. This context only marks
    // the short UI/targeting/cast interval in which the spawned Frame supplies
    // the physical origin required by vanilla and supported ability frameworks.
    public static class FrameAbilityContext
    {
        [ThreadStatic] private static Stack<Pawn> transientPilots;
        private static readonly HashSet<Pawn> castingPilots = new HashSet<Pawn>();
        private static Pawn targetingPilot;

        public static bool TryGetOperationalFrame(Pawn pilot, out FramePawn frame)
        {
            frame = null;
            if (pilot == null || pilot.Destroyed || pilot.Dead ||
                !(pilot.holdingOwner?.Owner is CompPilotableMech cockpit) ||
                !ReferenceEquals(cockpit.Pilot, pilot) ||
                !(cockpit.Frame is FramePawn candidate) ||
                !candidate.Spawned || candidate.Destroyed || candidate.Dead || candidate.Downed ||
                !cockpit.CanOperate)
                return false;

            frame = candidate;
            return true;
        }

        public static Scope EnterTransient(Pawn pilot)
        {
            if (!TryGetOperationalFrame(pilot, out FramePawn _)) return default(Scope);
            if (transientPilots == null) transientPilots = new Stack<Pawn>();
            transientPilots.Push(pilot);
            return new Scope(pilot);
        }

        public static bool TryGetProjectedFrame(Pawn pilot, out FramePawn frame)
        {
            bool transient = transientPilots != null && transientPilots.Contains(pilot);
            bool active = ReferenceEquals(targetingPilot, pilot) || castingPilots.Contains(pilot);
            if ((transient || active) && TryGetOperationalFrame(pilot, out frame)) return true;
            if (active && !TryGetOperationalFrame(pilot, out frame)) Cancel(pilot);
            frame = null;
            return false;
        }

        public static void BeginTargeting(Pawn pilot)
        {
            if (TryGetOperationalFrame(pilot, out FramePawn _)) targetingPilot = pilot;
        }

        public static void EndTargeting()
        {
            targetingPilot = null;
        }

        public static bool IsTargeting(Pawn pilot) => ReferenceEquals(targetingPilot, pilot);

        public static void BeginCast(Pawn pilot)
        {
            if (TryGetOperationalFrame(pilot, out FramePawn _)) castingPilots.Add(pilot);
        }

        public static void EndCast(Pawn pilot)
        {
            if (pilot != null) castingPilots.Remove(pilot);
        }

        public static bool IsCasting(Pawn pilot) => pilot != null && castingPilots.Contains(pilot);

        public static void Cancel(Pawn pilot)
        {
            if (pilot == null) return;
            castingPilots.Remove(pilot);
            if (ReferenceEquals(targetingPilot, pilot))
            {
                targetingPilot = null;
                if (Find.Targeter?.IsTargeting == true) Find.Targeter.StopTargeting();
            }
        }

        private static void ExitTransient(Pawn pilot)
        {
            if (transientPilots == null || transientPilots.Count == 0) return;
            Pawn popped = transientPilots.Pop();
            if (!ReferenceEquals(popped, pilot))
                throw new InvalidOperationException("ZENITH: pilot ability context stack was corrupted.");
        }

        public struct Scope : IDisposable
        {
            private readonly Pawn pilot;
            internal Scope(Pawn pilot) { this.pilot = pilot; }
            public void Dispose() { if (pilot != null) ExitTransient(pilot); }
        }
    }

    internal static class PilotAbilityGizmos
    {
        internal static IEnumerable<Gizmo> VanillaFor(FramePawn frame)
        {
            Pawn pilot = frame.Cockpit?.Pilot;
            if (pilot?.abilities == null || !frame.Cockpit.CanTakePlayerOrders) yield break;

            using (FrameAbilityContext.EnterTransient(pilot))
            {
                foreach (Ability ability in pilot.abilities.AllAbilitiesForReading)
                {
                    bool visible = (frame.Drafted || ability.def.displayGizmoWhileUndrafted) && ability.GizmosVisible();
                    if (!visible) continue;
                    foreach (Command command in ability.GetGizmos()) yield return command;
                    foreach (Gizmo extra in ability.GetGizmosExtra()) yield return extra;
                }

                Pawn_PsychicEntropyTracker tracker = pilot.psychicEntropy;
                if (tracker?.NeedToShowGizmo() == true) yield return tracker.GetGizmo();
            }
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.ConstantCaster), MethodType.Getter)]
    internal static class Patch_Ability_ConstantCaster_EmbarkedPilot
    {
        private static void Postfix(Ability __instance, ref Thing __result)
        {
            if (FrameAbilityContext.TryGetOperationalFrame(__instance?.pawn, out FramePawn frame))
                __result = frame;
        }
    }

    // Vanilla queues the original ability job through ability.pawn.jobs. Only
    // that exact call is redirected; the Job still stores the pilot Ability and
    // its Verb now resolves the Frame through ConstantCaster.
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    internal static class Patch_PawnJobTracker_TryTakeOrderedJob_PilotAbility
    {
        private static bool Prefix(Pawn ___pawn, Job job, JobTag? tag, bool requestQueueing, ref bool __result)
        {
            Pawn owner = job?.ability?.pawn;
            if (!ReferenceEquals(___pawn, owner) ||
                !FrameAbilityContext.TryGetOperationalFrame(owner, out FramePawn frame)) return true;

            FrameAbilityContext.BeginCast(owner);
            __result = frame.jobs.TryTakeOrderedJob(job, tag, requestQueueing);
            if (!__result) FrameAbilityContext.EndCast(owner);
            return false;
        }
    }

    // A self ability still targets its owning pilot. The spawned Frame is kept
    // in target A solely so the stock JobDriver has a valid actor/target for its
    // despawn checks; target B records the real logical target across save/load.
    [HarmonyPatch(typeof(Ability), nameof(Ability.GetJob))]
    internal static class Patch_Ability_GetJob_EmbarkedSelfTarget
    {
        private static void Postfix(Ability __instance, LocalTargetInfo target, Job __result)
        {
            Pawn pilot = __instance?.pawn;
            if (__result == null || !ReferenceEquals(target.Thing, pilot) ||
                !FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn frame)) return;
            __result.targetA = frame;
            __result.targetB = pilot;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.Activate),
        new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    internal static class Patch_Ability_Activate_EmbarkedSelfTarget
    {
        private static void Prefix(Ability __instance, ref LocalTargetInfo target, ref LocalTargetInfo dest)
        {
            Pawn pilot = __instance?.pawn;
            if (!ReferenceEquals(dest.Thing, pilot) ||
                !FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn frame) ||
                !ReferenceEquals(target.Thing, frame)) return;
            target = pilot;
            dest = LocalTargetInfo.Invalid;
        }
    }

    [HarmonyPatch(typeof(JobDriver_CastAbility), nameof(JobDriver_CastAbility.Notify_Starting))]
    internal static class Patch_JobDriverCastAbility_Starting_EmbarkedPilot
    {
        private static void Postfix(JobDriver_CastAbility __instance)
        {
            Pawn owner = __instance?.job?.ability?.pawn;
            if (owner != null && __instance.GetActor() is FramePawn)
                FrameAbilityContext.BeginCast(owner);
        }
    }

    [HarmonyPatch(typeof(JobDriver), nameof(JobDriver.Cleanup))]
    internal static class Patch_JobDriver_Cleanup_PilotAbility
    {
        private static void Postfix(JobDriver __instance)
        {
            Pawn owner = __instance?.job?.ability?.pawn;
            if (owner != null && __instance.GetActor() is FramePawn)
                FrameAbilityContext.EndCast(owner);
        }
    }

    [HarmonyPatch(typeof(Targeter), nameof(Targeter.BeginTargeting),
        new[] { typeof(ITargetingSource), typeof(ITargetingSource), typeof(bool),
            typeof(Func<LocalTargetInfo, ITargetingSource>), typeof(Action), typeof(bool) })]
    internal static class Patch_Targeter_BeginTargeting_PilotAbility
    {
        private static void Postfix(ITargetingSource source)
        {
            if (source is Verb_CastAbility verb && verb.ability != null &&
                FrameAbilityContext.TryGetOperationalFrame(verb.ability.pawn, out FramePawn _))
                FrameAbilityContext.BeginTargeting(verb.ability.pawn);
        }
    }

    [HarmonyPatch(typeof(Targeter), nameof(Targeter.StopTargeting))]
    internal static class Patch_Targeter_StopTargeting_PilotAbility
    {
        private static void Postfix() => FrameAbilityContext.EndTargeting();
    }

    internal static class AbilityContextScopePatch
    {
        internal static void Prefix(Ability __instance, out FrameAbilityContext.Scope __state) =>
            __state = FrameAbilityContext.EnterTransient(__instance?.pawn);

        internal static Exception Finalizer(Exception __exception, FrameAbilityContext.Scope __state)
        {
            __state.Dispose();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GizmoDisabled))]
    internal static class Patch_Ability_GizmoDisabled_EmbarkedPilot
    {
        private static void Prefix(Ability __instance, out FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Prefix(__instance, out __state);
        private static Exception Finalizer(Exception __exception, FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Finalizer(__exception, __state);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.AICanTargetNow))]
    internal static class Patch_Ability_AICanTargetNow_EmbarkedPilot
    {
        private static void Prefix(Ability __instance, out FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Prefix(__instance, out __state);
        private static Exception Finalizer(Exception __exception, FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Finalizer(__exception, __state);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.AIGetAOETarget))]
    internal static class Patch_Ability_AIGetAOETarget_EmbarkedPilot
    {
        private static void Prefix(Ability __instance, out FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Prefix(__instance, out __state);
        private static Exception Finalizer(Exception __exception, FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Finalizer(__exception, __state);
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.OnGizmoUpdate))]
    internal static class Patch_Ability_OnGizmoUpdate_EmbarkedPilot
    {
        private static void Prefix(Ability __instance, out FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Prefix(__instance, out __state);
        private static Exception Finalizer(Exception __exception, FrameAbilityContext.Scope __state) =>
            AbilityContextScopePatch.Finalizer(__exception, __state);
    }

    // Evidence-driven scoped compatibility: installed vanilla/VEF code reads
    // owner Pawn spatial getters directly during an ability operation. These
    // patches never activate outside a marked command/target/cast interval and
    // never alter Spawned or authoritative stored coordinates.
    [HarmonyPatch(typeof(Thing), nameof(Thing.Map), MethodType.Getter)]
    internal static class Patch_Thing_Map_PilotAbilityContext
    {
        private static void Postfix(Thing __instance, ref Map __result)
        {
            if (__instance is Pawn pilot && FrameAbilityContext.TryGetProjectedFrame(pilot, out FramePawn frame))
                __result = frame.Map;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.Position), MethodType.Getter)]
    internal static class Patch_Thing_Position_PilotAbilityContext
    {
        private static void Postfix(Thing __instance, ref IntVec3 __result)
        {
            if (__instance is Pawn pilot && FrameAbilityContext.TryGetProjectedFrame(pilot, out FramePawn frame))
                __result = frame.Position;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawPos), MethodType.Getter)]
    internal static class Patch_Pawn_DrawPos_PilotAbilityContext
    {
        private static void Postfix(Pawn __instance, ref Vector3 __result)
        {
            if (FrameAbilityContext.TryGetProjectedFrame(__instance, out FramePawn frame))
                __result = frame.DrawPos;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Drafted), MethodType.Getter)]
    internal static class Patch_Pawn_Drafted_PilotAbilityContext
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (FrameAbilityContext.TryGetProjectedFrame(__instance, out FramePawn frame))
                __result = frame.Drafted;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.IsColonistPlayerControlled), MethodType.Getter)]
    internal static class Patch_Pawn_PlayerControl_PilotAbilityContext
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (FrameAbilityContext.TryGetProjectedFrame(__instance, out FramePawn frame))
                __result = frame.Cockpit.CanTakePlayerOrders;
        }
    }

    // Hostile Frames already use vanilla target acquisition and combat. This
    // node only asks the pilot's stock ability tracker whether an offensive
    // ability can target that same enemy, then returns the ordinary ability Job.
    public sealed class JobGiver_FramePilotAbility : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (!(pawn is FramePawn frame) || frame.Cockpit?.CanUseHostileCombatAI != true)
                return null;
            Pawn pilot = frame.Cockpit.Pilot;
            if (pilot?.abilities == null || frame.CurJob?.ability != null) return null;

            IAttackTarget attackTarget = AttackTargetFinder.BestAttackTarget(frame,
                TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                target => target is Pawn, 0f, 65f);
            if (attackTarget?.Thing == null) return null;

            LocalTargetInfo target = attackTarget.Thing;
            using (FrameAbilityContext.EnterTransient(pilot))
            {
                foreach (Ability ability in pilot.abilities.AICastableAbilities(target, true))
                {
                    if (!ability.CanCast.Accepted || !ability.AICanTargetNow(target)) continue;
                    return ability.GetJob(target, target);
                }
            }
            return null;
        }
    }
}
