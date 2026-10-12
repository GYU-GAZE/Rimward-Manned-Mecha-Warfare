using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    // Existing numeric values are save data. Append only.
    public enum FrameAddonCategory
    {
        Weapon = 0,
        Utility = 1,
        Power = 2,
        Head = 3,
        Shoulder = 4,
        Offhand = 5
    }

    public sealed class FrameAddonExtension : DefModExtension
    {
        public FrameAddonCategory category;
        public int powerOutput;
        public int powerDraw;
        public float moveSpeedOffset;
        public HediffDef installedHediff;
        public HediffDef poweredHediff;
        public AbilityDef moduleAbility;
        public BodyPartGroupDef mountGroup;
        public BodyPartGroupDef armorCoverageGroup;
        public float armorSharp;
        public float armorBlunt;
        public float armorHeat;
    }

    public sealed class CompProperties_FrameLoadout : CompProperties
    {
        public int weaponCapacity = 1;
        public int utilityCapacity = 1;
        public int powerCapacity = 1;
        public int headCapacity = 1;
        public int shoulderCapacity = 1;
        public int offhandCapacity = 1;
        public ThingDef defaultWeapon;
        public ThingDef defaultUtility;
        public ThingDef defaultPower;
        public ThingDef defaultHead;
        public ThingDef defaultShoulder;
        public ThingDef defaultOffhand;
        public List<ThingDef> developmentWeapons = new List<ThingDef>();
        public List<ThingDef> developmentUtilities = new List<ThingDef>();
        public List<ThingDef> developmentPowerSources = new List<ThingDef>();
        public List<ThingDef> developmentHeads = new List<ThingDef>();
        public List<ThingDef> developmentShoulders = new List<ThingDef>();
        public List<ThingDef> developmentOffhands = new List<ThingDef>();
        public CompProperties_FrameLoadout() { compClass = typeof(CompFrameLoadout); }
    }

    public sealed class CompFrameLoadout : ThingComp, IThingHolder
    {
        private ThingOwner<Thing> utilities;
        private ThingOwner<Thing> powerSources;
        private ThingOwner<Thing> heads;
        private ThingOwner<Thing> shoulders;
        private ThingOwner<Thing> offhands;
        private SlotHolder powerHolder;
        private SlotHolder headHolder;
        private SlotHolder shoulderHolder;
        private SlotHolder offhandHolder;
        private bool weaponDefaultInitialized;
        private bool utilityDefaultInitialized;
        private bool powerDefaultInitialized;
        private bool headDefaultInitialized;
        private bool shoulderDefaultInitialized;
        private bool offhandDefaultInitialized;
        private ThingWithComps rememberedDroppedWeapon;

        public CompFrameLoadout()
        {
            utilities = new ThingOwner<Thing>(this, true, LookMode.Deep);
            RecreateHolders();
        }

        private void RecreateHolders()
        {
            powerHolder = new SlotHolder(this, FrameAddonCategory.Power);
            headHolder = new SlotHolder(this, FrameAddonCategory.Head);
            shoulderHolder = new SlotHolder(this, FrameAddonCategory.Shoulder);
            offhandHolder = new SlotHolder(this, FrameAddonCategory.Offhand);
            EnsureOwners();
        }

        private void EnsureOwners()
        {
            if (utilities == null) utilities = new ThingOwner<Thing>(this, true, LookMode.Deep);
            if (powerSources == null) powerSources = new ThingOwner<Thing>(powerHolder, true, LookMode.Deep);
            if (heads == null) heads = new ThingOwner<Thing>(headHolder, true, LookMode.Deep);
            if (shoulders == null) shoulders = new ThingOwner<Thing>(shoulderHolder, true, LookMode.Deep);
            if (offhands == null) offhands = new ThingOwner<Thing>(offhandHolder, true, LookMode.Deep);
        }

        public FramePawn Frame => (FramePawn)parent;
        public CompProperties_FrameLoadout Props => (CompProperties_FrameLoadout)props;
        public ThingWithComps InstalledWeapon => Frame.equipment?.Primary;
        public Thing InstalledUtility => First(utilities);
        public Thing InstalledPower => First(powerSources);
        public Thing InstalledHead => First(heads);
        public Thing InstalledShoulder => First(shoulders);
        public Thing InstalledOffhand => First(offhands);
        public ThingWithComps RememberedDroppedWeapon => Frame.mindState?.droppedWeapon as ThingWithComps ?? rememberedDroppedWeapon;
        public int AvailableAddonPower => Math.Max(0, Extension(InstalledPower)?.powerOutput ?? 0);
        public int RequiredAddonPower => InstalledModules
            .Where(module => Extension(module)?.category != FrameAddonCategory.Power)
            .Sum(module => Math.Max(0, Extension(module)?.powerDraw ?? 0));
        public bool HasSufficientAddonPower => AvailableAddonPower >= RequiredAddonPower;
        public bool IsUtilityPowered => IsModulePowered(InstalledUtility);
        public IEnumerable<Thing> InstalledModules
        {
            get
            {
                if (InstalledWeapon != null) yield return InstalledWeapon;
                foreach (Thing thing in utilities) yield return thing;
                foreach (Thing thing in powerSources) yield return thing;
                foreach (Thing thing in heads) yield return thing;
                foreach (Thing thing in shoulders) yield return thing;
                foreach (Thing thing in offhands) yield return thing;
            }
        }

        public ThingOwner GetDirectlyHeldThings() => utilities;
        public void GetChildHolders(List<IThingHolder> children)
        {
            children.Add(powerHolder);
            children.Add(headHolder);
            children.Add(shoulderHolder);
            children.Add(offhandHolder);
            ThingOwnerUtility.AppendThingHoldersFromThings(children, utilities);
        }

        internal sealed class SlotHolder : IThingHolder
        {
            internal readonly CompFrameLoadout loadout;
            internal readonly FrameAddonCategory category;
            internal SlotHolder(CompFrameLoadout loadout, FrameAddonCategory category)
            {
                this.loadout = loadout;
                this.category = category;
            }
            public IThingHolder ParentHolder => loadout;
            public ThingOwner GetDirectlyHeldThings() => loadout.OwnerFor(category);
            public void GetChildHolders(List<IThingHolder> children) =>
                ThingOwnerUtility.AppendThingHoldersFromThings(children, loadout.OwnerFor(category));
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            bool legacyWeaponInitialized = false;
            Scribe_Values.Look(ref legacyWeaponInitialized, "defaultLoadoutInitialized");
            Scribe_Values.Look(ref weaponDefaultInitialized, "weaponDefaultInitialized");
            Scribe_Values.Look(ref utilityDefaultInitialized, "utilityDefaultInitialized");
            Scribe_Values.Look(ref powerDefaultInitialized, "powerDefaultInitialized");
            Scribe_Values.Look(ref headDefaultInitialized, "headDefaultInitialized");
            Scribe_Values.Look(ref shoulderDefaultInitialized, "shoulderDefaultInitialized");
            Scribe_Values.Look(ref offhandDefaultInitialized, "offhandDefaultInitialized");
            if (powerHolder == null || headHolder == null || shoulderHolder == null || offhandHolder == null) RecreateHolders();
            Scribe_Deep.Look(ref utilities, "installedFrameUtility", this);
            Scribe_Deep.Look(ref powerSources, "installedFramePower", powerHolder);
            Scribe_Deep.Look(ref heads, "installedFrameHead", headHolder);
            Scribe_Deep.Look(ref shoulders, "installedFrameShoulder", shoulderHolder);
            Scribe_Deep.Look(ref offhands, "installedFrameOffhand", offhandHolder);
            Scribe_References.Look(ref rememberedDroppedWeapon, "rememberedDroppedFrameWeapon");
            EnsureOwners();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (legacyWeaponInitialized) weaponDefaultInitialized = true;
                RebindModuleAbilities();
                RefreshModuleEffects();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeDefaults(respawningAfterLoad);
            RebindModuleAbilities();
            RefreshModuleEffects();
        }

        public override void CompTick()
        {
            base.CompTick();
            if (Frame.IsHashIntervalTick(60))
            {
                RebindModuleAbilities();
                RefreshModuleEffects();
            }
        }

        public override void PostPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostPostApplyDamage(dinfo, totalDamageDealt);
            RefreshModuleEffects();
        }

        private void InitializeDefaults(bool loading)
        {
            InitializeDefault(ref weaponDefaultInitialized, Props.defaultWeapon, FrameAddonCategory.Weapon, loading);
            InitializeDefault(ref utilityDefaultInitialized, Props.defaultUtility, FrameAddonCategory.Utility, loading);
            InitializeDefault(ref powerDefaultInitialized, Props.defaultPower, FrameAddonCategory.Power, loading);
            InitializeDefault(ref headDefaultInitialized, Props.defaultHead, FrameAddonCategory.Head, loading);
            InitializeDefault(ref shoulderDefaultInitialized, Props.defaultShoulder, FrameAddonCategory.Shoulder, loading);
            InitializeDefault(ref offhandDefaultInitialized, Props.defaultOffhand, FrameAddonCategory.Offhand, loading);
        }

        private void InitializeDefault(ref bool initialized, ThingDef def, FrameAddonCategory category, bool loading)
        {
            if (initialized) return;
            initialized = true;
            if (!loading && Installed(category) == null && def != null) InstallDefault(def, category);
        }

        private void InstallDefault(ThingDef def, FrameAddonCategory category)
        {
            Thing item = ThingMaker.MakeThing(def);
            if (TryInstall(category, item)) return;
            if (!item.Destroyed) item.Destroy();
            Log.Error("ZENITH: Frame default " + category + " failed loadout installation.");
        }

        public Thing Installed(FrameAddonCategory category)
        {
            switch (category)
            {
                case FrameAddonCategory.Weapon: return InstalledWeapon;
                case FrameAddonCategory.Utility: return InstalledUtility;
                case FrameAddonCategory.Power: return InstalledPower;
                case FrameAddonCategory.Head: return InstalledHead;
                case FrameAddonCategory.Shoulder: return InstalledShoulder;
                case FrameAddonCategory.Offhand: return InstalledOffhand;
                default: return null;
            }
        }

        public bool IsInstalled(Thing module) => module != null && InstalledModules.Any(exact => ReferenceEquals(exact, module));

        public bool CanInstall(FrameAddonCategory category, Thing item, out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed) reason = "Frame is not a living chassis.";
            else if (category == FrameAddonCategory.Weapon && Frame.equipment == null) reason = "Frame has no equipment tracker.";
            else if (Capacity(category) < 1) reason = "Chassis has no " + category + " capacity.";
            else if (Installed(category) != null) reason = category + " slot is occupied.";
            else if (item == null || item.Destroyed || item.stackCount != 1) reason = category + " item is invalid.";
            else if (Extension(item)?.category != category) reason = "Item is not a Zenith Frame " + category + " addon.";
            else if (!item.Spawned && item.holdingOwner != null) reason = category + " is already owned by another container.";
            else if (item.Spawned && (!Frame.Spawned || item.Map != Frame.Map)) reason = category + " is not on the Frame's map.";
            return reason == null;
        }

        public bool CanInstallWeapon(ThingWithComps item, out string reason) => CanInstall(FrameAddonCategory.Weapon, item, out reason);
        public bool CanInstallUtility(Thing item, out string reason) => CanInstall(FrameAddonCategory.Utility, item, out reason);
        public bool CanInstallPower(Thing item, out string reason) => CanInstall(FrameAddonCategory.Power, item, out reason);
        public bool CanInstallHead(Thing item, out string reason) => CanInstall(FrameAddonCategory.Head, item, out reason);
        public bool CanInstallShoulder(Thing item, out string reason) => CanInstall(FrameAddonCategory.Shoulder, item, out reason);
        public bool CanInstallOffhand(Thing item, out string reason) => CanInstall(FrameAddonCategory.Offhand, item, out reason);

        public bool TryInstall(FrameAddonCategory category, Thing item)
        {
            if (!CanInstall(category, item, out _)) return false;
            Map map = item.Spawned ? item.Map : null;
            IntVec3 position = item.Spawned ? item.Position : IntVec3.Invalid;
            if (category == FrameAddonCategory.Weapon)
            {
                if (!(item is ThingWithComps weapon)) return false;
                Frame.Cockpit?.StopFrame();
                if (item.Spawned) item.DeSpawn();
                Frame.equipment.AddEquipment(weapon);
                if (ReferenceEquals(InstalledWeapon, item))
                {
                    if (ReferenceEquals(rememberedDroppedWeapon, item)) rememberedDroppedWeapon = null;
                    RefreshAfterMutation(item);
                    return true;
                }
            }
            else
            {
                ThingOwner<Thing> owner = OwnerFor(category);
                if (item.Spawned) item.DeSpawn();
                if (owner.TryAdd(item, false))
                {
                    RefreshAfterMutation(item);
                    return true;
                }
            }
            Restore(item, map, position);
            return false;
        }

        public bool TryInstallWeapon(ThingWithComps item) => TryInstall(FrameAddonCategory.Weapon, item);
        public bool TryInstallUtility(Thing item) => TryInstall(FrameAddonCategory.Utility, item);
        public bool TryInstallPower(Thing item) => TryInstall(FrameAddonCategory.Power, item);
        public bool TryInstallHead(Thing item) => TryInstall(FrameAddonCategory.Head, item);
        public bool TryInstallShoulder(Thing item) => TryInstall(FrameAddonCategory.Shoulder, item);
        public bool TryInstallOffhand(Thing item) => TryInstall(FrameAddonCategory.Offhand, item);

        private static void Restore(Thing item, Map map, IntVec3 position)
        {
            if (item.holdingOwner == null && !item.Destroyed && map != null && position.IsValid) GenSpawn.Spawn(item, position, map);
        }

        public bool CanRemove(FrameAddonCategory category, out string reason)
        {
            reason = null;
            if (Frame == null || Frame.Dead || Frame.Destroyed) reason = "Frame is not a living chassis.";
            else if (!Frame.Spawned) reason = "Frame must be spawned to remove a physical addon.";
            else if (Installed(category) == null) reason = category + " slot is empty.";
            return reason == null;
        }

        public bool CanRemoveWeapon(out string reason) => CanRemove(FrameAddonCategory.Weapon, out reason);
        public bool CanRemoveUtility(out string reason) => CanRemove(FrameAddonCategory.Utility, out reason);
        public bool CanRemovePower(out string reason) => CanRemove(FrameAddonCategory.Power, out reason);
        public bool CanRemoveHead(out string reason) => CanRemove(FrameAddonCategory.Head, out reason);
        public bool CanRemoveShoulder(out string reason) => CanRemove(FrameAddonCategory.Shoulder, out reason);
        public bool CanRemoveOffhand(out string reason) => CanRemove(FrameAddonCategory.Offhand, out reason);

        public bool TryRemove(FrameAddonCategory category, out Thing removed)
        {
            removed = null;
            if (!CanRemove(category, out _)) return false;
            Thing exact = Installed(category);
            CancelModuleCast(exact);
            if (category == FrameAddonCategory.Weapon)
            {
                Frame.Cockpit?.StopFrame();
                if (!Frame.equipment.TryDropEquipment((ThingWithComps)exact, out ThingWithComps weapon, Frame.Position, false)) return false;
                removed = weapon;
            }
            else if (!OwnerFor(category).TryDrop(exact, Frame.Position, Frame.Map, ThingPlaceMode.Near, out removed))
                return false;
            bool result = ReferenceEquals(removed, exact) && removed.Spawned && Installed(category) == null;
            if (result) RefreshAfterMutation(removed);
            return result;
        }

        public bool TryRemoveWeapon(out ThingWithComps removed)
        {
            bool result = TryRemove(FrameAddonCategory.Weapon, out Thing thing);
            removed = thing as ThingWithComps;
            return result;
        }
        public bool TryRemoveUtility(out Thing removed) => TryRemove(FrameAddonCategory.Utility, out removed);
        public bool TryRemovePower(out Thing removed) => TryRemove(FrameAddonCategory.Power, out removed);
        public bool TryRemoveHead(out Thing removed) => TryRemove(FrameAddonCategory.Head, out removed);
        public bool TryRemoveShoulder(out Thing removed) => TryRemove(FrameAddonCategory.Shoulder, out removed);
        public bool TryRemoveOffhand(out Thing removed) => TryRemove(FrameAddonCategory.Offhand, out removed);

        public bool TryReclaimRememberedWeapon()
        {
            if (Frame.Downed || InstalledWeapon != null || !Frame.Spawned) return false;
            ThingWithComps exact = RememberedDroppedWeapon;
            if (exact == null || exact.Destroyed || !exact.Spawned || exact.Map != Frame.Map ||
                !exact.Position.InHorDistOf(Frame.Position, 2f) || !TryInstallWeapon(exact)) return false;
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
            foreach (ThingOwner<Thing> owner in ContainedOwners()) FinalizeAddon(owner);
            RefreshModuleEffects();
        }

        private void FinalizeAddon(ThingOwner<Thing> owner)
        {
            foreach (Thing exact in owner.ToList())
            {
                CancelModuleCast(exact);
                if (Frame.Spawned && owner.TryDrop(exact, Frame.Position, Frame.Map, ThingPlaceMode.Near, out Thing _)) continue;
                if (owner.Remove(exact) && !exact.Destroyed) exact.Destroy();
            }
        }

        public bool IsModuleActive(Thing module) => IsInstalled(module) && IsMountValid(module);

        public bool IsModulePowered(Thing module)
        {
            if (!IsModuleActive(module)) return false;
            int draw = Extension(module)?.powerDraw ?? 0;
            return draw <= 0 || HasSufficientAddonPower;
        }

        public bool IsMountValid(Thing module)
        {
            FrameAddonExtension extension = Extension(module);
            if (extension?.mountGroup == null) return true;
            return Frame.RaceProps.body.AllParts.Any(part => part.groups.Contains(extension.mountGroup) &&
                !Frame.health.hediffSet.PartIsMissing(part) && Frame.health.hediffSet.GetPartHealth(part) > 0.0001f);
        }

        public BodyPartRecord MountFor(Thing module)
        {
            BodyPartGroupDef group = Extension(module)?.mountGroup;
            if (group == null) return null;
            return Frame.RaceProps.body.AllParts.FirstOrDefault(part => part.groups.Contains(group));
        }

        public bool CoversArmorPart(Thing module, BodyPartRecord part)
        {
            FrameAddonExtension extension = Extension(module);
            return IsModuleActive(module) && part != null && extension?.armorCoverageGroup != null &&
                part.groups.Contains(extension.armorCoverageGroup);
        }

        public float ArmorOffset(Thing module, StatDef stat)
        {
            FrameAddonExtension extension = Extension(module);
            if (extension == null) return 0f;
            if (stat == StatDefOf.ArmorRating_Sharp) return extension.armorSharp;
            if (stat == StatDefOf.ArmorRating_Blunt) return extension.armorBlunt;
            if (stat == StatDefOf.ArmorRating_Heat) return extension.armorHeat;
            return 0f;
        }

        public override float GetStatOffset(StatDef stat)
        {
            if (stat != StatDefOf.MoveSpeed) return 0f;
            return InstalledModules.Where(IsModulePowered).Sum(module => Extension(module)?.moveSpeedOffset ?? 0f);
        }

        public void RefreshModuleEffects()
        {
            if (Frame?.health?.hediffSet == null) return;
            List<Projection> expected = new List<Projection>();
            foreach (Thing module in InstalledModules)
            {
                FrameAddonExtension extension = Extension(module);
                BodyPartRecord mount = MountFor(module);
                if (!IsModuleActive(module)) continue;
                if (extension?.installedHediff != null) expected.Add(new Projection(extension.installedHediff, mount));
                if (extension?.poweredHediff != null && IsModulePowered(module)) expected.Add(new Projection(extension.poweredHediff, mount));
            }

            HashSet<HediffDef> moduleDefs = new HashSet<HediffDef>(DefDatabase<ThingDef>.AllDefsListForReading
                .Select(def => def.GetModExtension<FrameAddonExtension>())
                .Where(extension => extension != null)
                .SelectMany(extension => new[] { extension.installedHediff, extension.poweredHediff })
                .Where(def => def != null));
            foreach (Hediff hediff in Frame.health.hediffSet.hediffs.ToList())
                if (moduleDefs.Contains(hediff.def) && !expected.Any(projection => projection.Matches(hediff)))
                    Frame.health.RemoveHediff(hediff);
            foreach (Projection projection in expected)
                if (!Frame.health.hediffSet.hediffs.Any(projection.Matches))
                    Frame.health.AddHediff(projection.Def, projection.Part);
        }

        public void RebindModuleAbilities()
        {
            foreach (Thing module in InstalledModules)
            {
                CompFrameModuleAbility comp = module.TryGetComp<CompFrameModuleAbility>();
                comp?.Bind(Frame);
                if (comp?.Ability != null && Frame.CurJob?.ability == comp.Ability && !IsModulePowered(module))
                    Frame.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            }
        }

        public IEnumerable<Ability> ActiveModuleAbilities()
        {
            foreach (Thing module in InstalledModules)
            {
                CompFrameModuleAbility comp = module.TryGetComp<CompFrameModuleAbility>();
                if (comp?.Ability == null || !IsModulePowered(module)) continue;
                comp.Bind(Frame);
                yield return comp.Ability;
            }
        }

        private void RefreshAfterMutation(Thing module)
        {
            module?.TryGetComp<CompFrameModuleAbility>()?.Bind(IsInstalled(module) ? Frame : null);
            RebindModuleAbilities();
            RefreshModuleEffects();
            Frame?.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private void CancelModuleCast(Thing module)
        {
            CompFrameModuleAbility comp = module?.TryGetComp<CompFrameModuleAbility>();
            if (comp?.Ability == null) return;
            if (Frame.CurJob?.ability == comp.Ability) Frame.jobs.EndCurrentJob(Verse.AI.JobCondition.InterruptForced);
            if (Find.Targeter?.IsTargeting == true) Find.Targeter.StopTargeting();
            comp.Bind(null);
        }

        public override string CompInspectStringExtra()
        {
            return string.Join("\n", new[]
            {
                "Head: " + ModuleLabel(InstalledHead),
                "Shoulder: " + ModuleLabel(InstalledShoulder),
                "Primary Weapon: " + ModuleLabel(InstalledWeapon),
                "Offhand: " + ModuleLabel(InstalledOffhand),
                "Utility: " + ModuleLabel(InstalledUtility),
                "Power: " + ModuleLabel(InstalledPower),
                "Addon power: " + RequiredAddonPower + " required / " + AvailableAddonPower + " available"
            });
        }

        private string ModuleLabel(Thing module)
        {
            if (module == null) return "None";
            FrameAddonExtension extension = Extension(module);
            if (!IsMountValid(module)) return module.LabelCap + " [Mount damaged]";
            if ((extension?.powerDraw ?? 0) > 0) return module.LabelCap + (IsModulePowered(module) ? " [Powered]" : " [Unpowered]");
            return module.LabelCap + " [Passive]";
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!Prefs.DevMode || Frame.Faction != Faction.OfPlayer || !Frame.Spawned) yield break;
            foreach (FrameAddonCategory category in AllCategories)
            {
                FrameAddonCategory captured = category;
                yield return RemoveCommand(category.ToString(), () => TryRemove(captured, out _),
                    (out string reason) => CanRemove(captured, out reason));
                foreach (ThingDef def in DevelopmentDefs(category)) yield return InstallCommand(def, category);
            }
        }

        private delegate bool RemoveEligibility(out string reason);
        private Command_Action RemoveCommand(string label, Action action, RemoveEligibility eligibility)
        {
            var command = new Command_Action { defaultLabel = "DEV: Remove " + label, defaultDesc = "Remove the exact installed Frame addon through the physical loadout API.", icon = TexCommand.Install, action = action };
            if (!eligibility(out string reason)) command.Disable(reason);
            return command;
        }

        private Command_Action InstallCommand(ThingDef def, FrameAddonCategory category)
        {
            var command = new Command_Action
            {
                defaultLabel = "DEV: Install " + def.label,
                defaultDesc = "Create one development item and install it through the physical loadout API.",
                icon = TexCommand.Install,
                action = () =>
                {
                    Thing item = ThingMaker.MakeThing(def);
                    if (TryInstall(category, item)) return;
                    if (!item.Destroyed) item.Destroy();
                    Messages.Message(category + " could not be installed.", Frame, MessageTypeDefOf.RejectInput, false);
                }
            };
            if (Installed(category) != null) command.Disable(category + " slot is occupied.");
            return command;
        }

        private IEnumerable<ThingDef> DevelopmentDefs(FrameAddonCategory category)
        {
            switch (category)
            {
                case FrameAddonCategory.Weapon: return Props.developmentWeapons;
                case FrameAddonCategory.Utility: return Props.developmentUtilities;
                case FrameAddonCategory.Power: return Props.developmentPowerSources;
                case FrameAddonCategory.Head: return Props.developmentHeads;
                case FrameAddonCategory.Shoulder: return Props.developmentShoulders;
                case FrameAddonCategory.Offhand: return Props.developmentOffhands;
                default: return Enumerable.Empty<ThingDef>();
            }
        }

        private int Capacity(FrameAddonCategory category)
        {
            switch (category)
            {
                case FrameAddonCategory.Weapon: return Props.weaponCapacity;
                case FrameAddonCategory.Utility: return Props.utilityCapacity;
                case FrameAddonCategory.Power: return Props.powerCapacity;
                case FrameAddonCategory.Head: return Props.headCapacity;
                case FrameAddonCategory.Shoulder: return Props.shoulderCapacity;
                case FrameAddonCategory.Offhand: return Props.offhandCapacity;
                default: return 0;
            }
        }

        private ThingOwner<Thing> OwnerFor(FrameAddonCategory category)
        {
            switch (category)
            {
                case FrameAddonCategory.Utility: return utilities;
                case FrameAddonCategory.Power: return powerSources;
                case FrameAddonCategory.Head: return heads;
                case FrameAddonCategory.Shoulder: return shoulders;
                case FrameAddonCategory.Offhand: return offhands;
                default: return null;
            }
        }

        private IEnumerable<ThingOwner<Thing>> ContainedOwners()
        {
            yield return utilities;
            yield return powerSources;
            yield return heads;
            yield return shoulders;
            yield return offhands;
        }

        private static Thing First(ThingOwner<Thing> owner) => owner != null && owner.Count > 0 ? owner[0] : null;
        public static FrameAddonExtension Extension(Thing item) => item?.def?.GetModExtension<FrameAddonExtension>();
        public static readonly FrameAddonCategory[] AllCategories =
        {
            FrameAddonCategory.Weapon, FrameAddonCategory.Utility, FrameAddonCategory.Power,
            FrameAddonCategory.Head, FrameAddonCategory.Shoulder, FrameAddonCategory.Offhand
        };

        private sealed class Projection
        {
            internal readonly HediffDef Def;
            internal readonly BodyPartRecord Part;
            internal Projection(HediffDef def, BodyPartRecord part) { Def = def; Part = part; }
            internal bool Matches(Hediff hediff) => hediff.def == Def && ReferenceEquals(hediff.Part, Part);
        }
    }

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
            if (___pawn?.holdingOwner?.Owner is CompPilotableMech cockpit && ReferenceEquals(cockpit.Pilot, ___pawn))
                cockpit.EmergencyEject();
        }
    }
}
