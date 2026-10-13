using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public enum FrameColorChannel { Primary, Secondary, Visor }

    public static class FramePaintPresets
    {
        public static readonly string[] Groups = { "Neutral", "Military", "Industrial", "Ace", "Sensor" };
        public static readonly string[][] Hex =
        {
            new[] { "F0F0EC", "B8BAB7", "6A6D70", "24272B" },
            new[] { "777A55", "56634B", "3E4A43", "B5A27A" },
            new[] { "4D5258", "627181", "8A6752", "C47A26" },
            new[] { "B92B32", "E8652D", "D9B52C", "315FD4" },
            new[] { "27CFE2", "42D66B", "B449D1", "7657E8" }
        };
    }

    public sealed class Dialog_FrameColorPicker : Window
    {
        private readonly FramePawn frame;
        private FrameColorChannel channel;
        private Color color;
        private string hex;
        private bool wheelDragging;
        private bool valueDragging;
        private float selectedHue;
        private float selectedSaturation;
        public override Vector2 InitialSize => new Vector2(520f, 420f);
        public FrameColorChannel SelectedChannel => channel;

        public Dialog_FrameColorPicker(FramePawn frame, FrameColorChannel channel)
        {
            this.frame = frame;
            this.channel = channel;
            color = ChannelColor(channel);
            hex = FrameHexColor.Format(color);
            RememberHue(color, true);
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
        }

        private Color ChannelColor(FrameColorChannel selected) => selected == FrameColorChannel.Primary
            ? frame.Livery.Primary : selected == FrameColorChannel.Secondary
                ? frame.Livery.Secondary : frame.Livery.Visor;

        public void SelectChannel(FrameColorChannel selected)
        {
            channel = selected;
            color = ChannelColor(selected);
            hex = FrameHexColor.Format(color);
            RememberHue(color, true);
        }

        private void RememberHue(Color chosen, bool resetOnBlack = false)
        {
            Color.RGBToHSV(chosen, out float hue, out float saturation, out float value);
            if (value > 0.0001f || resetOnBlack)
            {
                selectedHue = hue;
                selectedSaturation = saturation;
            }
        }

        protected override void SetInitialSizeAndPosition()
        {
            base.SetInitialSizeAndPosition();
            windowRect = FramePopupPlacement.Beside(ITab_FrameVisual.CurrentScreenRect(), windowRect.size,
                UI.screenWidth, UI.screenHeight, 500f);
        }

        internal bool CommitHex()
        {
            if (!FrameHexColor.TryParse(hex, out Color entered)) return false;
            Apply(entered);
            return true;
        }

        internal void Apply(Color chosen)
        {
            if (frame == null || frame.Destroyed || frame.Faction != Faction.OfPlayer) return;
            color = chosen;
            hex = FrameHexColor.Format(chosen);
            RememberHue(chosen);
            CompFrameLivery paint = frame.Livery;
            paint.SetColors(channel == FrameColorChannel.Primary ? chosen : paint.Primary,
                channel == FrameColorChannel.Secondary ? chosen : paint.Secondary,
                channel == FrameColorChannel.Visor ? chosen : paint.Visor);
        }

        public override void DoWindowContents(Rect rect)
        {
            Widgets.Label(new Rect(12f, 3f, 280f, 26f), "Frame paint");
            if (Widgets.ButtonText(new Rect(rect.width - 86f, 3f, 78f, 27f), "Done")) Close();
            for (int i = 0; i < 3; i++)
            {
                FrameColorChannel selected = (FrameColorChannel)i;
                Rect tab = new Rect(12f + i * 105f, 34f, 98f, 26f);
                if (Widgets.ButtonText(tab, selected.ToString())) SelectChannel(selected);
                if (selected == channel)
                    Widgets.DrawBoxSolid(new Rect(tab.x + 4f, tab.yMax - 4f, tab.width - 8f, 3f),
                        new Color(0.18f, 0.8f, 0.9f));
            }
            Widgets.DrawBoxSolid(new Rect(10f, 65f, rect.width - 20f, 135f),
                new Color(0.12f, 0.16f, 0.19f, 0.5f));
            Color beforeWheel = color;
            Widgets.HSVColorWheel(new Rect(14f, 67f, 130f, 130f), ref color, ref wheelDragging);
            if (color != beforeWheel) Apply(color);
            Color.RGBToHSV(color, out _, out _, out float value);
            Rect valueBar = new Rect(158f, 68f, 24f, 128f);
            for (int strip = 0; strip < 32; strip++)
                Widgets.DrawBoxSolid(new Rect(valueBar.x, valueBar.y + strip * 4f, valueBar.width, 4f),
                    Color.HSVToRGB(selectedHue, selectedSaturation, 1f - strip / 31f));
            Widgets.DrawBox(valueBar);
            Widgets.DrawBoxSolid(new Rect(valueBar.x - 3f,
                valueBar.y + Mathf.Clamp((1f - value) * valueBar.height - 1f, 0f, valueBar.height - 3f),
                valueBar.width + 6f, 3f), Color.white);
            Event evt = Event.current;
            if (evt.type == EventType.MouseDown && evt.button == 0 && valueBar.Contains(evt.mousePosition))
                valueDragging = true;
            if (valueDragging && (evt.type == EventType.MouseDown || evt.type == EventType.MouseDrag))
            {
                Apply(Color.HSVToRGB(selectedHue, selectedSaturation,
                    1f - Mathf.Clamp01((evt.mousePosition.y - valueBar.y) / valueBar.height)));
                evt.Use();
            }
            if (evt.rawType == EventType.MouseUp) valueDragging = false;

            Rect current = new Rect(211f, 82f, 40f, 30f);
            Widgets.DrawBoxSolid(current, color);
            Widgets.DrawBox(current);
            GUI.SetNextControlName("FramePaintHex");
            hex = Widgets.TextField(new Rect(260f, 82f, rect.width - 272f, 30f), hex);
            if (evt.type == EventType.KeyDown &&
                (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter) &&
                GUI.GetNameOfFocusedControl() == "FramePaintHex")
            {
                if (CommitHex()) GUI.FocusControl(null);
                evt.Use();
            }
            if (!FrameHexColor.TryParse(hex, out _))
                Widgets.Label(new Rect(211f, 115f, 248f, 20f), "Use RRGGBB or #RRGGBB");
            if (Widgets.ButtonText(new Rect(211f, 149f, 70f, 27f), "Copy"))
                GUIUtility.systemCopyBuffer = FrameHexColor.Format(color);
            if (Widgets.ButtonText(new Rect(290f, 149f, 70f, 27f), "Paste") &&
                FrameHexColor.TryParse(GUIUtility.systemCopyBuffer, out Color pasted)) Apply(pasted);
            if (Widgets.ButtonText(new Rect(369f, 149f, rect.width - 381f, 27f), "Default"))
            {
                FrameChassisVisualExtension defaults = frame.Livery.Defaults;
                Apply(channel == FrameColorChannel.Primary ? defaults?.defaultPrimary ?? Color.white :
                    channel == FrameColorChannel.Secondary ? defaults?.defaultSecondary ?? Color.white :
                    defaults?.defaultVisor ?? Color.white);
            }

            Widgets.DrawBoxSolid(new Rect(10f, 205f, rect.width - 20f, 102f),
                new Color(0.12f, 0.16f, 0.19f, 0.5f));
            for (int group = 0; group < FramePaintPresets.Groups.Length; group++)
            {
                float y = 207f + group * 20f;
                Widgets.Label(new Rect(14f, y, 80f, 20f), FramePaintPresets.Groups[group]);
                for (int index = 0; index < 4; index++)
                {
                    string preset = FramePaintPresets.Hex[group][index];
                    FrameHexColor.TryParse(preset, out Color sample);
                    Rect swatch = new Rect(98f + index * 92f, y + 1f, 82f, 17f);
                    Widgets.DrawBoxSolid(swatch, sample);
                    Widgets.DrawBox(swatch);
                    TooltipHandler.TipRegion(swatch, "#" + preset);
                    if (Widgets.ButtonInvisible(swatch)) Apply(sample);
                }
            }

            Widgets.DrawBoxSolid(new Rect(10f, 311f, rect.width - 20f, 70f),
                new Color(0.12f, 0.16f, 0.19f, 0.5f));
            Widgets.Label(new Rect(14f, 314f, 180f, 20f), "Favorites");
            if (Widgets.ButtonText(new Rect(rect.width - 109f, 313f, 99f, 22f), "Save color"))
            {
                FrameColorPreferences settings = ZenithFrameMod.Instance?.Colors;
                string favoriteHex = FrameHexColor.Format(color);
                if (settings != null && !settings.favorites.Contains(favoriteHex))
                {
                    if (settings.favorites.Count >= 12) settings.favorites.RemoveAt(0);
                    settings.favorites.Add(favoriteHex);
                    ZenithFrameMod.Instance.WriteSettings();
                }
            }
            var favorites = ZenithFrameMod.Instance?.Colors?.favorites;
            if (favorites != null)
                for (int i = 0; i < favorites.Count; i++)
                {
                    if (!FrameHexColor.TryParse(favorites[i], out Color favorite)) continue;
                    float x = 14f + (i % 6) * 77f, y = 339f + (i / 6) * 20f;
                    Rect swatch = new Rect(x, y, 52f, 18f);
                    Widgets.DrawBoxSolid(swatch, favorite);
                    Widgets.DrawBox(swatch);
                    TooltipHandler.TipRegion(swatch, favorites[i]);
                    if (Widgets.ButtonInvisible(swatch)) Apply(favorite);
                    if (Widgets.ButtonText(new Rect(x + 53f, y, 18f, 18f), "×"))
                    {
                        favorites.RemoveAt(i);
                        ZenithFrameMod.Instance.WriteSettings();
                        break;
                    }
                }
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
        private static Rect drawnBounds;
        private static int drawnAtFrame = -10;
        private static readonly Texture2D BleedingIcon =
            ContentFinder<Texture2D>.Get("UI/Icons/Medical/Bleeding");

        public ITab_FrameVisual()
        {
            labelKey = "ZF_FrameTab";
            size = new Vector2(TabWidth, TabHeight);
        }

        public override bool IsVisible => SelPawn is FramePawn;

        internal static Rect CurrentScreenRect()
        {
            if (Time.frameCount - drawnAtFrame <= 1) return drawnBounds;
            // Native fallback belongs to InspectTabBase rather than rebuilding
            // a presumed MainTabWindow_Inspect host and x coordinate here.
            return new ITab_FrameVisual().TabRect;
        }

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
            // Capture the geometry of the GUI group actually drawing this tab.
            // A host moving that group needs no named-mod bridge.
            drawnBounds = UI.GUIToScreenRect(new Rect(Vector2.zero, size));
            drawnAtFrame = Time.frameCount;
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
                cameraZoom: data.Frame.Livery?.Defaults?.portraitZoom ?? 0.43f), ScaleMode.ScaleToFit);
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
            string state = active ? "Servicing" : order != null && frame.Service.IsBlocked(order) ? "Blocked" :
                order?.kind == FrameServiceOrderKind.Remove ? "Remove queued" :
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
            Widgets.DrawHighlightIfMouseover(panel);
            if (Widgets.ButtonInvisible(panel))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false));
            DrawIssues(FrameHealthSummary.For(data.Frame, true),
                new Rect(x + 8f, y + 4f, width - 16f, height - 8f));
        }

        private static void DrawPilot(FrameUiSnapshot data, float y)
        {
            if (data.Pilot == null)
            {
                Label(new Rect(20f, y + 10f, 250f, RowHeight), "No pilot");
                return;
            }
            Pawn pilot = data.Pilot;
            Rect portrait = new Rect(20f, y + 6f, 62f, 68f);
            Widgets.DrawBoxSolid(portrait, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            GUI.DrawTexture(portrait.ContractedBy(4f), PortraitsCache.Get(pilot, portrait.size, Rot4.South,
                cameraZoom: 0.95f), ScaleMode.ScaleToFit);
            Widgets.DrawBox(portrait);
            Label(new Rect(95f, y + 3f, 175f, 21f), pilot.LabelShortCap);
            Label(new Rect(95f, y + 24f, 175f, 21f), pilot.Faction?.Name ?? "No faction");
            Label(new Rect(95f, y + 45f, 175f, 21f), pilot.Dead ? "Dead" : pilot.Downed ? "Downed" :
                pilot.InMentalState ? pilot.MentalStateDef.LabelCap : "Embarked");
            if (Widgets.ButtonText(new Rect(95f, y + 75f, 132f, 22f), "Information"))
                OpenPilotInformation(pilot);
            Rect health = new Rect(282f, y + 1f, 324f, 100f);
            Widgets.DrawHighlightIfMouseover(health);
            if (Widgets.ButtonInvisible(health))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true, FrameInfoPage.Health));
            DrawIssues(FrameHealthSummary.For(pilot, false), health.ContractedBy(4f));
        }

        public static Dialog_FramePawnInfo OpenPilotInformation(Pawn pilot)
        {
            var dialog = new Dialog_FramePawnInfo(pilot, true);
            Find.WindowStack.Add(dialog);
            return dialog;
        }

        private static void DrawIssues(List<FrameHealthIssue> issues, Rect area)
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
                float reserve = shown + 1 < issues.Count ? RowHeight : 0f;
                if (used + RowHeight + reserve > area.height) break;
                used += RowHeight;
                shown++;
            }
            float rowY = area.y;
            for (int index = 0; index < shown; index++)
            {
                DrawHealthRow(issues[index], new Rect(area.x, rowY, area.width, RowHeight));
                rowY += RowHeight;
            }
            if (issues.Count > shown)
            {
                Rect overflow = new Rect(area.x, rowY, area.width, RowHeight);
                Label(overflow, "+" + (issues.Count - shown) + " more");
            }
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
            Label(left, issue.PartLabel, GameFont.Small, TextAnchor.UpperLeft, false, issue.PartColor);
            Label(right, issue.ConditionLabel, GameFont.Small, TextAnchor.UpperLeft, false, issue.ConditionColor);
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
