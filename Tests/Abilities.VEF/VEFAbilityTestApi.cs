using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using ZenithFrame;
using VEFAbility = VEF.Abilities.Ability;

namespace ZenithFrame.AbilityTests.VEF
{
    public sealed class TestTargetAbility : VEFAbility
    {
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            cooldown = GetCooldownForPawn();
            Pawn target = targets?.FirstOrDefault().Thing as Pawn;
            if (target != null)
                target.health.AddHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestVEFTargetEffect"));
        }
    }

    public static class VEFAbilityTestApi
    {
        private static AbilityDef TestDef => DefDatabase<AbilityDef>.GetNamed("ZF_TestVEFTarget");
        private static AbilityDef VPEDef => DefDatabase<AbilityDef>.GetNamedSilentFail("VPE_SpeedBoost");

        private static CompAbilities Comp(Pawn pawn) => pawn.GetComp<CompAbilities>();
        private static VEFAbility Ability(Pawn pawn) => Comp(pawn)?.LearnedAbilities.FirstOrDefault(a => a.def == TestDef);
        private static VEFAbility VPEAbility(Pawn pawn) => Comp(pawn)?.LearnedAbilities.FirstOrDefault(a => a.def == VPEDef);

        public static bool HasVPE() => VPEDef != null;

        public static bool Give(Pawn pawn)
        {
            CompAbilities comp = Comp(pawn);
            if (comp == null) return false;
            if (Ability(pawn) == null) comp.GiveAbility(TestDef);
            return Ability(pawn) != null;
        }

        public static void ClearTarget(Pawn target)
        {
            Hediff marker = target.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("ZF_TestVEFTargetEffect"));
            if (marker != null) target.health.RemoveHediff(marker);
        }

        public static bool Start(Pawn pawn, Pawn target)
        {
            VEFAbility ability = Ability(pawn);
            if (ability == null) return false;
            ability.StartAbilityJob(new[] { new GlobalTargetInfo(target) });
            return true;
        }

        public static bool EffectApplied(Pawn target) =>
            target.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestVEFTargetEffect"));

        public static int Cooldown(Pawn pawn) => Ability(pawn)?.cooldown ?? -1;
        public static string LoadId(Pawn pawn) => Ability(pawn)?.GetUniqueLoadID();

        public static bool HasExactCommand(FramePawn frame, Pawn pawn)
        {
            VEFAbility ability = Ability(pawn);
            return frame.GetGizmos().OfType<Command_Ability>().Count(c => ReferenceEquals(c.ability, ability)) == 1;
        }

        public static bool UsesFrameHost(FramePawn frame, Pawn pawn)
        {
            VEFAbility ability = Ability(pawn);
            return ability != null && ReferenceEquals(ability.pawn, pawn) && ReferenceEquals(ability.Caster, frame) &&
                frame.GetComp<CompAbilities>() == null;
        }

        public static bool FrameRunsVEFJob(FramePawn frame) => frame.CurJob != null && frame.jobs.curDriver is JobDriver_CastAbilityOnce;
        public static string FrameJobState(FramePawn frame) =>
            "job=" + (frame.CurJobDef?.defName ?? "null") +
            " driver=" + (frame.jobs.curDriver?.GetType().FullName ?? "null") +
            " target=" + (frame.CurJob?.targetA.ToString() ?? "null");

        public static bool GiveVPE(Pawn pawn)
        {
            if (VPEDef == null) return false;
            if (!EnsureVPEPsyset(pawn)) return false;
            CompAbilities comp = Comp(pawn);
            if (comp == null) return false;
            if (VPEAbility(pawn) == null) comp.GiveAbility(VPEDef);
            return VPEAbility(pawn) != null;
        }

        public static bool VPESetContains(Pawn pawn)
        {
            object psyset = CurrentPsyset(pawn);
            object abilities = psyset?.GetType().GetField("Abilities")?.GetValue(psyset);
            object result = abilities?.GetType().GetMethod("Contains")?.Invoke(abilities, new object[] { VPEDef });
            return result is bool contains && contains;
        }

        public static void ClearVPEEffect(Pawn pawn)
        {
            HediffDef effectDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_SpeedBoost");
            Hediff effect = effectDef == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(effectDef);
            if (effect != null) pawn.health.RemoveHediff(effect);
        }

        public static bool StartVPE(Pawn pawn)
        {
            VEFAbility ability = VPEAbility(pawn);
            if (ability == null) return false;
            ability.StartAbilityJob(new[] { new GlobalTargetInfo(pawn) });
            return true;
        }

        public static bool VPEEffectApplied(Pawn pawn)
        {
            HediffDef effectDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_SpeedBoost");
            return effectDef != null && pawn.health.hediffSet.HasHediff(effectDef);
        }

        public static int VPECooldown(Pawn pawn) => VPEAbility(pawn)?.cooldown ?? -1;
        public static string VPELoadId(Pawn pawn) => VPEAbility(pawn)?.GetUniqueLoadID();

        public static bool HasExactVPECommand(FramePawn frame, Pawn pawn)
        {
            VEFAbility ability = VPEAbility(pawn);
            return frame.GetGizmos().OfType<Command_Ability>().Count(c =>
                ReferenceEquals(c.ability, ability) && c.GetType().FullName == "VanillaPsycastsExpanded.Command_Ability_Psycast") == 1;
        }

        public static bool VPEUsesFrameHost(FramePawn frame, Pawn pawn)
        {
            VEFAbility ability = VPEAbility(pawn);
            return ability != null && ReferenceEquals(ability.pawn, pawn) && ReferenceEquals(ability.Caster, frame) &&
                frame.GetComp<CompAbilities>() == null;
        }

        public static string VPEDiagnostic(FramePawn frame, Pawn pawn)
        {
            VEFAbility ability = VPEAbility(pawn);
            return "ability=" + (ability?.GetUniqueLoadID() ?? "null") +
                " caster=" + (ability?.Caster?.GetUniqueLoadID() ?? "null") +
                " pawn=" + (ability?.pawn?.GetUniqueLoadID() ?? "null") +
                " frameComp=" + (frame.GetComp<CompAbilities>() != null) +
                " psyset=" + VPESetContains(pawn) +
                " commandCount=" + frame.GetGizmos().Count();
        }

        private static bool EnsureVPEPsyset(Pawn pawn)
        {
            HediffDef implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
            object implant = implantDef == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(implantDef);
            if (implant == null) return false;
            FieldInfo setsField = implant.GetType().GetField("psysets");
            IList sets = setsField?.GetValue(implant) as IList;
            if (sets == null) return false;
            if (sets.Count == 0)
            {
                Type psysetType = implant.GetType().Assembly.GetType("VanillaPsycastsExpanded.PsySet");
                object created = Activator.CreateInstance(psysetType);
                psysetType.GetField("Name").SetValue(created, "ZENITH test psyset");
                sets.Add(created);
            }
            FieldInfo index = implant.GetType().GetField("psysetIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            index?.SetValue(implant, 0);
            object abilities = sets[0].GetType().GetField("Abilities").GetValue(sets[0]);
            abilities.GetType().GetMethod("Add").Invoke(abilities, new object[] { VPEDef });
            return VPESetContains(pawn);
        }

        private static object CurrentPsyset(Pawn pawn)
        {
            HediffDef implantDef = DefDatabase<HediffDef>.GetNamedSilentFail("VPE_PsycastAbilityImplant");
            object implant = implantDef == null ? null : pawn.health.hediffSet.GetFirstHediffOfDef(implantDef);
            if (implant == null) return null;
            IList sets = implant.GetType().GetField("psysets")?.GetValue(implant) as IList;
            return sets != null && sets.Count != 0 ? sets[0] : null;
        }
    }
}
