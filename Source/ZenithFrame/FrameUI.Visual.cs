using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameColorChannel { Primary, Secondary, Visor }

    public sealed class Dialog_FrameColorPicker : Window
    {
        private static readonly List<Color> Palette = new List<Color>
        {
            Color.white, Color.black, Color.gray, Color.red, Color.yellow,
            Color.green, Color.cyan, Color.blue, Color.magenta
        };
        private readonly FramePawn frame;
        private readonly FrameColorChannel channel;
        private Color color;
        private string hex;
        private bool wheelDragging;
        public override Vector2 InitialSize => new Vector2(480f, 410f);

        public Dialog_FrameColorPicker(FramePawn frame, FrameColorChannel channel)
        {
            this.frame = frame;
            this.channel = channel;
            color = channel == FrameColorChannel.Primary ? frame.Livery.Primary :
                channel == FrameColorChannel.Secondary ? frame.Livery.Secondary : frame.Livery.Visor;
            hex = FrameHexColor.Format(color);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        private void Apply(Color chosen)
        {
            if (frame == null || frame.Destroyed || frame.Faction != Faction.OfPlayer) return;
            color = chosen;
            hex = FrameHexColor.Format(chosen);
            CompFrameLivery paint = frame.Livery;
            paint.SetColors(channel == FrameColorChannel.Primary ? chosen : paint.Primary,
                channel == FrameColorChannel.Secondary ? chosen : paint.Secondary,
                channel == FrameColorChannel.Visor ? chosen : paint.Visor);
        }

        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(12f, 8f, 420f, 26f), channel + " paint");
            Color beforeWheel = color;
            Widgets.HSVColorWheel(new Rect(18f, 46f, 170f, 170f), ref color, ref wheelDragging);
            if (color != beforeWheel) Apply(color);
            Widgets.DrawBoxSolid(new Rect(212f, 46f, 212f, 44f), color);
            Widgets.DrawBox(new Rect(212f, 46f, 212f, 44f));
            Widgets.Label(new Rect(212f, 100f, 210f, 25f), "Hex color");
            string edited = Widgets.TextField(new Rect(212f, 126f, 132f, 30f), hex);
            if (edited != hex)
            {
                hex = edited;
                if (FrameHexColor.TryParse(edited, out Color parsed)) Apply(parsed);
            }
            if (Widgets.ButtonText(new Rect(352f, 126f, 72f, 30f), "Apply") &&
                FrameHexColor.TryParse(hex, out Color entered)) Apply(entered);
            if (!FrameHexColor.TryParse(hex, out _))
                Widgets.Label(new Rect(212f, 160f, 215f, 22f), "Use #RRGGBB");

            for (int i = 0; i < Palette.Count; i++)
            {
                Rect swatch = new Rect(212f + (i % 5) * 43f, 188f + (i / 5) * 35f, 30f, 27f);
                Widgets.DrawBoxSolid(swatch, Palette[i]);
                Widgets.DrawBox(swatch);
                if (Widgets.ButtonInvisible(swatch)) Apply(Palette[i]);
            }

            Widgets.Label(new Rect(18f, 230f, 180f, 24f), "Saved colors");
            if (Widgets.ButtonText(new Rect(330f, 230f, 94f, 25f), "Save color"))
            {
                FrameColorPreferences settings = ZenithFrameMod.Instance?.Colors;
                string value = FrameHexColor.Format(color);
                if (settings != null && !settings.favorites.Contains(value))
                {
                    if (settings.favorites.Count == 12) settings.favorites.RemoveAt(0);
                    settings.favorites.Add(value);
                    ZenithFrameMod.Instance.WriteSettings();
                }
            }
            var favorites = ZenithFrameMod.Instance?.Colors?.favorites;
            if (favorites != null)
                for (int i = 0; i < favorites.Count; i++)
                {
                    if (!FrameHexColor.TryParse(favorites[i], out Color favorite)) continue;
                    float x = 18f + (i % 6) * 69f, y = 265f + (i / 6) * 40f;
                    Rect swatch = new Rect(x, y, 38f, 29f);
                    Widgets.DrawBoxSolid(swatch, favorite);
                    Widgets.DrawBox(swatch);
                    TooltipHandler.TipRegion(swatch, favorites[i]);
                    if (Widgets.ButtonInvisible(swatch)) Apply(favorite);
                    if (Widgets.ButtonText(new Rect(x + 39f, y + 2f, 25f, 24f), "×"))
                    {
                        favorites.RemoveAt(i);
                        ZenithFrameMod.Instance.WriteSettings();
                        break;
                    }
                }
            if (Widgets.ButtonText(new Rect(340f, 344f, 84f, 28f), "Done")) Close();
        }
    }

    // All measurements belong to this one tab; health and loadout values are
    // read again from the selected physical Frame on every redraw.
    public sealed class ITab_FrameVisual : ITab
    {
        public const float TabWidth = 620f;
        public const float TabHeight = 520f;
        public const float SlotSize = 78f;
        public const float SlotCardWidth = 100f;
        public const float SlotCardTop = 53f;
        public const float PreviewSize = 170f;
        public const float RowHeight = 23f;
        private const float OuterMargin = 14f;
        private const float PilotHeight = 103f;
        private const float SlotStatusHeight = 20f;
        private const float HeaderHeight = 40f;
        private const float ColumnGap = 16f;
        private static readonly Texture2D BleedingIcon =
            ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding");

        public ITab_FrameVisual()
        {
            labelKey = "ZF_FrameTab";
            size = new Vector2(TabWidth, TabHeight);
        }

        public override bool IsVisible => SelPawn is FramePawn;

        protected override void UpdateSize()
        {
            // InspectTabBase positions this tab PaneTopY - 30 - size.y.
            // Keep a small top margin at resolutions below the design height.
            size = new Vector2(TabWidth, Mathf.Min(TabHeight, Mathf.Max(0f, PaneTopY - 38f)));
        }

        protected override void FillTab()
        {
            FramePawn frame = SelPawn as FramePawn;
            if (frame == null) return;
            FrameUiSnapshot data = FrameUiSnapshot.From(frame);
            float rightX = size.x - OuterMargin - SlotCardWidth;
            float centerX = OuterMargin + SlotCardWidth + ColumnGap;
            float centerWidth = rightX - ColumnGap - centerX;
            float mainBottom = size.y - PilotHeight - 10f;
            float slotStep = (mainBottom - SlotCardTop) / 3f;
            float slotSize = Mathf.Min(SlotSize, slotStep - 41f);
            float previewSize = Mathf.Min(PreviewSize, mainBottom - 214f);
            float previewY = HeaderHeight + 14f;
            float conditionY = previewY + previewSize + 54f;

            DrawHeader(data);
            DrawSlot(frame, FrameAddonCategory.Head, "Head", OuterMargin, SlotCardTop, slotSize, slotStep, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Weapon, "Primary", OuterMargin, SlotCardTop + slotStep, slotSize, slotStep, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Utility, "Utility", OuterMargin, SlotCardTop + slotStep * 2f, slotSize, slotStep, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Shoulder, "Shoulder", rightX, SlotCardTop, slotSize, slotStep, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Offhand, "Offhand", rightX, SlotCardTop + slotStep, slotSize, slotStep, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Power, "Power", rightX, SlotCardTop + slotStep * 2f, slotSize, slotStep, data.IsReadOnly);
            DrawPreviewAndColors(data, centerX + 18f, previewY, previewSize);
            DrawStatus(data, centerX, previewY + previewSize + 7f, centerWidth);
            DrawFrameIssues(data, centerX, conditionY, centerWidth, mainBottom - conditionY - 3f);
            Widgets.DrawLineHorizontal(OuterMargin, mainBottom + 5f, size.x - OuterMargin * 2f);
            DrawPilot(data, mainBottom + 6f);
        }

        private static void DrawHeader(FrameUiSnapshot data)
        {
            Label(new Rect(18f, 5f, 430f, 30f), data.Frame.LabelShortCap, GameFont.Medium);
            Label(new Rect(18f, 29f, 480f, 23f), data.Frame.def.LabelCap);
            if (data.CanRename && Widgets.ButtonText(new Rect(TabWidth - 112f, 9f, 84f, 26f), "Rename"))
                Find.WindowStack.Add(new Dialog_RenameFrame(data.Frame));
        }

        private static void DrawPreviewAndColors(FrameUiSnapshot data, float x, float y, float previewSize)
        {
            Rect preview = new Rect(x, y, previewSize, previewSize);
            Widgets.DrawBoxSolid(preview, new Color(0.15f, 0.15f, 0.15f, 0.6f));
            // Keep the 3.5-world-unit machine inside its UI portrait camera.
            GUI.DrawTexture(preview, PortraitsCache.Get(data.Frame, preview.size, Rot4.South,
                cameraZoom: 0.43f), ScaleMode.ScaleToFit);
            Widgets.DrawBox(preview);
            TooltipHandler.TipRegion(preview, "Current assembled Frame and livery");
            float swatchX = x + previewSize + 16f;
            DrawSwatch(data.Frame, FrameColorChannel.Primary, swatchX, y + 10f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Secondary, swatchX, y + 58f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Visor, swatchX, y + 106f, data.IsReadOnly);
            if (!data.IsReadOnly && Widgets.ButtonText(new Rect(swatchX, y + previewSize - 29f, 120f, 26f), "Reset paint"))
                data.Frame.Livery.ResetColors();
        }

        private static void DrawSwatch(FramePawn frame, FrameColorChannel channel, float x, float y, bool readOnly)
        {
            Color color = channel == FrameColorChannel.Primary ? frame.Livery.Primary :
                channel == FrameColorChannel.Secondary ? frame.Livery.Secondary : frame.Livery.Visor;
            Rect swatch = new Rect(x, y, 29f, 29f);
            Widgets.DrawBoxSolid(swatch, color);
            Widgets.DrawBox(swatch, 2);
            Label(new Rect(x + 35f, y + 3f, 105f, 23f), channel.ToString());
            TooltipHandler.TipRegion(swatch, channel + " paint color" + (readOnly ? " (read only)" : " (click to edit)"));
            if (!readOnly && Widgets.ButtonInvisible(swatch))
                Find.WindowStack.Add(new Dialog_FrameColorPicker(frame, channel));
        }

        public static Rect SlotBoxRect(float x, float y, float slotSize) =>
            new Rect(x + 10f, y + 21f, slotSize, slotSize);

        private static void DrawSlot(FramePawn frame, FrameAddonCategory category, string title,
            float x, float y, float slotSize, float slotStep, bool readOnly)
        {
            Thing installed = frame.Service.Installed(category);
            FrameServiceOrder order = frame.Service.OrderFor(category);
            bool active = order != null && ReferenceEquals(order, frame.Service.ActiveOrder);
            Rect card = new Rect(x, y, SlotCardWidth, slotStep - 3f);
            Widgets.DrawBoxSolid(card, new Color(0.12f, 0.16f, 0.19f, 0.46f));
            Widgets.DrawBox(card);
            Label(new Rect(x + 8f, y + 2f, SlotCardWidth - 12f, 23f), title);
            Rect box = SlotBoxRect(x, y, slotSize);
            Widgets.DrawBoxSolid(box, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            if (installed != null) DrawContainedIcon(box.ContractedBy(7f), installed, 1f);
            else if (order?.kind == FrameServiceOrderKind.Install && order.incoming != null)
                DrawContainedIcon(box.ContractedBy(7f), order.incoming, 0.42f);
            Widgets.DrawBox(box, 2);
            Widgets.DrawHighlightIfMouseover(box);
            string state = active ? "Servicing" : order?.kind == FrameServiceOrderKind.Remove ? "Remove queued" :
                order?.kind == FrameServiceOrderKind.Install ? (installed == null ? "Queued" : "Replacing") :
                installed == null ? null : !frame.Loadout.IsMountValid(installed) ? "Mount lost" :
                !frame.Loadout.IsModulePowered(installed) && (CompFrameLoadout.Extension(installed)?.powerDraw ?? 0) > 0
                    ? "Unpowered" : null;
            if (state != null)
            {
                Rect status = new Rect(x + 4f, box.yMax, SlotCardWidth - 8f, SlotStatusHeight);
                Label(status, state, GameFont.Tiny, TextAnchor.MiddleCenter);
                TooltipHandler.TipRegion(status, state);
            }
            string tip = title + "\nInstalled: " + (installed?.LabelCap ?? "none");
            if (order != null) tip += "\n" + (active ? "Servicing" : "Queued") + ": " + order.kind +
                (order.incoming == null ? "" : " " + order.incoming.LabelCap) +
                "\nExpected installed: " + (order.expectedInstalled?.LabelCap ?? "empty");
            TooltipHandler.TipRegion(box, tip);
            // Keep the vanilla InfoCard corner outside both menu hitboxes.
            Rect info = new Rect(box.xMax - 27f, box.yMax - 27f, 25f, 25f);
            Rect upperHit = new Rect(box.x, box.y, box.width, box.height - 27f);
            Rect lowerHit = new Rect(box.x, box.yMax - 27f, box.width - 27f, 27f);
            if (!readOnly && (Widgets.ButtonInvisible(upperHit) || Widgets.ButtonInvisible(lowerHit)))
                Find.WindowStack.Add(new FloatMenu(SlotOptions(frame, category)));
            if (installed != null) Widgets.InfoCardButton(info.x, info.y, installed);
        }

        private static void DrawContainedIcon(Rect inner, Thing thing, float alpha)
        {
            Texture texture = Widgets.GetIconFor(thing, inner.size, null, false,
                out _, out float angle, out _, out Color color, out Material material);
            if (texture == null || texture == BaseContent.BadTex) return;
            Color previous = GUI.color;
            GUI.color = color;
            float scale = Mathf.Abs(angle) > 1f ? 0.65f : 0.9f;
            Widgets.DrawTextureFitted(inner, texture, scale,
                new Vector2(texture.width, texture.height), new Rect(0f, 0f, 1f, 1f), angle, material, alpha);
            GUI.color = previous;
        }

        public static List<FloatMenuOption> SlotOptions(FramePawn frame, FrameAddonCategory category)
        {
            FrameServiceOrder order = frame.Service.OrderFor(category);
            bool active = order != null && ReferenceEquals(order, frame.Service.ActiveOrder);
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
            return options;
        }

        private static void DrawStatus(FrameUiSnapshot data, float x, float y, float width)
        {
            string contextual = data.IsDocked ? "Docked" : data.ActiveService != null ? "Being serviced" :
                data.IsRecoverable ? "Recoverable" : data.IsHackable ? "Hackable " + data.HackProgress.ToStringPercent() : null;
            string state = "State  " + ("ZF_State_" + data.OperationalState).Translate();
            if (contextual != null) state += "  ·  " + contextual;
            Label(new Rect(x, y, width, RowHeight), state);
            Label(new Rect(x, y + RowHeight, width, RowHeight),
                "Addon power  " + data.RequiredPower + " / " + data.AvailablePower);
        }

        private static void DrawFrameIssues(FrameUiSnapshot data, float x, float y, float width, float height)
        {
            Rect panel = new Rect(x, y, width, height);
            Widgets.DrawBoxSolid(panel, new Color(0.12f, 0.16f, 0.19f, 0.46f));
            Widgets.DrawBox(panel);
            Label(new Rect(x + 8f, y + 3f, width - 94f, RowHeight), "Frame condition");
            if (Widgets.ButtonText(new Rect(x + width - 78f, y + 2f, 70f, 25f), "Health"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false));
            Widgets.DrawLineHorizontal(x + 8f, y + 29f, width - 16f);
            DrawIssues(FrameHealthSummary.For(data.Frame, true), new Rect(x + 8f, y + 34f, width - 16f, height - 39f),
                () => Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false)));
        }

        private static void DrawPilot(FrameUiSnapshot data, float y)
        {
            Label(new Rect(20f, y, 76f, RowHeight), "Pilot");
            if (data.Pilot == null)
            {
                Label(new Rect(95f, y + 28f, 195f, RowHeight), "No pilot");
                return;
            }
            Pawn pilot = data.Pilot;
            Rect portrait = new Rect(20f, y + 24f, 62f, 68f);
            Widgets.DrawBoxSolid(portrait, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            GUI.DrawTexture(portrait.ContractedBy(4f), PortraitsCache.Get(pilot, portrait.size, Rot4.South,
                cameraZoom: 0.95f), ScaleMode.ScaleToFit);
            Widgets.DrawBox(portrait);
            Label(new Rect(95f, y + 17f, 211f, 21f), pilot.LabelShortCap);
            Label(new Rect(95f, y + 38f, 211f, 21f), pilot.Faction?.Name ?? "No faction");
            Label(new Rect(95f, y + 59f, 211f, 21f), pilot.Dead ? "Dead" : pilot.Downed ? "Downed" :
                pilot.InMentalState ? pilot.MentalStateDef.LabelCap : "Embarked");
            if (Widgets.ButtonText(new Rect(95f, y + 81f, 132f, 22f), "Information"))
                OpenPilotInformation(pilot);
            Widgets.DrawLineVertical(316f, y + 10f, 87f);
            Label(new Rect(330f, y + 12f, 270f, RowHeight), "Pilot health");
            DrawIssues(FrameHealthSummary.For(pilot, false), new Rect(330f, y + 37f, 276f, 65f),
                () => Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true, FrameInfoPage.Health)));
        }

        public static Dialog_FramePawnInfo OpenPilotInformation(Pawn pilot)
        {
            var dialog = new Dialog_FramePawnInfo(pilot, true);
            Find.WindowStack.Add(dialog);
            return dialog;
        }

        private static void DrawIssues(List<FrameHealthIssue> issues, Rect area, System.Action openHealth)
        {
            if (issues.Count == 0)
            {
                Label(new Rect(area.x, area.y, area.width, RowHeight), "No critical issues");
                return;
            }
            int shown = 0;
            float used = 0f;
            while (shown < issues.Count)
            {
                float rowHeight = HealthRowHeight(issues[shown], area.width);
                float reserve = shown + 1 < issues.Count ? RowHeight : 0f;
                if (used + rowHeight + reserve > area.height) break;
                used += rowHeight;
                shown++;
            }
            float rowY = area.y;
            for (int index = 0; index < shown; index++)
            {
                float rowHeight = HealthRowHeight(issues[index], area.width);
                DrawHealthRow(issues[index], new Rect(area.x, rowY, area.width, rowHeight));
                rowY += rowHeight;
            }
            if (issues.Count > shown)
            {
                Rect overflow = new Rect(area.x, rowY, area.width, RowHeight);
                Label(overflow, "+" + (issues.Count - shown) + " more  ·  Health");
                if (Widgets.ButtonInvisible(overflow)) openHealth();
            }
        }

        private static float HealthRowHeight(FrameHealthIssue issue, float width)
        {
            float leftWidth = Mathf.Floor(width * 0.37f);
            float rightWidth = width - leftWidth - IconWidth(issue) - 8f;
            GameFont oldFont = Text.Font;
            bool oldWrap = Text.WordWrap;
            Text.Font = GameFont.Small;
            Text.WordWrap = true;
            float height = Mathf.Max(Text.CalcHeight(issue.PartLabel, leftWidth),
                Text.CalcHeight(issue.ConditionLabel, rightWidth)) + 2f;
            Text.Font = oldFont;
            Text.WordWrap = oldWrap;
            return Mathf.Max(RowHeight, height);
        }

        private static float IconWidth(FrameHealthIssue issue) =>
            (issue.Bleeding ? 19f : 0f) + (issue.StateIcon.HasValue ? 19f : 0f);

        private static void DrawHealthRow(FrameHealthIssue issue, Rect row)
        {
            float leftWidth = Mathf.Floor(row.width * 0.37f);
            float iconsWidth = IconWidth(issue);
            Rect left = new Rect(row.x, row.y, leftWidth - 4f, row.height);
            Rect right = new Rect(row.x + leftWidth, row.y,
                row.width - leftWidth - iconsWidth - 5f, row.height);
            Widgets.DrawHighlightIfMouseover(row);
            Label(left, issue.PartLabel, GameFont.Small, TextAnchor.UpperLeft, true, issue.PartColor);
            Label(right, issue.ConditionLabel, GameFont.Small, TextAnchor.UpperLeft, true, issue.ConditionColor);
            float iconX = row.xMax - iconsWidth;
            if (issue.Bleeding)
            {
                GUI.DrawTexture(new Rect(iconX, row.y + 2f, 16f, 16f), BleedingIcon);
                iconX += 19f;
            }
            if (issue.StateIcon.HasValue)
            {
                Color previous = GUI.color;
                GUI.color = issue.StateIcon.Color;
                GUI.DrawTexture(new Rect(iconX, row.y + 2f, 16f, 16f), issue.StateIcon.Texture);
                GUI.color = previous;
            }
            TooltipHandler.TipRegion(row, issue.Tooltip);
        }

        private static void Label(Rect rect, string value, GameFont font = GameFont.Small,
            TextAnchor anchor = TextAnchor.UpperLeft, bool wrap = false, Color? color = null)
        {
            GameFont previousFont = Text.Font;
            bool previousWrap = Text.WordWrap;
            TextAnchor previousAnchor = Text.Anchor;
            Color previousColor = GUI.color;
            Text.Font = font;
            Text.WordWrap = wrap;
            Text.Anchor = anchor;
            GUI.color = color ?? Color.white;
            if (!wrap) rect.height = Mathf.Max(rect.height, Text.LineHeight);
            Widgets.Label(rect, value);
            if (!wrap && Text.CalcSize(value).x > rect.width) TooltipHandler.TipRegion(rect, value);
            GUI.color = previousColor;
            Text.Anchor = previousAnchor;
            Text.WordWrap = previousWrap;
            Text.Font = previousFont;
        }
    }
}
