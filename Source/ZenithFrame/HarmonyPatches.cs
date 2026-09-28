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
