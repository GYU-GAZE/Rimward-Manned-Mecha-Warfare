using HarmonyLib;
using Verse;

namespace ZenithFrame
{
    // Any ordinary shooting verb hosted by the physical Frame trains its exact
    // pilot, including compatible externally-authored weapon verbs.
    [HarmonyPatch(typeof(Verb_Shoot), nameof(Verb_Shoot.WarmupComplete))]
    internal static class Patch_FrameShoot_PilotExperience
    {
        private static void Postfix(Verb_Shoot __instance) => FramePilotCombatExperience.LearnShooting(__instance);
    }
}
