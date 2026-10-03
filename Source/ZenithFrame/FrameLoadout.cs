using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameAddonCategory { Weapon, Utility, Power }

    public sealed class FrameAddonExtension : DefModExtension
    {
        public FrameAddonCategory category;
        public int powerOutput;
        public int powerDraw;
        public float moveSpeedOffset;
    }

    public sealed class CompProperties_FrameLoadout : CompProperties
    {
        public int weaponCapacity = 1;
        public int utilityCapacity = 1;
        public int powerCapacity = 1;
        public ThingDef defaultWeapon;
        public ThingDef defaultUtility;
        public ThingDef defaultPower;
        public List<ThingDef> developmentWeapons = new List<ThingDef>();
        public List<ThingDef> developmentUtilities = new List<ThingDef>();
        public List<ThingDef> developmentPowerSources = new List<ThingDef>();
        public CompProperties_FrameLoadout() { compClass = typeof(CompFrameLoadout); }
    }

    public sealed class CompFrameLoadout : ThingComp, IThingHolder
    {
        private ThingOwner<Thing> utilities;
        private ThingOwner<Thing> powerSources;
        private PowerHolder powerHolder;
        private bool weaponDefaultInitialized;
        private bool utilityDefaultInitialized;
        private bool powerDefaultInitialized;
        private ThingWithComps rememberedDroppedWeapon;

        public CompFrameLoadout()
        {
            utilities = new ThingOwner<Thing>(this, true, LookMode.Deep);
            powerHolder = new PowerHolder(this);
            powerSources = new ThingOwner<Thing>(powerHolder, true, LookMode.Deep);
        }

        public FramePawn Frame => (FramePawn)parent;
        public CompProperties_FrameLoadout Props => (CompProperties_FrameLoadout)props;
        public ThingWithComps InstalledWeapon => Frame.equipment?.Primary;
        public Thing InstalledUtility => utilities.Count == 0 ? null : utilities[0];
        public Thing InstalledPower => powerSources.Count == 0 ? null : powerSources[0];
        public ThingWithComps RememberedDroppedWeapon => Frame.mindState?.droppedWeapon as ThingWithComps ?? rememberedDroppedWeapon;
        public int AvailableAddonPower => Extension(InstalledPower)?.powerOutput ?? 0;
        public int RequiredAddonPower => Extension(InstalledUtility)?.powerDraw ?? 0;
        public bool HasSufficientAddonPower => AvailableAddonPower >= RequiredAddonPower;
        public bool IsUtilityPowered => InstalledUtility != null && RequiredAddonPower > 0 && HasSufficientAddonPower;
        public ThingOwner GetDirectlyHeldThings() => utilities;
        public void GetChildHolders(List<IThingHolder> children)
        {
            children.Add(powerHolder);
            ThingOwnerUtility.AppendThingHoldersFromThings(children, utilities);
        }

        private sealed class PowerHolder : IThingHolder
        {
            private readonly CompFrameLoadout loadout;
            public PowerHolder(CompFrameLoadout loadout) { this.loadout = loadout; }
            public IThingHolder ParentHolder => loadout;
            public ThingOwner GetDirectlyHeldThings() => loadout.powerSources;
            public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, loadout.powerSources);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            bool legacyWeaponInitialized = false;
            Scribe_Values.Look(ref legacyWeaponInitialized, "defaultLoadoutInitialized");
            Scribe_Values.Look(ref weaponDefaultInitialized, "weaponDefaultInitialized");
            Scribe_Values.Look(ref utilityDefaultInitialized, "utilityDefaultInitialized");
            Scribe_Values.Look(ref powerDefaultInitialized, "powerDefaultInitialized");
            Scribe_Deep.Look(ref utilities, "installedFrameUtility", this);
            if (powerHolder == null) powerHolder = new PowerHolder(this);
            Scribe_Deep.Look(ref powerSources, "installedFramePower", powerHolder);
            Scribe_References.Look(ref rememberedDroppedWeapon, "rememberedDroppedFrameWeapon");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyWeaponInitialized) weaponDefaultInitialized = true;
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeDefaults(respawningAfterLoad);
        }

        private void InitializeDefaults(bool loading)
        {
            if (!weaponDefaultInitialized)
            {
                weaponDefaultInitialized = true;
                if (!loading && InstalledWeapon == null && Props.defaultWeapon != null) InstallDefault(Props.defaultWeapon, FrameAddonCategory.Weapon);
            }
            if (!utilityDefaultInitialized)
            {
                utilityDefaultInitialized = true;
                if (!loading && InstalledUtility == null && Props.defaultUtility != null) InstallDefault(Props.defaultUtility, FrameAddonCategory.Utility);
            }
            if (!powerDefaultInitialized)
            {
                powerDefaultInitialized = true;
                if (!loading && InstalledPower == null && Props.defaultPower != null) InstallDefault(Props.defaultPower, FrameAddonCategory.Power);
            }
        }

        private void InstallDefault(ThingDef def, FrameAddonCategory category)
        {
            Thing item = ThingMaker.MakeThing(def);
            if (InstallByCategory(item, category)) return;
            if (!item.Destroyed) item.Destroy();
            Log.Error("ZENITH: Frame default " + category + " failed loadout installation.");
        }

        public bool CanInstallWeapon(ThingWithComps item, out string reason) => CanInstall(item, FrameAddonCategory.Weapon, InstalledWeapon, Props.weaponCapacity, out reason);
        public bool CanInstallUtility(Thing item, out string reason) => CanInstall(item, FrameAddonCategory.Utility, InstalledUtility, Props.utilityCapacity, out reason);
        public bool CanInstallPower(Thing item, out string reason) => CanInstall(item, FrameAddonCategory.Power, InstalledPower, Props.powerCapacity, out reason);

        private bool CanInstall(Thing item, FrameAddonCategory category, Thing occupied, int capacity, out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed) reason = "Frame is not a living chassis.";
            else if (category == FrameAddonCategory.Weapon && Frame.equipment == null) reason = "Frame has no equipment tracker.";
            else if (capacity < 1) reason = "Chassis has no " + category + " capacity.";
            else if (occupied != null) reason = category + " slot is occupied.";
            else if (item == null || item.Destroyed || item.stackCount != 1) reason = category + " item is invalid.";
            else if (Extension(item)?.category != category) reason = "Item is not a Zenith Frame " + category + " addon.";
            else if (!item.Spawned && item.holdingOwner != null) reason = category + " is already owned by another container.";
            else if (item.Spawned && (!Frame.Spawned || item.Map != Frame.Map)) reason = category + " is not on the Frame's map.";
            return reason == null;
        }

        public bool TryInstallWeapon(ThingWithComps item)
        {
            if (!CanInstallWeapon(item, out _)) return false;
            Map map = item.Spawned ? item.Map : null;
            IntVec3 position = item.Spawned ? item.Position : IntVec3.Invalid;
            Frame.Cockpit?.StopFrame();
            if (item.Spawned) item.DeSpawn();
            Frame.equipment.AddEquipment(item);
            if (ReferenceEquals(InstalledWeapon, item))
            {
                if (ReferenceEquals(rememberedDroppedWeapon, item)) rememberedDroppedWeapon = null;
                return true;
            }
            Restore(item, map, position);
            return false;
        }

        public bool TryInstallUtility(Thing item) => TryInstallContained(item, FrameAddonCategory.Utility, utilities);
        public bool TryInstallPower(Thing item) => TryInstallContained(item, FrameAddonCategory.Power, powerSources);

        private bool TryInstallContained(Thing item, FrameAddonCategory category, ThingOwner<Thing> owner)
        {
            bool valid = category == FrameAddonCategory.Utility ? CanInstallUtility(item, out _) : CanInstallPower(item, out _);
            if (!valid) return false;
            Map map = item.Spawned ? item.Map : null;
            IntVec3 position = item.Spawned ? item.Position : IntVec3.Invalid;
            if (item.Spawned) item.DeSpawn();
            if (owner.TryAdd(item, false)) return true;
            Restore(item, map, position);
            return false;
        }

        private static void Restore(Thing item, Map map, IntVec3 position)
        {
            if (item.holdingOwner == null && !item.Destroyed && map != null && position.IsValid) GenSpawn.Spawn(item, position, map);
        }

        public bool CanRemoveWeapon(out string reason) => CanRemove(InstalledWeapon, FrameAddonCategory.Weapon, out reason);
        public bool CanRemoveUtility(out string reason) => CanRemove(InstalledUtility, FrameAddonCategory.Utility, out reason);
        public bool CanRemovePower(out string reason) => CanRemove(InstalledPower, FrameAddonCategory.Power, out reason);
        private bool CanRemove(Thing item, FrameAddonCategory category, out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed) reason = "Frame is not a living chassis.";
            else if (!Frame.Spawned) reason = "Frame must be spawned to remove a physical addon.";
            else if (item == null) reason = category + " slot is empty.";
            return reason == null;
        }

        public bool TryRemoveWeapon(out ThingWithComps removed)
        {
            removed = null;
            if (!CanRemoveWeapon(out _)) return false;
            ThingWithComps exact = InstalledWeapon;
            Frame.Cockpit?.StopFrame();
            if (!Frame.equipment.TryDropEquipment(exact, out removed, Frame.Position, false)) return false;
            return ReferenceEquals(removed, exact) && removed.Spawned && InstalledWeapon == null;
        }

        public bool TryRemoveUtility(out Thing removed) => TryRemoveContained(utilities, InstalledUtility, out removed);
        public bool TryRemovePower(out Thing removed) => TryRemoveContained(powerSources, InstalledPower, out removed);
        private bool TryRemoveContained(ThingOwner<Thing> owner, Thing exact, out Thing removed)
        {
            removed = null;
            return exact != null && Frame.Spawned && owner.TryDrop(exact, Frame.Position, Frame.Map, ThingPlaceMode.Near, out removed) && ReferenceEquals(removed, exact) && removed.Spawned;
        }

        public bool TryReclaimRememberedWeapon()
        {
            if (Frame.Downed || InstalledWeapon != null || !Frame.Spawned) return false;
            ThingWithComps exact = RememberedDroppedWeapon;
            if (exact == null || exact.Destroyed || !exact.Spawned || exact.Map != Frame.Map || !exact.Position.InHorDistOf(Frame.Position, 2f) || !TryInstallWeapon(exact)) return false;
            if (ReferenceEquals(Frame.mindState.droppedWeapon, exact)) Frame.mindState.droppedWeapon = null;
            if (ReferenceEquals(rememberedDroppedWeapon, exact)) rememberedDroppedWeapon = null;
            return true;
        }

        internal void RememberDroppedWeapon(ThingWithComps exact)
        {
            if (exact != null && !exact.Destroyed && !ReferenceEquals(InstalledWeapon, exact)) rememberedDroppedWeapon = exact;
        }

        internal void PrepareForFrameDestruction()
        {
            FinalizeAddon(utilities);
            FinalizeAddon(powerSources);
        }

        private void FinalizeAddon(ThingOwner<Thing> owner)
        {
            foreach (Thing exact in owner.ToList())
            {
                if (Frame.Spawned && owner.TryDrop(exact, Frame.Position, Frame.Map, ThingPlaceMode.Near, out Thing _)) continue;
                if (owner.Remove(exact) && !exact.Destroyed) exact.Destroy();
            }
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (stat != StatDefOf.MoveSpeed || !IsUtilityPowered) return 0f;
            return Extension(InstalledUtility)?.moveSpeedOffset ?? 0f;
        }

        public override string CompInspectStringExtra()
        {
            string utility = InstalledUtility?.LabelCap ?? "None";
            if (InstalledUtility != null) utility += IsUtilityPowered ? " [Powered]" : " [Unpowered]";
            return "Weapon: " + (InstalledWeapon?.LabelCap ?? "None") + "\nUtility: " + utility +
                "\nPower: " + (InstalledPower?.LabelCap ?? "None") + "\nAddon power: " + RequiredAddonPower + " required / " + AvailableAddonPower + " available";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || Frame.Faction != Faction.OfPlayer || !Frame.Spawned) yield break;
            yield return RemoveCommand("weapon", () => TryRemoveWeapon(out _), CanRemoveWeapon);
            foreach (ThingDef def in Props.developmentWeapons) yield return InstallCommand(def, FrameAddonCategory.Weapon);
            yield return RemoveCommand("utility", () => TryRemoveUtility(out _), CanRemoveUtility);
            foreach (ThingDef def in Props.developmentUtilities) yield return InstallCommand(def, FrameAddonCategory.Utility);
            yield return RemoveCommand("power source", () => TryRemovePower(out _), CanRemovePower);
            foreach (ThingDef def in Props.developmentPowerSources) yield return InstallCommand(def, FrameAddonCategory.Power);
        }

        private delegate bool RemoveEligibility(out string reason);
        private Command_Action RemoveCommand(string label, System.Action action, RemoveEligibility eligibility)
        {
            var command = new Command_Action { defaultLabel = "DEV: Remove " + label, defaultDesc = "Remove the exact installed Frame addon through the physical loadout API.", icon = TexCommand.Install, action = action };
            if (!eligibility(out string reason)) command.Disable(reason);
            return command;
        }

        private Command_Action InstallCommand(ThingDef def, FrameAddonCategory category)
        {
            var command = new Command_Action { defaultLabel = "DEV: Install " + def.label, defaultDesc = "Create one development item and install it through the physical loadout API.", icon = TexCommand.Install,
                action = () => { Thing item = ThingMaker.MakeThing(def); if (InstallByCategory(item, category)) return; if (!item.Destroyed) item.Destroy(); Messages.Message(category + " could not be installed.", Frame, MessageTypeDefOf.RejectInput, false); } };
            bool occupied = category == FrameAddonCategory.Weapon ? InstalledWeapon != null : category == FrameAddonCategory.Utility ? InstalledUtility != null : InstalledPower != null;
            if (occupied) command.Disable(category + " slot is occupied.");
            return command;
        }

        private bool InstallByCategory(Thing item, FrameAddonCategory category) => category == FrameAddonCategory.Weapon
            ? item is ThingWithComps weapon && TryInstallWeapon(weapon)
            : category == FrameAddonCategory.Utility ? TryInstallUtility(item) : TryInstallPower(item);
        private static FrameAddonExtension Extension(Thing item) => item?.def?.GetModExtension<FrameAddonExtension>();
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    internal static class Patch_PawnHealthTracker_MakeDowned_FrameWeapon
    {
        private static void Prefix(Pawn ___pawn, ref ThingWithComps __state) { if (___pawn is FramePawn frame) __state = frame.Loadout?.InstalledWeapon; }
        private static void Postfix(Pawn ___pawn, ThingWithComps __state)
        {
            if (___pawn is FramePawn frame && __state != null && !ReferenceEquals(frame.Loadout?.InstalledWeapon, __state)) frame.Loadout?.RememberDroppedWeapon(__state);
            if (___pawn?.holdingOwner?.Owner is CompPilotableMech cockpit && ReferenceEquals(cockpit.Pilot, ___pawn))
                cockpit.EmergencyEject();
        }
    }
}
