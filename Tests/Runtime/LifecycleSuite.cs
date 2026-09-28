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
    public sealed class LifecycleSuite : GameComponent
    {
        private int phase;
        private int step;
        private int deadline;
        private bool finished;
        private string pilotId;
        private FramePawn frame;
        private Pawn pilot;
        private Pawn other;
        private Thing target;
        private IntVec3 destination;
        private int targetHealth;
        private readonly List<string> passed = new List<string>();
        public LifecycleSuite(Game game) { }
        private string ReportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "phase-" + phase + ".txt");
        public override void ExposeData()
        {
            Scribe_Values.Look(ref phase, "testPhase");
            Scribe_Values.Look(ref pilotId, "testPilotId");
            Scribe_References.Look(ref frame, "testFrame");
            Scribe_References.Look(ref pilot, "testPilot");
            Scribe_References.Look(ref other, "testOther");
        }
        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add("PASS " + name);
        }
        private void Unique(Pawn p, string stage)
        {
            var all = new List<Thing>();
            foreach (Map m in Find.Maps)
            {
                all.AddRange(m.listerThings.AllThings);
                foreach (Thing t in m.listerThings.AllThings)
                {
                    if (t is IThingHolder h) all.AddRange(ThingOwnerUtility.GetAllThingsRecursively(h));
                    if (t is ThingWithComps c)
                        foreach (var holder in c.AllComps.OfType<IThingHolder>()) all.AddRange(ThingOwnerUtility.GetAllThingsRecursively(holder));
                }
            }
            all.AddRange(ThingOwnerUtility.GetAllThingsRecursively(Current.Game.GetComponent<PilotRecovery>()));
            all.AddRange(Find.WorldPawns.AllPawnsAliveOrDead);
            // Multiple traversal paths may legitimately visit the same object; different
            // objects with the same load ID must never exist.
            var matches = all.Where(t => t.GetUniqueLoadID() == p.GetUniqueLoadID()).Distinct().ToList();
            Check(matches.Count == 1 && ReferenceEquals(matches[0], p), stage + ": exactly one pawn object with pilot ID");
            Check(!p.Destroyed && !p.Dead && (!p.Spawned || p.holdingOwner?.Owner is Map), stage + ": live and exclusively owned");
        }
        public override void GameComponentUpdate()
        {
            if (finished || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline) throw new TimeoutException("Phase " + phase + " step " + step);
                if (phase == 0) BeforeSave();
                else if (phase == 1) AfterLoad();
                else RecoveryLoad();
            }
            catch (Exception e)
            {
                finished = true;
                File.WriteAllLines(ReportPath, passed.Concat(new[] { "FAIL " + e }));
                Log.Error("ZENITH TEST FAILURE " + e);
                Application.Quit(1);
            }
        }
        private void BeforeSave()
        {
            // Let the vanilla arrival pods land before arranging the disposable arena.
            if (Find.TickManager.TicksGame < 1000) return;
            Map map = Find.CurrentMap;
            if (step == 0)
            {
                IntVec3 center = map.Center;
                foreach (IntVec3 cell in CellRect.CenteredOn(center, 18))
                {
                    foreach (Thing t in cell.GetThingList(map).ToList())
                        if (!(t is Pawn) && !(t is IThingHolder) && (t.def.category == ThingCategory.Plant || t.def.category == ThingCategory.Building || t.def.category == ThingCategory.Item)) t.Destroy();
                    map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                    map.roofGrid.SetRoof(cell, null);
                }
                foreach (Pawn p in map.mapPawns.FreeColonistsSpawned) p.drafter.Drafted = true;
                frame = SpawnFrame(center);
                Check(!frame.RaceProps.IsMechanoid, "Frame is not classified as a RimWorld/Biotech mechanoid");
                pilot = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                other = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(pilot, center + new IntVec3(6, 0, 0), map);
                GenSpawn.Spawn(other, center + new IntVec3(0, 0, 1), map);
                other.drafter.Drafted = true;
                pilotId = pilot.GetUniqueLoadID();
                var boardMenuPawns = new List<Pawn> { pilot };
                var boardOptions = FloatMenuMakerMap.GetOptions(boardMenuPawns, frame.DrawPos, out var _);
                Check(boardOptions.Any(option => option.Label.Contains("Board Frame")), "pilot right-click menu offers Board Frame");
                Unique(pilot, "before boarding");
                Check(!frame.SetDrafted(true), "empty Frame refuses drafting");
                Check(!frame.Cockpit.TryBoard(null) && !frame.Cockpit.TryBoard(frame), "null and self boarding rejected");
                other.SetFaction(null);
                Check(!frame.Cockpit.TryBoard(other) && other.Spawned, "foreign faction pilot rejected");
                other.SetFaction(Faction.OfPlayer);
                Check(!frame.OrderMove(center + IntVec3.North), "empty Frame refuses movement");
                Check(!frame.Cockpit.TryBoard(pilot), "nonadjacent boarding rejected");
                Check(pilot.Spawned && pilot.holdingOwner.Owner == map, "failed boarding preserves map ownership");
                pilot.jobs.TryTakeOrderedJob(JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("ZF_BoardFrame"), frame));
                Next(1);
            }
            else if (frame.Cockpit.HasPilot)
            {
                Check(ReferenceEquals(frame.Cockpit.Pilot, pilot), "real boarding job reaches Frame and transfers pilot");
                Unique(pilot, "boarded");
                Check(!pilot.Spawned && pilot.holdingOwner == frame.Cockpit.GetDirectlyHeldThings(), "pilot owned by cockpit");
                Check(!map.mapPawns.AllPawnsSpawned.Contains(pilot) && map.mapPawns.AllPawnsUnspawned.Contains(pilot), "map discovers contained pilot without spawning it");
                Check(!frame.Cockpit.TryBoard(other) && other.Spawned, "second pilot rejected without loss");
                Check(frame.Cockpit.Pilot.Faction == frame.Faction, "boarded faction coherence");
                Check(frame.CanTakeOrder, "piloted Frame enters ordinary pawn order pipeline");
                Check(frame.SetDrafted(true), "piloted Frame drafts");
                SaveNext(1);
            }
        }
        private void AfterLoad()
        {
            if (step == 0)
            {
                Check(pilot != null && frame != null && frame.Cockpit.Pilot == pilot && pilot.GetUniqueLoadID() == pilotId, "fresh-process save/load restores pilot identity and relationship");
                Unique(pilot, "after load");
                Check(frame.Drafted && frame.Cockpit.CanOperate, "draft and control survive load");
                Check(frame.GetGizmos().Count() == 2, "only Frame-specific disembark plus normal-semantics draft gizmo remain");
                Check(frame.GetGizmos().OfType<Command>().Any(command => command.hotKey == KeyBindingDefOf.Command_ColonistDraft), "draft command uses normal colonist draft hotkey");
                foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList()) p.SetFaction(null);
                Check(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Count == 0, "last-colonist fixture has only embarked pilot");
                Next(10, 650);
            }
            else if (step == 10 && Find.TickManager.TicksGame >= deadline - 100)
            {
                Check(!Find.GameEnder.gameEnding && !Find.LetterStack.LettersListForReading.Any(l => l.def == LetterDefOf.GameEnded), "last embarked colonist survives game-over countdown");
                destination = frame.Position + new IntVec3(0, 0, 5);
                var moveSelection = new List<Pawn> { frame };
                var moveOptions = FloatMenuMakerMap.GetOptions(moveSelection, destination.ToVector3Shifted(), out var _);
                var gotoOption = moveOptions.FirstOrDefault(option => option.isGoto && !option.Disabled);
                Check(gotoOption != null, "ordinary drafted-pawn right-click movement option available");
                gotoOption.Chosen(colonistOrdering: true, null);
                Next(1);
            }
            else if (step == 1 && frame.Position == destination)
            {
                Check(true, "real pathing reaches ordered destination");
                target = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.Steel);
                GenSpawn.Spawn(target, frame.Position + new IntVec3(8, 0, 0), frame.Map);
                targetHealth = target.HitPoints;
                var attackSelection = new List<Pawn> { frame };
                var attackOptions = FloatMenuMakerMap.GetOptions(attackSelection, target.DrawPos, out var _);
                var attackOption = attackOptions.FirstOrDefault(option => option.Priority == MenuOptionPriority.AttackEnemy && !option.Disabled);
                Check(attackOption != null, "ordinary drafted-pawn right-click attack option available");
                attackOption.Chosen(colonistOrdering: true, null);
                Next(2);
            }
            else if (step == 2 && target.HitPoints < targetHealth)
            {
                Check(true, "real projectile damages target");
                Next(3, 300);
            }
            else if (step == 3 && Find.TickManager.TicksGame >= deadline - 100)
            {
                Check(frame.CurJob != null && frame.CurJob.def == JobDefOf.AttackStatic, "ordinary player-issued attack remains active after the first burst");
                Check(!frame.drafter.FireAtWill, "fire at will disabled");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                Check(frame.Cockpit.TryDisembark(), "disembark succeeds");
                Unique(pilot, "disembarked");
                Check(pilot.Spawned && !frame.Cockpit.HasPilot && !frame.Drafted, "disembark empties cockpit and stops Frame");
                for (int i = 0; i < 20; i++)
                {
                    Check(frame.Cockpit.TryBoard(pilot), "repeat board " + i);
                    Check(frame.Cockpit.TryDisembark(), "repeat disembark " + i);
                    Unique(pilot, "repeat " + i);
                }
                Check(frame.Cockpit.TryBoard(pilot), "board before downing");
                var legs = frame.RaceProps.body.AllParts.Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)).ToList();
                Check(legs.Count == 2, "downing fixture has two mechanical legs");
                foreach (var leg in legs) frame.health.AddHediff(HediffDefOf.MissingBodyPart, leg);
                Check(frame.Downed && !frame.Dead && !frame.Cockpit.HasPilot && pilot.Spawned, "real capacity-loss downing ejects immediately");
                Unique(pilot, "downed Frame");
                FramePawn deadFrame = SpawnFrame(pilot.Position);
                Check(deadFrame.Cockpit.TryBoard(pilot), "board before death");
                deadFrame.Kill(null);
                Check(deadFrame.Dead && !deadFrame.Cockpit.HasPilot && pilot.Spawned, "death ejects before destruction");
                Unique(pilot, "dead Frame");
                FramePawn vanished = SpawnFrame(pilot.Position);
                Check(vanished.Cockpit.TryBoard(pilot), "board before vanish");
                vanished.Destroy();
                Check(vanished.Destroyed && pilot.Spawned, "direct destruction ejects");
                Unique(pilot, "vanished Frame");
                frame = SpawnFrame(pilot.Position);
                Check(frame.Cockpit.TryBoard(pilot), "board before off-map destruction");
                frame.DeSpawn();
                Check(!frame.Cockpit.TryDisembark() && frame.Cockpit.Pilot == pilot, "failed off-map disembark preserves owner");
                frame.Destroy();
                Check(!frame.Cockpit.HasPilot && pilot.holdingOwner == Current.Game.GetComponent<PilotRecovery>().GetDirectlyHeldThings(), "off-map destruction transfers to independent owner");
                Unique(pilot, "rescue container");
                frame = null;
                SaveNext(2);
            }
        }
        private void RecoveryLoad()
        {
            if (step == 0)
            {
                Check(pilot != null && pilot.GetUniqueLoadID() == pilotId, "rescue owner survives fresh-process load");
                Unique(pilot, "rescue reload");
                Next(1);
            }
            else if (pilot.Spawned)
            {
                Check(Current.Game.GetComponent<PilotRecovery>().GetDirectlyHeldThings().Count == 0, "rescue delivers pilot and empties owner");
                Unique(pilot, "recovered");
                Check(pilot.Faction == Faction.OfPlayer, "faction preserved through complete lifecycle");
                Finish();
            }
        }
        private FramePawn SpawnFrame(IntVec3 cell) => (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), cell, Find.CurrentMap);
        private void Next(int nextStep, int ticks = 2500) { step = nextStep; deadline = Find.TickManager.TicksGame + ticks; }
        private void SaveNext(int nextPhase)
        {
            string report = ReportPath;
            phase = nextPhase;
            GameDataSaveLoader.SaveGame("ZenithLifecycle");
            File.WriteAllLines(report, passed.Concat(new[] { "PASS phase complete; saved for fresh-process reload" }));
            finished = true;
            Application.Quit(0);
        }
        private void Finish()
        {
            File.WriteAllLines(ReportPath, passed.Concat(new[] { "PASS ALL LIFECYCLE PHASES COMPLETE" }));
            finished = true;
            Application.Quit(0);
        }
    }
}


