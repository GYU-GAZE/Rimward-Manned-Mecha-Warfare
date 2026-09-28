using System;
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

    // Frames are deliberately neither human colonists nor Biotech colony mechs.
    // This narrow bridge lets the ordinary right-click order pipeline keep a
    // valid piloted Frame in FloatMenuContext without changing its race identity.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.CanTakeOrder), MethodType.Getter)]
    internal static class Patch_Pawn_CanTakeOrder_Frame
    {
        private static void Postfix(Pawn __instance, ref bool __result)
        {
            if (__instance is FramePawn frame)
                __result = frame.Cockpit?.CanOperate == true;
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

    // DraftedAttack asks for a ranged option and then also asks for a melee
    // option. For the ranged-only MVP, letting vanilla evaluate the melee path
    // creates a bogus disabled "non-controlled pawn" entry that prevents the
    // normal one-click attack option from auto-taking.
    [HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.GetMeleeAttackAction))]
    internal static class Patch_FloatMenuUtility_GetMeleeAttackAction_Frame
    {
        private static bool Prefix(Pawn pawn, ref Action __result, ref string failStr)
        {
            if (!(pawn is FramePawn) || !FloatMenuUtility.UseRangedAttack(pawn))
                return true;

            __result = null;
            failStr = "";
            return false;
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

            Verb verb = primary.TryGetComp<CompEquippable>()?.PrimaryVerb;
            if (verb == null || verb.verbProps.IsMeleeAttack)
                return false;

            if (!frame.Cockpit.CanOperate)
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
                    pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                };
                return false;
            }

            failStr = failStr.CapitalizeFirst();
            return false;
        }
    }
}
