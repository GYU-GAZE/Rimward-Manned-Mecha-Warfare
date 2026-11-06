using System.Linq;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace ZenithFrame
{
    public sealed class FrameDamageExtension : DefModExtension
    {
        public float pilotTraumaFraction = 0.5f;
    }

    [DefOf]
    public static class ZenithBodyPartDefOf
    {
        public static BodyPartDef ZF_FrameTorso;
        public static BodyPartDef ZF_FrameCore;
        public static BodyPartDef ZF_FrameCockpit;
        public static BodyPartDef ZF_FrameHeadAssembly;

        static ZenithBodyPartDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ZenithBodyPartDefOf)); }
    }

    public static class FrameDamageRules
    {
        public static BodyPartRecord FindPart(FramePawn frame, BodyPartDef def) =>
            frame?.RaceProps?.body?.AllParts.FirstOrDefault(part => part.def == def);

        public static bool IsPartDestroyed(FramePawn frame, BodyPartDef def)
        {
            BodyPartRecord part = FindPart(frame, def);
            return part == null || frame.health.hediffSet.PartIsMissing(part) ||
                frame.health.hediffSet.GetPartHealth(part) <= 0.0001f;
        }

        public static bool IsStructurallyDestroyed(FramePawn frame) =>
            frame == null || IsPartDestroyed(frame, ZenithBodyPartDefOf.ZF_FrameTorso) ||
            IsPartDestroyed(frame, ZenithBodyPartDefOf.ZF_FrameCore);

        public static bool IsRecoverablePart(BodyPartRecord part) => part != null &&
            part.def != ZenithBodyPartDefOf.ZF_FrameTorso && part.def != ZenithBodyPartDefOf.ZF_FrameCore;

        public static bool IsMechanicallyDisabled(FramePawn frame) =>
            frame != null && (IsPartDestroyed(frame, ZenithBodyPartDefOf.ZF_FrameHeadAssembly) ||
            IsPartDestroyed(frame, ZenithBodyPartDefOf.ZF_FrameCockpit) ||
            !frame.health.capacities.CapableOf(PawnCapacityDefOf.Moving));

        public static bool ApplyDeterministicDamage(FramePawn frame, BodyPartRecord part, float amount)
        {
            if (frame == null || part == null || amount <= 0f || frame.Dead || frame.Destroyed) return false;
            var damage = new DamageInfo(DamageDefOf.Blunt, amount, 999f, hitPart: part);
            damage.SetAllowDamagePropagation(false);
            frame.TakeDamage(damage);
            return true;
        }

        public static IEnumerable<BodyPartRecord> DevelopmentTargets(FramePawn frame) =>
            frame?.RaceProps?.body?.AllParts.Where(part => part.def.defName.StartsWith("ZF_Frame")) ??
            Enumerable.Empty<BodyPartRecord>();
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromLethalDamageThreshold))]
    internal static class Patch_PawnHealthTracker_LethalDamageThreshold_Frame
    {
        private static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!(___pawn is FramePawn)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDeadFromRequiredCapacity))]
    internal static class Patch_PawnHealthTracker_RequiredCapacity_Frame
    {
        private static bool Prefix(Pawn ___pawn, ref PawnCapacityDef __result)
        {
            if (!(___pawn is FramePawn)) return true;
            __result = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    internal static class Patch_PawnHealthTracker_ShouldBeDead_Frame
    {
        private static void Postfix(Pawn ___pawn, ref bool __result)
        {
            if (___pawn is FramePawn frame && FrameDamageRules.IsStructurallyDestroyed(frame))
                __result = true;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned))]
    internal static class Patch_PawnHealthTracker_ShouldBeDowned_Frame
    {
        private static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!(___pawn is FramePawn frame)) return true;
            __result = FrameDamageRules.IsMechanicallyDisabled(frame);
            return false;
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_Thing_TakeDamage_FrameCockpitTrauma
    {
        private struct TraumaState
        {
            internal Pawn Pilot;
            internal BodyPartRecord Cockpit;
            internal float Fraction;
        }

        private static void Prefix(Thing __instance, ref TraumaState __state)
        {
            if (!(__instance is FramePawn frame) || frame.Cockpit?.Pilot == null) return;
            __state.Pilot = frame.Cockpit.Pilot;
            __state.Cockpit = FrameDamageRules.FindPart(frame, ZenithBodyPartDefOf.ZF_FrameCockpit);
            __state.Fraction = frame.def.GetModExtension<FrameDamageExtension>()?.pilotTraumaFraction ?? 0f;
        }

        private static void Postfix(DamageInfo dinfo, DamageWorker.DamageResult __result, TraumaState __state)
        {
            if (__state.Pilot == null || __state.Pilot.Dead || __state.Cockpit == null ||
                __state.Fraction <= 0f || __result?.hediffs == null)
                return;

            float cockpitDamage = __result.hediffs
                .OfType<Hediff_Injury>()
                .Where(injury => ReferenceEquals(injury.Part, __state.Cockpit))
                .Sum(injury => injury.Severity);
            float transfer = cockpitDamage * __state.Fraction;
            if (transfer <= 0f) return;

            var pilotDamage = new DamageInfo(dinfo.Def, transfer, dinfo.ArmorPenetrationInt,
                dinfo.Angle, dinfo.Instigator, null, dinfo.Weapon, dinfo.Category,
                __state.Pilot, dinfo.InstigatorGuilty, dinfo.SpawnFilth,
                dinfo.WeaponQuality, dinfo.CheckForJobOverride, dinfo.PreventCascade);
            __state.Pilot.TakeDamage(pilotDamage);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_Pawn_Kill_ContainedFramePilot
    {
        private static void Prefix(Pawn __instance)
        {
            if (__instance?.holdingOwner?.Owner is CompPilotableMech cockpit &&
                ReferenceEquals(cockpit.Pilot, __instance))
                cockpit.EmergencyEject();
        }
    }
}
