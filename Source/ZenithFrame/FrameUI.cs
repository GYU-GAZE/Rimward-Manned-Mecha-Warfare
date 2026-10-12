using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameOperationalState
    {
        Unpiloted,
        Operational,
        Drafted,
        HostileOperational,
        Disabled,
        Downed,
        Destroyed
    }

    public enum FrameUtilityState { None, Powered, Unpowered }

    // All presentation data is derived from the live chassis and its exact
    // owners. The tab never caches, copies or serializes pilot/loadout state.
    public sealed class FrameUiSnapshot
    {
        public FramePawn Frame { get; }
        public Pawn Pilot { get; }
        public Pawn PilotForInspection => Pilot;
        public ThingWithComps Weapon { get; }
        public Thing Head { get; }
        public Thing Shoulder { get; }
        public Thing Offhand { get; }
        public Thing Utility { get; }
        public Thing Power { get; }
        public int AvailablePower { get; }
        public int RequiredPower { get; }
        public FrameUtilityState UtilityState { get; }
        public FrameOperationalState OperationalState { get; }
        public float Integrity { get; }
        public float FrameMoving { get; }
        public float FrameManipulation { get; }
        public float FrameSight { get; }
        public float PilotHealth { get; }
        public float PilotConsciousness { get; }
        public float PilotBleedRate { get; }
        public int ShootingLevel { get; }
        public int MeleeLevel { get; }
        public int FrameAbilityCount { get; }
        public int PilotAbilityCount { get; }
        public bool IsReadOnly { get; }
        public bool CanRename { get; }
        public bool CanDisembark { get; }
        public bool CanInspectPilot => Pilot != null;
        public bool IsRecoverable { get; }
        public bool IsHackable { get; }
        public bool IsDocked { get; }
        public bool NeedsRepair { get; }
        public bool NeedsReconstruction { get; }
        public string PendingService { get; }
        public string ActiveService { get; }
        public float HackProgress { get; }

        private FrameUiSnapshot(FramePawn frame)
        {
            Frame = frame;
            Pilot = frame?.Cockpit?.Pilot;
            CompFrameLoadout loadout = frame?.Loadout;
            Weapon = loadout?.InstalledWeapon;
            Head = loadout?.InstalledHead;
            Shoulder = loadout?.InstalledShoulder;
            Offhand = loadout?.InstalledOffhand;
            Utility = loadout?.InstalledUtility;
            Power = loadout?.InstalledPower;
            AvailablePower = loadout?.AvailableAddonPower ?? 0;
            RequiredPower = loadout?.RequiredAddonPower ?? 0;
            UtilityState = Utility == null ? FrameUtilityState.None :
                loadout.IsUtilityPowered ? FrameUtilityState.Powered : FrameUtilityState.Unpowered;
            OperationalState = ResolveOperationalState(frame);
            Integrity = frame?.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            FrameMoving = Capacity(frame, PawnCapacityDefOf.Moving);
            FrameManipulation = Capacity(frame, PawnCapacityDefOf.Manipulation);
            FrameSight = Capacity(frame, PawnCapacityDefOf.Sight);
            PilotHealth = Pilot?.health?.summaryHealth?.SummaryHealthPercent ?? 0f;
            PilotConsciousness = Capacity(Pilot, PawnCapacityDefOf.Consciousness);
            PilotBleedRate = Pilot?.health?.hediffSet?.BleedRateTotal ?? 0f;
            ShootingLevel = SkillLevel(Pilot, SkillDefOf.Shooting);
            MeleeLevel = SkillLevel(Pilot, SkillDefOf.Melee);
            FrameAbilityCount = (frame?.abilities?.AllAbilitiesForReading.Count() ?? 0) +
                (loadout?.ActiveModuleAbilities().Count() ?? 0);
            PilotAbilityCount = Pilot?.abilities?.AllAbilitiesForReading.Count() ?? 0;
            IsReadOnly = frame?.Faction != Faction.OfPlayer;
            CanRename = frame != null && !frame.Destroyed && frame.Faction == Faction.OfPlayer;
            CanDisembark = frame?.Spawned == true && frame.Faction == Faction.OfPlayer && Pilot != null;
            IsRecoverable = frame?.Recovery?.IsRecoverablyDisabled == true;
            IsHackable = frame?.Hacking?.IsHackableDisabledChassis == true;
            IsDocked = DockingBayUtility.IsDocked(frame);
            NeedsRepair = FrameRepairUtility.NextRepairableInjury(frame) != null;
            NeedsReconstruction = FrameRepairUtility.NextReconstructablePart(frame) != null;
            PendingService = frame?.Service?.PendingLabel;
            ActiveService = frame?.Service?.ActiveWorker?.LabelShortCap;
            HackProgress = frame?.Hacking?.ProgressFraction ?? 0f;
        }

        public static FrameUiSnapshot From(FramePawn frame) => new FrameUiSnapshot(frame);

        private static int SkillLevel(Pawn pawn, SkillDef skill) => pawn?.skills?.GetSkill(skill)?.Level ?? -1;
        private static float Capacity(Pawn pawn, PawnCapacityDef capacity) =>
            pawn?.health?.capacities?.GetLevel(capacity) ?? 0f;

        private static FrameOperationalState ResolveOperationalState(FramePawn frame)
        {
            if (frame == null || frame.Destroyed || frame.Dead) return FrameOperationalState.Destroyed;
            if (frame.Recovery?.IsRecoverablyDisabled == true) return FrameOperationalState.Disabled;
            if (frame.Downed) return FrameOperationalState.Downed;
            if (frame.Cockpit?.HasPilot != true) return FrameOperationalState.Unpiloted;
            if (frame.Cockpit.CanUseHostileCombatAI) return FrameOperationalState.HostileOperational;
            if (frame.Drafted) return FrameOperationalState.Drafted;
            return FrameOperationalState.Operational;
        }
    }

    public static class FrameUiActions
    {
        public static bool IsValidName(string value)
        {
            string name = value?.Trim();
            return !name.NullOrEmpty() && name.Length <= 28 && CharacterCardUtility.ValidNameRegex.IsMatch(name);
        }

        public static bool TryRename(FramePawn frame, string value)
        {
            if (frame == null || frame.Destroyed || frame.Faction != Faction.OfPlayer) return false;
            string name = value?.Trim();
            if (!IsValidName(name)) return false;
            frame.Name = new NameSingle(name);
            return frame.Name.ToStringFull == name;
        }
    }

    public sealed class Dialog_RenameFrame : Dialog_Rename<Dialog_RenameFrame.Target>
    {
        public sealed class Target : IRenameable
        {
            private readonly FramePawn frame;
            public Target(FramePawn frame) { this.frame = frame; }
            public string RenamableLabel { get => frame.Name?.ToStringFull ?? frame.LabelShort; set => FrameUiActions.TryRename(frame, value); }
            public string BaseLabel => frame.def.LabelCap;
            public string InspectLabel => frame.LabelCap;
        }

        public Dialog_RenameFrame(FramePawn frame) : base(new Target(frame)) { }

        protected override AcceptanceReport NameIsValid(string name) =>
            FrameUiActions.IsValidName(name) ? true : "NameIsInvalid".Translate();
    }

    public enum FrameInfoPage { Bio, Health, Gear }

    public sealed class Dialog_FramePawnInfo : Window
    {
        private readonly Pawn pawn;
        private readonly bool characterAvailable;
        private FrameInfoPage page;
        private Vector2 gearScroll;

        public override Vector2 InitialSize => new Vector2(690f, 570f);

        public Dialog_FramePawnInfo(Pawn pawn, bool characterAvailable,
            FrameInfoPage initialPage = FrameInfoPage.Bio)
        {
            this.pawn = pawn;
            this.characterAvailable = characterAvailable && pawn?.story != null;
            page = this.characterAvailable ? initialPage : FrameInfoPage.Health;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
        }

        public bool SupportsPage(FrameInfoPage requested) =>
            requested == FrameInfoPage.Health || characterAvailable;

        public void ShowPage(FrameInfoPage requested)
        {
            if (SupportsPage(requested)) page = requested;
        }

        public override void DoWindowContents(Rect inRect)
        {
            if (pawn == null || pawn.Destroyed)
            {
                Widgets.Label(inRect, "ZF_InformationUnavailable".Translate());
                return;
            }

            ClippedHeader(new Rect(0f, 0f, inRect.width, 32f), pawn.LabelCap);
            float x = 0f;
            if (characterAvailable)
            {
                if (Widgets.ButtonText(new Rect(x, 38f, 130f, 28f), "TabCharacter".Translate()))
                    ShowPage(FrameInfoPage.Bio);
                x += 136f;
            }
            if (Widgets.ButtonText(new Rect(x, 38f, 130f, 28f), "TabHealth".Translate()))
                ShowPage(FrameInfoPage.Health);
            x += 136f;
            if (characterAvailable && Widgets.ButtonText(new Rect(x, 38f, 170f, 28f), "Gear / Inventory"))
                ShowPage(FrameInfoPage.Gear);

            Rect content = new Rect(0f, 76f, inRect.width, inRect.height - 76f);
            if (page == FrameInfoPage.Health)
                HealthCardUtility.DrawPawnHealthCard(content, pawn, false, HealthCardUtility.ShowBloodLoss(pawn), pawn);
            else if (page == FrameInfoPage.Bio)
                CharacterCardUtility.DrawCharacterCard(content, pawn, showName: false);
            else
                DrawGear(content);
        }

        private void DrawGear(Rect content)
        {
            // The pilot stays in its real cockpit owner. This is an inspection
            // of the exact live trackers, without selecting or spawning it.
            List<Thing> equipment = pawn.equipment?.AllEquipmentListForReading?.Cast<Thing>().ToList() ?? new List<Thing>();
            List<Thing> apparel = pawn.apparel?.WornApparel?.Cast<Thing>().ToList() ?? new List<Thing>();
            List<Thing> inventory = pawn.inventory?.innerContainer?.Cast<Thing>().ToList() ?? new List<Thing>();
            float rows = equipment.Count + apparel.Count + inventory.Count;
            Rect view = new Rect(0f, 0f, content.width - 18f,
                Mathf.Max(content.height, 200f + rows * 31f));
            Widgets.BeginScrollView(content, ref gearScroll, view);
            float y = 0f;
            DrawGearSection(ref y, view.width, "Equipment".Translate(), equipment);
            DrawGearSection(ref y, view.width, "Apparel".Translate(), apparel);
            DrawGearSection(ref y, view.width, "Inventory".Translate(), inventory);
            Widgets.EndScrollView();
        }

        private static void DrawGearSection(ref float y, float width, string title, List<Thing> things)
        {
            Widgets.ListSeparator(ref y, width, title);
            if (things.Count == 0)
            {
                Widgets.Label(new Rect(12f, y, width - 12f, 27f), "None");
                y += 31f;
                return;
            }
            foreach (Thing thing in things)
            {
                Rect row = new Rect(0f, y, width, 29f);
                Widgets.DrawHighlightIfMouseover(row);
                Widgets.Label(new Rect(12f, y + 3f, width - 48f, 24f), thing.LabelCap);
                Widgets.InfoCardButton(width - 29f, y + 2f, thing);
                y += 31f;
            }
        }

        private static void ClippedHeader(Rect rect, string value)
        {
            Text.Font = GameFont.Medium;
            Text.WordWrap = false;
            Widgets.Label(rect, value);
            if (Text.CalcSize(value).x > rect.width) TooltipHandler.TipRegion(rect, value);
            Text.WordWrap = true;
            Text.Font = GameFont.Small;
        }
    }
}
