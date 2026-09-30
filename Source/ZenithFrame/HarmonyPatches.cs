using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    [StaticConstructorOnStartup]
    public static class FrameHarmonyBootstrap
    {
        static FrameHarmonyBootstrap()
        {
            new Harmony("gyugaze.zenithframe.rimward").PatchAll();
        }
    }

    // A disabled Frame remains a mechanical chassis. Rescue and capture share
    // this semantic gate in vanilla, so excluding the Frame here also prevents
    // autonomous rescue work and prisoner-style acquisition without changing
    // ordinary Pawn behavior.
    [HarmonyPatch(typeof(HealthAIUtility), nameof(HealthAIUtility.CanRescueNow))]
    internal static class Patch_HealthAIUtility_CanRescueNow_Frame
    {
        private static void Postfix(Pawn patient, ref bool __result)
        {
            if (patient is FramePawn)
                __result = false;
        }
    }

    // Frames are deliberately neither human colonists nor Biotech colony mechs.
    // This narrow bridge lets the ordinary right-click order pipeline keep a
    // valid piloted Frame in FloatMenuContext without changing its race identity.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.CanTakeOrder), MethodType.Getter)]
    internal static class Patch_Pawn_CanTakeOrder_Frame
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__instance is FramePawn frame)
                __result = frame.Cockpit?.CanTakePlayerOrders == true;
        }
    }

    // Melee retaliation can be invoked outside the current JobDriver when an
    // adjacent enemy attacks. Keep the no-pilot invariant at the actual melee
    // entry point as well as in the Frame-specific inert idle job.
    [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
    internal static class Patch_Pawn_MeleeVerbs_TryMeleeAttack_Frame
    {
        private static bool Prefix(Pawn_MeleeVerbs __instance, ref bool __result)
        {
            if (__instance.Pawn is FramePawn frame && !frame.Cockpit.CanOperate)
            {
                __result = false;
                return false;
            }

            return true;
        }
    }

    // CanTakeOrder is intentionally broader than the set of actions a Frame
    // should receive. Keep the ordinary Pawn move/attack providers, but do not
    // accidentally turn a Frame into a worker that can equip, haul, tend, open,
    // rescue, trade, or perform other colonist right-click jobs.
    [HarmonyPatch(typeof(FloatMenuOptionProvider), nameof(FloatMenuOptionProvider.SelectedPawnValid))]
    internal static class Patch_FloatMenuOptionProvider_SelectedPawnValid_Frame
    {
        private static void Postfix(FloatMenuOptionProvider __instance, Pawn pawn, ref bool __result)
        {
            if (!(pawn is FramePawn))
                return;

            bool ordinaryCombatProvider =
                __instance is FloatMenuOptionProvider_DraftedMove ||
                __instance is FloatMenuOptionProvider_DraftedAttack;

            __result = __result && ordinaryCombatProvider;
        }
    }

    // Vanilla's melee order helper has the same hard-coded controlled-pawn
    // gate as ranged attack. Frames use the same reachability, violence,
    // melee-verb and AttackMelee job semantics, without Biotech command range.
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetMeleeAttackAction))]
    internal static class Patch_FloatMenuUtility_GetMeleeAttackAction_Frame
    {
        private static bool Prefix(Pawn pawn, LocalTargetInfo target, ref Action __result, ref string failStr, bool ignoreControlled)
        {
            if (!(pawn is FramePawn frame))
                return true;

            __result = null;
            failStr = "";

            if (!ignoreControlled && !pawn.Drafted)
            {
                failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
            }
            else if (!frame.Cockpit.CanTakePlayerOrders)
            {
                failStr = "CannotOrderNonControlledLower".Translate();
            }
            else if (target.IsValid && !pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly))
            {
                failStr = "NoPath".Translate();
            }
            else if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                failStr = "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn);
            }
            else if (pawn.meleeVerbs.TryGetMeleeVerb(target.Thing) == null)
            {
                failStr = "Incapable".Translate();
            }
            else if (pawn == target.Thing)
            {
                failStr = "CannotAttackSelf".Translate();
            }
            else if (target.Thing is Pawn targetPawn &&
                     (pawn.InSameExtraFaction(targetPawn, ExtraFactionType.HomeFaction) ||
                      pawn.InSameExtraFaction(targetPawn, ExtraFactionType.MiniFaction)))
            {
                failStr = "CannotAttackSameFactionMember".Translate();
            }
            else
            {
                __result = () =>
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackMelee, target);
                    if (target.Thing is Pawn downableTarget)
                        job.killIncappedTarget = downableTarget.Downed;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                };
                return false;
            }

            failStr = failStr.CapitalizeFirst();
            return false;
        }
    }

    // PawnAttackGizmoUtility is also hard-coded to vanilla player-controlled
    // categories. Let its own melee/squad gizmos recognize operable Frames.
    [HarmonyPatch(typeof(PawnAttackGizmoUtility), "CanOrderPlayerPawn")]
    internal static class Patch_PawnAttackGizmoUtility_CanOrderPlayerPawn_Frame
    {
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            if (pawn is FramePawn frame && frame.Cockpit?.CanTakePlayerOrders == true)
                __result = true;
        }
    }

    // Vanilla ranged orders reject anything that is not a colonist, colony mech,
    // or controllable subhuman. Frames intentionally belong to none of those
    // categories, so only that control gate is bridged here. The resulting job
    // is still RimWorld's ordinary AttackStatic job and ordinary weapon verb.
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetRangedAttackAction))]
    internal static class Patch_FloatMenuUtility_GetRangedAttackAction_Frame
    {
        private static bool Prefix(Pawn pawn, LocalTargetInfo target, ref Action __result, ref string failStr)
        {
            if (!(pawn is FramePawn frame))
                return true;

            __result = null;
            failStr = "";

            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary == null)
                return false;

            Verb verb = primary.TryGetComp<CompEquippable>()?.AllVerbs
                .FirstOrDefault(candidate => candidate is Verb_FrameShot);
            if (verb == null || verb.verbProps.IsMeleeAttack)
                return false;

            if (!frame.Cockpit.CanTakePlayerOrders)
            {
                failStr = "CannotOrderNonControlledLower".Translate();
            }
            else if (!pawn.Drafted)
            {
                failStr = "IsNotDraftedLower".Translate(pawn.LabelShort, pawn);
            }
            else if (target.IsValid && !verb.CanHitTarget(target))
            {
                if (!pawn.Position.InHorDistOf(target.Cell, verb.EffectiveRange))
                {
                    failStr = "OutOfRange".Translate();
                }
                else
                {
                    float minRange = verb.verbProps.EffectiveMinRange(target, pawn);
                    failStr = (float)pawn.Position.DistanceToSquared(target.Cell) < minRange * minRange
                        ? "TooClose".Translate()
                        : "CannotHitTarget".Translate();
                }
            }
            else if (pawn.WorkTagIsDisabled(WorkTags.Violent))
            {
                failStr = "IsIncapableOfViolenceLower".Translate(pawn.LabelShort, pawn);
            }
            else if (pawn == target.Thing)
            {
                failStr = "CannotAttackSelf".Translate();
            }
            else if (target.Thing is Pawn targetPawn &&
                     (pawn.InSameExtraFaction(targetPawn, ExtraFactionType.HomeFaction) ||
                      pawn.InSameExtraFaction(targetPawn, ExtraFactionType.MiniFaction)))
            {
                failStr = "CannotAttackSameFactionMember".Translate();
            }
            else
            {
                __result = () =>
                {
                    Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
                    job.verbToUse = verb;
                    job.playerForced = true;
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                };
                return false;
            }

            failStr = failStr.CapitalizeFirst();
            return false;
        }
    }
}
