using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameColorChannel { Primary, Secondary, Visor }

    public sealed class Dialog_FrameColorPicker : Dialog_ColorPickerBase
    {
        private static readonly List<Color> Palette = new List<Color>
        {
            Color.white, Color.black, Color.gray, Color.red, Color.yellow,
            Color.green, Color.cyan, Color.blue, Color.magenta
        };
        private readonly FramePawn frame;
        private readonly FrameColorChannel channel;
        protected override bool ShowDarklight => false;
        protected override Color DefaultColor => channel == FrameColorChannel.Primary ?
            frame.Livery.Defaults?.defaultPrimary ?? Color.white :
            channel == FrameColorChannel.Secondary ? frame.Livery.Defaults?.defaultSecondary ?? Color.white :
            frame.Livery.Defaults?.defaultVisor ?? Color.white;
        protected override List<Color> PickableColors => Palette;
        protected override float ForcedColorValue => -1f;
        protected override bool ShowColorTemperatureBar => false;

        public Dialog_FrameColorPicker(FramePawn frame, FrameColorChannel channel)
            : base(Widgets.ColorComponents.All, Widgets.ColorComponents.All)
        {
            this.frame = frame;
            this.channel = channel;
            color = oldColor = channel == FrameColorChannel.Primary ? frame.Livery.Primary :
                channel == FrameColorChannel.Secondary ? frame.Livery.Secondary : frame.Livery.Visor;
        }

        protected override void SaveColor(Color chosen)
        {
            if (frame == null || frame.Destroyed || frame.Faction != Faction.OfPlayer) return;
            CompFrameLivery paint = frame.Livery;
            paint.SetColors(channel == FrameColorChannel.Primary ? chosen : paint.Primary,
                channel == FrameColorChannel.Secondary ? chosen : paint.Secondary,
                channel == FrameColorChannel.Visor ? chosen : paint.Visor);
        }
    }

    // Fixed visual composition. Every value is read from the selected physical
    // Frame and its exact Thing/Pawn owners on each redraw.
    public sealed class ITab_FrameVisual : ITab
    {
        public ITab_FrameVisual()
        {
            labelKey = "ZF_FrameTab";
            size = new Vector2(520f, 560f);
        }

        public override bool IsVisible => SelPawn is FramePawn;

        protected override void FillTab()
        {
            FramePawn frame = SelPawn as FramePawn;
            if (frame == null) return;
            FrameUiSnapshot data = FrameUiSnapshot.From(frame);
            float x = 12f, y = 8f;
            DrawHeader(data, x, y);
            DrawSlot(frame, FrameAddonCategory.Head, "HEAD", x + 4f, y + 40f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Shoulder, "SHOULDER", x + 408f, y + 40f, data.IsReadOnly);
            DrawPreviewAndColors(data, x + 162f, y + 39f);
            DrawSlot(frame, FrameAddonCategory.Weapon, "PRIMARY", x + 4f, y + 153f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Offhand, "OFFHAND", x + 408f, y + 153f, data.IsReadOnly);
            DrawStatus(data, x + 111f, y + 198f);
            DrawSlot(frame, FrameAddonCategory.Utility, "UTILITY", x + 4f, y + 273f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Power, "POWER", x + 408f, y + 273f, data.IsReadOnly);
            DrawFrameIssues(data, x + 111f, y + 276f);
            Widgets.DrawLineHorizontal(x, y + 393f, 496f);
            DrawPilot(data, x + 4f, y + 398f);
        }

        private static void DrawHeader(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x + 4f, y, 340f, 25f), data.Frame.LabelShortCap, GameFont.Medium);
            if (data.CanRename && Widgets.ButtonText(new Rect(x + 347f, y, 69f, 25f), "Rename"))
                Find.WindowStack.Add(new Dialog_RenameFrame(data.Frame));
            Label(new Rect(x + 4f, y + 25f, 480f, 18f), data.Frame.def.LabelCap);
        }

        private static void DrawPreviewAndColors(FrameUiSnapshot data, float x, float y)
        {
            Rect preview = new Rect(x, y, 160f, 160f);
            Widgets.DrawBoxSolid(preview, new Color(0.15f, 0.15f, 0.15f, 0.6f));
            GUI.DrawTexture(preview, PortraitsCache.Get(data.Frame, preview.size, Rot4.South));
            Widgets.DrawBox(preview);
            TooltipHandler.TipRegion(preview, "Current assembled Frame and livery");
            DrawSwatch(data.Frame, FrameColorChannel.Primary, x + 168f, y + 8f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Secondary, x + 168f, y + 48f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Visor, x + 168f, y + 88f, data.IsReadOnly);
            if (!data.IsReadOnly && Widgets.ButtonText(new Rect(x + 163f, y + 132f, 85f, 22f), "Reset paint"))
                data.Frame.Livery.ResetColors();
        }

        private static void DrawSwatch(FramePawn frame, FrameColorChannel channel, float x, float y, bool readOnly)
        {
            Color color = channel == FrameColorChannel.Primary ? frame.Livery.Primary :
                channel == FrameColorChannel.Secondary ? frame.Livery.Secondary : frame.Livery.Visor;
            Rect swatch = new Rect(x, y, 31f, 31f);
            Widgets.DrawBoxSolid(swatch, color);
            Widgets.DrawBox(swatch, 2);
            string shortName = channel == FrameColorChannel.Primary ? "PRI" :
                channel == FrameColorChannel.Secondary ? "SEC" : "VIS";
            Label(new Rect(x + 36f, y + 6f, 35f, 20f), shortName);
            TooltipHandler.TipRegion(swatch, channel + " paint color" + (readOnly ? " (read only)" : " (click to edit)"));
            if (!readOnly && Widgets.ButtonInvisible(swatch))
                Find.WindowStack.Add(new Dialog_FrameColorPicker(frame, channel));
        }

        private static void DrawSlot(FramePawn frame, FrameAddonCategory category, string title,
            float x, float y, bool readOnly)
        {
            Thing installed = frame.Service.Installed(category);
            FrameServiceOrder order = frame.Service.OrderFor(category);
            bool active = ReferenceEquals(order, frame.Service.ActiveOrder);
            Rect box = new Rect(x, y + 18f, 80f, 80f);
            Label(new Rect(x, y, 95f, 17f), title);
            Widgets.DrawBoxSolid(box, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            if (installed != null) Widgets.ThingIcon(box.ContractedBy(8f), installed);
            else if (order?.kind == FrameServiceOrderKind.Install && order.incoming != null)
                Widgets.ThingIcon(box.ContractedBy(8f), order.incoming, 0.42f);
            Widgets.DrawBox(box, 2);
            Widgets.DrawHighlightIfMouseover(box);
            string state = active ? "SERVICING" : order?.kind == FrameServiceOrderKind.Remove ? "QUEUED REMOVE" :
                order?.kind == FrameServiceOrderKind.Install ? (installed == null ? "QUEUED" : "QUEUED REPLACE") :
                installed == null ? "EMPTY" : !frame.Loadout.IsMountValid(installed) ? "MOUNT LOST" :
                !frame.Loadout.IsModulePowered(installed) && (CompFrameLoadout.Extension(installed)?.powerDraw ?? 0) > 0
                    ? "UNPOWERED" : "INSTALLED";
            Label(new Rect(x - 6f, y + 100f, 98f, 17f), state);
            string tip = title + "\nInstalled: " + (installed?.LabelCap ?? "none");
            if (order != null) tip += "\n" + (active ? "Servicing" : "Queued") + ": " + order.kind +
                (order.incoming == null ? "" : " " + order.incoming.LabelCap) +
                "\nExpected installed: " + (order.expectedInstalled?.LabelCap ?? "empty");
            TooltipHandler.TipRegion(box, tip);
            if (!readOnly && Widgets.ButtonInvisible(box)) OpenSlotMenu(frame, category);
            if (installed != null) Widgets.InfoCardButton(x + 55f, y + 73f, installed);
        }

        private static void OpenSlotMenu(FramePawn frame, FrameAddonCategory category)
        {
            FrameServiceOrder order = frame.Service.OrderFor(category);
            bool active = ReferenceEquals(order, frame.Service.ActiveOrder);
            var options = new List<FloatMenuOption>();
            if (order != null)
                options.Add(new FloatMenuOption(active ? "Cancel active service" : "Cancel queued change",
                    () => frame.Service.CancelOrder(category)));
            if (!active)
            {
                foreach (Thing candidate in frame.Service.StoredCandidates(category))
                {
                    Thing exact = candidate;
                    options.Add(new FloatMenuOption("Install " + exact.LabelCap,
                        () => frame.Service.QueueInstall(category, exact)));
                }
                if (frame.Service.Installed(category) != null)
                    options.Add(new FloatMenuOption("Remove installed", () => frame.Service.QueueRemove(category)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("No compatible modules in valid storage", null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private static void DrawStatus(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 278f, 19f), "Integrity  " + data.Integrity.ToStringPercent());
            Label(new Rect(x, y + 20f, 278f, 19f), "State  " + ("ZF_State_" + data.OperationalState).Translate());
            Label(new Rect(x, y + 40f, 278f, 19f), "Addon power  " + data.RequiredPower + " / " + data.AvailablePower);
            string contextual = data.IsDocked ? "Docked" : data.ActiveService != null ? "Being serviced" :
                data.IsRecoverable ? "Recoverable" : data.IsHackable ? "Hackable " + data.HackProgress.ToStringPercent() : null;
            if (contextual != null) Label(new Rect(x, y + 59f, 278f, 18f), contextual);
        }

        private static void DrawFrameIssues(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 276f, 19f), "STRUCTURAL ISSUES");
            DrawIssues(FrameHealthSummary.For(data.Frame, true), x, y + 20f, 276f, () =>
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false)));
            if (Widgets.ButtonText(new Rect(x + 82f, y + 94f, 110f, 22f), "Health"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false));
        }

        private static void DrawPilot(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 90f, 20f), "PILOT", GameFont.Medium);
            if (data.Pilot == null)
            {
                Label(new Rect(x + 94f, y + 25f, 360f, 22f), "No pilot");
                return;
            }
            Pawn pilot = data.Pilot;
            Rect portrait = new Rect(x, y + 22f, 82f, 105f);
            GUI.DrawTexture(portrait, PortraitsCache.Get(pilot, portrait.size, Rot4.South));
            Widgets.DrawBox(portrait);
            Label(new Rect(x + 93f, y + 20f, 170f, 22f), pilot.LabelShortCap, GameFont.Medium);
            Label(new Rect(x + 93f, y + 44f, 170f, 20f), pilot.Faction?.Name ?? "No faction");
            Label(new Rect(x + 93f, y + 65f, 170f, 20f), pilot.Dead ? "Dead" : pilot.Downed ? "Downed" :
                pilot.InMentalState ? pilot.MentalStateDef.LabelCap : "Embarked");
            DrawIssues(FrameHealthSummary.For(pilot, false), x + 269f, y + 20f, 211f,
                () => Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true)));
            if (Widgets.ButtonText(new Rect(x + 93f, y + 100f, 75f, 24f), "Bio"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true));
            if (Widgets.ButtonText(new Rect(x + 174f, y + 100f, 84f, 24f), "Health"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, false));
        }

        private static void DrawIssues(List<FrameHealthIssue> issues, float x, float y, float width,
            System.Action openHealth)
        {
            if (issues.Count == 0) { Label(new Rect(x, y, width, 18f), "No critical issues"); return; }
            for (int index = 0; index < Mathf.Min(3, issues.Count); index++)
                Label(new Rect(x, y + index * 18f, width, 18f), issues[index].Label);
            if (issues.Count > 3)
            {
                Rect overflow = new Rect(x, y + 54f, width, 18f);
                Label(overflow, "+" + (issues.Count - 3) + " more... click for Health");
                if (Widgets.ButtonInvisible(overflow)) openHealth();
            }
        }

        private static void Label(Rect rect, string value, GameFont font = GameFont.Small)
        {
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            Text.Font = font;
            Text.WordWrap = false;
            Widgets.Label(rect, value);
            if (Text.CalcSize(value).x > rect.width) TooltipHandler.TipRegion(rect, value);
            Text.WordWrap = previousWrap;
            Text.Font = previousFont;
        }
    }
}
