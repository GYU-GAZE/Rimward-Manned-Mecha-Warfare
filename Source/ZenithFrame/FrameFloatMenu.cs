using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    // RimWorld 1.6 discovers FloatMenuOptionProvider subclasses automatically.
    // Boarding belongs to the pilot's ordinary right-click order flow.
    public sealed class FloatMenuOptionProvider_BoardFrame : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!(clickedPawn is FramePawn frame)) return null;

            Pawn pilot = context.FirstSelectedPawn;
            if (pilot == null || !frame.Cockpit.CanBoard(pilot, false)) return null;

            if (!pilot.CanReach(frame, PathEndMode.Touch, Danger.Deadly))
                return new FloatMenuOption("Board Frame: Cannot reach", null);

            return new FloatMenuOption("Board Frame", () =>
            {
                pilot.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ZF_BoardFrame"), frame));
            });
        }
    }
}
