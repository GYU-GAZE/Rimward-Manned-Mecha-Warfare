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
    // One disposable quicktest game, no phase files or save/load chain.
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
        private Thing shoulder;
        private Thing rifle;
        private Thing haulItem;
        private Dialog_FrameColorPicker picker;
        private int pickerGuiFrames;

        private bool Enabled => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance-suite.txt"));
        private string Report => Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance.txt");
        public AcceptanceSuite(Game game) { }

        private void Check(bool ok, string label)
        {
            if (!ok) throw new InvalidOperationException(label + " (step " + step + ")");
            File.AppendAllText(Report, "PASS " + label + Environment.NewLine);
        }

        private void Next(int value, int waitTicks = 6500)
        {
            step = value;
            deadline = Find.TickManager.TicksGame + waitTicks;
        }

        private static Pawn SpawnColonist(IntVec3 cell, Map map) => (Pawn)GenSpawn.Spawn(
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                canGeneratePawnRelations: false)), cell, map);

        private void Start()
        {
            File.WriteAllText(Report, "ZENITH one-process acceptance" + Environment.NewLine);
            Map map = Find.CurrentMap;
            IntVec3 center = map.Center;
            frame = (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), center, map);
            worker = SpawnColonist(center + IntVec3.West * 3, map);
            foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned)
            {
                colonist.workSettings.SetPriority(WorkTypeDefOf.Construction, 0);
                colonist.jobs.StopAll();
            }
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
                pawn.relations?.ClearAllRelations();
            Check(frame.Livery != null && frame.Livery.Primary.a > 0f &&
                frame.Livery.Secondary.a > 0f && frame.Livery.Visor.a > 0f,
                "three physical chassis paint channels available");
            Check(FrameHexColor.TryParse("#a1B2c3", out Color sample) &&
                FrameHexColor.Format(sample) == "#A1B2C3" &&
                !FrameHexColor.TryParse("#GGGGGG", out _), "hex input normalizes and rejects invalid values");
            Check(frame.def.inspectorTabs.Contains(typeof(ITab_FrameVisual)), "visual Frame tab registered");
            rifle = frame.equipment.Primary;
            head = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
            shoulder = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShoulderSystem"));
            var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            map.zoneManager.RegisterZone(stockpile);
            stockpile.GetStoreSettings().filter.SetDisallowAll();
            stockpile.GetStoreSettings().filter.SetAllow(head.def, true);
            stockpile.GetStoreSettings().filter.SetAllow(shoulder.def, true);
            stockpile.GetStoreSettings().filter.SetAllow(ThingDefOf.Steel, true);
            for (int i = 0; i < 5; i++) stockpile.AddCell(center + IntVec3.North * 5 + IntVec3.East * i);
            GenSpawn.Spawn(head, center + IntVec3.North * 5, map);
            GenSpawn.Spawn(shoulder, center + IntVec3.North * 5 + IntVec3.East, map);
            Check(frame.Service.QueueInstall(FrameAddonCategory.Head, head) &&
                frame.Service.QueueInstall(FrameAddonCategory.Shoulder, shoulder) &&
                frame.Service.QueueRemove(FrameAddonCategory.Weapon), "three categories queue together");
            Check(frame.Service.ActiveOrder == null && frame.Service.OrderFor(FrameAddonCategory.Power) == null &&
                frame.Service.Orders.Count == 3, "idle slots have no active service");
            Check(frame.Service.CancelOrder(FrameAddonCategory.Shoulder) &&
                frame.Service.OrderFor(FrameAddonCategory.Head) != null &&
                frame.Service.OrderFor(FrameAddonCategory.Weapon) != null &&
                frame.Service.QueueInstall(FrameAddonCategory.Shoulder, shoulder),
                "canceling one category preserves other orders");
            Job job = new WorkGiver_ServiceFrameModule().JobOnThing(worker, frame, true);
            Check(job != null && worker.jobs.TryTakeOrderedJob(job), "worker starts first exact order");
            Find.Selector.Select(frame);
            guiAction = () =>
            {
                picker = new Dialog_FrameColorPicker(frame, FrameColorChannel.Primary);
                Find.WindowStack.Add(picker);
            };
            Next(1);
        }

        private void AfterService()
        {
            Check(ReferenceEquals(frame.Loadout.InstalledHead, head) &&
                ReferenceEquals(frame.Loadout.InstalledShoulder, shoulder) &&
                frame.equipment.Primary == null && rifle.Spawned,
                "worker completes later queued changes with exact Things");
            pilot = SpawnColonist(frame.Position + IntVec3.South, frame.Map);
            Check(frame.Cockpit.TryBoard(pilot), "pilot boards exact Frame");
            guiAction = CheckManualMenus;
            Next(2);
        }

        private void CheckManualMenus()
        {
            Thing replacement = ThingMaker.MakeThing(head.def);
            GenSpawn.Spawn(replacement, frame.Position + IntVec3.North * 5 + IntVec3.East * 2, frame.Map);
            var slotOptions = ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head);
            FloatMenuOption replace = slotOptions.FirstOrDefault(option => option.Label.StartsWith("Install ") &&
                !option.Disabled);
            Check(replace != null && slotOptions.Any(option => option.Label == "Remove installed"),
                "occupied slot offers exact replacement and Remove");
            replace.Chosen(colonistOrdering: true, null);
            Check(ReferenceEquals(frame.Service.OrderFor(FrameAddonCategory.Head)?.incoming, replacement),
                "slot menu queues exact replacement");
            FloatMenuOption cancel = ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head)
                .FirstOrDefault(option => option.Label == "Cancel queued change");
            Check(cancel != null, "queued slot exposes Cancel");
            cancel.Chosen(colonistOrdering: true, null);
            Check(frame.Service.OrderFor(FrameAddonCategory.Head) == null,
                "slot Cancel preserves installed Head");
            ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head)
                .First(option => option.Label == "Remove installed").Chosen(colonistOrdering: true, null);
            Check(frame.Service.OrderFor(FrameAddonCategory.Head)?.kind == FrameServiceOrderKind.Remove,
                "slot Remove queues physical removal");
            ITab_FrameVisual.SlotOptions(frame, FrameAddonCategory.Head)
                .First(option => option.Label == "Cancel queued change").Chosen(colonistOrdering: true, null);
            var selected = new List<Pawn> { frame };
            var self = FloatMenuMakerMap.GetOptions(selected, frame.DrawPos, out _);
            FloatMenuOption disembark = self.FirstOrDefault(option => option.Label == "Disembark");
            Check(disembark != null && !disembark.Disabled, "self right-click Disembark is available");
            disembark.Chosen(colonistOrdering: true, null);
            Check(!frame.Cockpit.HasPilot && pilot.Spawned, "Disembark returns exact pilot to map");
            Check(frame.Cockpit.TryBoard(pilot), "same pilot boards again");
            haulItem = ThingMaker.MakeThing(ThingDefOf.Steel);
            GenSpawn.Spawn(haulItem, frame.Position + IntVec3.South * 4, frame.Map);
            var options = FloatMenuMakerMap.GetOptions(selected, haulItem.DrawPos, out _);
            FloatMenuOption haul = options.FirstOrDefault(option => option.Label.StartsWith("Haul "));
            Check(haul != null && !haul.Disabled &&
                !options.Any(option => option.Label.StartsWith("Prioritize ") ||
                    option.Label.StartsWith("Construct ") || option.Label.StartsWith("Clean ")),
                "manual Haul available without ordinary work providers");
            haul.Chosen(colonistOrdering: true, null);
            Check(frame.CurJobDef == JobDefOf.HaulToCell || frame.CurJobDef == JobDefOf.HaulToContainer,
                "manual Haul starts vanilla job");
            Next(3, 5000);
        }

        private void Finish()
        {
            Check(StoreUtility.IsInValidStorage(haulItem), "manual Haul delivered exact item");
            Pawn patient = SpawnColonist(frame.Position + IntVec3.South * 4, frame.Map);
            patient.health.AddHediff(DefDatabase<HediffDef>.GetNamed("Anesthetic"));
            Building_Bed bed = (Building_Bed)GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Bed),
                frame.Position + IntVec3.East * 7 + IntVec3.South * 3, frame.Map);
            bed.SetFaction(Faction.OfPlayer);
            guiAction = () =>
            {
                var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { frame }, patient.DrawPos, out _);
                FloatMenuOption rescue = options.FirstOrDefault(option => option.Label.Contains("Rescue"));
                Check(rescue != null && !rescue.Disabled, "manual Rescue appears for downed colonist");
                rescue.Chosen(colonistOrdering: true, null);
                Check(frame.CurJobDef == JobDefOf.Rescue, "manual Rescue starts vanilla job");
                File.AppendAllText(Report, "PASS acceptance complete" + Environment.NewLine);
                finished = true;
                Application.Quit(0);
            };
            Next(4);
        }

        public override void GameComponentOnGUI()
        {
            if (!Enabled || finished) return;
            if (picker != null && ++pickerGuiFrames >= 4)
            {
                picker.Close();
                picker = null;
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
                    throw new TimeoutException("Acceptance step " + step + " timed out; job=" +
                        worker?.CurJobDef?.defName + " queue=" + frame?.Service?.PendingLabel);
                if (step == 0) Start();
                else if (step == 1 && !frame.Service.HasPendingOrder && !frame.Service.IsServiceLocked)
                    AfterService();
                else if (step == 3 && StoreUtility.IsInValidStorage(haulItem)) Finish();
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
