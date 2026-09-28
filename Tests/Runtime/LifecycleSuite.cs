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
        private string frameId;
        private FramePawn frame;
        private Pawn pilot;
        private Pawn other;
        private Thing target;
        private IntVec3 destination;
        private FramePawn squadMateFrame;
        private IntVec3 squadStartA;
        private IntVec3 squadStartB;
        private Action pendingGuiAction;
        private readonly List<string> passed = new List<string>();
        public LifecycleSuite(Game game) { }
        private string ReportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "phase-" + phase + ".txt");
        public override void ExposeData()
        {
            Scribe_Values.Look(ref phase, "testPhase");
            Scribe_Values.Look(ref pilotId, "testPilotId");
            Scribe_Values.Look(ref frameId, "testFrameId");
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
            if (finished || pendingGuiAction != null || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline) throw new TimeoutException("Phase " + phase + " step " + step);
                if (phase == 0) BeforeSave();
                else if (phase == 1) AfterLoad();
                else if (phase == 2) RecoveryLoad();
                else DisabledFrameLoad();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
        public override void GameComponentOnGUI()
        {
            if (finished || pendingGuiAction == null) return;
            Action action = pendingGuiAction;
            pendingGuiAction = null;
            try { action(); }
            catch (Exception e) { Fail(e); }
        }
        private void OnGui(Action action) => pendingGuiAction = action;
        private void Fail(Exception e)
        {
            finished = true;
            File.WriteAllLines(ReportPath, passed.Concat(new[] { "FAIL " + e }));
            Log.Error("ZENITH TEST FAILURE " + e);
            Application.Quit(1);
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
                    map.fogGrid.Unfog(cell);
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
                Unique(pilot, "before boarding");
                Check(!frame.SetDrafted(true), "empty Frame refuses drafting");
                Check(!frame.Cockpit.TryBoard(null) && !frame.Cockpit.TryBoard(frame), "null and self boarding rejected");
                other.SetFaction(null);
                Check(!frame.Cockpit.TryBoard(other) && other.Spawned, "foreign faction pilot rejected");
                other.SetFaction(Faction.OfPlayer);
                Check(!frame.CanTakeOrder, "empty Frame refuses ordinary player orders");
                Check(!frame.Cockpit.TryBoard(pilot), "nonadjacent boarding rejected");
                Check(pilot.Spawned && pilot.holdingOwner.Owner == map, "failed boarding preserves map ownership");
                OnGui(() =>
                {
                    var boardMenuPawns = new List<Pawn> { pilot };
                    var boardOptions = FloatMenuMakerMap.GetOptions(boardMenuPawns, frame.DrawPos, out var _);
                    var boardOption = boardOptions.FirstOrDefault(option => option.Label.Contains("Board Frame"));
                    Check(boardOption != null,
                        "pilot right-click menu offers Board Frame; actual options: " +
                        string.Join(" | ", boardOptions.Select(option => option.Label)));
                    boardOption.Chosen(colonistOrdering: true, null);
                    Check(pilot.CurJobDef == DefDatabase<JobDef>.GetNamed("ZF_BoardFrame"), "Board Frame menu option issues the real boarding job");
                    Next(1);
                });
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
                Check(frame.CanTakeOrder && !frame.IsPlayerControlled && !frame.IsColonyMech, "Frame receives orders without impersonating a colonist or Biotech mech");
                Find.Selector.ClearSelection();
                Find.Selector.Select(frame);
                var frameCommands = frame.GetGizmos().OfType<Command>().ToList();
                Check(frameCommands.Any(command => command.hotKey == KeyBindingDefOf.Command_ColonistDraft), "draft command uses normal colonist draft hotkey");
                Check(frameCommands.Any(command => command.hotKey == KeyBindingDefOf.Misc2), "vanilla melee attack gizmo is available for a drafted Frame");
                Check(!frameCommands.Any(command => command.defaultLabel == "Move" || command.defaultLabel == "Fire rifle"), "temporary Move/Fire chess-piece gizmos are absent");
                Thing looseRifle = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugRifle"));
                GenSpawn.Spawn(looseRifle, frame.Position + new IntVec3(2, 0, 0), frame.Map);
                var itemSelection = new List<Pawn> { frame };
                var itemOptions = FloatMenuMakerMap.GetOptions(itemSelection, looseRifle.DrawPos, out var _);
                Check(itemOptions.Count == 1 && itemOptions[0].isGoto, "Frame order bridge does not expose colonist equip/pickup/work actions");
                looseRifle.Destroy();
                foreach (Pawn p in Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList()) p.SetFaction(null);
                Check(Find.CurrentMap.mapPawns.FreeColonistsSpawned.Count == 0, "last-colonist fixture has only embarked pilot");
                Next(10, 650);
            }
            else if (step == 10 && Find.TickManager.TicksGame >= deadline - 100)
            {
                Check(!Find.GameEnder.gameEnding && !Find.LetterStack.LettersListForReading.Any(l => l.def == LetterDefOf.GameEnded), "last embarked colonist survives game-over countdown");
                destination = frame.Position + new IntVec3(0, 0, 5);
                OnGui(() =>
                {
                    var moveSelection = new List<Pawn> { frame };
                    var moveOptions = FloatMenuMakerMap.GetOptions(moveSelection, destination.ToVector3Shifted(), out var _);
                    string commandRangeText = "OutOfCommandRange".Translate();
                    Check(!moveOptions.Any(option => option.Label.Contains(commandRangeText)), "ordinary movement never exposes Biotech command-range rejection");
                    var gotoOption = moveOptions.FirstOrDefault(option => option.isGoto && !option.Disabled);
                    Check(gotoOption != null, "ordinary drafted-pawn right-click movement option available");
                    gotoOption.Chosen(colonistOrdering: true, null);
                    Next(11);
                });
            }
            else if (step == 11 && frame.CurJobDef == JobDefOf.Goto)
            {
                Check(true, "ordinary movement option issues Goto");
                destination = frame.CurJob.targetA.Cell;
                Next(1);
            }
            else if (step == 1 && frame.Position == destination)
            {
                Check(true, "real pathing reaches ordered destination");
                target = GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfAncientsHostile),
                    frame.Position + new IntVec3(8, 0, 0), frame.Map);
                OnGui(() =>
                {
                    var attackSelection = new List<Pawn> { frame };
                    var attackOptions = FloatMenuMakerMap.GetOptions(attackSelection, target.DrawPos, out var attackContext);
                    Check(target.Spawned && target.HostileTo(Faction.OfPlayer) && !target.Position.Fogged(target.Map),
                        "hostile ranged target is spawned and visible");
                    Check(attackContext.ClickedThings.Contains(target),
                        $"hostile target is present under its draw position; target={target.Position} clicked={attackContext.ClickedCell} things={string.Join(", ", attackContext.ClickedThings)}");
                    Check(FloatMenuUtility.GetMeleeAttackAction(frame, target, out var meleeFail) != null, "ordinary melee attack action is available as a valid alternative");
                    var attackOption = FloatMenuMakerMap.GetAutoTakeOption(attackOptions);
                    Check(attackOption != null && attackOption.Priority == MenuOptionPriority.AttackEnemy && attackOption.autoTakeablePriority == 40f,
                        "ordinary hostile right-click defaults to ranged attack when both ranged and melee are valid; actual options: " +
                        string.Join(" | ", attackOptions.Select(option =>
                            $"{option.Label} disabled={option.Disabled} priority={option.Priority} auto={option.autoTakeable} autoPriority={option.autoTakeablePriority}")));
                    attackOption.Chosen(colonistOrdering: true, null);
                    Next(2);
                });
            }
            else if (step == 2 && TargetDamaged())
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
                if (target != null && !target.Destroyed) target.Destroy();

                other.SetFaction(Faction.OfPlayer);
                IntVec3 mateCell = other.Position + IntVec3.East;
                squadMateFrame = SpawnFrame(mateCell);
                Check(squadMateFrame.Cockpit.TryBoard(other), "second Frame accepts second pilot for group-control test");
                Check(squadMateFrame.SetDrafted(true), "second piloted Frame drafts");
                squadStartA = frame.Position;
                squadStartB = squadMateFrame.Position;
                IntVec3 groupDestination = frame.Position + new IntVec3(0, 0, 6);
                OnGui(() =>
                {
                    var groupSelection = new List<Pawn> { frame, squadMateFrame };
                    var groupMoveOptions = FloatMenuMakerMap.GetOptions(groupSelection, groupDestination.ToVector3Shifted(), out var _);
                    Check(!groupMoveOptions.Any(option => option.Label.Contains("OutOfCommandRange".Translate())), "group move has no Biotech command-range rejection");
                    var groupGoto = groupMoveOptions.FirstOrDefault(option => option.isGoto && !option.Disabled);
                    Check(groupGoto != null, "ordinary multiselect movement option available");
                    groupGoto.Chosen(colonistOrdering: true, null);
                    Next(31, 1200);
                });
            }
            else if (step == 31 && frame.CurJobDef == JobDefOf.Goto && squadMateFrame.CurJobDef == JobDefOf.Goto)
            {
                Check(true, "one group move command reaches both Frames");
                Next(4, 1200);
            }
            else if (step == 4 && frame.Position != squadStartA && squadMateFrame.Position != squadStartB)
            {
                Check(true, "both Frames physically move under one group order");
                IntVec3 targetCell = frame.Position + new IntVec3(8, 0, 0);
                target = GenSpawn.Spawn(PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile), targetCell, frame.Map);
                OnGui(() =>
                {
                    var groupSelection = new List<Pawn> { frame, squadMateFrame };
                    var groupAttackOptions = FloatMenuMakerMap.GetOptions(groupSelection, target.DrawPos, out var _);
                    var groupAttack = FloatMenuMakerMap.GetAutoTakeOption(groupAttackOptions);
                    Check(groupAttack != null && groupAttack.Priority == MenuOptionPriority.AttackEnemy, "ordinary multiselect hostile attack is directly auto-takeable");
                    groupAttack.Chosen(colonistOrdering: true, null);
                    Next(41, 1200);
                });
            }
            else if (step == 41 && frame.CurJobDef == JobDefOf.AttackStatic && squadMateFrame.CurJobDef == JobDefOf.AttackStatic)
            {
                Check(true, "one group attack command reaches both Frames");
                Next(5, 1200);
            }
            else if (step == 5 && TargetDamaged())
            {
                Check(true, "group-issued Frame attack produces real projectile damage");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                squadMateFrame.jobs.StopAll();
                squadMateFrame.stances.CancelBusyStanceHard();
                Check(squadMateFrame.Cockpit.TryDisembark(), "group-test pilot disembarks");
                squadMateFrame.Destroy();
                squadMateFrame = null;
                if (target != null && !target.Destroyed) target.Destroy();

                Check(frame.Cockpit.TryDisembark(), "disembark succeeds");
                Unique(pilot, "disembarked");
                Check(pilot.Spawned && !frame.Cockpit.HasPilot && !frame.Drafted && !frame.CanTakeOrder, "disembark empties cockpit, stops Frame and removes order eligibility");
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
        private bool TargetDamaged()
        {
            if (target == null || target.Destroyed) return true;
            if (target is Pawn pawnTarget)
                return pawnTarget.Dead || pawnTarget.Downed || pawnTarget.health.hediffSet.hediffs.OfType<Hediff_Injury>().Any();
            return false;
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
                frame = SpawnFrame(pilot.Position + IntVec3.East);
                frameId = frame.GetUniqueLoadID();
                Check(frame.Cockpit.TryBoard(pilot), "recovery fixture boards pilot before disablement");
                Check(frame.SetDrafted(true), "recovery fixture drafts before disablement");
                DownByMissingLegs(frame);
                Check(frame.Downed && frame.Recovery.IsRecoverablyDisabled, "missing moving limbs create a recoverable disabled chassis");
                Check(!frame.Cockpit.HasPilot && pilot.Spawned, "disabled transition ejects exact pilot");
                Check(!frame.CanTakeOrder && !frame.SetDrafted(true) && !frame.Cockpit.CanBoard(pilot, false), "disabled chassis rejects drafting, orders and boarding");
                Check(frame.GetInspectString().Contains("Status: Disabled chassis (recoverable)"), "disabled chassis has concise recoverable inspect status");
                Find.Selector.ClearSelection();
                Find.Selector.Select(frame);
                Check(Find.Selector.IsSelected(frame), "disabled chassis remains selectable");
                Check(frame.GetGizmos().OfType<Command>().Any(command => command.defaultLabel == "DEV: Recover disabled chassis"), "disabled chassis exposes development-only recovery command in Dev Mode");
                Unique(pilot, "disabled before save");
                SaveNext(3);
            }
        }

        private void DisabledFrameLoad()
        {
            if (step == 0)
            {
                Check(frame != null && frame.GetUniqueLoadID() == frameId, "fresh-process disabled load restores the same Frame identity");
                Check(frame.Downed && frame.Recovery.IsRecoverablyDisabled && !frame.Cockpit.HasPilot, "fresh-process load preserves disabled empty chassis state");
                Check(!frame.Drafted && !frame.CanTakeOrder && !frame.Cockpit.CanBoard(pilot, false), "loaded disabled chassis remains inactive and rejects boarding");
                Unique(pilot, "disabled reload");
                OnGui(() =>
                {
                    var disabledOptions = FloatMenuMakerMap.GetOptions(new List<Pawn> { frame }, (frame.Position + IntVec3.North * 3).ToVector3Shifted(), out var _);
                    Check(!disabledOptions.Any(option => option.isGoto || option.Priority == MenuOptionPriority.AttackEnemy), "disabled chassis receives no move or attack right-click orders");
                    Check(FloatMenuUtility.GetMeleeAttackAction(frame, frame.Position + IntVec3.North, out var _) == null, "disabled chassis receives no forced melee order");
                    var boardOptions = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    Check(!boardOptions.Any(option => option.Label.Contains("Board Frame")), "pilot right-click offers no boarding action for disabled chassis");

                    FramePawn sameFrame = frame;
                    Check(frame.Recovery.TryRecover(), "development recovery restores disabled chassis");
                    Check(ReferenceEquals(frame, sameFrame) && frame.GetUniqueLoadID() == frameId, "recovery preserves exact Frame object and load identity");
                    Check(!frame.Downed && !frame.Dead && !frame.Destroyed, "recovered Frame is mobile and alive");
                    Check(!frame.Cockpit.HasPilot && !frame.Drafted && !frame.CanTakeOrder, "recovered Frame remains unpiloted, undrafted and inactive");
                    Check(frame.health.hediffSet.GetMissingPartsCommonAncestors().Any(), "development recovery stops before full-healing unrelated remaining chassis loss");
                    Check(frame.equipment.Primary?.def.defName == "ZF_DebugRifle", "recovery reclaims the existing debug weapon dropped at disablement");

                    var recoverBoardOptions = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    var board = recoverBoardOptions.FirstOrDefault(option => option.Label.Contains("Board Frame") && !option.Disabled);
                    Check(board != null, "recovered Frame re-enters normal pilot right-click boarding flow");
                    board.Chosen(colonistOrdering: true, null);
                    Next(10);
                });
            }
            else if (step == 10 && (pilot.CurJobDef == DefDatabase<JobDef>.GetNamed("ZF_BoardFrame") || frame.Cockpit.HasPilot))
            {
                Check(true, "recovered Frame boarding uses and completes the existing boarding job");
                Next(1);
            }
            else if (step == 1 && frame.Cockpit.HasPilot)
            {
                Check(ReferenceEquals(frame.Cockpit.Pilot, pilot), "same pilot boards same recovered Frame");
                Check(frame.SetDrafted(true) && frame.CanTakeOrder, "recovered and reboarded Frame drafts through Pawn-first controls");
                destination = frame.Position + new IntVec3(0, 0, 4);
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { frame }, destination.ToVector3Shifted(), out var _);
                    var move = options.FirstOrDefault(option => option.isGoto && !option.Disabled);
                    Check(move != null, "recovered Frame receives ordinary movement action");
                    move.Chosen(colonistOrdering: true, null);
                    Next(11, 1200);
                });
            }
            else if (step == 11 && frame.CurJobDef == JobDefOf.Goto)
            {
                destination = frame.CurJob.targetA.Cell;
                Check(true, "recovered Frame receives ordinary Goto job");
                Next(2, 1200);
            }
            else if (step == 2 && frame.Position == destination)
            {
                Check(true, "recovered Frame physically moves");
                target = GenSpawn.Spawn(PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile), frame.Position + new IntVec3(8, 0, 0), frame.Map);
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { frame }, target.Position.ToVector3Shifted(), out var _);
                    var attack = FloatMenuMakerMap.GetAutoTakeOption(options);
                    Check(attack != null && attack.autoTakeablePriority == 40f,
                        "recovered Frame ordinary hostile action selects ranged attack; actual options: " +
                        string.Join(" | ", options.Select(option => $"{option.Label} disabled={option.Disabled} priority={option.Priority} auto={option.autoTakeable} autoPriority={option.autoTakeablePriority}")));
                    attack.Chosen(colonistOrdering: true, null);
                    Next(3, 1200);
                });
            }
            else if (step == 3 && TargetDamaged())
            {
                Check(true, "recovered Frame ranged attack causes real damage");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                target = GenSpawn.Spawn(PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfAncientsHostile), frame.Position + new IntVec3(5, 0, 0), frame.Map);
                OnGui(() =>
                {
                    Action melee = FloatMenuUtility.GetMeleeAttackAction(frame, target, out var fail);
                    Check(melee != null, "recovered Frame retains ordinary forced melee action; failure=" + fail);
                    melee();
                    Next(4, 1200);
                });
            }
            else if (step == 4 && frame.CurJobDef == JobDefOf.AttackMelee)
            {
                Check(true, "recovered Frame forced melee uses AttackMelee and closes distance");
                Next(5, 1200);
            }
            else if (step == 5 && TargetDamaged())
            {
                Check(true, "recovered Frame melee attack causes real damage");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                Check(frame.Cockpit.TryDisembark(), "pilot disembarks after recovered-control checks");
                Check(!frame.Cockpit.HasPilot && pilot.Spawned, "Frame is empty before ordinary-damage disablement");
                Check(DownByOrdinaryDamage(frame), "ordinary applied chassis damage can create recoverable downing");
                Check(frame.Recovery.IsRecoverablyDisabled && !frame.CanTakeOrder, "empty damage-downed Frame is recoverable and inactive");
                Check(frame.Recovery.TryRecover(), "second disable/recover cycle succeeds");
                Check(frame.GetUniqueLoadID() == frameId && !frame.Downed && !frame.Cockpit.HasPilot, "second recovery preserves identity without creating a pilot");

                DownByMissingLegs(frame);
                Check(frame.Recovery.IsRecoverablyDisabled, "recovered Frame can be disabled again without stale recovery state");
                frame.Kill(null);
                Check(frame.Dead && !frame.Recovery.CanRecover && !frame.Recovery.TryRecover(), "dead disabled chassis cannot be recovered");

                FramePawn destroyed = SpawnFrame(pilot.Position + IntVec3.East);
                DownByMissingLegs(destroyed);
                Check(destroyed.Recovery.IsRecoverablyDisabled, "empty destruction fixture reaches disabled state");
                destroyed.Destroy();
                Check(destroyed.Destroyed && !destroyed.Recovery.CanRecover && !destroyed.Recovery.TryRecover(), "destroyed disabled chassis cannot be recovered");
                Unique(pilot, "recovery lifecycle complete");
                Finish();
            }
        }

        private static void DownByMissingLegs(FramePawn targetFrame)
        {
            foreach (BodyPartRecord leg in targetFrame.RaceProps.body.AllParts.Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)))
                if (!targetFrame.health.hediffSet.PartIsMissing(leg))
                    targetFrame.health.AddHediff(HediffDefOf.MissingBodyPart, leg);
        }

        private static bool DownByOrdinaryDamage(FramePawn targetFrame)
        {
            foreach (BodyPartRecord leg in targetFrame.RaceProps.body.AllParts.Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)))
            {
                for (int hit = 0; hit < 20 && !targetFrame.Downed && !targetFrame.Dead && !targetFrame.health.hediffSet.PartIsMissing(leg); hit++)
                    targetFrame.TakeDamage(new DamageInfo(DamageDefOf.Blunt, 4f, 999f, hitPart: leg));
            }
            return targetFrame.Downed && !targetFrame.Dead;
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


