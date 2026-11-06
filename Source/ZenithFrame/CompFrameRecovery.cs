using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace ZenithFrame
{
    public sealed class CompProperties_FrameRecovery : CompProperties
    {
        public CompProperties_FrameRecovery() { compClass = typeof(CompFrameRecovery); }
    }

    // This component proves the disabled-chassis lifecycle. It deliberately has
    // no repair cost or persistent recovery flag: the real Pawn health state is
    // authoritative, and the command is available in development mode only.
    public sealed class CompFrameRecovery : ThingComp
    {
        public FramePawn Frame => (FramePawn)parent;

        public bool IsRecoverablyDisabled =>
            Frame.Spawned && Frame.Downed && !Frame.Dead && !Frame.Destroyed &&
            Frame.Cockpit != null && !Frame.Cockpit.HasPilot;

        public bool CanRecover => IsRecoverablyDisabled;

        public bool TryRecover()
        {
            if (!CanRecover) return false;

            Frame.Cockpit.StopFrame();

            // This development recovery path may clear ordinary damage but it
            // never reconstructs destroyed mechanical structure. The service
            // milestone owns material-based part reconstruction.
            var partOrder = Frame.RaceProps.body.AllParts;
            if (Frame.Downed)
            {
                List<Hediff_Injury> injuries = Frame.health.hediffSet.hediffs
                    .OfType<Hediff_Injury>()
                    .OrderByDescending(injury => injury.Severity)
                    .ThenBy(injury => injury.Part == null ? int.MaxValue : partOrder.IndexOf(injury.Part))
                    .ToList();
                foreach (Hediff_Injury injury in injuries)
                {
                    Frame.health.RemoveHediff(injury);
                    if (!Frame.Downed) break;
                }
            }

            Frame.Loadout?.TryReclaimRememberedWeapon();
            Frame.Cockpit.StopFrame();
            return !Frame.Downed && !Frame.Dead && !Frame.Destroyed;
        }

        public override string CompInspectStringExtra()
        {
            return IsRecoverablyDisabled ? "ZF_DisabledRecoverable".Translate() : null;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || Frame.Faction != Faction.OfPlayer || Frame.Dead || Frame.Destroyed)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: Damage Frame part",
                defaultDesc = "Apply deterministic non-propagating damage to one exact Zenith mechanical body part.",
                icon = TexCommand.Attack,
                action = () => Find.WindowStack.Add(new FloatMenu(FrameDamageRules.DevelopmentTargets(Frame)
                    .Where(part => !Frame.health.hediffSet.PartIsMissing(part))
                    .Select(part => new FloatMenuOption(part.LabelCap, () =>
                        FrameDamageRules.ApplyDeterministicDamage(Frame, part, part.def.GetMaxHealth(Frame) + 1f)))
                    .ToList()))
            };

            if (!IsRecoverablyDisabled) yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: Recover disabled chassis",
                defaultDesc = "Development-only lifecycle proof. Repairs only enough damage to restore mobility; no final repair economy is implemented.",
                icon = TexCommand.Install,
                action = () =>
                {
                    if (TryRecover())
                        Messages.Message("ZF_RecoveryComplete".Translate(), Frame, MessageTypeDefOf.PositiveEvent, false);
                    else
                        Messages.Message("ZF_RecoveryUnavailable".Translate(), Frame, MessageTypeDefOf.RejectInput, false);
                }
            };
        }
    }
}
