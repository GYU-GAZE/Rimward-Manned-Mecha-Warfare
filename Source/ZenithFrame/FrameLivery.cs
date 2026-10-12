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
        private Color visor = Color.white;

        public Color Primary => primary;
        public Color Secondary => secondary;
        public Color Visor => visor;
        public FrameChassisVisualExtension Defaults => parent.def.GetModExtension<FrameChassisVisualExtension>();

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref primary, "zenithPaintPrimary", Defaults?.defaultPrimary ?? Color.white);
            Scribe_Values.Look(ref secondary, "zenithPaintSecondary", Defaults?.defaultSecondary ?? Color.white);
            Scribe_Values.Look(ref visor, "zenithPaintVisor", Defaults?.defaultVisor ?? Color.white);
        }

        public void SetColors(Color newPrimary, Color newSecondary) => SetColors(newPrimary, newSecondary, visor);

        public void ResetColors() => SetColors(Defaults?.defaultPrimary ?? Color.white,
            Defaults?.defaultSecondary ?? Color.white, Defaults?.defaultVisor ?? Color.white);

        public void SetColors(Color newPrimary, Color newSecondary, Color newVisor)
        {
            newPrimary = (Color32)newPrimary;
            newSecondary = (Color32)newSecondary;
            newVisor = (Color32)newVisor;
            if (((Color32)primary).Equals((Color32)newPrimary) &&
                ((Color32)secondary).Equals((Color32)newSecondary) &&
                ((Color32)visor).Equals((Color32)newVisor)) return;
            primary = newPrimary;
            secondary = newSecondary;
            visor = newVisor;
            ((FramePawn)parent).Drawer?.renderer?.SetAllGraphicsDirty();
        }

    }
}
