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

    public sealed class FloatMenuOptionProvider_FrameDisembark : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool CanSelfTarget => true;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!(context.FirstSelectedPawn is FramePawn frame) || !ReferenceEquals(clickedPawn, frame) ||
                frame.Faction != Faction.OfPlayer || !frame.Spawned || !frame.Cockpit.HasPilot ||
                frame.Service?.IsServiceLocked == true) return null;
            return new FloatMenuOption("Disembark", () =>
            {
                if (!frame.Cockpit.TryDisembark())
                    Messages.Message("No safe exit available.", MessageTypeDefOf.RejectInput, false);
            });
        }
    }

    // WorkGivers is deliberately NOT allowed for Frames: it exposes every
    // colonist work order and requires a colonist work think tree. This provider
    // creates only an explicit vanilla storage-haul job for the clicked Thing.
    public sealed class FloatMenuOptionProvider_FrameHaul : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(context.FirstSelectedPawn is FramePawn frame) || !frame.Cockpit.CanTakePlayerOrders ||
                clickedThing is Pawn || clickedThing is Corpse || clickedThing.def.category != ThingCategory.Item ||
                !clickedThing.def.EverHaulable || clickedThing.IsForbidden(frame) ||
                !HaulAIUtility.PawnCanAutomaticallyHaulFast(frame, clickedThing, true)) return null;
            Job candidate = HaulAIUtility.HaulToStorageJob(frame, clickedThing, true);
            if (candidate == null) return null;
            return new FloatMenuOption("Haul " + clickedThing.LabelCap, () =>
            {
                if (!frame.Cockpit.CanTakePlayerOrders || !clickedThing.Spawned) return;
                Job exact = HaulAIUtility.HaulToStorageJob(frame, clickedThing, true);
                if (exact != null) { exact.playerForced = true; frame.jobs.TryTakeOrderedJob(exact, JobTag.Misc); }
            });
        }
    }
}
