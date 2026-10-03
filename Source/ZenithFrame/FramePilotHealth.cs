using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    public sealed class JobDriver_FramePilotSelfTend : JobDriver
    {
        private FramePawn Frame => job.targetA.Thing as FramePawn;
        private Medicine Medicine => job.targetB.Thing as Medicine;
        private Pawn ExactPilot => job.targetC.Thing as Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddEndCondition(() => IsValid() ? JobCondition.Ongoing : JobCondition.Incompletable);

            Toil tend = ToilMaker.MakeToil("FramePilotSelfTend");
            tend.initAction = () =>
            {
                Pawn pilot = ExactPilot;
                if (pilot == null)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                float speed = pilot.GetStatValue(StatDefOf.MedicalTendSpeed);
                tend.actor.jobs.curDriver.ticksLeftThisToil = (int)(600f / speed);
            };
            tend.tickAction = () => ExactPilot?.skills?.Learn(SkillDefOf.Medicine, 0.1f);
            tend.defaultCompleteMode = ToilCompleteMode.Delay;
            tend.WithProgressBarToilDelay(TargetIndex.A);
            yield return tend;

            Toil finish = ToilMaker.MakeToil("FinishFramePilotSelfTend");
            finish.initAction = () =>
            {
                if (!IsValid())
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                TendUtility.DoTend(ExactPilot, ExactPilot, Medicine);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private bool IsValid()
        {
            FramePawn frame = Frame;
            Pawn pilot = ExactPilot;
            Medicine medicine = Medicine;
            return frame != null && ReferenceEquals(pawn, frame) && frame.Spawned && !frame.Dead &&
                !frame.Downed && frame.Cockpit != null && ReferenceEquals(frame.Cockpit.Pilot, pilot) &&
                pilot != null && !pilot.Dead && !pilot.Downed && pilot.health.HasHediffsNeedingTend() &&
                (medicine == null || (!medicine.Destroyed && pilot.inventory?.innerContainer.Contains(medicine) == true));
        }
    }
}
