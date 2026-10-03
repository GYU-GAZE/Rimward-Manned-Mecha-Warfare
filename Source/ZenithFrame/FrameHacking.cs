using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ZenithFrame
{
    public sealed class CompProperties_FrameHacking : CompProperties
    {
        public CompProperties_FrameHacking() { compClass = typeof(CompFrameHacking); }
    }

    public sealed class CompFrameHacking : ThingComp
    {
        public const int WorkTicks = 180;
        public FramePawn Frame => (FramePawn)parent;

        public bool IsHackableDisabledChassis =>
            Frame.Spawned && !Frame.Dead && !Frame.Destroyed &&
            Frame.Cockpit != null && !Frame.Cockpit.HasPilot &&
            Frame.Faction != Faction.OfPlayer &&
            (Frame.Faction == null || Frame.Faction.HostileTo(Faction.OfPlayer));

        public bool CanHack(Pawn hacker, bool requireReachAndReserve)
        {
            if (!IsHackableDisabledChassis || hacker == null || hacker == Frame)
                return false;

            bool eligibleColonist =
                hacker.Spawned && hacker.Map == Frame.Map &&
                hacker.Faction == Faction.OfPlayer && hacker.RaceProps.Humanlike &&
                hacker.IsColonistPlayerControlled && !hacker.Dead && !hacker.Downed &&
                !hacker.InMentalState && !hacker.IsPrisoner && !hacker.IsSlave;
            if (!eligibleColonist)
                return false;

            return !requireReachAndReserve ||
                hacker.CanReserveAndReach(Frame, PathEndMode.Touch, Danger.Deadly);
        }

        public bool TryHack(Pawn hacker)
        {
            if (!CanHack(hacker, false))
                return false;

            Frame.Cockpit.StopFrame();
            Lord hostileLord = Frame.GetLord();
            if (hostileLord != null)
                hostileLord.RemovePawn(Frame);
            Frame.SetFaction(Faction.OfPlayer);
            Frame.Cockpit.StopFrame();
            return Frame.Faction == Faction.OfPlayer &&
                !Frame.Cockpit.HasPilot && !Frame.Drafted;
        }
    }

    public sealed class JobDriver_HackFrame : JobDriver
    {
        private FramePawn TargetFrame => job.targetA.Thing as FramePawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => TargetFrame?.Hacking?.CanHack(pawn, false) != true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(CompFrameHacking.WorkTicks)
                .WithProgressBarToilDelay(TargetIndex.A);

            Toil complete = ToilMaker.MakeToil("HackFrame");
            complete.initAction = () =>
            {
                if (TargetFrame?.Hacking?.TryHack(pawn) != true)
                    EndJobWith(JobCondition.Incompletable);
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }

    // RimWorld 1.6 discovers FloatMenuOptionProvider subclasses automatically.
    public sealed class FloatMenuOptionProvider_HackFrame : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!(clickedPawn is FramePawn frame))
                return null;

            Pawn hacker = context.FirstSelectedPawn;
            if (frame.Hacking?.CanHack(hacker, true) != true)
                return null;

            return new FloatMenuOption("Hack Frame", () =>
            {
                hacker.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(ZenithJobDefOf.ZF_HackFrame, frame));
            });
        }
    }
}
