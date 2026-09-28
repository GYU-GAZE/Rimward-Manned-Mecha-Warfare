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
            if (rescued.Count == 0 || Find.TickManager.TicksGame % 60 != 0) return;
            Map map = Find.AnyPlayerHomeMap;
            if (map == null) return;
            foreach (Pawn pawn in new List<Pawn>(rescued))
            {
                if (rescued.TryDrop(pawn, map.Center, map, ThingPlaceMode.Near, out Pawn result))
                    Messages.Message("ZENITH: recovered pilot " + result.LabelShortCap + " from an off-map Frame.", result, MessageTypeDefOf.NeutralEvent);
            }
        }
    }
}
