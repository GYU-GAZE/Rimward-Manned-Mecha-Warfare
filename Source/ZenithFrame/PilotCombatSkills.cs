using RimWorld;
using Verse;

namespace ZenithFrame
{
    public sealed class StatPart_PilotSkill : StatPart
    {
        public SkillDef skill;

        public override void TransformValue(StatRequest req, ref float val)
        {
            if (!TryContribution(req, out Pawn pilot, out float contribution)) return;

            // A non-humanlike Frame has no SkillTracker, so vanilla already put
            // noSkillOffset into the unfinalized chassis stat. Replace exactly
            // that term with the same SkillNeed contribution vanilla would use
            // for this pilot, then let the original post-process curve run.
            val += contribution - parentStat.noSkillOffset;
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!TryContribution(req, out Pawn pilot, out float contribution)) return null;
            int level = pilot.skills.GetSkill(skill).Level;
            return "Pilot " + skill.LabelCap + " (" + level + "): " + contribution.ToString("0.##");
        }

        private bool TryContribution(StatRequest req, out Pawn pilot, out float contribution)
        {
            pilot = null;
            contribution = 0f;
            if (!(req.Thing is FramePawn frame) || frame.Cockpit?.CanOperate != true)
                return false;

            pilot = frame.Cockpit.Pilot;
            if (pilot?.skills == null || parentStat?.skillNeedOffsets == null)
                return false;

            foreach (SkillNeed need in parentStat.skillNeedOffsets)
            {
                if (need.skill != skill) continue;
                contribution = need.ValueFor(pilot);
                return true;
            }

            return false;
        }
    }

    internal static class FramePilotCombatExperience
    {
        internal static void LearnShooting(Verb verb)
        {
            if (!(verb?.CasterPawn is FramePawn frame) || frame.Cockpit?.CanOperate != true)
                return;
            if (!(verb.CurrentTarget.Thing is Pawn target) || target.Downed)
                return;

            Pawn pilot = frame.Cockpit.Pilot;
            if (pilot?.skills == null) return;
            float xpPerSecond = target.HostileTo(frame) ? 170f : 20f;
            pilot.skills.Learn(SkillDefOf.Shooting, xpPerSecond * verb.verbProps.AdjustedFullCycleTime(verb, frame));
        }

        internal static void LearnMelee(Verb_MeleeAttack verb)
        {
            if (!(verb?.CasterPawn is FramePawn frame) || frame.Cockpit?.CanOperate != true ||
                !frame.Spawned || frame.stances.FullBodyBusy)
                return;

            Pawn target = verb.CurrentTarget.Pawn;
            if (target == null || target.Downed || target.GetPosture() != PawnPosture.Standing)
                return;

            Pawn pilot = frame.Cockpit.Pilot;
            if (pilot?.skills == null) return;
            pilot.skills.Learn(SkillDefOf.Melee, 200f * verb.verbProps.AdjustedFullCycleTime(verb, frame));
        }
    }
}
