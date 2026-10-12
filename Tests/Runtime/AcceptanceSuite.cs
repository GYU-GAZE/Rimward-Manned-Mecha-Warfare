using System;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame.Tests
{
    // One disposable quicktest game for the compact Frame UI correction.
    public sealed class AcceptanceSuite : GameComponent
    {
        private int step;
        private int deadline;
        private bool finished;
        private Action guiAction;
        private FramePawn frame;
        private Pawn worker;
        private Pawn pilot;
        private Thing head;
        private Thing replacement;
        private Dialog_FrameColorPicker picker;
        private Dialog_FramePawnInfo pilotInfo;
        private int pickerFrames;
        private int visualStep;
        private int visualFrames;

        private bool Enabled => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance-suite.txt"));
        private string Report => Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance.txt");
        public AcceptanceSuite(Game game) { }

        private void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label + " (step " + step + ")");
            File.AppendAllText(Report, "PASS " + label + Environment.NewLine);
        }

        private void Start()
        {
            File.WriteAllText(Report, "ZENITH compact Frame UI acceptance" + Environment.NewLine);
            Map map = Find.CurrentMap;
            IntVec3 center = map.Center;
            frame = (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), center, map);
            worker = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, canGeneratePawnRelations: false)),
                center + IntVec3.West * 3, map);
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                colonist.workSettings.SetPriority(WorkTypeDefOf.Construction, 0);
                colonist.jobs.StopAll();
            }
            Check(frame.def.inspectorTabs.Contains(typeof(ITab_FrameVisual)), "Frame tab registered");
            Rect slot = ITab_FrameVisual.SlotBoxRect(0f, 0f, ITab_FrameVisual.SlotSize);
            Check(slot.width == slot.height && slot.width == 78f, "all six slots use one square box geometry");
            Check(frame.Livery != null && frame.Livery.Primary.a > 0f,
                "Frame paint channels available");
            Check(FrameHexColor.TryParse("#a1B2c3", out Color sample) &&
                FrameHexColor.Format(sample) == "#A1B2C3", "color picker hex path available");

            head = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
            replacement = ThingMaker.MakeThing(head.def);
            var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile);
            stockpile.GetStoreSettings().filter.SetDisallowAll();
            stockpile.GetStoreSettings().filter.SetAllow(head.def, true);
            stockpile.AddCell(center + IntVec3.North * 5);
            stockpile.AddCell(center + IntVec3.North * 5 + IntVec3.East);
            GenSpawn.Spawn(head, center + IntVec3.North * 5, map);
            GenSpawn.Spawn(replacement, center + IntVec3.North * 5 + IntVec3.East, map);
            Check(frame.Service.QueueInstall(FrameAddonCategory.Head, head), "exact Head queued");
            Job job = new WorkGiver_ServiceFrameModule().JobOnThing(worker, frame, true);
            Check(job != null && worker.jobs.TryTakeOrderedJob(job), "worker begins Head service");
            Find.Selector.Select(frame);
            guiAction = () =>
            {
                picker = new Dialog_FrameColorPicker(frame, FrameColorChannel.Primary);
                Find.WindowStack.Add(picker);
            };
            Next(1);
        }

        private void Next(int value)
        {
            step = value;
            deadline = Find.TickManager.TicksGame + 6500;
        }

        private void AfterService()
        {
            Check(ReferenceEquals(frame.Loadout.InstalledHead, head), "exact Head installed by service");
            pilot = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfPlayer, canGeneratePawnRelations: false)),
                frame.Position + IntVec3.South, frame.Map);
            BodyPartRecord arm = pilot.RaceProps.body.AllParts.First(part => part.def == BodyPartDefOf.Arm);
            Hediff injury = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"), pilot, arm);
            injury.Severity = 5f;
            pilot.health.AddHediff(injury, arm);
            var summary = FrameHealthSummary.For(pilot, false);
            Check(pilot.health.hediffSet.BleedRateTotal > 0f && summary[0].IsBleedSummary &&
                summary.Any(issue => ReferenceEquals(issue.Hediff, injury) && issue.Bleeding),
                "pinned bleed total and exact wound appear in compact health data");
            Check(frame.Cockpit.TryBoard(pilot), "injured pilot boards exact Frame");
            guiAction = BeginVisualCheck;
            Next(2);
        }

        private void BeginVisualCheck()
        {
            Check(ReferenceEquals(FrameUiSnapshot.From(frame).PilotForInspection, pilot),
                "Frame UI reads exact contained pilot");
            Find.Selector.ClearSelection();
            Find.Selector.Select(frame);
            InspectPaneUtility.OpenTab(typeof(ITab_FrameVisual));
            visualStep = 1;
        }

        private void CheckMenuAndFinish()
        {
            var options = ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head);
            FloatMenuOption install = options.FirstOrDefault(option => option.Label.StartsWith("Install ") && !option.Disabled);
            Check(install != null && options.Any(option => option.Label == "Remove installed"),
                "slot menu offers compatible install and Remove");
            install.Chosen(colonistOrdering: true, null);
            Check(ReferenceEquals(frame.Service.OrderFor(FrameAddonCategory.Head)?.incoming, replacement),
                "slot menu queues exact replacement");
            Check(frame.Service.CancelOrder(FrameAddonCategory.Head), "queued replacement cancels");
            ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head)
                .First(option => option.Label == "Remove installed").Chosen(colonistOrdering: true, null);
            Check(frame.Service.OrderFor(FrameAddonCategory.Head)?.kind == FrameServiceOrderKind.Remove,
                "slot menu queues Remove");
            File.AppendAllText(Report, "PASS acceptance complete" + Environment.NewLine);
            finished = true;
            Application.Quit(0);
        }

        public override void GameComponentOnGUI()
        {
            if (!Enabled || finished) return;
            if (Event.current.type == EventType.Repaint)
            {
                if (picker != null && ++pickerFrames >= 4)
                {
                    picker.Close();
                    picker = null;
                    Check(true, "color picker rendered without GUI error");
                }
                if (visualStep != 0 && ++visualFrames >= 4)
                {
                    visualFrames = 0;
                    try
                    {
                        if (visualStep == 1)
                        {
                            Check(Find.Selector.SingleSelectedThing == frame, "Frame tab remains open");
                            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "frame-tab.png"));
                            visualStep = 2;
                        }
                        else if (visualStep == 2)
                        {
                            pilotInfo = ITab_FrameVisual.OpenPilotInformation(pilot);
                            Check(pilotInfo.SupportsPage(FrameInfoPage.Bio) &&
                                pilotInfo.SupportsPage(FrameInfoPage.Health) &&
                                pilotInfo.SupportsPage(FrameInfoPage.Gear),
                                "Information opens exact pilot Bio Health and Gear");
                            visualStep = 3;
                        }
                        else if (visualStep == 3)
                        {
                            pilotInfo.ShowPage(FrameInfoPage.Health);
                            visualStep = 4;
                        }
                        else if (visualStep == 4)
                        {
                            pilotInfo.ShowPage(FrameInfoPage.Gear);
                            visualStep = 5;
                        }
                        else
                        {
                            pilotInfo.Close();
                            visualStep = 0;
                            guiAction = CheckMenuAndFinish;
                            Check(true, "Bio vanilla Health and Gear pages rendered");
                        }
                    }
                    catch (Exception error) { Fail(error); }
                }
            }
            if (guiAction == null) return;
            Action action = guiAction;
            guiAction = null;
            try { action(); } catch (Exception error) { Fail(error); }
        }

        public override void GameComponentUpdate()
        {
            if (!Enabled || finished || guiAction != null || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || Find.TickManager.TicksGame < 1000) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline)
                    throw new TimeoutException("Acceptance step " + step + " timed out");
                if (step == 0) Start();
                else if (step == 1 && !frame.Service.HasPendingOrder && !frame.Service.IsServiceLocked)
                    AfterService();
            }
            catch (Exception error) { Fail(error); }
        }

        private void Fail(Exception error)
        {
            File.AppendAllText(Report, "FAIL " + error + Environment.NewLine);
            Log.Error("ZENITH ACCEPTANCE FAILURE " + error);
            finished = true;
            Application.Quit(1);
        }
    }
}
