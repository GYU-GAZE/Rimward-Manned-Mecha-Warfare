using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    [DefOf]
    public static class ZenithJobDefOf
    {
        public static JobDef ZF_FrameIdleJob;
        public static JobDef ZF_BoardFrame;
        public static JobDef ZF_HackFrame;
        public static JobDef ZF_FramePilotSelfTend;

        static ZenithJobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ZenithJobDefOf)); }
    }

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
        protected override Job TryGiveJob(Pawn pawn) => JobMaker.MakeJob(ZenithJobDefOf.ZF_FrameIdleJob);
    }

    public sealed class ThinkNode_Conditional_OperationalPlayerFrame : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn) =>
            pawn is FramePawn frame && frame.Cockpit.CanTakePlayerOrders && frame.Drafted;
    }

    public sealed class JobGiver_FramePlayerCombatWait : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn) =>
            JobMaker.MakeJob(JobDefOf.Wait_Combat, 180, checkOverrideOnExpiry: true);
    }

    public sealed class ThinkNode_Conditional_OperationalHostileFrame : ThinkNode_Conditional
    {
        protected override bool Satisfied(Pawn pawn) =>
            pawn is FramePawn frame && frame.Cockpit.CanUseHostileCombatAI;
    }

    // Vanilla JobDriver_Wait scans for adjacent hostiles and may launch an
    // opportunistic melee attack. An empty Frame needs a genuinely inert job:
    // no target scan, no verb use and no movement.
    public sealed class JobDriver_FrameIdle : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil idle = ToilMaker.MakeToil("FrameIdle");
            idle.defaultCompleteMode = ToilCompleteMode.Delay;
            idle.defaultDuration = 120;
            yield return idle;
        }
    }
}
