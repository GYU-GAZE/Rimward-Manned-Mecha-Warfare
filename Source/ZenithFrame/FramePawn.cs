using Verse;
using UnityEngine;

namespace ZenithFrame
{
    public sealed partial class FramePawn : Pawn
    {
        public CompPilotableMech Cockpit => GetComp<CompPilotableMech>();
        public CompFrameLoadout Loadout => GetComp<CompFrameLoadout>();
        public CompFrameRecovery Recovery => GetComp<CompFrameRecovery>();
        public CompFrameHacking Hacking => GetComp<CompFrameHacking>();
        public override void Kill(DamageInfo? dinfo, Hediff exactCulprit = null)
        {
            Cockpit?.EmergencyEject();
            base.Kill(dinfo, exactCulprit);
        }
        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Cockpit?.EmergencyEject();
            base.Destroy(mode);
        }
        public override void ExitMap(bool allowedToJoinOrCreateCaravan, Rot4 exitDir)
        {
            base.ExitMap(allowedToJoinOrCreateCaravan, exitDir);
            Pawn pilot = Cockpit?.Pilot;
            if (Find.WorldPawns.Contains(this) && pilot?.Faction == RimWorld.Faction.OfPlayer &&
                pilot.RaceProps.Humanlike && !pilot.IsPrisoner && !pilot.IsSlave)
                Find.WorldPawns.ForcefullyKeptPawns.Add(this);
            Cockpit?.Notify_ExitedMap();
        }
        public override void DrawGUIOverlay()
        {
            base.DrawGUIOverlay();
            if (!Spawned || Map.fogGrid.IsFogged(Position)) return;
            Vector2 position = GenMapUI.LabelDrawPosFor(this, -0.6f);
            GenMapUI.DrawThingLabel(position, LabelShort, GenMapUI.DefaultThingLabelColor);
        }
    }
}
