using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public sealed class CompProperties_FrameLivery : CompProperties
    {
        public CompProperties_FrameLivery() { compClass = typeof(CompFrameLivery); }
    }

    // Paint belongs to the physical chassis, never to a pilot or an installed module.
    public sealed class CompFrameLivery : ThingComp
    {
        private Color primary = Color.white;
        private Color secondary = Color.white;

        public Color Primary => primary;
        public Color Secondary => secondary;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref primary, "zenithPaintPrimary", Color.white);
            Scribe_Values.Look(ref secondary, "zenithPaintSecondary", Color.white);
        }

        public void SetColors(Color newPrimary, Color newSecondary)
        {
            // GraphicDatabase and MaterialPool quantize colors to Color32 too.
            newPrimary = (Color32)newPrimary;
            newSecondary = (Color32)newSecondary;
            if (((Color32)primary).Equals((Color32)newPrimary) &&
                ((Color32)secondary).Equals((Color32)newSecondary))
                return;
            primary = newPrimary;
            secondary = newSecondary;
            ((FramePawn)parent).Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            FramePawn frame = (FramePawn)parent;
            if (!Prefs.DevMode || frame.Faction != Faction.OfPlayer || !frame.Spawned || frame.Dead)
                yield break;

            yield return new Command_Action
            {
                defaultLabel = "DEV: Cycle Frame livery",
                defaultDesc = "White, red/cyan, blue/yellow. Development-only paint proof on this exact chassis.",
                icon = TexCommand.Install,
                action = () =>
                {
                    if (primary.r > 0.95f && primary.g > 0.95f)
                        SetColors(new Color32(230, 40, 55, 255), new Color32(40, 210, 225, 255));
                    else if (primary.r > 0.75f && primary.g < 0.3f)
                        SetColors(new Color32(50, 90, 230, 255), new Color32(245, 185, 35, 255));
                    else
                        SetColors(Color.white, Color.white);
                }
            };
        }
    }
}
