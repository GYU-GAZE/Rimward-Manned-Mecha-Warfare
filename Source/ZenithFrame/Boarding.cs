using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    public sealed class JobDriver_BoardFrame : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !(job.targetA.Thing is FramePawn frame) || !frame.Cockpit.CanBoard(pawn, false));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil board = ToilMaker.MakeToil("BoardFrame");
            board.initAction = () =>
            {
                var cockpit = ((FramePawn)job.targetA.Thing).Cockpit;
                if (!cockpit.TryBoard(pawn)) EndJobWith(JobCondition.Incompletable);
            };
            board.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return board;
        }
    }

    public sealed class JobGiver_FrameIdle : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) => JobMaker.MakeJob(RimWorld.JobDefOf.Wait, 120);
    }
}
