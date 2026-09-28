using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    public sealed class CompProperties_PilotableMech : CompProperties
    {
        public CompProperties_PilotableMech() { compClass = typeof(CompPilotableMech); }
    }

    public sealed class CompPilotableMech : ThingComp, IThingHolder
    {
        private ThingOwner<Pawn> pilots;
        public CompPilotableMech() { pilots = new ThingOwner<Pawn>(this, oneStackOnly: true, contentsLookMode: LookMode.Deep); }
        public Pawn Frame => (Pawn)parent;
        public Pawn Pilot => pilots.Count == 0 ? null : pilots[0];
        public bool HasPilot => pilots.Count != 0;
        public bool CanOperate => PilotRules.CanOperate(HasPilot,
            Frame.Spawned && !Frame.Dead && !Frame.Downed && !Frame.Destroyed && !Frame.InMentalState,
            Pilot != null && !Pilot.Dead && !Pilot.Downed && !Pilot.InMentalState,
            Pilot != null && Frame.Faction == Faction.OfPlayer && Pilot.Faction == Frame.Faction);
        public ThingOwner GetDirectlyHeldThings() => pilots;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, pilots);

        public bool CanBoard(Pawn pawn, bool requireAdjacent = true)
        {
            return pawn != null && pawn != Frame && !(pawn is FramePawn) && PilotRules.CanBoard(HasPilot,
                Frame.Spawned && !Frame.Dead && !Frame.Downed && !Frame.Destroyed,
                pawn.Spawned && pawn.RaceProps.Humanlike && pawn.IsColonistPlayerControlled && !pawn.Dead && !pawn.Downed && !pawn.InMentalState && !pawn.IsPrisoner && !pawn.IsSlave && pawn.carryTracker.CarriedThing == null,
                pawn.Faction == Frame.Faction && Frame.Faction == Faction.OfPlayer,
                pawn.Map == Frame.Map, pawn.holdingOwner != null,
                !requireAdjacent || pawn.Position.AdjacentTo8WayOrInside(Frame.Position));
        }

        public bool TryBoard(Pawn pawn)
        {
            if (!CanBoard(pawn)) return false;
            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            Rot4 rotation = pawn.Rotation;
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            pawn.pather.StopDead();
            pawn.DeSpawn();
            if (!pilots.TryAdd(pawn, false))
            {
                GenSpawn.Spawn(pawn, position, map, rotation);
                return false;
            }
            return true;
        }

        public bool TryDisembark()
        {
            if (!HasPilot || !Frame.Spawned) return false;
            if (!pilots.TryDrop(Pilot, Frame.Position, Frame.Map, ThingPlaceMode.Near, out Pawn _)) return false;
            StopFrame();
            return true;
        }

        public void StopFrame()
        {
            if (Frame.drafter != null) Frame.drafter.Drafted = false;
            Frame.pather?.StopDead();
            Frame.jobs?.StopAll();
            Frame.stances?.CancelBusyStanceHard();
            Frame.equipment?.PrimaryEq?.PrimaryVerb.Reset();
        }

        public void EmergencyEject()
        {
            if (!HasPilot) return;
            StopFrame();
            if (TryDisembark()) return;
            // Off-map destruction has no spawn destination. Transfer to an independent,
            // deep-scribed game owner BEFORE the Frame's destruction is allowed to proceed.
            var recovery = Current.Game?.GetComponent<PilotRecovery>();
            if (recovery == null || !recovery.Accept(Pilot))
                throw new System.InvalidOperationException("ZENITH: cannot detach pilot safely; aborting Frame destruction.");
        }

        public override void Notify_Downed() { EmergencyEject(); }
        public override void PostExposeData() { base.PostExposeData(); Scribe_Deep.Look(ref pilots, "zenithPilot", this); }
        public override string CompInspectStringExtra() => HasPilot ? "Pilot: " + Pilot.LabelShortCap : "No pilot - Frame inactive";
    }
}
