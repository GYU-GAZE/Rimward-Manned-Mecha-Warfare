using System.Collections.Generic;
using RimWorld;
using Verse;

namespace ZenithFrame
{
    // Exceptional off-map rescue owner. Never hand a live pilot to world-pawn garbage collection.
    public sealed class PilotRecovery : GameComponent, IThingHolder
    {
        private ThingOwner<Pawn> rescued;
        public PilotRecovery(Game game) { rescued = new ThingOwner<Pawn>(this, false, LookMode.Deep); }
        public IThingHolder ParentHolder => null;
        public ThingOwner GetDirectlyHeldThings() => rescued;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, rescued);
        public bool Accept(Pawn pawn) => rescued.TryAddOrTransfer(pawn, false);
        public override void ExposeData() { Scribe_Deep.Look(ref rescued, "zenithRescuedPilots", this); }
        public override void GameComponentTick()
        {
            // Vanilla's game-over check recognizes only a hard-coded set of pod owners.
            // Its public countdown flag can be cleared before the 400-tick expiry;
            // no patch or invented map/position for the pilot is necessary.
            if (Find.GameEnder.gameEnding && HasLivingColonistPilot()) Find.GameEnder.gameEnding = false;
            if (rescued.Count == 0 || Find.TickManager.TicksGame % 60 != 0) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            foreach (Pawn pawn in new List<Pawn>(rescued))
            {
                if (rescued.TryDrop(pawn, map.Center, map, ThingPlaceMode.Near, out Pawn result))
                    Messages.Message("ZENITH: recovered pilot " + result.LabelShortCap + " from an off-map Frame.", result, MessageTypeDefOf.NeutralEvent);
            }
        }
        private bool HasLivingColonistPilot()
        {
            foreach (Pawn pawn in rescued) if (!pawn.Dead && pawn.IsFreeColonist) return true;
            foreach (Map map in Find.Maps)
                foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
                    if (pawn is FramePawn frame && frame.Cockpit.Pilot != null && !frame.Cockpit.Pilot.Dead && frame.Cockpit.Pilot.IsFreeColonist) return true;
            return false;
        }
    }
}
