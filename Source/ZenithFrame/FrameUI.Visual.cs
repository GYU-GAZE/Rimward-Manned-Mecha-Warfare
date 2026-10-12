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

    // Fixed visual composition. Every value is read from the selected physical
    // Frame and its exact Thing/Pawn owners on each redraw.
    public sealed class ITab_FrameVisual : ITab
    {
        public ITab_FrameVisual()
        {
            labelKey = "ZF_FrameTab";
            size = new Vector2(720f, 570f);
        }

        public override bool IsVisible => SelPawn is FramePawn;

        protected override void FillTab()
        {
            FramePawn frame = SelPawn as FramePawn;
            if (frame == null) return;
            FrameUiSnapshot data = FrameUiSnapshot.From(frame);
            DrawHeader(data, 16f, 8f);
            DrawSlot(frame, FrameAddonCategory.Head, "Head", 20f, 51f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Shoulder, "Shoulder", 605f, 51f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Weapon, "Primary", 20f, 167f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Offhand, "Offhand", 605f, 167f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Utility, "Utility", 20f, 283f, data.IsReadOnly);
            DrawSlot(frame, FrameAddonCategory.Power, "Power", 605f, 283f, data.IsReadOnly);
            DrawPreviewAndColors(data, 242f, 53f);
            DrawStatus(data, 232f, 285f);
            DrawFrameIssues(data, 137f, 365f);
            Widgets.DrawLineHorizontal(18f, 438f, 682f);
            DrawPilot(data, 20f, 445f);
        }

        private static void DrawHeader(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x + 4f, y, 480f, 25f), data.Frame.LabelShortCap, GameFont.Medium);
            if (data.CanRename && Widgets.ButtonText(new Rect(x + 570f, y, 75f, 25f), "Rename"))
                Find.WindowStack.Add(new Dialog_RenameFrame(data.Frame));
            Label(new Rect(x + 4f, y + 25f, 560f, 18f), data.Frame.def.LabelCap);
        }

        private static void DrawPreviewAndColors(FrameUiSnapshot data, float x, float y)
        {
            Rect preview = new Rect(x, y, 218f, 218f);
            Widgets.DrawBoxSolid(preview, new Color(0.15f, 0.15f, 0.15f, 0.6f));
            // Vanilla's cache renderer uses an orthographic camera of 1/zoom.
            // The 3.5-world-unit chassis needs a wider UI camera than a human.
            GUI.DrawTexture(preview, PortraitsCache.Get(data.Frame, preview.size, Rot4.South,
                cameraZoom: 0.43f), ScaleMode.ScaleToFit);
            Widgets.DrawBox(preview);
            TooltipHandler.TipRegion(preview, "Current assembled Frame and livery");
            DrawSwatch(data.Frame, FrameColorChannel.Primary, x + 233f, y + 12f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Secondary, x + 233f, y + 59f, data.IsReadOnly);
            DrawSwatch(data.Frame, FrameColorChannel.Visor, x + 233f, y + 106f, data.IsReadOnly);
            if (!data.IsReadOnly && Widgets.ButtonText(new Rect(x + 233f, y + 169f, 95f, 25f), "Reset paint"))
                data.Frame.Livery.ResetColors();
        }

        private static void DrawSwatch(FramePawn frame, FrameColorChannel channel, float x, float y, bool readOnly)
        {
            Color color = channel == FrameColorChannel.Primary ? frame.Livery.Primary :
                channel == FrameColorChannel.Secondary ? frame.Livery.Secondary : frame.Livery.Visor;
            Rect swatch = new Rect(x, y, 34f, 34f);
            Widgets.DrawBoxSolid(swatch, color);
            Widgets.DrawBox(swatch, 2);
            Label(new Rect(x + 41f, y + 7f, 67f, 20f), channel.ToString());
            TooltipHandler.TipRegion(swatch, channel + " paint color" + (readOnly ? " (read only)" : " (click to edit)"));
            if (!readOnly && Widgets.ButtonInvisible(swatch))
                Find.WindowStack.Add(new Dialog_FrameColorPicker(frame, channel));
        }

        private static void DrawSlot(FramePawn frame, FrameAddonCategory category, string title,
            float x, float y, bool readOnly)
        {
            Thing installed = frame.Service.Installed(category);
            FrameServiceOrder order = frame.Service.OrderFor(category);
            bool active = order != null && ReferenceEquals(order, frame.Service.ActiveOrder);
            Rect box = new Rect(x, y + 20f, 96f, 96f);
            Label(new Rect(x, y, 112f, 18f), title);
            Widgets.DrawBoxSolid(box, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            if (installed != null) DrawContainedIcon(box.ContractedBy(9f), installed, 1f);
            else if (order?.kind == FrameServiceOrderKind.Install && order.incoming != null)
                DrawContainedIcon(box.ContractedBy(9f), order.incoming, 0.42f);
            Widgets.DrawBox(box, 2);
            Widgets.DrawHighlightIfMouseover(box);
            string state = active ? "Servicing" : order?.kind == FrameServiceOrderKind.Remove ? "Remove queued" :
                order?.kind == FrameServiceOrderKind.Install ? (installed == null ? "Queued" : "Replacing") :
                installed == null ? null : !frame.Loadout.IsMountValid(installed) ? "Mount lost" :
                !frame.Loadout.IsModulePowered(installed) && (CompFrameLoadout.Extension(installed)?.powerDraw ?? 0) > 0
                    ? "Unpowered" : null;
            if (state != null)
            {
                Rect badge = new Rect(box.x + 2f, box.yMax - 22f, box.width - 4f, 20f);
                Widgets.DrawBoxSolid(badge, new Color(0.10f, 0.10f, 0.10f, 0.93f));
                Label(badge, state);
            }
            string tip = title + "\nInstalled: " + (installed?.LabelCap ?? "none");
            if (order != null) tip += "\n" + (active ? "Servicing" : "Queued") + ": " + order.kind +
                (order.incoming == null ? "" : " " + order.incoming.LabelCap) +
                "\nExpected installed: " + (order.expectedInstalled?.LabelCap ?? "empty");
            TooltipHandler.TipRegion(box, tip);
            // The info corner is outside BOTH menu hitboxes. A full-box
            // ButtonInvisible consumed the vanilla InfoCard click.
            Rect info = new Rect(box.xMax - 27f, box.yMax - 27f, 25f, 25f);
            Rect upperHit = new Rect(box.x, box.y, box.width, box.height - 27f);
            Rect lowerHit = new Rect(box.x, box.yMax - 27f, box.width - 27f, 27f);
            if (!readOnly && (Widgets.ButtonInvisible(upperHit) || Widgets.ButtonInvisible(lowerHit)))
                OpenSlotMenu(frame, category);
            if (installed != null) Widgets.InfoCardButton(info.x, info.y, installed);
        }

        private static void DrawContainedIcon(Rect inner, Thing thing, float alpha)
        {
            Texture texture = Widgets.GetIconFor(thing, inner.size, null, false,
                out _, out float angle, out _, out Color color, out Material material);
            if (texture == null || texture == BaseContent.BadTex) return;
            Color previous = GUI.color;
            GUI.color = color;
            // Use the icon texture's own proportions, not the world drawSize or
            // GenUI.IconDrawScale (2.5 for the development rifle).
            float scale = Mathf.Abs(angle) > 1f ? 0.65f : 0.9f;
            Widgets.DrawTextureFitted(inner, texture, scale,
                new Vector2(texture.width, texture.height), new Rect(0f, 0f, 1f, 1f), angle, material, alpha);
            GUI.color = previous;
        }

        private static void OpenSlotMenu(FramePawn frame, FrameAddonCategory category)
        {
            Find.WindowStack.Add(new FloatMenu(SlotOptions(frame, category)));
        }

        // The drawn slot and the focused acceptance harness use this same
        // action list, so the menu's real remove/replace/cancel path is checked.
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

        private static void DrawStatus(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 355f, 20f), "Integrity  " + data.Integrity.ToStringPercent());
            Label(new Rect(x, y + 22f, 355f, 20f), "State  " + ("ZF_State_" + data.OperationalState).Translate());
            Label(new Rect(x, y + 44f, 355f, 20f), "Addon power  " + data.RequiredPower + " / " + data.AvailablePower);
            string contextual = data.IsDocked ? "Docked" : data.ActiveService != null ? "Being serviced" :
                data.IsRecoverable ? "Recoverable" : data.IsHackable ? "Hackable " + data.HackProgress.ToStringPercent() : null;
            if (contextual != null) Label(new Rect(x, y + 66f, 355f, 20f), contextual);
        }

        private static void DrawFrameIssues(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 260f, 19f), "Frame health");
            DrawIssues(FrameHealthSummary.For(data.Frame, true), x, y + 18f, 455f, 54f, () =>
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false)));
            if (Widgets.ButtonText(new Rect(x + 383f, y - 2f, 72f, 23f), "Health"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(data.Frame, false));
        }

        private static void DrawPilot(FrameUiSnapshot data, float x, float y)
        {
            Label(new Rect(x, y, 90f, 20f), "Pilot", GameFont.Medium);
            if (data.Pilot == null)
            {
                Label(new Rect(x + 115f, y + 31f, 380f, 22f), "No pilot");
                return;
            }
            Pawn pilot = data.Pilot;
            Rect portrait = new Rect(x, y + 20f, 100f, 104f);
            Widgets.DrawBoxSolid(portrait, new Color(0.14f, 0.14f, 0.14f, 0.8f));
            GUI.DrawTexture(portrait.ContractedBy(5f), PortraitsCache.Get(pilot, portrait.size, Rot4.South,
                cameraZoom: 0.78f), ScaleMode.ScaleToFit);
            Widgets.DrawBox(portrait);
            Label(new Rect(x + 113f, y + 19f, 207f, 22f), pilot.LabelShortCap, GameFont.Medium);
            Label(new Rect(x + 113f, y + 44f, 207f, 20f), pilot.Faction?.Name ?? "No faction");
            Label(new Rect(x + 113f, y + 65f, 207f, 20f), pilot.Dead ? "Dead" : pilot.Downed ? "Downed" :
                pilot.InMentalState ? pilot.MentalStateDef.LabelCap : "Embarked");
            Label(new Rect(x + 335f, y + 3f, 235f, 20f), "Pilot health");
            DrawIssues(FrameHealthSummary.For(pilot, false), x + 335f, y + 24f, 325f, 95f,
                () => Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true)));
            if (Widgets.ButtonText(new Rect(x + 113f, y + 96f, 86f, 26f), "Bio"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, true));
            if (Widgets.ButtonText(new Rect(x + 208f, y + 96f, 94f, 26f), "Health"))
                Find.WindowStack.Add(new Dialog_FramePawnInfo(pilot, false));
        }

        private static void DrawIssues(List<FrameHealthIssue> issues, float x, float y, float width, float height,
            System.Action openHealth)
        {
            if (issues.Count == 0) { Label(new Rect(x, y, width, 18f), "No critical issues"); return; }
            int rows = Mathf.FloorToInt(height / 18f);
            int shown = Mathf.Min(issues.Count, rows);
            if (issues.Count > rows) shown = Mathf.Max(0, rows - 1);
            for (int index = 0; index < shown; index++)
                Label(new Rect(x, y + index * 18f, width, 18f), issues[index].Label);
            if (issues.Count > shown)
            {
                Rect overflow = new Rect(x, y + shown * 18f, width, 18f);
                Label(overflow, "+" + (issues.Count - shown) + " more  ·  Health");
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
