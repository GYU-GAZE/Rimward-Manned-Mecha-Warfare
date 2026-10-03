using RimWorld;
using Verse;

namespace ZenithFrame
{
    // The vanilla Ability object, tracker, cooldown and effect remain owned by
    // the Frame Pawn. This subclass adds only Zenith's operator invariant.
    public sealed class Ability_FrameOverdrive : Ability
    {
        public Ability_FrameOverdrive() { }
        public Ability_FrameOverdrive(Pawn pawn) : base(pawn) { }
        public Ability_FrameOverdrive(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_FrameOverdrive(Pawn pawn, Precept sourcePrecept, AbilityDef def)
            : base(pawn, sourcePrecept, def) { }

        public override AcceptanceReport CanCast
        {
            get
            {
                AcceptanceReport vanilla = base.CanCast;
                if (!vanilla.Accepted) return vanilla;
                if (!(pawn is FramePawn frame) || frame.Cockpit?.CanOperate != true || frame.Service?.IsServiceLocked == true)
                    return "Frame Overdrive requires a valid operating pilot.";
                return true;
            }
        }
    }
}
