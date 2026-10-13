using RimWorld;
using System.Linq;
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
                return new FloatMenuOption("ZF_BoardNoPath".Translate(), null);

            return new FloatMenuOption("ZF_BoardFrame".Translate(), () =>
            {
                pilot.jobs.TryTakeOrderedJob(
                    JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ZF_BoardFrame"), frame));
            });
        }
    }

    public sealed class FloatMenuOptionProvider_FrameDisembark : FloatMenuOptionProvider, IFrameOrderProvider
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
            return new FloatMenuOption("ZF_Disembark".Translate(), () =>
            {
                if (!frame.Cockpit.TryDisembark())
                    Messages.Message("ZF_NoSafeExit".Translate(), MessageTypeDefOf.RejectInput, false);
            });
        }
    }

    public sealed class FloatMenuOptionProvider_FramePilotSelfTend : FloatMenuOptionProvider, IFrameOrderProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override bool CanSelfTarget => true;

        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!(context.FirstSelectedPawn is FramePawn frame) || !ReferenceEquals(frame, clickedPawn) ||
                frame.Faction != Faction.OfPlayer || !frame.Spawned || !frame.Cockpit.CanTakePlayerOrders)
                return null;
            Pawn pilot = frame.Cockpit.Pilot;
            if (pilot?.health?.HasHediffsNeedingTend() != true) return null;
            Medicine medicine = CompPilotableMech.BestPilotMedicine(pilot);
            return new FloatMenuOption("ZF_PilotSelfTend".Translate(), () =>
                CompPilotableMech.StartPilotSelfTend(frame, pilot, medicine));
        }
    }

    public sealed class FloatMenuOptionProvider_DockFrame : FloatMenuOptionProvider, IFrameOrderProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;

        protected override FloatMenuOption GetSingleOptionFor(Thing clickedThing, FloatMenuContext context)
        {
            if (!(clickedThing is Building bay) || bay.def.defName != DockingBayUtility.DockingBayDefName ||
                !(context.FirstSelectedPawn is FramePawn frame) || !frame.Cockpit.CanTakePlayerOrders ||
                frame.Faction != Faction.OfPlayer || !frame.Spawned || frame.Map != bay.Map ||
                DockingBayUtility.IsDocked(frame)) return null;
            IntVec3 cell = bay.OccupiedRect().Cells.Where(candidate =>
                candidate.Standable(frame.Map) && frame.CanReach(candidate, PathEndMode.OnCell, Danger.Deadly))
                .DefaultIfEmpty(IntVec3.Invalid).First();
            if (!cell.IsValid) return new FloatMenuOption("ZF_DockNoPath".Translate(), null);
            return new FloatMenuOption("ZF_DockFrame".Translate(), () =>
            {
                if (!frame.Cockpit.CanTakePlayerOrders || !bay.Spawned || bay.Map != frame.Map ||
                    !cell.Standable(frame.Map) || !frame.CanReach(cell, PathEndMode.OnCell, Danger.Deadly)) return;
                Job job = JobMaker.MakeJob(JobDefOf.Goto, cell);
                job.playerForced = true;
                frame.jobs.TryTakeOrderedJob(job);
            });
        }
    }

    // WorkGivers is deliberately NOT allowed for Frames: it exposes every
    // colonist work order and requires a colonist work think tree. This provider
    // creates only an explicit vanilla storage-haul job for the clicked Thing.
    public sealed class FloatMenuOptionProvider_FrameHaul : FloatMenuOptionProvider, IFrameOrderProvider
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
            return new FloatMenuOption("ZF_HaulItem".Translate(clickedThing.LabelCap), () =>
            {
                if (!frame.Cockpit.CanTakePlayerOrders || !clickedThing.Spawned) return;
                Job exact = HaulAIUtility.HaulToStorageJob(frame, clickedThing, true);
                if (exact != null) { exact.playerForced = true; frame.jobs.TryTakeOrderedJob(exact, JobTag.Misc); }
            });
        }
    }
}
