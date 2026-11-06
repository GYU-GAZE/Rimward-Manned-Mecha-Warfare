using Verse;

namespace ZenithFrame
{
    // The cockpit comp is the explicit machine marker. The subclass retains
    // lifecycle overrides; consumers identify machines through this contract.
    public static class FrameIdentity
    {
        public static CompPilotableMech Cockpit(Pawn pawn) => pawn?.GetComp<CompPilotableMech>();
        public static bool IsFrame(Pawn pawn) => Cockpit(pawn) != null;
        public static bool TryGet(Pawn pawn, out FramePawn frame)
        {
            frame = Cockpit(pawn)?.Frame as FramePawn;
            return frame != null;
        }
        public static bool IsContainedPilot(Pawn pawn) => pawn != null && !pawn.Spawned &&
            pawn.holdingOwner?.Owner is CompPilotableMech cockpit && ReferenceEquals(cockpit.Pilot, pawn);
    }
}
