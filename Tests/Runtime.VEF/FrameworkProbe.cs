using System.Linq;
using RimWorld.Planet;
using VEF.Abilities;
using Verse;
using ZenithFrame.VEFCompat;
using VEFAbility = VEF.Abilities.Ability;

namespace ZenithFrame.Tests.VEF
{
    public sealed class FrameworkTargetAbility : VEFAbility
    {
        public int ticks;
        public override void Tick() { base.Tick(); ticks++; }
        public override void Cast(params GlobalTargetInfo[] targets)
        {
            base.Cast(targets);
            Pawn target = targets.FirstOrDefault().Thing as Pawn;
            target?.health.AddHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestVEFFrameworkMarker"));
        }
    }
    [StaticConstructorOnStartup]
    public static class FrameworkProbeBootstrap
    { static FrameworkProbeBootstrap() => AcceptanceSuite.Framework = new FrameworkProbe(); }

    public sealed class FrameworkProbe : IFrameworkProbe
    {
        private FrameworkTargetAbility Ability(Pawn pilot) => pilot.GetComp<CompAbilities>()?.LearnedAbilities
            .OfType<FrameworkTargetAbility>().FirstOrDefault();
        public void Give(Pawn pilot) => pilot.GetComp<CompAbilities>().GiveAbility(
            DefDatabase<AbilityDef>.GetNamed("ZF_TestVEFFramework"));
        public bool Owned(Pawn pilot, FramePawn frame) => Ability(pilot) is FrameworkTargetAbility ability &&
            ReferenceEquals(ability.pawn, pilot) && ReferenceEquals(ability.Caster, frame) &&
            frame.GetComp<CompAbilities>() == null && ability.ticks > 0;
        public string Describe(Pawn pilot, FramePawn frame)
        {
            FrameworkTargetAbility ability = Ability(pilot);
            if (ability == null) return "missing original ability";
            bool allowed = VEFFrameExecution.Allowed(ability, out string actionReason);
            bool enabled = ability.IsEnabledForPawn(out string enabledReason);
            return $"owner={ReferenceEquals(ability.pawn, pilot)} caster={ReferenceEquals(ability.Caster, frame)} " +
                $"verb={ReferenceEquals(ability.verb.caster, frame)} ticks={ability.ticks} " +
                $"frameComp={frame.GetComp<CompAbilities>() != null} allowed={allowed} ({actionReason}) " +
                $"enabled={enabled} ({enabledReason}) job={frame.CurJob?.def.defName} " +
                $"driver={frame.jobs.curDriver?.GetType().FullName} " +
                $"casting={ReferenceEquals(pilot.GetComp<CompAbilities>().currentlyCasting, ability)}";
        }
        public bool Start(Pawn pilot, Pawn target)
        {
            Ability(pilot).StartAbilityJob(new[] { new GlobalTargetInfo(target) });
            return FrameAbilityContext.TryGetOperationalFrame(pilot, out FramePawn frame) &&
                frame.jobs.curDriver is JobDriver_CastAbilityOnce;
        }
        public bool Applied(Pawn target) => target.health.hediffSet.HasHediff(
            DefDatabase<HediffDef>.GetNamed("ZF_TestVEFFrameworkMarker"));
        public bool Blocked(Pawn pilot)
        {
            FrameworkTargetAbility ability = Ability(pilot);
            bool blocked = !ability.IsEnabledForPawn(out _) && !VEFFrameExecution.Allowed(ability, out _);
            ability.StartAbilityJob(new[] { new GlobalTargetInfo(pilot) });
            return blocked && pilot.GetComp<CompAbilities>().currentlyCasting == null;
        }
    }
}
