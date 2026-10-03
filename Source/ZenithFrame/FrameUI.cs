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
            FrameAbilityCount = frame?.abilities?.AllAbilitiesForReading.Count() ?? 0;
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

    public sealed class ITab_Frame : ITab
    {
        private Vector2 scrollPosition;

        public ITab_Frame()
        {
            labelKey = "ZF_FrameTab";
            size = new Vector2(520f, 560f);
        }

        public override bool IsVisible => SelPawn is FramePawn;

        protected override void UpdateSize()
        {
            base.UpdateSize();
            size = new Vector2(520f, Mathf.Clamp(PaneTopY - 30f, 410f, 560f));
        }

        protected override void FillTab()
        {
            FramePawn frame = SelPawn as FramePawn;
            if (frame == null) return;
            FrameUiSnapshot data = FrameUiSnapshot.From(frame);
            Rect outer = new Rect(12f, 10f, size.x - 24f, size.y - 20f);
            Rect view = new Rect(0f, 0f, outer.width - 16f, 690f);
            Widgets.BeginScrollView(outer, ref scrollPosition, view);
            float y = 0f;
            DrawFrameSection(data, view.width, ref y);
            DrawPilotSection(data, view.width, ref y);
            DrawLoadoutSection(data, view.width, ref y);
            DrawStatusSection(data, view.width, ref y);
            Widgets.EndScrollView();
        }

        private static void DrawFrameSection(FrameUiSnapshot data, float width, ref float y)
        {
            Section("ZF_FrameSection".Translate(), width, ref y);
            Row("ZF_FrameName".Translate(), data.Frame.LabelShortCap, width, ref y);
            Row("ZF_Chassis".Translate(), data.Frame.def.LabelCap, width, ref y);
            Row("Faction".Translate(), data.Frame.Faction?.Name ?? "None".Translate(), width, ref y);
            Row("ZF_OperationalState".Translate(), StateLabel(data.OperationalState), width, ref y);
            Row("ZF_Integrity".Translate(), data.Integrity.ToStringPercent(), width, ref y);
            Row("ZF_Moving".Translate(), data.FrameMoving.ToStringPercent(), width, ref y);
            Row("ZF_Manipulation".Translate(), data.FrameManipulation.ToStringPercent(), width, ref y);
            Row("ZF_Sight".Translate(), data.FrameSight.ToStringPercent(), width, ref y);

            Rect buttons = new Rect(0f, y, width, 30f);
            float half = (width - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, half, 28f), "ZF_FrameHealth".Translate()))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false));
            if (data.CanRename && Widgets.ButtonText(new Rect(half + 6f, y, half, 28f), "Rename".Translate()))
                Find.WindowStack.Add(new Dialog_RenameFrame(data.Frame));
            TooltipHandler.TipRegion(buttons, "ZF_FrameActionsTip".Translate());
            y += 38f;
        }

        private static void DrawPilotSection(FrameUiSnapshot data, float width, ref float y)
        {
            Section("ZF_PilotSection".Translate(), width, ref y);
            if (data.Pilot == null)
            {
                Row("Pilot".Translate(), "None".Translate(), width, ref y);
                y += 6f;
                return;
            }

            Rect portrait = new Rect(0f, y, 82f, 124f);
            GUI.DrawTexture(portrait, PortraitsCache.Get(data.Pilot, portrait.size, Rot4.South));
            Rect details = new Rect(92f, y, width - 92f, 124f);
            float py = details.y;
            ClippedLabel(new Rect(details.x, py, details.width, 24f), data.Pilot.LabelShortCap, GameFont.Medium);
            py += 27f;
            ClippedLabel(new Rect(details.x, py, details.width, 22f), (data.Pilot.Faction?.Name ?? "None".Translate()) + " - " + PilotState(data.Pilot));
            py += 23f;
            ClippedLabel(new Rect(details.x, py, details.width, 22f), "Shooting".Translate() + ": " + data.ShootingLevel + "    " + "Melee".Translate() + ": " + data.MeleeLevel);
            py += 23f;
            ClippedLabel(new Rect(details.x, py, details.width, 22f), "ZF_PilotHealth".Translate() + ": " + data.PilotHealth.ToStringPercent() + "    " + "ZF_Consciousness".Translate() + ": " + data.PilotConsciousness.ToStringPercent());
            py += 23f;
            ClippedLabel(new Rect(details.x, py, details.width, 22f), "ZF_BleedRate".Translate() + ": " + data.PilotBleedRate.ToStringPercent() + "/d");
            y += 130f;

            float half = (width - 6f) / 2f;
            if (Widgets.ButtonText(new Rect(0f, y, half, 28f), "ZF_InspectPilot".Translate()))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Pilot, true));
            if (data.CanDisembark && Widgets.ButtonText(new Rect(half + 6f, y, half, 28f), "Disembark"))
            {
                if (!data.Frame.Cockpit.TryDisembark())
                    Messages.Message("No safe exit available.", MessageTypeDefOf.RejectInput, false);
            }
            y += 38f;
        }

        private static void DrawLoadoutSection(FrameUiSnapshot data, float width, ref float y)
        {
            Section("ZF_LoadoutSection".Translate(), width, ref y);
            ServiceItemRow("Weapon".Translate(), FrameAddonCategory.Weapon, data.Weapon, data, width, ref y);
            ServiceItemRow("Utility".Translate(), FrameAddonCategory.Utility, data.Utility, data, width, ref y,
                data.Utility == null ? null : UtilityLabel(data.UtilityState));
            ServiceItemRow("Power".Translate(), FrameAddonCategory.Power, data.Power, data, width, ref y);
            if (!data.PendingService.NullOrEmpty()) Row("Pending".Translate(), data.PendingService, width, ref y);
            y += 6f;
        }

        private static void DrawStatusSection(FrameUiSnapshot data, float width, ref float y)
        {
            Section("ZF_StatusSection".Translate(), width, ref y);
            Row("ZF_AddonPower".Translate(), data.RequiredPower + " " + "ZF_Required".Translate() + " / " + data.AvailablePower + " " + "ZF_Available".Translate(), width, ref y);
            Row("ZF_FrameAbilities".Translate(), data.FrameAbilityCount.ToString(), width, ref y);
            Row("ZF_PilotAbilities".Translate(), data.Pilot == null ? "-" : data.PilotAbilityCount.ToString(), width, ref y);
            if (data.IsRecoverable) Row("Status".Translate(), "ZF_Recoverable".Translate(), width, ref y);
            Row("Docking".Translate(), data.IsDocked ? "Docked" : "Not docked", width, ref y);
            if (!data.ActiveService.NullOrEmpty()) Row("Service".Translate(), "Being serviced by " + data.ActiveService, width, ref y);
            if (data.NeedsReconstruction) Row("Status".Translate(), "Missing part reconstruction required", width, ref y);
            else if (data.NeedsRepair) Row("Status".Translate(), "Needs repair", width, ref y);
            if (data.IsHackable)
            {
                Row("ZF_Acquisition".Translate(), "ZF_Hackable".Translate(), width, ref y);
                Row("Hack progress", data.HackProgress.ToStringPercent(), width, ref y);
            }
        }

        private static void Section(string label, float width, ref float y)
        {
            if (y > 0f) y += 4f;
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, y, width, 28f), label);
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, y + 27f, width);
            y += 33f;
        }

        private static void Row(string label, string value, float width, ref float y)
        {
            Rect row = new Rect(0f, y, width, 23f);
            Widgets.DrawHighlightIfMouseover(row);
            ClippedLabel(new Rect(4f, y, 150f, 22f), label);
            ClippedLabel(new Rect(158f, y, width - 162f, 22f), value);
            y += 24f;
        }

        private static void ItemRow(string label, Thing thing, float width, ref float y, string suffix = null)
        {
            Rect row = new Rect(0f, y, width, 25f);
            Widgets.DrawHighlightIfMouseover(row);
            ClippedLabel(new Rect(4f, y, 100f, 22f), label);
            string value = thing?.LabelCap ?? "None".Translate();
            if (!suffix.NullOrEmpty()) value += " [" + suffix + "]";
            float infoWidth = thing == null ? 0f : 28f;
            ClippedLabel(new Rect(108f, y, width - 112f - infoWidth, 22f), value);
            if (thing != null) Widgets.InfoCardButton(width - 24f, y, thing);
            y += 26f;
        }

        private static void ServiceItemRow(string label, FrameAddonCategory category, Thing thing,
            FrameUiSnapshot data, float width, ref float y, string suffix = null)
        {
            float actionWidth = data.IsReadOnly ? 0f : 76f;
            Rect row = new Rect(0f, y, width, 27f);
            Widgets.DrawHighlightIfMouseover(row);
            ClippedLabel(new Rect(4f, y, 78f, 22f), label);
            string value = thing?.LabelCap ?? "None".Translate();
            if (!suffix.NullOrEmpty()) value += " [" + suffix + "]";
            float infoWidth = thing == null ? 0f : 28f;
            ClippedLabel(new Rect(86f, y, width - 90f - infoWidth - actionWidth, 22f), value);
            if (thing != null) Widgets.InfoCardButton(width - actionWidth - 24f, y, thing);
            if (!data.IsReadOnly)
            {
                string action = thing == null ? "Equip..." : "Replace...";
                if (Widgets.ButtonText(new Rect(width - 74f, y, 74f, 24f), action))
                {
                    List<FloatMenuOption> options = data.Frame.Service.StoredCandidates(category)
                        .Select(candidate => new FloatMenuOption(candidate.LabelCap, () =>
                            data.Frame.Service.QueueInstall(category, candidate))).ToList();
                    if (thing != null) options.Add(new FloatMenuOption("Remove", () => data.Frame.Service.QueueRemove(category)));
                    if (options.Count == 0) options.Add(new FloatMenuOption("No compatible modules in valid storage", null));
                    Find.WindowStack.Add(new FloatMenu(options));
                }
            }
            y += 28f;
        }

        private static void ClippedLabel(Rect rect, string value, GameFont font = GameFont.Small)
        {
            GameFont oldFont = Text.Font;
            bool oldWrap = Text.WordWrap;
            Text.Font = font;
            Text.WordWrap = false;
            Widgets.Label(rect, value);
            if (Text.CalcSize(value).x > rect.width) TooltipHandler.TipRegion(rect, value);
            Text.WordWrap = oldWrap;
            Text.Font = oldFont;
        }

        private static string UtilityLabel(FrameUtilityState state) => state == FrameUtilityState.Powered ?
            "ZF_Powered".Translate() : state == FrameUtilityState.Unpowered ? "ZF_Unpowered".Translate() : "None".Translate();

        private static string StateLabel(FrameOperationalState state) => ("ZF_State_" + state).Translate();

        private static string PilotState(Pawn pawn)
        {
            if (pawn.Dead) return "Dead".Translate();
            if (pawn.Downed) return "Downed".Translate();
            if (pawn.InMentalState) return pawn.MentalStateDef.LabelCap;
            return "ZF_Contained".Translate();
        }
    }

    public sealed class Dialog_FramePawnInfo : Window
    {
        private readonly Pawn pawn;
        private readonly bool characterAvailable;
        private bool health;

        public override Vector2 InitialSize => new Vector2(690f, 570f);

        public Dialog_FramePawnInfo(Pawn pawn, bool characterAvailable)
        {
            this.pawn = pawn;
            this.characterAvailable = characterAvailable && pawn?.story != null;
            health = !this.characterAvailable;
            doCloseX = true;
            closeOnClickedOutside = true;
            absorbInputAroundWindow = false;
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
                if (Widgets.ButtonText(new Rect(x, 38f, 130f, 28f), "TabCharacter".Translate())) health = false;
                x += 136f;
            }
            if (Widgets.ButtonText(new Rect(x, 38f, 130f, 28f), "TabHealth".Translate())) health = true;

            Rect content = new Rect(0f, 76f, inRect.width, inRect.height - 76f);
            if (health)
                HealthCardUtility.DrawPawnHealthCard(content, pawn, false, HealthCardUtility.ShowBloodLoss(pawn), pawn);
            else
                CharacterCardUtility.DrawCharacterCard(content, pawn, showName: false);
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
