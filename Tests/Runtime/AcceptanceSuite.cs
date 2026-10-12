using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame.Tests
{
    // One disposable quicktest game for paint, health and Frame orders.
    public sealed class AcceptanceSuite : GameComponent
    {
        private int step;
        private int deadline;
        private bool finished;
        private Action guiAction;
        private FramePawn frame;
        private Pawn pilot;
        private Dialog_FrameColorPicker picker;
        private Dialog_FramePawnInfo pilotInfo;
        private int visualStep;
        private int visualFrames;
        private Pawn patient;
        private Pawn captive;
        private Pawn meleeTarget;
        private Building_Bed rescueBed;
        private Building_Bed prisonBed;
        private Building dock;
        private float meleeHealth;
        private int captureReady;

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
            File.WriteAllText(Report, "ZENITH paint, health and Frame order acceptance" + Environment.NewLine);
            Map map = Find.CurrentMap;
            IntVec3 center = map.Center;
            frame = (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), center, map);
            Check(frame.def.inspectorTabs.Contains(typeof(ITab_FrameVisual)), "Frame tab registered");
            Rect slot = ITab_FrameVisual.SlotBoxRect(0f, 0f, ITab_FrameVisual.SlotSize);
            Check(slot.width == slot.height && slot.width == 78f, "all six slots use one square box geometry");
            Check(frame.Livery != null && frame.Livery.Primary.a > 0f,
                "Frame paint channels available");
            Check(FrameHexColor.TryParse("#a1B2c3", out Color sample) &&
                FrameHexColor.Format(sample) == "#A1B2C3", "color picker hex path available");
            Check(frame.def.race.hideTrainingTab && frame.def.race.trainability == TrainabilityDefOf.None,
                "Frame training tab hidden without changing trainability");
            Check(FramePaintPresets.Hex.Length == 5 && FramePaintPresets.Hex.All(group => group.Length == 4) &&
                FramePaintPresets.Hex.SelectMany(group => group).All(value => FrameHexColor.TryParse(value, out _)),
                "five curated four-color paint families parse");
            Rect tab = new Rect(0f, 18f, 620f, 520f);
            Rect right = FramePopupPlacement.Beside(tab, new Vector2(690f, 570f), 1366f, 768f, 620f);
            Rect narrow = FramePopupPlacement.Beside(tab, new Vector2(690f, 570f), 1280f, 800f, 620f);
            Rect left = FramePopupPlacement.Beside(new Rect(730f, 18f, 620f, 520f),
                new Vector2(520f, 420f), 1366f, 768f, 500f);
            Check(right.x >= tab.xMax + FramePopupPlacement.Gap && right.xMax <= 1358f &&
                narrow.x >= tab.xMax + FramePopupPlacement.Gap && narrow.xMax <= 1272f &&
                left.xMax <= 730f - FramePopupPlacement.Gap,
                "popup placement prefers right, narrows at 1280 and falls back left");

            Find.Selector.Select(frame);
            Next(1);
        }

        private void Next(int value)
        {
            step = value;
            deadline = Find.TickManager.TicksGame + 6500;
        }

        private void AfterService()
        {
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
            picker = new Dialog_FrameColorPicker(frame, FrameColorChannel.Visor);
            Check(picker.InitialSize == new Vector2(520f, 420f) &&
                picker.SelectedChannel == FrameColorChannel.Visor, "compact picker opens on clicked Visor channel");
            Color originalPrimary = frame.Livery.Primary;
            Color originalSecondary = frame.Livery.Secondary;
            FrameHexColor.TryParse("#315FD4", out Color visorPreset);
            picker.Apply(visorPreset);
            Check(FrameHexColor.Format(frame.Livery.Visor) == "#315FD4" &&
                frame.Livery.Primary == originalPrimary && frame.Livery.Secondary == originalSecondary,
                "chosen paint changes only the exact selected channel");
            picker.SelectChannel(FrameColorChannel.Secondary);
            Check(picker.SelectedChannel == FrameColorChannel.Secondary,
                "picker can switch the selected paint channel");
            FrameHexColor.TryParse("B92B32", out Color secondaryPreset);
            picker.Apply(secondaryPreset);
            Check(FrameHexColor.Format(frame.Livery.Secondary) == "#B92B32" &&
                FrameHexColor.Format(frame.Livery.Visor) == "#315FD4",
                "second channel update preserves visor paint");
            frame.Livery.ResetColors();
            Check(frame.Livery.Primary == originalPrimary && frame.Livery.Secondary == originalSecondary,
                "main Reset paint restores chassis colors");
            picker.Close();
            picker = null;
            Find.Selector.ClearSelection();
            Find.Selector.Select(frame);
            InspectPaneUtility.OpenTab(typeof(ITab_FrameVisual));
            visualStep = 1;
        }

        private void CheckMenuAndFinish()
        {
            BeginSelfTend();
        }

        private FloatMenuOption Option(Thing clicked, string startsWith)
        {
            List<FloatMenuOption> options = FloatMenuMakerMap.GetOptions(
                new List<Pawn> { frame }, clicked.DrawPos, out _);
            return options.FirstOrDefault(option => option.Label.StartsWith(startsWith) && !option.Disabled);
        }

        private Pawn SpawnColonist(IntVec3 cell, Faction faction) =>
            (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, faction, canGeneratePawnRelations: false)), cell, frame.Map);

        private void DownWithAnesthetic(Pawn pawn)
        {
            pawn.health.AddHediff(HediffMaker.MakeHediff(
                DefDatabase<HediffDef>.GetNamed("Anesthetic"), pawn));
            Check(pawn.Downed && !pawn.Dead, pawn.LabelShort + " is downed and alive");
        }

        private void BeginSelfTend()
        {
            Medicine medicine = (Medicine)ThingMaker.MakeThing(ThingDefOf.MedicineHerbal);
            Check(pilot.inventory.innerContainer.TryAdd(medicine), "medicine owned by exact pilot inventory");
            FloatMenuOption option = Option(frame, "Pilot self-tend");
            Check(option != null, "selected Frame self-right-click offers Pilot self-tend");
            option.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == ZenithJobDefOf.ZF_FramePilotSelfTend &&
                ReferenceEquals(frame.CurJob.targetC.Thing, pilot) &&
                ReferenceEquals(frame.CurJob.targetB.Thing, medicine),
                "self-tend job targets exact Frame pilot and pilot-owned medicine");
            Next(3);
        }

        private void BeginRescue()
        {
            Check(!pilot.health.HasHediffsNeedingTend(), "pilot self-tend completes on real wound");
            rescueBed = (Building_Bed)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed),
                frame.Position + IntVec3.East * 6, frame.Map);
            rescueBed.SetFaction(Faction.OfPlayer);
            patient = SpawnColonist(frame.Position + IntVec3.South * 5, Faction.OfPlayer);
            DownWithAnesthetic(patient);
            Building_Bed found = RestUtility.FindBedFor(patient, frame, false);
            File.AppendAllText(Report, "Rescue bed probe: found=" + (found?.LabelCap ?? "none") +
                " testBedValid=" + RestUtility.CanUseBedNow(rescueBed, patient, false) +
                " frameCanReach=" + frame.CanReach(rescueBed, PathEndMode.OnCell, Danger.Some) +
                " bedFaction=" + (rescueBed.Faction?.Name ?? "none") + Environment.NewLine);
            Check(found != null, "friendly patient has reachable rescue bed");
            rescueBed = found;
            FloatMenuOption rescue = Option(patient, "Rescue");
            Check(rescue != null, "vanilla Rescue option enabled for piloted Frame");
            rescue.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.Rescue &&
                ReferenceEquals(frame.CurJob.targetA.Thing, patient) &&
                ReferenceEquals(frame.CurJob.targetB.Thing, rescueBed),
                "vanilla Rescue job has exact patient and bed");
            Next(4);
        }

        private void BeginCapture()
        {
            Check(patient.InBed() && ReferenceEquals(patient.CurrentBed(), rescueBed),
                "Rescue carried friendly patient into bed");
            Faction enemy = Find.FactionManager.AllFactionsVisible
                .First(faction => faction.HostileTo(Faction.OfPlayer) && faction.def.humanlikeFaction);
            captive = SpawnColonist(frame.Position + IntVec3.South * 5, enemy);
            DownWithAnesthetic(captive);
            IntVec3 prisonCenter = frame.Position + IntVec3.East * 11;
            for (int dx = -3; dx <= 3; dx++)
                for (int dz = -3; dz <= 3; dz++)
                {
                    if (Math.Abs(dx) != 3 && Math.Abs(dz) != 3) continue;
                    IntVec3 cell = prisonCenter + new IntVec3(dx, 0, dz);
                    ThingDef wall = dx == 0 && dz == -3 ? ThingDefOf.Door : ThingDefOf.Wall;
                    Thing structure = GenSpawn.Spawn(ThingMaker.MakeThing(wall), cell, frame.Map);
                    structure.SetFaction(Faction.OfPlayer);
                }
            prisonBed = (Building_Bed)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed),
                prisonCenter, frame.Map);
            prisonBed.SetFaction(Faction.OfPlayer);
            prisonBed.ForPrisoners = true;
            captureReady = Find.TickManager.TicksGame + 60;
            Next(45);
        }

        private void StartCapture()
        {
            Room room = prisonBed.GetRoom();
            room?.Notify_RoomShapeChanged();
            File.AppendAllText(Report, "Capture bed probe: prisonCell=" +
                prisonBed.Position.IsInPrisonCell(frame.Map) + " forPrisoners=" +
                prisonBed.ForPrisoners + " room=" +
                (room?.DebugString() ?? "none") + Environment.NewLine);
            Check(RestUtility.FindBedFor(captive, frame, false, false, GuestStatus.Prisoner) == prisonBed,
                "capturable hostile has reachable prisoner bed");
            FloatMenuOption capture = Option(captive, "Capture");
            Check(capture != null, "vanilla Capture option enabled for piloted Frame");
            capture.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.Capture &&
                ReferenceEquals(frame.CurJob.targetA.Thing, captive) &&
                ReferenceEquals(frame.CurJob.targetB.Thing, prisonBed),
                "vanilla Capture job has exact captive and prison bed");
            Next(5);
        }

        private void BeginMelee()
        {
            Check(captive.InBed() && ReferenceEquals(captive.CurrentBed(), prisonBed) &&
                captive.IsPrisonerOfColony, "Capture carried hostile into prisoner bed and converted status");
            ThingWithComps weapon = frame.equipment.Primary;
            Check(weapon != null && frame.equipment.TryDropEquipment(weapon, out ThingWithComps _,
                frame.Position, false), "Frame removes its exact ranged weapon");
            Check(frame.equipment.Primary == null && !FloatMenuUtility.UseRangedAttack(frame),
                "unarmed Frame is not routed through ranged attack");
            Faction enemy = Find.FactionManager.AllFactionsVisible
                .First(faction => faction.HostileTo(Faction.OfPlayer) && faction.def.humanlikeFaction);
            meleeTarget = SpawnColonist(frame.Position + IntVec3.South * 4, enemy);
            Check(frame.meleeVerbs.TryGetMeleeVerb(meleeTarget) != null,
                "unarmed Frame has innate melee verb");
            frame.drafter.Drafted = true;
            Check(FloatMenuUtility.GetMeleeAttackAction(frame, meleeTarget, out _) != null,
                "vanilla melee action accepts drafted Frame");
            List<FloatMenuOption> attackOptions = FloatMenuMakerMap.GetOptions(
                new List<Pawn> { frame }, meleeTarget.DrawPos, out _);
            File.AppendAllText(Report, "Melee options: " + string.Join(" | ",
                attackOptions.Select(option => option.Label + " [disabled=" + option.Disabled + "]")) +
                Environment.NewLine);
            FloatMenuOption attack = attackOptions.FirstOrDefault(option =>
                option.Label.IndexOf("attack", StringComparison.OrdinalIgnoreCase) >= 0 && !option.Disabled);
            Check(attack != null, "drafted right-click offers enabled attack");
            meleeHealth = meleeTarget.health.summaryHealth.SummaryHealthPercent;
            attack.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.AttackMelee,
                "chosen attack starts vanilla AttackMelee");
            Next(6);
        }

        private void BeginDock()
        {
            Check(meleeTarget.Dead || meleeTarget.health.summaryHealth.SummaryHealthPercent < meleeHealth,
                "innate melee attack dealt actual damage");
            frame.jobs.StopAll();
            frame.drafter.Drafted = false;
            dock = (Building)GenSpawn.Spawn(ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed(DockingBayUtility.DockingBayDefName)),
                frame.Position + IntVec3.North * 8, frame.Map);
            Check(!DockingBayUtility.IsDocked(frame) && DockingBayUtility.BoardingTicks(frame) == 300,
                "undocked Frame uses normal boarding time");
            FloatMenuOption option = Option(dock, "Dock Frame");
            Check(option != null, "operational Frame right-clicks bay for Dock Frame");
            option.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.Goto, "Dock Frame starts ordinary Goto");
            Next(7);
        }

        private void FinishDock()
        {
            Check(DockingBayUtility.IsDocked(frame) && dock.OccupiedRect().Contains(frame.Position) &&
                DockingBayUtility.BoardingTicks(frame) == 60,
                "Frame reaches occupied bay footprint and gains fivefold bonus");
            IntVec3 exit = GenRadial.RadialCellsAround(dock.Position, 8f, false)
                .First(cell => cell.InBounds(frame.Map) && cell.Standable(frame.Map) &&
                    !dock.OccupiedRect().Contains(cell) &&
                    frame.CanReach(cell, PathEndMode.OnCell, Danger.Some));
            Job leave = JobMaker.MakeJob(JobDefOf.Goto, exit);
            Check(frame.jobs.TryTakeOrderedJob(leave), "Frame accepts reachable exit order from docking bay");
            Next(8);
        }

        private void FinishAcceptance()
        {
            Check(!DockingBayUtility.IsDocked(frame) && DockingBayUtility.BoardingTicks(frame) == 300,
                "moving away ends positional docking bonus");
            File.AppendAllText(Report, "PASS acceptance complete" + Environment.NewLine);
            finished = true;
            Application.Quit(0);
        }

        public override void GameComponentOnGUI()
        {
            if (!Enabled || finished) return;
            if (Event.current.type == EventType.Repaint)
            {
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
                            picker = new Dialog_FrameColorPicker(frame, FrameColorChannel.Primary);
                            Find.WindowStack.Add(picker);
                            CheckBeside(picker, "paint popup opens beside Frame tab");
                            visualStep = 3;
                        }
                        else if (visualStep == 3)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "paint-editor.png"));
                            visualStep = 4;
                        }
                        else if (visualStep == 4)
                        {
                            picker.Close();
                            picker = null;
                            pilotInfo = ITab_FrameVisual.OpenPilotInformation(pilot);
                            CheckBeside(pilotInfo, "pilot Information opens beside Frame tab");
                            Check(pilotInfo.SupportsPage(FrameInfoPage.Bio) &&
                                pilotInfo.SupportsPage(FrameInfoPage.Health) &&
                                pilotInfo.SupportsPage(FrameInfoPage.Gear),
                                "Information opens exact pilot Bio Health and Gear");
                            visualStep = 5;
                        }
                        else if (visualStep == 5)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "pilot-info.png"));
                            visualStep = 6;
                        }
                        else if (visualStep == 6)
                        {
                            pilotInfo.ShowPage(FrameInfoPage.Health);
                            visualStep = 7;
                        }
                        else if (visualStep == 7)
                        {
                            pilotInfo.ShowPage(FrameInfoPage.Gear);
                            visualStep = 8;
                        }
                        else if (visualStep == 8)
                        {
                            pilotInfo.Close();
                            pilotInfo = new Dialog_FramePawnInfo(frame, false);
                            Find.WindowStack.Add(pilotInfo);
                            CheckBeside(pilotInfo, "Frame health opens beside Frame tab");
                            visualStep = 9;
                        }
                        else if (visualStep == 9)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "frame-health.png"));
                            visualStep = 10;
                        }
                        else if (visualStep == 10)
                        {
                            pilotInfo.Close();
                            pilotInfo = new Dialog_FramePawnInfo(pilot, true, FrameInfoPage.Health);
                            Find.WindowStack.Add(pilotInfo);
                            CheckBeside(pilotInfo, "pilot health opens beside Frame tab");
                            visualStep = 11;
                        }
                        else if (visualStep == 11)
                        {
                            ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "pilot-health.png"));
                            visualStep = 12;
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

        private void CheckBeside(Window popup, string label)
        {
            Rect tab = ITab_FrameVisual.CurrentScreenRect();
            Rect positioned = popup.windowRect;
            Check(positioned.x >= tab.xMax + FramePopupPlacement.Gap &&
                positioned.xMax <= UI.screenWidth - 8f && positioned.y >= 8f &&
                positioned.yMax <= UI.screenHeight - 8f, label);
        }

        public override void GameComponentUpdate()
        {
            if (!Enabled || finished || guiAction != null || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || Find.TickManager.TicksGame < 1000) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline)
                    throw new TimeoutException("Acceptance step " + step + " timed out; Frame=" +
                        frame?.Position + " Job=" + frame?.CurJobDef?.defName +
                        " Target=" + frame?.CurJob?.targetA + " Dock=" + dock?.Position);
                if (step == 0) Start();
                else if (step == 1)
                    AfterService();
                else if (step == 3 && frame.CurJobDef != ZenithJobDefOf.ZF_FramePilotSelfTend)
                    BeginRescue();
                else if (step == 4 && patient.InBed())
                    BeginCapture();
                else if (step == 45 && Find.TickManager.TicksGame >= captureReady)
                    StartCapture();
                else if (step == 5 && captive.InBed())
                    BeginMelee();
                else if (step == 6 && (meleeTarget.Dead || meleeTarget.health.summaryHealth.SummaryHealthPercent < meleeHealth))
                    BeginDock();
                else if (step == 7 && DockingBayUtility.IsDocked(frame))
                    FinishDock();
                else if (step == 8 && !DockingBayUtility.IsDocked(frame))
                    FinishAcceptance();
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
