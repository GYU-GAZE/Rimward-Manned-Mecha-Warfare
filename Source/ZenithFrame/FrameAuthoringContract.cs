using System.Collections.Generic;
using System.Linq;
using Verse;

namespace ZenithFrame
{
    public static class FrameAuthoringContract
    {
        private static readonly string[] Anchors =
            { "ZF_FrameTorso", "ZF_FrameCore", "ZF_FrameCockpit", "ZF_FrameHeadAssembly" };

        public static IEnumerable<string> Errors(ThingDef chassis)
        {
            BodyDef body = chassis?.race?.body;
            if (body == null) { yield return "ZENITH chassis must declare a BodyDef."; yield break; }
            foreach (string anchor in Anchors)
                if (body.AllParts.Count(part => part.def.defName == anchor) != 1)
                    yield return "ZENITH BodyDef requires exactly one structural anchor: " + anchor;
            if (body.corePart?.def?.defName != "ZF_FrameTorso")
                yield return "ZENITH BodyDef corePart must be the structural Torso.";
            if (chassis.GetCompProperties<CompProperties_PilotableMech>() == null)
                yield return "ZENITH chassis must have the cockpit identity comp.";
            FrameChassisVisualExtension visual = chassis.GetModExtension<FrameChassisVisualExtension>();
            if (visual != null && string.IsNullOrEmpty(visual.visualFamily))
                yield return "ZENITH modular chassis visuals require a visualFamily.";
            if (visual != null && (visual.worldDrawSize <= 0f || visual.portraitZoom <= 0f))
                yield return "ZENITH chassis draw size and portrait zoom must be positive.";
            CompProperties_FrameLoadout slots = chassis.GetCompProperties<CompProperties_FrameLoadout>();
            if (slots == null) yield break;
            foreach (ThingDef module in new[] { slots.defaultWeapon, slots.defaultUtility, slots.defaultPower,
                slots.defaultHead, slots.defaultShoulder, slots.defaultOffhand }.Where(def => def != null))
            {
                FrameAddonExtension addon = module.GetModExtension<FrameAddonExtension>();
                if (addon == null) { yield return "ZENITH default module is not marked: " + module.defName; continue; }
                if (!FrameAuthoringPolicy.AllowsFamily(visual?.visualFamily, addon.allowedChassisFamilies,
                    module.GetModExtension<FrameModuleVisualExtension>() != null))
                    yield return "ZENITH default module visual family mismatch: " + module.defName;
                if (addon.mountGroup != null && !body.AllParts.Any(part => part.groups.Contains(addon.mountGroup)))
                    yield return "ZENITH BodyDef has no default module mount: " + addon.mountGroup.defName;
            }
        }
    }
}
