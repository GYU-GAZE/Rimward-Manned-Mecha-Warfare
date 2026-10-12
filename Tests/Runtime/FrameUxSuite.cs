using System;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame.Tests
{
    // One disposable game exercises the real queue, worker jobs and vanilla
    // FloatMenu pipeline, including a same-process save/load of queued orders.
    public sealed class FrameUxSuite : GameComponent
    {
        private int stage;
        private int step;
        private int deadline;
        private FramePawn frame;
        private Pawn worker;
        private Pawn pilot;
        private Thing head;
        private Thing shoulder;
        private Thing power;
        private Thing utility;
        private ThingWithComps rifle;
        private Thing haulItem;
        private Pawn patient;
        private Pawn captive;
        private Building_Bed prisonerBed;
        private int captureCheckAtTick;
        private string frameId;
        private string headId;
        private string shoulderId;
        private string rifleId;
        private bool finished;
        private Action guiAction;

        private bool Enabled => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "frame-ux-suite.txt"));
        private string ReportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "frame-ux-phase-0.txt");
        public FrameUxSuite(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stage, "uxStage");
            Scribe_Values.Look(ref step, "uxStep");
            Scribe_Values.Look(ref deadline, "uxDeadline");
            Scribe_References.Look(ref frame, "uxFrame");
            Scribe_References.Look(ref worker, "uxWorker");
            Scribe_References.Look(ref pilot, "uxPilot");
            Scribe_References.Look(ref head, "uxHead");
            Scribe_References.Look(ref shoulder, "uxShoulder");
            Scribe_References.Look(ref power, "uxPower");
            Scribe_References.Look(ref utility, "uxUtility");
            Scribe_References.Look(ref rifle, "uxRifle");
            Scribe_Values.Look(ref frameId, "uxFrameId");
            Scribe_Values.Look(ref headId, "uxHeadId");
            Scribe_Values.Look(ref shoulderId, "uxShoulderId");
            Scribe_Values.Look(ref rifleId, "uxRifleId");
        }

        private void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException(label + " [step=" + step + "]");
            File.AppendAllText(ReportPath, "PASS " + label + Environment.NewLine);
        }

        private void Next(int next, int ticks = 4500)
        {
            step = next;
            deadline = Find.TickManager.TicksGame + ticks;
        }

        private static Pawn SpawnWorker(IntVec3 cell, Map map) => (Pawn)GenSpawn.Spawn(
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                canGeneratePawnRelations: false)), cell, map);

        private void InitialSave()
        {
            File.WriteAllText(ReportPath, "ZENITH focused Frame UX and service queue" + Environment.NewLine);
            Map map = Find.CurrentMap;
            IntVec3 center = map.Center;
            frame = (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), center, map);
            worker = SpawnWorker(center + IntVec3.West * 3, map);
            // Quicktest starts with several unrelated colonists. Keep the
            // service worker deterministic while retaining the real jobs.
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                colonist.workSettings.SetPriority(WorkTypeDefOf.Construction, 0);
                colonist.jobs.StopAll();
            }
            // Quicktest can generate relations to temporary pawns that it does
            // not serialize. They are unrelated to this disposable fixture.
            foreach (Pawn mapPawn in map.mapPawns.AllPawnsSpawned.ToList())
                mapPawn.relations?.ClearAllRelations();
            frameId = frame.GetUniqueLoadID();
            rifle = frame.equipment.Primary;
            rifleId = rifle.GetUniqueLoadID();
            head = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
            shoulder = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShoulderSystem"));
            power = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugReactor"));
            utility = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugMobilityBooster"));
            headId = head.GetUniqueLoadID();
            shoulderId = shoulder.GetUniqueLoadID();
            var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile);
            stockpile.GetStoreSettings().filter.SetDisallowAll();
            foreach (Thing item in new[] { head, shoulder, power, utility })
                stockpile.GetStoreSettings().filter.SetAllow(item.def, true);
            for (int index = 0; index < 5; index++) stockpile.AddCell(center + IntVec3.North * 5 + IntVec3.East * index);
            GenSpawn.Spawn(head, center + IntVec3.North * 5, map);
            GenSpawn.Spawn(shoulder, center + IntVec3.North * 5 + IntVec3.East, map);
            GenSpawn.Spawn(power, center + IntVec3.North * 5 + IntVec3.East * 2, map);
            GenSpawn.Spawn(utility, center + IntVec3.North * 5 + IntVec3.East * 3, map);
            Check(frame.Service.QueueInstall(FrameAddonCategory.Head, head) &&
                frame.Service.QueueInstall(FrameAddonCategory.Shoulder, shoulder) &&
                frame.Service.QueueRemove(FrameAddonCategory.Weapon),
                "three different exact slot orders coexist");
            Check(frame.Service.Orders.Count == 3 &&
                frame.Service.Orders.Select(order => order.category).SequenceEqual(new[]
                    { FrameAddonCategory.Head, FrameAddonCategory.Shoulder, FrameAddonCategory.Weapon }) &&
                ReferenceEquals(frame.Service.Orders[2].expectedInstalled, rifle),
                "enqueue order and exact expected installed weapon are retained");
            stage = 1;
            GameDataSaveLoader.SaveGame("ZenithFrameUX");
            Check(File.Exists(GenFilePaths.FilePathForSavedGame("ZenithFrameUX")),
                "multi-order queue saved before any worker starts");
            finished = true;
            GameDataSaveLoader.LoadGame("ZenithFrameUX");
        }

        private void AfterLoad()
        {
            if (step == 0)
            {
                Check(frame != null && frame.Spawned && frame.GetUniqueLoadID() == frameId &&
                    head.GetUniqueLoadID() == headId && shoulder.GetUniqueLoadID() == shoulderId &&
                    rifle.GetUniqueLoadID() == rifleId, "save/load retains exact Frame and module identities");
                Check(frame.Service.Orders.Count == 3 &&
                    frame.Service.Orders.Select(order => order.category).SequenceEqual(new[]
                        { FrameAddonCategory.Head, FrameAddonCategory.Shoulder, FrameAddonCategory.Weapon }),
                    "three queued intents survive save/load in enqueue order");
                Check(frame.Service.CancelOrder(FrameAddonCategory.Shoulder) && frame.Service.Orders.Count == 2 &&
                    ReferenceEquals(frame.Service.OrderFor(FrameAddonCategory.Head).incoming, head) &&
                    ReferenceEquals(frame.Service.OrderFor(FrameAddonCategory.Weapon).expectedInstalled, rifle),
                    "cancelling the pending middle slot preserves its neighbors");
                Check(frame.Service.QueueInstall(FrameAddonCategory.Shoulder, shoulder) &&
                    frame.Service.Orders.Last().category == FrameAddonCategory.Shoulder,
                    "requeueing a cancelled category appends it after existing intents");
                Check(frame.Service.QueueInstall(FrameAddonCategory.Utility, utility),
                    "fourth category can queue independently");
                utility.Destroy();
                Check(frame.Service.OrderFor(FrameAddonCategory.Head) != null &&
                    frame.Service.OrderFor(FrameAddonCategory.Shoulder) != null,
                    "invalid exact incoming item has not disturbed later valid orders");
                Job firstJob = new WorkGiver_ServiceFrameModule().JobOnThing(worker, frame, true);
                Check(firstJob != null && ReferenceEquals(firstJob.targetB.Thing, head) &&
                    worker.jobs.TryTakeOrderedJob(firstJob), "worker accepts first exact queued operation " + worker.GetUniqueLoadID());
                Next(1, 6500);
            }
            else if (step == 1)
            {
                if (!ReferenceEquals(frame.Loadout.InstalledHead, head)) return;
                Check(frame.Service.OrderFor(FrameAddonCategory.Head) == null &&
                    frame.Service.OrderFor(FrameAddonCategory.Weapon) != null &&
                    frame.Service.OrderFor(FrameAddonCategory.Shoulder) != null &&
                    frame.Service.OrderFor(FrameAddonCategory.Utility) == null,
                    "first completion removes only its order and stale incoming cancels alone");
                Next(2, 6500);
            }
            else if (step == 2)
            {
                if (frame.Service.ActiveOrder?.category != FrameAddonCategory.Weapon) return;
                Check(worker.CurJobDef == ZenithJobDefOf.ZF_ServiceFrameModule &&
                    ReferenceEquals(frame.Service.ActiveWorker, worker),
                    "same worker continues next service without Wandering: expected=" + worker?.GetUniqueLoadID() +
                    " actual=" + frame.Service.ActiveWorker?.GetUniqueLoadID() +
                    " job=" + worker.CurJobDef?.defName + " queue=" + worker.jobs.jobQueue.Count);
                Next(3, 6500);
            }
            else if (step == 3)
            {
                if (frame.Service.HasPendingOrder || frame.Service.IsServiceLocked) return;
                Check(ReferenceEquals(frame.Loadout.InstalledHead, head) &&
                    ReferenceEquals(frame.Loadout.InstalledShoulder, shoulder) &&
                    frame.equipment.Primary == null && rifle.Spawned,
                    "all queued operations finish with exact installed and removed Things");
                Check(frame.Service.QueueInstall(FrameAddonCategory.Power, power) &&
                    frame.Service.QueueRemove(FrameAddonCategory.Head),
                    "active-cancel fixture has an unrelated later order");
                Job install = new WorkGiver_ServiceFrameModule().JobOnThing(worker, frame, true);
                Check(install != null && worker.jobs.TryTakeOrderedJob(install),
                    "worker starts exact Power installation");
                Next(4, 4500);
            }
            else if (step == 4)
            {
                if (!ReferenceEquals(worker.carryTracker.CarriedThing, power) ||
                    frame.Service.ActiveOrder?.category != FrameAddonCategory.Power) return;
                Thing installedBeforeCancel = frame.Service.ActiveOrder.expectedInstalled;
                bool cancelled = frame.Service.CancelOrder(FrameAddonCategory.Power);
                Check(cancelled &&
                    frame.Service.OrderFor(FrameAddonCategory.Power) == null &&
                    frame.Service.OrderFor(FrameAddonCategory.Head) != null &&
                    frame.Service.ActiveOrder?.category != FrameAddonCategory.Power &&
                    ReferenceEquals(frame.Loadout.InstalledPower, installedBeforeCancel) &&
                    !power.Destroyed && (power.Spawned || ReferenceEquals(worker.carryTracker.CarriedThing, power)),
                    "active install cancellation releases lock and preserves carried exact module: cancel=" + cancelled +
                    " powerOrder=" + (frame.Service.OrderFor(FrameAddonCategory.Power) != null) +
                    " headOrder=" + (frame.Service.OrderFor(FrameAddonCategory.Head) != null) +
                    " active=" + frame.Service.ActiveOrder?.category + " installedChanged=" +
                    !ReferenceEquals(frame.Loadout.InstalledPower, installedBeforeCancel) + " destroyed=" + power.Destroyed +
                    " spawned=" + power.Spawned + " carried=" +
                    ReferenceEquals(worker.carryTracker.CarriedThing, power));
                Job removal = new WorkGiver_ServiceFrameModule().JobOnThing(worker, frame, true);
                Check(removal != null && worker.jobs.TryTakeOrderedJob(removal),
                    "unrelated Head removal remains startable after active Power cancellation");
                Next(5, 4500);
            }
            else if (step == 5)
            {
                if (frame.Service.ActiveOrder?.category != FrameAddonCategory.Head) return;
                Check(ReferenceEquals(frame.Loadout.InstalledHead, head) &&
                    frame.Service.CancelOrder(FrameAddonCategory.Head) &&
                    ReferenceEquals(frame.Loadout.InstalledHead, head) &&
                    frame.Service.ActiveOrder?.category != FrameAddonCategory.Head,
                    "active removal cancellation keeps installed item and clears service lock");
                pilot = SpawnWorker(frame.Position + IntVec3.South, frame.Map);
                Check(frame.Cockpit.TryBoard(pilot) && frame.Cockpit.CanTakePlayerOrders,
                    "exact pilot boards for manual-order menu checks");
                guiAction = CheckRightClickPolicy;
                Next(6, 4500);
            }
        }

        private void CheckRightClickPolicy()
        {
            var selected = new System.Collections.Generic.List<Pawn> { frame };
            var selfOptions = FloatMenuMakerMap.GetOptions(selected, frame.DrawPos, out _);
            FloatMenuOption disembark = selfOptions.FirstOrDefault(option => option.Label == "Disembark");
            Check(disembark != null && !disembark.Disabled,
                "selected piloted Frame receives self right-click Disembark");
            Check(!frame.GetGizmos().OfType<Command_Action>().Any(command => command.defaultLabel == "Disembark"),
                "Disembark no longer occupies a gizmo");
            Check(frame.def.inspectorTabs.Contains(typeof(ITab_FrameVisual)),
                "Frame uses the fixed six-slot visual tab");
            Check(FrameHealthSummary.For(frame, true) != null &&
                FrameHealthSummary.For(pilot, false) != null,
                "compact health summaries derive from both real Pawns");
            disembark.Chosen(colonistOrdering: true, null);
            Check(!frame.Cockpit.HasPilot && pilot.Spawned && ReferenceEquals(pilot.holdingOwner?.Owner, frame.Map),
                "self right-click restores the exact contained pilot to the map");
            Check(frame.Cockpit.TryBoard(pilot), "same exact pilot can board again");
            var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, frame.Map.zoneManager);
            frame.Map.zoneManager.RegisterZone(stockpile);
            stockpile.GetStoreSettings().filter.SetDisallowAll();
            stockpile.GetStoreSettings().filter.SetAllow(ThingDefOf.Steel, true);
            stockpile.AddCell(frame.Position + IntVec3.East * 9);
            haulItem = ThingMaker.MakeThing(ThingDefOf.Steel);
            GenSpawn.Spawn(haulItem, frame.Position + IntVec3.South * 4, frame.Map);
            var haulOptions = FloatMenuMakerMap.GetOptions(selected, haulItem.DrawPos, out _);
            FloatMenuOption haul = haulOptions.FirstOrDefault(option => option.Label.StartsWith("Haul "));
            Check(haul != null && !haul.Disabled &&
                !haulOptions.Any(option => option.Label.StartsWith("Prioritize ") ||
                    option.Label.StartsWith("Clean ") || option.Label.StartsWith("Construct ")),
                "piloted Frame receives manual Haul without general colonist work providers");
            haul.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.HaulToCell || frame.CurJobDef == JobDefOf.HaulToContainer,
                "manual Haul starts a vanilla storage job");
            Next(7, 5000);
        }

        private void PrepareRescue()
        {
            Check(StoreUtility.IsInValidStorage(haulItem), "manual Haul delivered the exact item to valid storage");
            patient = SpawnWorker(frame.Position + IntVec3.South * 4, frame.Map);
            patient.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
            Check(patient.Downed && !patient.Dead, "ordinary colonist rescue fixture is downed");
            var bed = (Building_Bed)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed),
                frame.Position + IntVec3.East * 7 + IntVec3.South * 3, frame.Map);
            bed.SetFaction(Faction.OfPlayer);
            guiAction = () =>
            {
                var options = FloatMenuMakerMap.GetOptions(new System.Collections.Generic.List<Pawn> { frame }, patient.DrawPos, out _);
                FloatMenuOption rescue = options.FirstOrDefault(option => option.Label.Contains("Rescue"));
                Check(rescue != null && !rescue.Disabled, "piloted Frame receives vanilla Rescue for ordinary downed Pawn");
                rescue.Chosen(colonistOrdering: true, null);
                Check(frame.CurJobDef == JobDefOf.Rescue, "manual Rescue starts vanilla Rescue job");
                Next(8, 5500);
            };
        }

        private void PrepareCapture()
        {
            Check(patient.InBed(),
                "manual Rescue carried ordinary patient into a bed");
            captive = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, Faction.OfAncientsHostile, canGeneratePawnRelations: false)),
                frame.Position + IntVec3.South * 4, frame.Map);
            captive.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
            Check(captive.Downed && captive.Faction.HostileTo(Faction.OfPlayer),
                "ordinary hostile capture fixture is downed");
            IntVec3 roomCenter = frame.Position + IntVec3.East * 12 + IntVec3.North * 7;
            for (int x = -2; x <= 2; x++)
                for (int z = -2; z <= 2; z++)
                    if (Math.Abs(x) == 2 || Math.Abs(z) == 2)
                    {
                        bool entrance = x == 0 && z == -2;
                        ThingDef wallOrDoor = entrance ? ThingDefOf.Door : ThingDefOf.Wall;
                        Building wall = (Building)GenSpawn.Spawn(ThingMaker.MakeThing(wallOrDoor, ThingDefOf.Steel),
                            roomCenter + new IntVec3(x, 0, z), frame.Map);
                        wall.SetFaction(Faction.OfPlayer);
                    }
            prisonerBed = (Building_Bed)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed),
                roomCenter, frame.Map);
            prisonerBed.SetFaction(Faction.OfPlayer);
            prisonerBed.ForPrisoners = true;
            prisonerBed.GetRoom()?.Notify_BedTypeChanged();
            for (int x = -1; x <= 1; x++)
                for (int z = -1; z <= 1; z++)
                    frame.Map.roofGrid.SetRoof(roomCenter + new IntVec3(x, 0, z), RoofDefOf.RoofConstructed);
            captureCheckAtTick = Find.TickManager.TicksGame + 120;
            Next(81, 4500);
        }

        private void CheckCaptureOption()
        {
            prisonerBed.GetRoom()?.Notify_RoomShapeChanged();
            var options = FloatMenuMakerMap.GetOptions(new System.Collections.Generic.List<Pawn> { frame }, captive.DrawPos, out _);
            FloatMenuOption capture = options.FirstOrDefault(option => option.Label.Contains("Capture"));
            Check(capture != null && !capture.Disabled, "piloted Frame receives vanilla Capture with a prisoner bed: " +
                string.Join(" | ", options.Select(option => option.Label)) +
                " bedPrisoners=" + prisonerBed.ForPrisoners + " bedRoom=" + prisonerBed.GetRoom()?.IsPrisonCell +
                " properRoom=" + prisonerBed.GetRoom()?.ProperRoom +
                " sameRoomAsFrame=" + ReferenceEquals(prisonerBed.GetRoom(), frame.GetRoom()) +
                " roomCells=" + prisonerBed.GetRoom()?.CellCount +
                " roomBeds=" + prisonerBed.GetRoom()?.ContainedBeds.Count());
            capture.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.Capture, "manual Capture starts vanilla Capture job");
            Next(9, 5500);
        }

        private void CheckDownedFrameBoundary()
        {
            Check(captive.InBed() && captive.IsPrisonerOfColony,
                "manual Capture delivered the exact hostile Pawn as prisoner");
            Check(frame.Cockpit.TryDisembark(), "pilot disembarks after manual jobs");
            foreach (BodyPartRecord leg in frame.RaceProps.body.AllParts.Where(part =>
                part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)))
                frame.health.AddHediff(HediffDefOf.MissingBodyPart, leg);
            Check(frame.Downed && !frame.Dead && !HealthAIUtility.CanRescueNow(worker, frame, true),
                "ordinary colonist cannot rescue a disabled mechanical Frame");
            guiAction = () =>
            {
                var options = FloatMenuMakerMap.GetOptions(new System.Collections.Generic.List<Pawn> { worker }, frame.DrawPos, out _);
                Check(!options.Any(option => option.Label.Contains("Rescue") || option.Label.Contains("Capture")),
                    "ordinary colonist menu excludes biological Rescue/Capture for downed Frame");
                File.AppendAllText(ReportPath, "PASS focused Frame UX/service/manual orders complete" + Environment.NewLine);
                finished = true;
                Application.Quit(0);
            };
        }

        public override void GameComponentOnGUI()
        {
            if (!Enabled || finished || guiAction == null) return;
            Action action = guiAction;
            guiAction = null;
            try { action(); }
            catch (Exception error) { Fail(error); }
        }

        private void Fail(Exception error)
        {
            File.AppendAllText(ReportPath, "FAIL " + error + Environment.NewLine);
            Log.Error("ZENITH FRAME UX TEST FAILURE " + error);
            finished = true;
            Application.Quit(1);
        }

        public override void GameComponentUpdate()
        {
            if (!Enabled || finished || guiAction != null || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || Find.TickManager.TicksGame < 1000) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline)
                    throw new TimeoutException("Frame UX step " + step + " timed out; worker=" + worker?.CurJobDef?.defName +
                        " queue=" + frame?.Service?.PendingLabel + " lock=" + frame?.Service?.IsServiceLocked);
                if (stage == 0) InitialSave();
                else if (step <= 5) AfterLoad();
                else if (step == 7 && StoreUtility.IsInValidStorage(haulItem)) PrepareRescue();
                else if (step == 8 && patient.InBed()) PrepareCapture();
                else if (step == 81 && Find.TickManager.TicksGame >= captureCheckAtTick)
                    guiAction = CheckCaptureOption;
                else if (step == 9 && captive.InBed()) CheckDownedFrameBoundary();
            }
            catch (Exception error) { Fail(error); }
        }
    }
}
