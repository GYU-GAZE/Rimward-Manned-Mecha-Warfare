using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameAddonCategory
    {
        Weapon,
        Utility,
        Power
    }

    public sealed class FrameAddonExtension : DefModExtension
    {
        public FrameAddonCategory category;
    }

    public sealed class CompProperties_FrameLoadout : CompProperties
    {
        public int weaponCapacity = 1;
        public ThingDef defaultWeapon;
        public List<ThingDef> developmentWeapons = new List<ThingDef>();

        public CompProperties_FrameLoadout() { compClass = typeof(CompFrameLoadout); }
    }

    // Loadout policy lives here, while Pawn_EquipmentTracker remains the only
    // physical owner and scribe path for the installed weapon Thing.
    public sealed class CompFrameLoadout : ThingComp
    {
        private bool defaultInitialized;
        private ThingWithComps rememberedDroppedWeapon;

        public FramePawn Frame => (FramePawn)parent;
        public CompProperties_FrameLoadout Props => (CompProperties_FrameLoadout)props;
        public ThingWithComps InstalledWeapon => Frame.equipment?.Primary;
        public ThingWithComps RememberedDroppedWeapon =>
            Frame.mindState?.droppedWeapon as ThingWithComps ?? rememberedDroppedWeapon;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref defaultInitialized, "defaultLoadoutInitialized");
            Scribe_References.Look(ref rememberedDroppedWeapon, "rememberedDroppedFrameWeapon");
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (defaultInitialized) return;
            defaultInitialized = true;

            // Loading an older or deliberately weaponless save must never
            // manufacture a replacement merely because Primary is empty.
            if (respawningAfterLoad || InstalledWeapon != null || Props.defaultWeapon == null) return;
            ThingWithComps weapon = ThingMaker.MakeThing(Props.defaultWeapon) as ThingWithComps;
            if (weapon == null || !TryInstallWeapon(weapon))
            {
                if (weapon != null && !weapon.Destroyed) weapon.Destroy();
                Log.Error("ZENITH: Frame default weapon could not be initialized through loadout policy.");
            }
        }

        public bool CanInstallWeapon(ThingWithComps candidate, out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed)
                reason = "Frame is not a living chassis.";
            else if (Frame.equipment == null || Props.weaponCapacity < 1)
                reason = "Chassis has no Weapon capacity.";
            else if (InstalledWeapon != null)
                reason = "Weapon slot is occupied.";
            else if (candidate == null || candidate.Destroyed)
                reason = "Weapon item is invalid.";
            else if (candidate.def.GetModExtension<FrameAddonExtension>()?.category != FrameAddonCategory.Weapon)
                reason = "Item is not a Zenith Frame Weapon addon.";
            else if (!candidate.Spawned && candidate.holdingOwner != null)
                reason = "Weapon is already owned by another container.";
            else if (candidate.Spawned && (!Frame.Spawned || candidate.Map != Frame.Map))
                reason = "Weapon is not on the Frame's map.";
            return reason == null;
        }

        public bool TryInstallWeapon(ThingWithComps candidate)
        {
            if (!CanInstallWeapon(candidate, out _)) return false;
            Map map = candidate.Spawned ? candidate.Map : null;
            IntVec3 position = candidate.Spawned ? candidate.Position : IntVec3.Invalid;

            PrepareForWeaponMutation();
            if (candidate.Spawned) candidate.DeSpawn();
            Frame.equipment.AddEquipment(candidate);
            if (ReferenceEquals(InstalledWeapon, candidate))
            {
                if (ReferenceEquals(rememberedDroppedWeapon, candidate)) rememberedDroppedWeapon = null;
                return true;
            }

            if (candidate.holdingOwner == null && !candidate.Destroyed && map != null && position.IsValid)
                GenSpawn.Spawn(candidate, position, map);
            return false;
        }

        public bool CanRemoveWeapon(out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed)
                reason = "Frame is not a living chassis.";
            else if (!Frame.Spawned)
                reason = "Frame must be spawned to remove a physical weapon.";
            else if (InstalledWeapon == null)
                reason = "Weapon slot is empty.";
            return reason == null;
        }

        public bool TryRemoveWeapon(out ThingWithComps removed)
        {
            removed = null;
            if (!CanRemoveWeapon(out _)) return false;
            ThingWithComps exact = InstalledWeapon;
            PrepareForWeaponMutation();
            if (!Frame.equipment.TryDropEquipment(exact, out removed, Frame.Position, false)) return false;
            return ReferenceEquals(removed, exact) && removed.Spawned && InstalledWeapon == null;
        }

        public bool TryReclaimRememberedWeapon()
        {
            if (Frame.Downed || InstalledWeapon != null || !Frame.Spawned) return false;
            ThingWithComps exact = RememberedDroppedWeapon;
            if (exact == null || exact.Destroyed || !exact.Spawned || exact.Map != Frame.Map ||
                !exact.Position.InHorDistOf(Frame.Position, 2f)) return false;
            if (!TryInstallWeapon(exact)) return false;
            if (ReferenceEquals(Frame.mindState.droppedWeapon, exact)) Frame.mindState.droppedWeapon = null;
            if (ReferenceEquals(rememberedDroppedWeapon, exact)) rememberedDroppedWeapon = null;
            return true;
        }

        internal void RememberDroppedWeapon(ThingWithComps exact)
        {
            if (exact != null && !exact.Destroyed && !ReferenceEquals(InstalledWeapon, exact))
                rememberedDroppedWeapon = exact;
        }

        private void PrepareForWeaponMutation()
        {
            Frame.Cockpit?.StopFrame();
        }

        public override string CompInspectStringExtra() =>
            "Weapon: " + (InstalledWeapon?.LabelCap ?? "None");

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || Frame.Faction != Faction.OfPlayer || !Frame.Spawned) yield break;

            var remove = new Command_Action
            {
                defaultLabel = "DEV: Remove weapon",
                defaultDesc = "Remove the exact installed Frame weapon to the map through the real loadout API.",
                icon = TexCommand.Install,
                action = () => TryRemoveWeapon(out _)
            };
            if (!CanRemoveWeapon(out string removeReason)) remove.Disable(removeReason);
            yield return remove;

            foreach (ThingDef weaponDef in Props.developmentWeapons)
            {
                ThingDef exactDef = weaponDef;
                var install = new Command_Action
                {
                    defaultLabel = "DEV: Install " + exactDef.label,
                    defaultDesc = "Create one development item and install it through the real physical loadout API.",
                    icon = TexCommand.Install,
                    action = () =>
                    {
                        ThingWithComps weapon = ThingMaker.MakeThing(exactDef) as ThingWithComps;
                        if (weapon != null && TryInstallWeapon(weapon)) return;
                        if (weapon != null && !weapon.Destroyed) weapon.Destroy();
                        Messages.Message("Weapon could not be installed.", Frame, MessageTypeDefOf.RejectInput, false);
                    }
                };
                if (InstalledWeapon != null) install.Disable("Weapon slot is occupied.");
                yield return install;
            }
        }
    }

    // RimWorld's humanlike downing path remembers Primary in mindState, but
    // the custom non-humanlike Frame classification drops it without keeping
    // that reference. Capture the exact pre-downing object and retain only a
    // shallow reference after vanilla has performed the physical transfer.
    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    internal static class Patch_PawnHealthTracker_MakeDowned_FrameWeapon
    {
        private static void Prefix(Pawn ___pawn, ref ThingWithComps __state)
        {
            if (___pawn is FramePawn frame) __state = frame.Loadout?.InstalledWeapon;
        }

        private static void Postfix(Pawn ___pawn, ThingWithComps __state)
        {
            if (___pawn is FramePawn frame && __state != null && !ReferenceEquals(frame.Loadout?.InstalledWeapon, __state))
                frame.Loadout?.RememberDroppedWeapon(__state);
        }
    }
}
