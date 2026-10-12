using System;
using System.IO;
using System.Linq;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame.Tests
{
    public sealed partial class LifecycleSuite
    {
        private float smokeWorkBeforeSave;
        private float smokeRepairSeverity;
        private IntVec3 smokeStationaryCell;

        private void ExposeSmokeData()
        {
            Scribe_Values.Look(ref smokeWorkBeforeSave, "smokeWorkBeforeSave");
            Scribe_Values.Look(ref smokeRepairSeverity, "smokeRepairSeverity");
            Scribe_Values.Look(ref smokeStationaryCell, "smokeStationaryCell");
        }

        private void SmokeUpdate()
        {
            if (phase == 0) SmokeRenderingBoardAndColony();
            else if (phase == 1) SmokeReloadEjectRepairAndService();
            else SmokeServiceReloadAndRemove();
        }

        private static float ServiceField(JobDriver_ServiceFrameModule driver, string field) =>
            (float)typeof(JobDriver_ServiceFrameModule).GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);

        private static Toil CurrentToil(JobDriver driver) => (Toil)typeof(JobDriver)
            .GetProperty("CurToil", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(driver);

        private static bool ManInBlackQueued()
        {
            foreach (QueuedIncident queued in Find.Storyteller.incidentQueue)
                if (queued.FiringIncident.def.defName == "StrangerInBlackJoin") return true;
            return false;
        }

        private void SmokePilotIdentity(bool embarked, string stage)
        {
            string id = pilot.GetUniqueLoadID();
            Map map = frame.Map;
            Check(pilotId == id && !pilot.Destroyed && !pilot.Dead, stage + ": exact live pilot ID");
            Check(frame.Cockpit.GetDirectlyHeldThings().Count == (embarked ? 1 : 0) &&
                ReferenceEquals(frame.Cockpit.Pilot, embarked ? pilot : null), stage + ": one cockpit reference or empty cockpit");
            int spawnedIds = map.mapPawns.AllPawnsSpawned.Count(p => p.GetUniqueLoadID() == id);
            int unspawnedIds = map.mapPawns.AllPawnsUnspawned.Count(p => p.GetUniqueLoadID() == id);
            Check(spawnedIds == (embarked ? 0 : 1) && unspawnedIds == (embarked ? 1 : 0),
                stage + ": exactly one map identity in correct spawned state; spawned=" + spawnedIds +
                " unspawned=" + unspawnedIds + " holder=" + pilot.ParentHolder?.GetType().FullName);
            Check(map.mapPawns.AllPawns.Count(p => p.GetUniqueLoadID() == id) == 1 &&
                Find.WorldPawns.AllPawnsAliveOrDead.Count(p => p.GetUniqueLoadID() == id) == 0,
                stage + ": no second Pawn with the same logical load ID on map or in world");
            Find.ColonistBar.MarkColonistsDirty();
            Check(Find.ColonistBar.Entries.Count(entry => entry.pawn?.GetUniqueLoadID() == id) == 1,
                stage + ": exactly one ColonistBar entry");
            if (embarked)
                Check(ReferenceEquals(FrameColonistBarBridge.SelectionThing(pilot), frame),
                    stage + ": portrait selects containing Frame");
            Unique(pilot, stage);
        }

        private void SmokeRenderMeshesAndMutation()
        {
            frame.Drawer.renderer.EnsureGraphicsInitialized();
            FrameRenderNode torso = RenderNode(frame, FrameVisualPart.Torso);
            FrameRenderNode head = RenderNode(frame, FrameVisualPart.Head);
            foreach (FrameRenderNode node in new[] { torso, head })
                foreach (Rot4 facing in Rot4.AllRotations)
                {
                    var parms = new PawnDrawParms { pawn = frame, facing = facing, posture = PawnPosture.Standing };
                    Check(node.GraphicAt(facing)?.path == FrameVisualRules.Source(frame, node.Part).PathFor(facing) &&
                        node.Worker.GetFinalizedMaterial(node, parms) != null &&
                        MeshPool.GetMetaData(node.GetMesh(parms)).flipped == (facing == Rot4.West),
                        node.Part + " final mesh and authored material for " + facing);
                }
            smokeStationaryCell = frame.Position;
            moduleHead = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
            moduleShoulder = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShoulderSystem"));
            Check(frame.Loadout.TryInstallHead(moduleHead) && frame.Loadout.TryInstallShoulder(moduleShoulder),
                "stationary Head and Shoulder install exact visual modules");
            frame.Drawer.renderer.EnsureGraphicsInitialized();
            Check(frame.Position == smokeStationaryCell &&
                RenderNode(frame, FrameVisualPart.Helmet).GraphicAt(Rot4.East)?.path ==
                    FrameVisualRules.Source(frame, FrameVisualPart.Helmet).sides &&
                RenderNode(frame, FrameVisualPart.Shoulder).GraphicAt(Rot4.West)?.path ==
                    FrameVisualRules.Source(frame, FrameVisualPart.Shoulder).sides,
                "stationary render tree resolves installed overlays without movement");
            foreach (FrameVisualPart part in new[] { FrameVisualPart.Helmet, FrameVisualPart.Shoulder })
            {
                FrameRenderNode node = RenderNode(frame, part);
                var east = new PawnDrawParms { pawn = frame, facing = Rot4.East, posture = PawnPosture.Standing };
                var west = new PawnDrawParms { pawn = frame, facing = Rot4.West, posture = PawnPosture.Standing };
                Check(!MeshPool.GetMetaData(node.GetMesh(east)).flipped &&
                    MeshPool.GetMetaData(node.GetMesh(west)).flipped &&
                    ReferenceEquals(node.Worker.GetFinalizedMaterial(node, east),
                        node.Worker.GetFinalizedMaterial(node, west)),
                    part + " uses the same side material and final mirrored West mesh");
            }
            Thing removedHead = null;
            Thing removedShoulder = null;
            Check(frame.Loadout.TryRemoveHead(out removedHead) && ReferenceEquals(removedHead, moduleHead) &&
                frame.Loadout.TryRemoveShoulder(out removedShoulder) && ReferenceEquals(removedShoulder, moduleShoulder),
                "stationary Head and Shoulder removal preserves physical identity");
            frame.Drawer.renderer.EnsureGraphicsInitialized();
            Check(frame.Position == smokeStationaryCell &&
                !FrameVisualRules.Visible(frame, FrameVisualPart.Helmet) &&
                !FrameVisualRules.Visible(frame, FrameVisualPart.Shoulder),
                "stationary overlay removal takes effect immediately");
            Check(frame.Loadout.TryInstallHead(removedHead) && frame.Loadout.TryInstallShoulder(removedShoulder),
                "visual fixtures reinstall without spawning replacements");
        }

        private void SmokeRenderingBoardAndColony()
        {
            if (Find.TickManager.TicksGame < 1000) return;
            if (step == 0)
            {
                Map map = Find.CurrentMap;
                foreach (Pawn existing in map.mapPawns.FreeColonistsSpawned.ToList()) existing.SetFaction(null);
                frame = SpawnFrame(map.Center);
                pilot = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), frame.Position + IntVec3.East, map);
                other = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), frame.Position + IntVec3.North, map);
                pilotId = pilot.GetUniqueLoadID();
                frameId = frame.GetUniqueLoadID();
                Check(map.mapPawns.FreeColonists.Count == 2, "two-colonist colony fixture");
                SmokeRenderMeshesAndMutation();
                Job boarding = JobMaker.MakeJob(ZenithJobDefOf.ZF_BoardFrame, frame);
                Check(pilot.jobs.TryTakeOrderedJob(boarding), "real boarding job accepted");
                Next(1, 2500);
            }
            else if (step == 1 && pilot.CurJobDef == ZenithJobDefOf.ZF_BoardFrame &&
                CurrentToil(pilot.jobs.curDriver)?.debugName == "BoardFrame")
            {
                JobDriver driver = pilot.jobs.curDriver;
                int duration = CurrentToil(driver).defaultDuration;
                if (driver.ticksLeftThisToil >= duration - 30) return;
                Check(duration == DockingBayUtility.BoardingTicks(frame) &&
                    driver.ticksLeftThisToil > 0 && driver.ticksLeftThisToil < duration,
                    "boarding progress derives from live timer and advances between zero and one");
                Next(2, 2200);
            }
            else if (step == 2 && frame.Cockpit.HasPilot)
            {
                SmokePilotIdentity(true, "before colony downing");
                other.health.AddHediff(HediffDefOf.Anesthetic);
                Check(other.Downed && !other.Dead && frame.Cockpit.CanOperate,
                    "other colonist downed while embarked pilot stays capable");
                Check(!ManInBlackQueued(),
                    "downing does not queue Man in Black");
                Next(3, 900);
            }
            else if (step == 3 && Find.TickManager.TicksGame >= deadline - 350)
            {
                SmokePilotIdentity(true, "two-colonist colony after downing");
                Check(!Find.GameEnder.gameEnding && !ManInBlackQueued(),
                    "embarked living colonist prevents game-over and Man in Black");
                SaveNext(1);
            }
        }

        private void SmokeReloadEjectRepairAndService()
        {
            if (step == 0)
            {
                SmokePilotIdentity(true, "fresh-process embarked reload");
                frame.Drawer.renderer.EnsureGraphicsInitialized();
                Check(FrameVisualRules.Visible(frame, FrameVisualPart.Helmet) &&
                    FrameVisualRules.Visible(frame, FrameVisualPart.Shoulder) &&
                    MeshPool.GetMetaData(RenderNode(frame, FrameVisualPart.Helmet).GetMesh(
                        new PawnDrawParms { pawn = frame, facing = Rot4.West, posture = PawnPosture.Standing })).flipped,
                    "fresh-process reload restores installed overlays and mirrored final mesh");
                Check(frame.Cockpit.TryDisembark(), "ordinary disembark succeeds");
                SmokePilotIdentity(false, "disembarked");
                Check(frame.Cockpit.TryBoard(pilot), "same exact pilot reboards");
                SmokePilotIdentity(true, "reboarded");
                frame.Kill(null);
                Check(pilot.Spawned && !pilot.Dead && !frame.Cockpit.HasPilot,
                    "lethal Frame event emergency-ejects the exact live pilot");
                Check(Find.CurrentMap.mapPawns.AllPawns.Count(p => p.GetUniqueLoadID() == pilotId) == 1 &&
                    Find.WorldPawns.AllPawnsAliveOrDead.Count(p => p.GetUniqueLoadID() == pilotId) == 0,
                    "emergency ejection leaves one logical pilot identity");
                frame = SpawnFrame(Find.CurrentMap.Center + IntVec3.West * 5);
                frameId = frame.GetUniqueLoadID();
                Hediff_Injury injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut,
                    frame, frame.RaceProps.body.corePart);
                injury.Severity = 12f;
                frame.health.AddHediff(injury, frame.RaceProps.body.corePart);
                smokeRepairSeverity = injury.Severity;
                Check(pilot.jobs.TryTakeOrderedJob(JobMaker.MakeJob(ZenithJobDefOf.ZF_RepairFrame, frame)),
                    "real repair job accepted");
                Next(1, 2800);
            }
            else if (step == 1 && pilot.CurJobDef == ZenithJobDefOf.ZF_RepairFrame &&
                frame.Service.IsServiceLocked &&
                FrameRepairUtility.NextRepairableInjury(frame)?.Severity < smokeRepairSeverity - 0.05f)
            {
                Check(CurrentToil(pilot.jobs.curDriver).handlingFacing &&
                    CurrentToil(pilot.jobs.curDriver).activeSkill() == SkillDefOf.Construction,
                    "active repair faces target and uses Construction work feedback");
                Check(FrameRepairUtility.NextRepairableInjury(frame).Severity < smokeRepairSeverity,
                    "mechanical repair still reduces actual injury");
                pilot.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Check(!frame.Service.IsServiceLocked, "repair interruption releases service lock and effecter toil");
                moduleHead = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
                IntVec3 cell = frame.Position + IntVec3.North * 4;
                var stockpile = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, frame.Map.zoneManager);
                frame.Map.zoneManager.RegisterZone(stockpile);
                stockpile.AddCell(cell);
                stockpile.GetStoreSettings().filter.SetDisallowAll();
                stockpile.GetStoreSettings().filter.SetAllow(moduleHead.def, true);
                GenSpawn.Spawn(moduleHead, cell, frame.Map);
                moduleHeadId = moduleHead.GetUniqueLoadID();
                Check(frame.Service.QueueInstall(FrameAddonCategory.Head, moduleHead),
                    "stored exact Head module queues for installation");
                Job service = new WorkGiver_ServiceFrameModule().JobOnThing(pilot, frame, true);
                Check(service != null && pilot.jobs.TryTakeOrderedJob(service), "real module installation job accepted");
                Next(2, 4200);
            }
            else if (step == 2 && pilot.CurJobDef == ZenithJobDefOf.ZF_ServiceFrameModule &&
                frame.Service.IsServiceLocked)
            {
                var driver = pilot.jobs.curDriver as JobDriver_ServiceFrameModule;
                float total = ServiceField(driver, "totalWork");
                float left = ServiceField(driver, "workLeft");
                if (left > total * 0.65f) return;
                Check(total > 0f && left > 0f && left < total,
                    "installation progress is actual remaining work within saved total");
                smokeWorkBeforeSave = left;
                SaveNext(2);
            }
        }

        private void SmokeServiceReloadAndRemove()
        {
            if (step == 0)
            {
                Check(pilot.CurJobDef == ZenithJobDefOf.ZF_ServiceFrameModule &&
                    ReferenceEquals(frame.Service.PendingModule, moduleHead) &&
                    moduleHead.GetUniqueLoadID() == moduleHeadId,
                    "fresh-process mid-install restores exact job and module");
                var driver = pilot.jobs.curDriver as JobDriver_ServiceFrameModule;
                Check(ServiceField(driver, "totalWork") > 0f &&
                    ServiceField(driver, "workLeft") <= smokeWorkBeforeSave &&
                    ServiceField(driver, "workLeft") > 0f,
                    "installation progress survives save/load without reset or instant finish");
                Next(1, 4000);
            }
            else if (step == 1 && ReferenceEquals(frame.Loadout.InstalledHead, moduleHead))
            {
                Check(!frame.Service.HasPendingOrder &&
                    frame.Map.mapPawns.AllPawns.Count(p => p.GetUniqueLoadID() == pilotId) == 1,
                    "installation completes once with exact module and pilot");
                Check(frame.Service.QueueRemove(FrameAddonCategory.Head), "Head removal queues");
                Job removal = new WorkGiver_ServiceFrameModule().JobOnThing(pilot, frame, true);
                Check(removal != null && pilot.jobs.TryTakeOrderedJob(removal), "real module removal job accepted");
                Next(2, 4000);
            }
            else if (step == 2 && pilot.CurJobDef == ZenithJobDefOf.ZF_ServiceFrameModule &&
                frame.Service.IsServiceLocked)
            {
                var driver = pilot.jobs.curDriver as JobDriver_ServiceFrameModule;
                float total = ServiceField(driver, "totalWork");
                float left = ServiceField(driver, "workLeft");
                if (left > total * 0.6f) return;
                Check(total > 0f && left > 0f && left < total,
                    "removal progress advances from actual work timer");
                Next(3, 4000);
            }
            else if (step == 3 && frame.Loadout.InstalledHead == null && moduleHead.Spawned)
            {
                Check(!frame.Service.HasPendingOrder && moduleHead.GetUniqueLoadID() == moduleHeadId &&
                    !moduleHead.Destroyed, "module removal completes once with exact physical module");
                Finish();
            }
        }
    }
}
