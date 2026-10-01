using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

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
        private int hackStartedTick;
        private int inertUntil;
        private IntVec3 inertFrameStart;
        private float inertTargetInjurySeverity;
        private string inertCase;
        private string hostileFrameId;
        private string hostilePilotId;
        private FramePawn hostileFrame;
        private Pawn hostilePilot;
        private IntVec3 hostileFrameStart;
        private ThingWithComps hostileRifle;
        private bool hostileProbePassed;
        private string raidFrameId;
        private string raidPilotId;
        private string raidInfantryId;
        private FramePawn raidFrame;
        private Pawn raidPilot;
        private Pawn raidInfantry;
        private FramePawn worldFrame;
        private Pawn worldPilot;
        private string worldFrameId;
        private string worldPilotId;
        private IntVec3 raidFrameNaturalStart;
        private bool raidFrameMovedNaturally;
        private float savedPilotShootingStat;
        private float savedPilotMeleeStat;
        private float savedPilotShootingXp;
        private float savedPilotMeleeXp;
        private float combatXpBefore;
        private int savedAbilityId;
        private int savedAbilityCooldown;
        private int savedAbilityEffectTicks;
        private float savedAbilityBaseMoveSpeed;
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
            Scribe_Values.Look(ref hostileFrameId, "testHostileFrameId");
            Scribe_Values.Look(ref hostilePilotId, "testHostilePilotId");
            Scribe_References.Look(ref hostileFrame, "testHostileFrame");
            Scribe_References.Look(ref hostilePilot, "testHostilePilot");
            Scribe_Values.Look(ref raidFrameId, "testRaidFrameId");
            Scribe_Values.Look(ref raidPilotId, "testRaidPilotId");
            Scribe_Values.Look(ref raidInfantryId, "testRaidInfantryId");
            Scribe_References.Look(ref raidFrame, "testRaidFrame");
            Scribe_References.Look(ref raidPilot, "testRaidPilot");
            Scribe_References.Look(ref raidInfantry, "testRaidInfantry");
            Scribe_References.Look(ref worldFrame, "testWorldFrame");
            Scribe_References.Look(ref worldPilot, "testWorldPilot");
            Scribe_Values.Look(ref worldFrameId, "testWorldFrameId");
            Scribe_Values.Look(ref worldPilotId, "testWorldPilotId");
            Scribe_Values.Look(ref raidFrameNaturalStart, "testRaidFrameNaturalStart");
            Scribe_Values.Look(ref raidFrameMovedNaturally, "testRaidFrameMovedNaturally");
            Scribe_Values.Look(ref savedPilotShootingStat, "testPilotShootingStat");
            Scribe_Values.Look(ref savedPilotMeleeStat, "testPilotMeleeStat");
            Scribe_Values.Look(ref savedPilotShootingXp, "testPilotShootingXp");
            Scribe_Values.Look(ref savedPilotMeleeXp, "testPilotMeleeXp");
            Scribe_Values.Look(ref savedAbilityId, "testAbilityId");
            Scribe_Values.Look(ref savedAbilityCooldown, "testAbilityCooldown");
            Scribe_Values.Look(ref savedAbilityEffectTicks, "testAbilityEffectTicks");
            Scribe_Values.Look(ref savedAbilityBaseMoveSpeed, "testAbilityBaseMoveSpeed");
        }
        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add("PASS " + name);
        }
        private void Unique(Pawn p, string stage)
        {
            var owners = DirectOwnerPaths(p);
            Check(owners.Count == 1, stage + ": exactly one direct owner; found=" + string.Join(" | ", owners));
            Check(!p.Destroyed && !p.Dead, stage + ": live and valid under " + owners.Single());
        }

        private static List<string> DirectOwnerPaths(Pawn pawn)
        {
            var owners = new List<string>();
            foreach (Map map in Find.Maps)
            {
                if (pawn.Spawned && pawn.Map == map) owners.Add("map:" + map.uniqueID);
                foreach (FramePawn candidate in map.mapPawns.AllPawnsSpawned.OfType<FramePawn>())
                    if (ReferenceEquals(candidate.Cockpit.Pilot, pawn)) owners.Add("map-frame-cockpit:" + candidate.GetUniqueLoadID());
            }
            foreach (FramePawn candidate in Find.WorldPawns.AllPawnsAliveOrDead.OfType<FramePawn>())
                if (ReferenceEquals(candidate.Cockpit.Pilot, pawn)) owners.Add("world-frame-cockpit:" + candidate.GetUniqueLoadID());
            if (Find.WorldPawns.Contains(pawn)) owners.Add("world-pawns");
            if (Current.Game.GetComponent<PilotRecovery>().GetDirectlyHeldThings().Contains(pawn)) owners.Add("pilot-recovery");
            return owners;
        }
        public override void GameComponentUpdate()
        {
            if (finished || pendingGuiAction != null || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline)
                    throw new TimeoutException("Phase " + phase + " step " + step + DiagnosticState());
                if (phase == 0) BeforeSave();
                else if (phase == 1) AfterLoad();
                else if (phase == 2) RecoveryLoad();
                else if (phase == 3) DisabledFrameLoad();
                else if (phase == 4) HackingLoad();
                else if (phase == 5) HackedFrameLoad();
                else if (phase == 6) FactionlessHackingLoad();
                else if (phase == 7) HostilePilotLoad();
                else if (phase == 8) EnemyRaidStart();
                else if (phase == 9) EnemyRaidActiveLoad();
                else if (phase == 10) EnemyRaidRetreatLoad();
                else if (phase == 11) WorldPawnLoadAndPlayerExit();
                else if (phase == 12) PlayerWorldFrameLoad();
                else if (phase == 13) WorldRecoveryAndMapRemovalLoad();
                else if (phase == 14) PilotSkillBridgeStart();
                else if (phase == 15) PilotSkillBridgeLoadAndCombat();
                else if (phase == 16) FrameAbilityStart();
                else if (phase == 17) FrameAbilityActiveLoad();
                else if (phase == 18) FrameAbilityEmptyLoadAndLifecycle();
                else FrameAbilityCapturedLoad();
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
                pilot = GenerateHuman(Faction.OfPlayer);
                other = GenerateHuman(Faction.OfPlayer);
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
                Check(ReferenceEquals(FrameColonistBarBridge.SelectionThing(pilot), frame),
                    "embarked player pilot colonist-bar portrait resolves selection to its containing spawned Frame");
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
                Check(frameCommands.Any(command => command.hotKey == KeyBindingDefOf.Misc6 && command.defaultLabel == "CommandFireAtWillLabel".Translate()),
                    "drafted player Frame exposes vanilla Fire At Will state through Misc6 and TexCommand.FireAtWill UI");
                Check(frameCommands.Any(command => command.hotKey == KeyBindingDefOf.Misc2), "vanilla melee attack gizmo is available for a drafted Frame");
                Command_VerbTarget rangedCommand = frameCommands.OfType<Command_VerbTarget>().SingleOrDefault(command => command.verb == ManualVerb(frame));
                Check(rangedCommand != null, "drafted Frame exposes the equipped rifle through vanilla Command_VerbTarget");
                Check(rangedCommand.hotKey == KeyBindingDefOf.Misc1, "vanilla ranged weapon command uses Misc1 / B");
                Check(rangedCommand.verb is Verb_FrameShot && rangedCommand.verb.Available(), "normal ranged command uses the Frame weapon verb and is available while piloted and drafted");
                Check(!frameCommands.Any(command => command.defaultLabel == "Move" || command.defaultLabel == "Fire rifle"), "temporary Move/Fire chess-piece gizmos are absent");
                Check(frame.GetType().GetMethod(nameof(FramePawn.DrawGUIOverlay)).DeclaringType == typeof(FramePawn),
                    "every Frame uses its narrow LabelShort map-overlay override");
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
                IntVec3 firstRangedTargetCell = frame.Position + new IntVec3(8, 0, 0);
                ClearTestArea(CellRect.FromLimits(frame.Position, firstRangedTargetCell).ExpandedBy(2), frame.Map);
                target = SpawnFrame(firstRangedTargetCell, Faction.OfAncientsHostile);
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
                Check(frame.drafter.FireAtWill && frame.CurJob.playerForced && frame.CurJob.verbToUse is Verb_FrameShot &&
                    !frame.Cockpit.CanUseHostileCombatAI,
                    "manual sustained attack enables firing only for its explicit player-forced target");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                frame.drafter.FireAtWill = false;
                IntVec3 automaticTargetCell = frame.Position + IntVec3.East * 8;
                ClearTestArea(CellRect.FromLimits(frame.Position, automaticTargetCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), automaticTargetCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                Next(30, 600);
            }
            else if (step == 30 && Find.TickManager.TicksGame >= deadline - 100)
            {
                Check(!TargetDamaged(), "Fire At Will off prevents natural ranged target acquisition");
                Check(FloatMenuUtility.GetRangedAttackAction(frame, target, out var _) != null,
                    "Fire At Will off preserves explicit right-click/B ranged orders");
                frame.drafter.FireAtWill = true;
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                Next(32, 2400);
            }
            else if (step == 32 && TargetDamaged())
            {
                Check(frame.CurJobDef == JobDefOf.Wait_Combat && frame.drafter.FireAtWill,
                    "Fire At Will on naturally acquires and damages a hostile through vanilla Wait_Combat");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                if (other != null && !other.Destroyed) other.Destroy();
                IntVec3 otherCell = frame.Position + IntVec3.West * 2;
                ClearTestArea(CellRect.CenteredOn(otherCell, 1), frame.Map);
                other = GenerateHuman(Faction.OfPlayer);
                GenSpawn.Spawn(other, otherCell, frame.Map);
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
                IntVec3 targetCell = frame.Position + new IntVec3(8, 0, 8);
                ClearTestArea(CellRect.FromLimits(frame.Position, targetCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), targetCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                OnGui(() =>
                {
                    var groupSelection = new List<Pawn> { frame, squadMateFrame };
                    var groupAttackOptions = FloatMenuMakerMap.GetOptions(groupSelection, target.DrawPos, out var _);
                    var groupAttack = FloatMenuMakerMap.GetAutoTakeOption(groupAttackOptions);
                    Check(groupAttack != null && groupAttack.Priority == MenuOptionPriority.AttackEnemy, "ordinary multiselect hostile attack is directly auto-takeable");
                    groupAttack.Chosen(colonistOrdering: true, null);
                    Next(41, 2400);
                });
            }
            else if (step == 41 && frame.CurJobDef == JobDefOf.AttackStatic && squadMateFrame.CurJobDef == JobDefOf.AttackStatic)
            {
                Check(true, "one group attack command reaches both Frames");
                Next(5, 2400);
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
                Check(!ManualVerb(frame).Available(), "unpiloted Frame cannot fire through the normal ranged command");
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
                Check(!HealthAIUtility.CanRescueNow(pilot, frame, forced: true), "downed player Frame is semantically excluded from forced rescue");
                Check(!new WorkGiver_RescueDowned().HasJobOnThing(pilot, frame, forced: true), "automatic RescueDowned work rejects a disabled Frame");
                OnGui(() =>
                {
                    var playerFrameOptions = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    Check(!playerFrameOptions.Any(option => option.Label.Contains("Rescue".Translate())), "downed player Frame exposes no Rescue Pawn option");

                    FramePawn hostileDisabled = SpawnFrame(pilot.Position + IntVec3.East * 3, Faction.OfAncientsHostile);
                    DownByMissingLegs(hostileDisabled);
                    Check(hostileDisabled.Downed && hostileDisabled.Faction.HostileTo(Faction.OfPlayer), "hostile rescue fixture is disabled and remains hostile");
                    Check(!HealthAIUtility.CanRescueNow(pilot, hostileDisabled, forced: true), "hostile disabled Frame is excluded from forced rescue/capture eligibility");
                    var hostileOptions = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, hostileDisabled.DrawPos, out var _);
                    Check(!hostileOptions.Any(option => option.Label.Contains("Capture".Translate()) || option.Label.Contains("Rescue".Translate())), "hostile disabled Frame exposes neither Capture Pawn nor Rescue Pawn");
                    hostileDisabled.Destroy();
                    Next(51);
                });
            }
            else if (step == 51)
            {
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
        private string DiagnosticState()
        {
            string frameState = frame == null ? " frame=null" :
                " frameJob=" + (frame.CurJobDef?.defName ?? "null") +
                " jobVerb=" + (frame.CurJob?.verbToUse?.GetType().Name ?? "null") +
                " playerForced=" + (frame.CurJob?.playerForced.ToString() ?? "null") +
                " framePos=" + frame.Position + " drafted=" + frame.Drafted +
                " fireAtWill=" + (frame.drafter?.FireAtWill.ToString() ?? "null") +
                " canOperate=" + frame.Cockpit.CanOperate + " canOrder=" + frame.Cockpit.CanTakePlayerOrders +
                " stance=" + (frame.stances?.curStance?.GetType().Name ?? "null") +
                " verbs=" + (frame.equipment?.PrimaryEq == null ? "none" : string.Join(",", frame.equipment.PrimaryEq.AllVerbs.Select(verb =>
                    verb.GetType().Name + ":available=" + verb.Available() + ":manual=" + verb.verbProps.onlyManualCast)));
            string targetState = target == null ? " target=null" :
                " target=" + target.LabelShort + " targetSpawned=" + target.Spawned + " targetPos=" + target.Position +
                " targetDestroyed=" + target.Destroyed + (target is Pawn targetPawn
                    ? " targetDead=" + targetPawn.Dead + " targetDowned=" + targetPawn.Downed +
                      " targetInjuries=" + targetPawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Count()
                    : "");
            return frameState + targetState;
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
                IntVec3 recoveredRangedCell = frame.Position + new IntVec3(8, 0, 0);
                ClearTestArea(CellRect.FromLimits(frame.Position, recoveredRangedCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), recoveredRangedCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
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
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), frame.Position + new IntVec3(5, 0, 0), frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
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

                OnGui(() =>
                {
                    FramePawn operationalHostile = SpawnFrame(pilot.Position + IntVec3.East * 3, Faction.OfAncientsHostile);
                    Check(!operationalHostile.Hacking.CanHack(pilot, false), "operational hostile Frame is not hackable");
                    Check(!HasHackOption(pilot, operationalHostile), "Hack Frame is absent for an operational hostile chassis");
                    operationalHostile.Destroy();

                    FramePawn playerDisabled = SpawnFrame(pilot.Position + IntVec3.North * 3);
                    DownByMissingLegs(playerDisabled);
                    Check(!playerDisabled.Hacking.CanHack(pilot, false), "player-owned disabled Frame is not hackable");
                    Check(!HasHackOption(pilot, playerDisabled), "Hack Frame is absent for a player-owned disabled chassis");
                    playerDisabled.Destroy();

                    FramePawn occupied = SpawnFrame(pilot.Position + IntVec3.West);
                    Check(occupied.Cockpit.TryBoard(pilot), "occupied hacking-negative fixture accepts a pilot");
                    Check(!occupied.Hacking.CanHack(other, false), "Frame containing a pilot is not hackable");
                    Check(!HasHackOption(other, occupied), "Hack Frame is absent for a chassis containing a pilot");
                    Check(occupied.Cockpit.TryDisembark(), "occupied hacking-negative fixture returns its pilot");
                    occupied.Destroy();

                    FramePawn deadHostile = SpawnFrame(pilot.Position + IntVec3.South * 3, Faction.OfAncientsHostile);
                    DownByMissingLegs(deadHostile);
                    deadHostile.Kill(null);
                    Check(!deadHostile.Hacking.CanHack(pilot, false), "dead hostile Frame is not hackable");

                    FramePawn destroyedHostile = SpawnFrame(pilot.Position + IntVec3.South * 4, Faction.OfAncientsHostile);
                    DownByMissingLegs(destroyedHostile);
                    destroyedHostile.Destroy();
                    Check(!destroyedHostile.Hacking.CanHack(pilot, false), "destroyed hostile Frame is not hackable");

                    frame = SpawnFrame(pilot.Position + IntVec3.East * 5, Faction.OfAncientsHostile);
                    DownByMissingLegs(frame);
                    frameId = frame.GetUniqueLoadID();
                    Check(frame.Hacking.IsHackableDisabledChassis && frame.Hacking.CanHack(pilot, true), "hostile empty downed Frame is hackable by an eligible colonist");
                    Check(HasHackOption(pilot, frame), "Hack Frame appears only for the eligible hostile disabled chassis");
                    UniqueFrame(frame, "hostile disabled before hacking save");
                    ClearCombatNoise();
                    SaveNext(4);
                });
            }
        }

        private void HackingLoad()
        {
            if (step == 0)
            {
                ClearCombatNoise();
                Check(frame != null && frame.GetUniqueLoadID() == frameId, "pre-hack fresh-process load restores the same hostile Frame identity");
                Check(frame.Hacking.IsHackableDisabledChassis && frame.Downed && !frame.Cockpit.HasPilot, "pre-hack load preserves hostile empty disabled state");
                UniqueFrame(frame, "pre-hack reload");
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    var hack = options.FirstOrDefault(option => option.Label == "Hack Frame" && !option.Disabled);
                    Check(hack != null, "eligible colonist receives explicit Hack Frame right-click action");
                    Check(!options.Any(option => option.Label.Contains("Capture".Translate()) || option.Label.Contains("Rescue".Translate())), "hostile disabled Frame still exposes no Capture or Rescue beside hacking");
                    hack.Chosen(colonistOrdering: true, null);
                    Check(pilot.CurJobDef == DefDatabase<JobDef>.GetNamed("ZF_HackFrame"), "Hack Frame option issues the real hacking job");
                    Check(frame.Faction.HostileTo(Faction.OfPlayer), "hacking does not change ownership when the job is merely issued");
                    hackStartedTick = Find.TickManager.TicksGame;
                    Next(1, 1800);
                });
            }
            else if (step == 1 && pilot.Position.AdjacentTo8WayOrInside(frame.Position))
            {
                Check(pilot.CurJobDef == DefDatabase<JobDef>.GetNamed("ZF_HackFrame"), "hacking job reserves and paths the colonist into touch range");
                Check(frame.Faction.HostileTo(Faction.OfPlayer), "Frame remains hostile when timed hacking work begins");
                Next(2, 1200);
            }
            else if (step == 2 && frame.Faction == Faction.OfPlayer)
            {
                Check(Find.TickManager.TicksGame - hackStartedTick >= CompFrameHacking.WorkTicks, "hacking completes only after the visible work delay");
                Check(frame.GetUniqueLoadID() == frameId && frame.Downed && !frame.Cockpit.HasPilot && !frame.Drafted, "hacking converts the same Frame and leaves it disabled empty and undrafted");
                Check(frame.Recovery.IsRecoverablyDisabled && !frame.CanTakeOrder, "hacked Frame remains in the existing recovery lifecycle and cannot operate");
                UniqueFrame(frame, "hacked before save");
                SaveNext(5);
            }
            else if (step == 2 && pilot.CurJobDef != ZenithJobDefOf.ZF_HackFrame)
            {
                throw new InvalidOperationException("hostile hacking job ended before conversion; job=" +
                    (pilot.CurJobDef?.defName ?? "null") + " mental=" + pilot.InMentalState +
                    " downed=" + pilot.Downed + " canHack=" + frame.Hacking.CanHack(pilot, false) +
                    " forbidden=" + frame.IsForbidden(pilot) + " spawned=" + frame.Spawned);
            }
        }

        private void HackedFrameLoad()
        {
            if (step == 0)
            {
                Check(frame != null && frame.GetUniqueLoadID() == frameId, "post-hack fresh-process load restores the same Frame identity");
                Check(frame.Faction == Faction.OfPlayer && frame.Downed && frame.Recovery.IsRecoverablyDisabled, "post-hack load preserves player ownership and disabled state");
                Check(!frame.Cockpit.HasPilot && !frame.Drafted && !frame.CanTakeOrder, "post-hack loaded Frame remains empty undrafted and inactive");
                UniqueFrame(frame, "post-hack reload");
                Check(frame.Recovery.TryRecover(), "hacked disabled Frame uses the existing recovery API");
                Check(frame.GetUniqueLoadID() == frameId && !frame.Downed && !frame.Cockpit.HasPilot, "recovery preserves hacked Frame identity and empty cockpit");
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    var board = options.FirstOrDefault(option => option.Label.Contains("Board Frame") && !option.Disabled);
                    Check(board != null, "recovered hacked Frame re-enters the existing boarding flow");
                    board.Chosen(colonistOrdering: true, null);
                    Next(1, 1800);
                });
            }
            else if (step == 1 && frame.Cockpit.HasPilot)
            {
                Check(ReferenceEquals(frame.Cockpit.Pilot, pilot) && frame.SetDrafted(true), "same pilot boards and drafts the hacked recovered Frame");
                Command_VerbTarget ranged = frame.GetGizmos().OfType<Command_VerbTarget>()
                    .SingleOrDefault(command => command.verb == ManualVerb(frame));
                Check(ranged != null && ranged.hotKey == KeyBindingDefOf.Misc1 && ranged.verb.Available(), "hacked recovered Frame restores the normal B ranged command");
                IntVec3 hackedRangedCell = frame.Position + IntVec3.East * 8;
                ClearTestArea(CellRect.FromLimits(frame.Position, hackedRangedCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), hackedRangedCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { frame }, target.DrawPos, out var _);
                    var attack = FloatMenuMakerMap.GetAutoTakeOption(options);
                    Check(attack != null && attack.autoTakeablePriority == 40f, "hacked recovered Frame restores ordinary right-click ranged attack");
                    attack.Chosen(colonistOrdering: true, null);
                    Next(2, 1200);
                });
            }
            else if (step == 2 && TargetDamaged())
            {
                Check(true, "hacked recovered Frame ranged attack causes real damage");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), frame.Position + IntVec3.East * 5, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                OnGui(() =>
                {
                    Action melee = FloatMenuUtility.GetMeleeAttackAction(frame, target, out var fail);
                    Check(melee != null, "hacked recovered Frame restores forced melee; failure=" + fail);
                    melee();
                    Next(3, 1200);
                });
            }
            else if (step == 3 && frame.CurJobDef == JobDefOf.AttackMelee)
            {
                Check(true, "hacked recovered Frame forced melee uses the ordinary AttackMelee job");
                Next(4, 1200);
            }
            else if (step == 4 && TargetDamaged())
            {
                Check(true, "hacked recovered Frame melee attack causes real damage");
                UniqueFrame(frame, "hacking lifecycle complete");
                Unique(pilot, "hacking lifecycle pilot complete");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                Check(frame.Cockpit.TryDisembark(), "pilot disembarks before empty-Frame inertness checks");
                frame.Destroy();
                StartInertCase(Faction.OfPlayer, Faction.OfAncientsHostile, "player-owned empty Frame", 5);
            }
            else if (step == 5 && Find.TickManager.TicksGame >= inertUntil)
            {
                FinishInertCase();
                StartInertCase(Faction.OfAncientsHostile, Faction.OfPlayer, "hostile-faction empty Frame", 6);
            }
            else if (step == 6 && Find.TickManager.TicksGame >= inertUntil)
            {
                FinishInertCase();
                StartInertCase(null, Faction.OfAncientsHostile, "factionless empty Frame", 7);
            }
            else if (step == 7 && Find.TickManager.TicksGame >= inertUntil)
            {
                FinishInertCase();
                OnGui(() =>
                {
                    FramePawn operationalFactionless = SpawnFactionlessFrame(pilot.Position + IntVec3.East * 3);
                    Check(!operationalFactionless.Hacking.CanHack(pilot, false), "operational factionless Frame is not hackable");
                    Check(!HasHackOption(pilot, operationalFactionless), "Hack Frame is absent for an operational factionless chassis");
                    operationalFactionless.Destroy();

                    Faction neutralFaction = Faction.OfAncients;
                    Check(neutralFaction != null && neutralFaction != Faction.OfPlayer && !neutralFaction.HostileTo(Faction.OfPlayer), "neutral faction fixture is friendly or neutral to the player");
                    FramePawn neutralDisabled = SpawnFrame(pilot.Position + IntVec3.North * 3, neutralFaction);
                    DownByMissingLegs(neutralDisabled);
                    Check(!neutralDisabled.Hacking.IsHackableDisabledChassis && !neutralDisabled.Hacking.CanHack(pilot, false), "neutral or friendly disabled Frame is not hackable");
                    Check(!HasHackOption(pilot, neutralDisabled), "Hack Frame is absent for a neutral or friendly disabled chassis");
                    neutralDisabled.Destroy();

                    frame = SpawnFactionlessFrame(pilot.Position + IntVec3.East * 5);
                    DownByMissingLegs(frame);
                    RelocatePawn(pilot, CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 1), frame.Map);
                    frameId = frame.GetUniqueLoadID();
                    Check(frame.Faction == null && frame.Hacking.IsHackableDisabledChassis && frame.Hacking.CanHack(pilot, true), "factionless empty downed Frame is hackable by an eligible colonist");
                    UniqueFrame(frame, "factionless disabled before hacking save");
                    SaveNext(6);
                });
            }
        }

        private void FactionlessHackingLoad()
        {
            if (step == 0)
            {
                Check(frame != null && frame.GetUniqueLoadID() == frameId, "pre-hack fresh-process load restores the same factionless Frame identity");
                Check(frame.Faction == null && frame.Hacking.IsHackableDisabledChassis && frame.Downed && !frame.Cockpit.HasPilot, "pre-hack load preserves factionless empty disabled state");
                UniqueFrame(frame, "factionless pre-hack reload");
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, frame.DrawPos, out var _);
                    var hack = options.FirstOrDefault(option => option.Label == "Hack Frame" && !option.Disabled);
                    Check(hack != null, "eligible colonist receives Hack Frame for a loaded factionless disabled chassis");
                    hack.Chosen(colonistOrdering: true, null);
                    Check(pilot.CurJobDef == ZenithJobDefOf.ZF_HackFrame, "factionless Hack Frame option issues the real hacking job");
                    Check(frame.Faction == null, "factionless ownership is unchanged when the hacking job is merely issued");
                    hackStartedTick = Find.TickManager.TicksGame;
                    Next(1, 1800);
                });
            }
            else if (step == 1 && pilot.Position.AdjacentTo8WayOrInside(frame.Position))
            {
                Check(pilot.CurJobDef == ZenithJobDefOf.ZF_HackFrame, "factionless hacking job reserves and paths into touch range");
                Check(frame.Faction == null, "Frame remains factionless when timed hacking work begins");
                Next(2, 1200);
            }
            else if (step == 2 && frame.Faction == Faction.OfPlayer)
            {
                Check(Find.TickManager.TicksGame - hackStartedTick >= CompFrameHacking.WorkTicks, "factionless hacking completes only after the visible work delay");
                Check(frame.GetUniqueLoadID() == frameId && frame.Downed && !frame.Cockpit.HasPilot && !frame.Drafted, "factionless hacking converts the same Frame and leaves it disabled empty and undrafted");
                Check(frame.Recovery.IsRecoverablyDisabled && !frame.CanTakeOrder, "hacked factionless Frame remains inactive in the recovery lifecycle");
                UniqueFrame(frame, "factionless hacking complete");
                Unique(pilot, "factionless hacking pilot complete");
                frame.Destroy();
                ClearCombatNoise();

                FramePawn mismatchFrame = SpawnFrame(pilot.Position + IntVec3.North * 3, Faction.OfAncientsHostile);
                Pawn mismatchPilot = GenerateHuman(Faction.OfPlayer);
                GenSpawn.Spawn(mismatchPilot, CellFinder.RandomClosewalkCellNear(mismatchFrame.Position, mismatchFrame.Map, 1), mismatchFrame.Map);
                Check(!mismatchFrame.Cockpit.CanInstallPilot(mismatchPilot) && !mismatchFrame.Cockpit.TryInstallPilot(mismatchPilot),
                    "hostile Frame rejects a mismatched player-faction pilot without consuming it");
                Check(mismatchPilot.Spawned && mismatchPilot.holdingOwner?.Owner is Map && !mismatchFrame.Cockpit.HasPilot,
                    "rejected mismatched pilot remains safely map-owned");
                mismatchPilot.Destroy();
                mismatchFrame.Destroy();

                hostileFrame = FrameDebugActions.SpawnHostilePilotedFrame(pilot.Position + IntVec3.East * 12, Find.CurrentMap);
                hostilePilot = hostileFrame.Cockpit.Pilot;
                hostileFrameId = hostileFrame.GetUniqueLoadID();
                hostilePilotId = hostilePilot.GetUniqueLoadID();
                Check(hostileFrame.Faction != null && hostileFrame.Faction.HostileTo(Faction.OfPlayer) && hostilePilot.Faction == hostileFrame.Faction,
                    "debug action creates one hostile Frame with a same-faction human pilot");
                Check(!hostilePilot.Spawned && hostilePilot.holdingOwner == hostileFrame.Cockpit.GetDirectlyHeldThings(),
                    "hostile pilot is stored directly in the Frame cockpit ThingOwner");
                Check(hostileFrame.Cockpit.CanOperate && hostileFrame.Cockpit.CanUseHostileCombatAI && !hostileFrame.Cockpit.CanTakePlayerOrders,
                    "hostile Frame can operate through combat AI without accepting player orders");
                Check(!hostileFrame.CanTakeOrder && !hostileFrame.SetDrafted(true),
                    "hostile Frame rejects ordinary player order and draft entry points");
                Check(!hostileFrame.Hacking.IsHackableDisabledChassis && !hostileFrame.Hacking.CanHack(pilot, false),
                    "operational occupied hostile Frame is not hackable");
                UniqueFrame(hostileFrame, "hostile piloted before save");
                Unique(hostilePilot, "hostile pilot before save");
                SaveNext(7);
            }
            else if (step == 2 && pilot.CurJobDef != ZenithJobDefOf.ZF_HackFrame)
            {
                throw new InvalidOperationException("factionless hacking job ended before conversion; job=" +
                    (pilot.CurJobDef?.defName ?? "null") + " mental=" + pilot.InMentalState +
                    " downed=" + pilot.Downed + " canHack=" + frame.Hacking.CanHack(pilot, false));
            }
        }

        private void HostilePilotLoad()
        {
            if (step == 0)
            {
                Check(hostileFrame != null && hostilePilot != null && hostileFrame.GetUniqueLoadID() == hostileFrameId &&
                    hostilePilot.GetUniqueLoadID() == hostilePilotId, "fresh-process load preserves hostile Frame and pilot identities");
                Check(ReferenceEquals(hostileFrame.Cockpit.Pilot, hostilePilot) && !hostilePilot.Spawned &&
                    hostilePilot.holdingOwner == hostileFrame.Cockpit.GetDirectlyHeldThings(),
                    "fresh-process load preserves exact hostile cockpit ownership");
                Check(hostileFrame.Cockpit.CanOperate && hostileFrame.Cockpit.CanUseHostileCombatAI &&
                    !hostileFrame.Cockpit.CanTakePlayerOrders && !hostileFrame.CanTakeOrder,
                    "loaded hostile Frame operates through AI and still rejects player orders");
                Check(hostileFrame.drafter.FireAtWill, "loaded hostile Frame enables vanilla autonomous target acquisition");
                Check(!hostileFrame.Hacking.CanHack(pilot, false), "loaded operational hostile Frame remains ineligible for hacking");
                UniqueFrame(hostileFrame, "hostile piloted after load");
                Unique(hostilePilot, "hostile pilot after load");

                ClearCombatNoise();
                if (other != null && other.Faction == Faction.OfPlayer) other.SetFaction(null);
                IntVec3 safePilotCell = hostileFrame.Position.x + 75 < hostileFrame.Map.Size.x
                    ? hostileFrame.Position + IntVec3.East * 70
                    : hostileFrame.Position + IntVec3.West * 70;
                ClearTestArea(CellRect.CenteredOn(safePilotCell, 2), hostileFrame.Map);
                RelocatePawn(pilot, safePilotCell, hostileFrame.Map);
                pilot.drafter.Drafted = true;
                IntVec3 rangedTargetCell = hostileFrame.Position + IntVec3.West * 36;
                ClearTestArea(CellRect.FromLimits(hostileFrame.Position, rangedTargetCell).ExpandedBy(3), hostileFrame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer), rangedTargetCell, hostileFrame.Map);
                Pawn playerTarget = (Pawn)target;
                playerTarget.jobs.StopAll();
                playerTarget.drafter.Drafted = true;
                Check(playerTarget.Spawned && playerTarget.RaceProps.Humanlike && playerTarget.HostileTo(hostileFrame) &&
                    hostileFrame.HostileTo(playerTarget), "hostile AI fixture provides a spawned mutually hostile humanlike target");
                Check(hostileFrame.equipment.PrimaryEq.AllVerbs.Any(verb => verb is Verb_HostileFrameShot && verb.Available()),
                    "hostile Frame weapon verb is available to vanilla combat AI");
                hostileFrameStart = hostileFrame.Position;
                Next(1, 2400);
            }
            else if (step == 1 && hostileFrame.Position != hostileFrameStart)
            {
                Check(true, "hostile piloted Frame uses ordinary pathing to close weapon range; observed job=" +
                    (hostileFrame.CurJobDef?.defName ?? "null"));
                Pawn playerTarget = (Pawn)target;
                IntVec3 firingCell = CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 10);
                ClearTestArea(CellRect.FromLimits(hostileFrame.Position, firingCell).ExpandedBy(2), hostileFrame.Map);
                RelocatePawn(playerTarget, firingCell, hostileFrame.Map);
                playerTarget.stances.stunner.StunFor(5000, hostileFrame);
                Next(2, 4800);
            }
            else if (step == 1 && !hostileProbePassed && Find.TickManager.TicksGame >= deadline - 2300)
            {
                Pawn playerTarget = target as Pawn;
                Thing selected = new HostileCombatProbe().FindTarget(hostileFrame);
                Check(selected != null,
                    "vanilla target search succeeds after map-cache registration; targetSpawned=" + (playerTarget?.Spawned ?? false) +
                    " targetDowned=" + (playerTarget?.Downed ?? false) + " targetDead=" + (playerTarget?.Dead ?? false) +
                    " distance=" + (playerTarget == null ? -1f : hostileFrame.Position.DistanceTo(playerTarget.Position)) +
                    " targetJob=" + (playerTarget?.CurJobDef?.defName ?? "null") +
                    " autoTargetable=" + (playerTarget != null && AttackTargetFinder.IsAutoTargetable(playerTarget)) +
                    " potentialThreat=" + (playerTarget != null && GenHostility.IsPotentialThreat(playerTarget)) +
                    " activeThreat=" + (playerTarget != null && GenHostility.IsActiveThreatTo(playerTarget, hostileFrame.Faction)) +
                    " canReach=" + (playerTarget != null && hostileFrame.CanReach(playerTarget, PathEndMode.Touch, Danger.Deadly)) +
                    " lineOfSight=" + (playerTarget != null && GenSight.LineOfSight(hostileFrame.Position, playerTarget.Position, hostileFrame.Map)) +
                    " attackVerb=" + (hostileFrame.TryGetAttackVerb(playerTarget, false, false)?.GetType().Name ?? "null") +
                    " current=" + (hostileFrame.CurJobDef?.defName ?? "null"));
                hostileProbePassed = true;
            }
            else if (step == 2 && TargetDamaged())
            {
                Check(true, "hostile piloted Frame acquires and damages the player pawn through vanilla ranged combat AI");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                Check(hostileFrame.equipment.TryDropEquipment(hostileFrame.equipment.Primary, out hostileRifle,
                    hostileFrame.Position, forbid: false), "hostile Frame can temporarily drop its rifle for the melee fixture");
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer),
                    CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 4), hostileFrame.Map);
                Pawn meleeTarget = (Pawn)target;
                meleeTarget.jobs.StopAll();
                meleeTarget.drafter.Drafted = true;
                Next(4, 1800);
            }
            else if (step == 4 && hostileFrame.CurJobDef == JobDefOf.AttackMelee)
            {
                Check(true, "hostile piloted Frame chooses vanilla AttackMelee when unarmed");
                Next(5, 1800);
            }
            else if (step == 5 && TargetDamaged())
            {
                Check(true, "hostile piloted Frame causes legitimate melee damage");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                if (hostileRifle.Spawned) hostileRifle.DeSpawn();
                hostileFrame.equipment.AddEquipment(hostileRifle);

                DownByMissingLegs(hostileFrame);
                Next(6, 300);
            }
            else if (step == 6 && hostileFrame.Downed && !hostileFrame.Cockpit.HasPilot)
            {
                Check(hostileFrame.GetUniqueLoadID() == hostileFrameId && hostilePilot.GetUniqueLoadID() == hostilePilotId,
                    "downing preserves the exact hostile Frame and pilot identities");
                Check(hostilePilot.Spawned && hostilePilot.holdingOwner?.Owner is Map &&
                    hostilePilot.Position.AdjacentTo8WayOrInside(hostileFrame.Position),
                    "downing ejects the exact hostile pilot safely beside the chassis");
                Check(!hostileFrame.Cockpit.CanOperate && hostileFrame.Recovery.IsRecoverablyDisabled &&
                    hostileFrame.Hacking.CanHack(pilot, false),
                    "ejected hostile chassis becomes inert recoverable and hackable; canOperate=" + hostileFrame.Cockpit.CanOperate +
                    " recoverable=" + hostileFrame.Recovery.IsRecoverablyDisabled + " canHack=" + hostileFrame.Hacking.CanHack(pilot, false) +
                    " playerPilotDead=" + pilot.Dead + " playerPilotDowned=" + pilot.Downed);
                Unique(hostilePilot, "hostile pilot after ejection");
                hostilePilot.jobs.StopAll();
                hostilePilot.stances.stunner.StunFor(5000, hostileFrame);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer),
                    CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 1), hostileFrame.Map);
                Pawn adjacentTarget = (Pawn)target;
                adjacentTarget.jobs.StopAll();
                adjacentTarget.stances.stunner.StunFor(5000, hostileFrame);
                inertFrameStart = hostileFrame.Position;
                inertTargetInjurySeverity = InjurySeverity(adjacentTarget);
                inertUntil = Find.TickManager.TicksGame + 360;
                Next(7, 900);
            }
            else if (step == 7 && Find.TickManager.TicksGame >= inertUntil)
            {
                Pawn adjacentTarget = (Pawn)target;
                Check(hostileFrame.Position == inertFrameStart && !hostileFrame.pather.Moving,
                    "ejected hostile Frame remains stationary for 360 ticks beside a player pawn");
                Check(Math.Abs(InjurySeverity(adjacentTarget) - inertTargetInjurySeverity) < 0.001f,
                    "ejected hostile Frame causes no retaliatory melee damage");
                Check(hostileFrame.CurJobDef == null || hostileFrame.CurJobDef == ZenithJobDefOf.ZF_FrameIdleJob,
                    "ejected hostile Frame runs only its inert idle job");
                adjacentTarget.Destroy();
                hostilePilot.DeSpawn();
                Find.WorldPawns.PassToWorld(hostilePilot);
                Check(Find.WorldPawns.Contains(hostilePilot) && !hostilePilot.Dead && !hostilePilot.Destroyed,
                    "ejected hostile pilot leaves the later player-combat fixture through normal world ownership");
                IntVec3 hackerCell = CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 2);
                RelocatePawn(pilot, hackerCell, hostileFrame.Map);
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, hostileFrame.DrawPos, out var _);
                    var hack = options.FirstOrDefault(option => option.Label == "Hack Frame" && !option.Disabled);
                    Check(hack != null, "ejected hostile chassis exposes the real Hack Frame action");
                    hack.Chosen(colonistOrdering: true, null);
                    hackStartedTick = Find.TickManager.TicksGame;
                    Next(8, 1800);
                });
            }
            else if (step == 8 && hostileFrame.Faction == Faction.OfPlayer)
            {
                Check(Find.TickManager.TicksGame - hackStartedTick >= CompFrameHacking.WorkTicks,
                    "captured hostile chassis completes the normal timed hacking job");
                Check(hostileFrame.GetUniqueLoadID() == hostileFrameId && hostileFrame.Downed && !hostileFrame.Cockpit.HasPilot,
                    "hacking preserves hostile chassis identity and leaves it disabled empty");
                Check(hostileFrame.Recovery.TryRecover(), "captured hostile chassis enters the normal recovery API");
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { pilot }, hostileFrame.DrawPos, out var _);
                    var board = options.FirstOrDefault(option => option.Label.Contains("Board Frame") && !option.Disabled);
                    Check(board != null, "recovered captured chassis exposes normal player boarding");
                    board.Chosen(colonistOrdering: true, null);
                    Next(9, 1800);
                });
            }
            else if (step == 9 && hostileFrame.Cockpit.HasPilot)
            {
                Check(ReferenceEquals(hostileFrame.Cockpit.Pilot, pilot) && hostileFrame.SetDrafted(true),
                    "original player pilot boards and drafts the captured hostile chassis");
                Check(hostileFrame.GetUniqueLoadID() == hostileFrameId && hostileFrame.Cockpit.CanTakePlayerOrders &&
                    !hostileFrame.Cockpit.CanUseHostileCombatAI && hostileFrame.CanTakeOrder,
                    "same chassis transitions from hostile AI to ordinary player control");
                Command_VerbTarget ranged = hostileFrame.GetGizmos().OfType<Command_VerbTarget>()
                    .SingleOrDefault(command => command.verb == ManualVerb(hostileFrame));
                Check(ranged != null && ranged.hotKey == KeyBindingDefOf.Misc1 && ranged.verb.Available(),
                    "captured reused Frame restores the normal B ranged command");
                IntVec3 playerRangedTargetCell = hostileFrame.Position + IntVec3.East * 8;
                ClearTestArea(CellRect.FromLimits(hostileFrame.Position, playerRangedTargetCell).ExpandedBy(2), hostileFrame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), playerRangedTargetCell, hostileFrame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, hostileFrame);
                OnGui(() =>
                {
                    var options = FloatMenuMakerMap.GetOptions(new List<Pawn> { hostileFrame }, target.DrawPos, out var _);
                    var attack = FloatMenuMakerMap.GetAutoTakeOption(options);
                    Check(attack != null && attack.autoTakeablePriority == 40f,
                        "captured reused Frame accepts ordinary player right-click ranged attack; options=" +
                        string.Join(" | ", options.Select(option => option.Label + " disabled=" + option.Disabled +
                            " auto=" + option.autoTakeablePriority)));
                    attack.Chosen(colonistOrdering: true, null);
                    Next(10, 1200);
                });
            }
            else if (step == 10 && TargetDamaged())
            {
                Check(true, "captured reused Frame causes real ranged damage under player control");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), hostileFrame.Position + IntVec3.East * 5, hostileFrame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, hostileFrame);
                OnGui(() =>
                {
                    Action melee = FloatMenuUtility.GetMeleeAttackAction(hostileFrame, target, out var fail);
                    Check(melee != null, "captured reused Frame accepts forced melee; failure=" + fail);
                    melee();
                    Next(11, 1200);
                });
            }
            else if (step == 11 && hostileFrame.CurJobDef == JobDefOf.AttackMelee)
            {
                Check(true, "captured reused Frame uses the ordinary AttackMelee job");
                Next(12, 12000);
            }
            else if (step == 12 && TargetDamaged())
            {
                Check(true, "captured reused Frame causes real melee damage under player control");
                UniqueFrame(hostileFrame, "hostile Frame lifecycle complete");
                Unique(pilot, "player pilot in captured Frame lifecycle complete");
                Unique(hostilePilot, "ejected hostile pilot lifecycle complete");
                SaveNext(8);
            }
        }

        private void EnemyRaidStart()
        {
            if (step != 0) return;

            Check(hostileFrame.Cockpit.TryDisembark() && ReferenceEquals(pilot, hostileFrame.Cockpit.Pilot) == false,
                "captured Frame releases the player pilot before the raid fixture");
            hostileFrame.Destroy();
            if (hostilePilot.Spawned) hostilePilot.Destroy();
            ClearCombatNoise();

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
            parms.forced = true;
            parms.silent = true;
            parms.sendLetter = false;
            Check(ZenithIncidentDefOf.ZF_DevelopmentEnemyFrameRaid.Worker.TryExecute(parms),
                "development IncidentWorker_RaidEnemy starts a real mixed raid");

            raidFrame = Find.CurrentMap.mapPawns.AllPawnsSpawned.OfType<FramePawn>()
                .Single(candidate => candidate.Faction != null && candidate.Faction.HostileTo(Faction.OfPlayer) && candidate.GetLord() != null);
            Lord raidLord = raidFrame.GetLord();
            raidPilot = raidFrame.Cockpit.Pilot;
            raidInfantry = raidLord.ownedPawns.Single(candidate => !(candidate is FramePawn));
            raidFrameId = raidFrame.GetUniqueLoadID();
            raidPilotId = raidPilot.GetUniqueLoadID();
            raidInfantryId = raidInfantry.GetUniqueLoadID();
            raidFrameNaturalStart = raidFrame.Position;
            raidFrameMovedNaturally = false;
            foreach (Hediff_Injury injury in pilot.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                pilot.health.RemoveHediff(injury);
            pilot.drafter.Drafted = true;

            Check(raidLord.LordJob is LordJob_AssaultColony && raidLord.ownedPawns.Count == 2 &&
                raidLord.ownedPawns.Contains(raidFrame) && raidLord.ownedPawns.Contains(raidInfantry) &&
                !raidLord.ownedPawns.Contains(raidPilot),
                "raid uses one vanilla assault Lord containing infantry and the embarked Frame only");
            Check(raidFrame.Cockpit.CanUseHostileCombatAI && ReferenceEquals(raidFrame.Cockpit.Pilot, raidPilot) &&
                !raidPilot.Spawned && raidPilot.holdingOwner == raidFrame.Cockpit.GetDirectlyHeldThings(),
                "raid arrives with one real same-faction pilot exclusively owned by the cockpit");
            UniqueFrame(raidFrame, "active mixed raid before save");
            Unique(raidPilot, "active mixed raid pilot before save");
            SaveNext(9);
        }

        private void EnemyRaidActiveLoad()
        {
            if (step == 0)
            {
                Lord raidLord = raidFrame?.GetLord();
                Check(raidFrame != null && raidPilot != null && raidInfantry != null &&
                    raidFrame.GetUniqueLoadID() == raidFrameId && raidPilot.GetUniqueLoadID() == raidPilotId &&
                    raidInfantry.GetUniqueLoadID() == raidInfantryId,
                    "active raid fresh-process load preserves all three pawn identities");
                Check(raidLord != null && raidLord.LordJob is LordJob_AssaultColony &&
                    raidInfantry.GetLord() == raidLord && raidLord.ownedPawns.Contains(raidFrame) &&
                    !raidLord.ownedPawns.Contains(raidPilot),
                    "active raid load preserves exact assault Lord membership without exposing the contained pilot");
                Check(ReferenceEquals(raidFrame.Cockpit.Pilot, raidPilot) && raidFrame.Cockpit.CanUseHostileCombatAI,
                    "active raid load restores the operational piloted Frame");
                UniqueFrame(raidFrame, "active mixed raid after load");
                Unique(raidPilot, "active mixed raid pilot after load");

                Next(1, 12000);
            }
            else if (step == 1)
            {
                if (raidFrame.Position != raidFrameNaturalStart)
                    raidFrameMovedNaturally = true;
                bool frameWeaponDamage = pilot.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                    .Any(injury => injury.sourceDef == raidFrame.equipment.Primary.def);
                if (!raidFrameMovedNaturally || !frameWeaponDamage)
                    return;
                Check(true, "Lord-owned hostile Frame naturally moves and damages a colony target under AssaultColony without a forced job or target-AI invocation");
                raidFrame.jobs.StopAll();
                raidFrame.stances.CancelBusyStanceHard();
                pilot.stances.stunner.StunFor(5000, raidFrame);
                Lord raidLord = raidFrame.GetLord();
                DownByMissingLegs(raidFrame);
                Check(raidFrame.Downed && !raidFrame.Cockpit.HasPilot && raidFrame.GetLord() == null,
                    "downing removes the empty disabled Frame from hostile Lord control");
                Check(ReferenceEquals(raidPilot.GetLord(), raidLord) && raidLord.ownedPawns.Contains(raidPilot) &&
                    raidPilot.Spawned && raidPilot.Position.AdjacentTo8WayOrInside(raidFrame.Position),
                    "downing ejects the exact pilot and transfers it coherently into the same raid Lord");
                Check(!raidFrame.Cockpit.CanOperate && !raidFrame.pather.Moving,
                    "empty disabled raid Frame is inert and cannot retreat itself");
                raidPilot.stances.stunner.StunFor(5000, raidFrame);
                Check(raidFrame.Hacking.TryHack(pilot) && raidFrame.Faction == Faction.OfPlayer && raidFrame.GetLord() == null,
                    "hacking converts the exact raid Frame and leaves no stale hostile Lord control");
                Check(raidFrame.GetUniqueLoadID() == raidFrameId && raidFrame.Recovery.TryRecover(),
                    "hacked raid chassis preserves identity through normal recovery");
                RelocatePawn(pilot, CellFinder.RandomClosewalkCellNear(raidFrame.Position, raidFrame.Map, 1), raidFrame.Map);
                Check(raidFrame.Cockpit.TryBoard(pilot) && ReferenceEquals(raidFrame.Cockpit.Pilot, pilot) && raidFrame.SetDrafted(true),
                    "player reuses the same recovered raid Frame through normal boarding and drafting");
                UniqueFrame(raidFrame, "captured raid Frame reuse");
                Unique(pilot, "player pilot inside captured raid Frame");
                Check(raidFrame.Cockpit.TryDisembark(), "captured raid Frame releases the player pilot for retreat fixture cleanup");
                raidFrame.Destroy();
                if (raidPilot.Spawned) raidPilot.Destroy();
                if (raidInfantry.Spawned) raidInfantry.Destroy();
                ClearCombatNoise();

                IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
                parms.forced = true;
                parms.silent = true;
                parms.sendLetter = false;
                Check(ZenithIncidentDefOf.ZF_DevelopmentEnemyFrameRaid.Worker.TryExecute(parms),
                    "second real mixed raid starts for retreat lifecycle");
                raidFrame = Find.CurrentMap.mapPawns.AllPawnsSpawned.OfType<FramePawn>()
                    .Single(candidate => candidate.Faction != null && candidate.Faction.HostileTo(Faction.OfPlayer) && candidate.GetLord() != null);
                raidPilot = raidFrame.Cockpit.Pilot;
                raidInfantry = raidFrame.GetLord().ownedPawns.Single(candidate => !(candidate is FramePawn));
                raidFrameId = raidFrame.GetUniqueLoadID();
                raidPilotId = raidPilot.GetUniqueLoadID();
                raidInfantryId = raidInfantry.GetUniqueLoadID();
                IntVec3 mapCenter = new IntVec3(raidFrame.Map.Size.x / 2, 0, raidFrame.Map.Size.z / 2);
                IntVec3 frameRetreatStart = CellFinder.RandomClosewalkCellNear(mapCenter, raidFrame.Map, 8);
                IntVec3 infantryRetreatStart = CellFinder.RandomClosewalkCellNear(frameRetreatStart, raidFrame.Map, 4);
                RelocatePawn(raidFrame, frameRetreatStart, Find.CurrentMap);
                RelocatePawn(raidInfantry, infantryRetreatStart, Find.CurrentMap);
                raidFrame.jobs.StopAll();
                Lord retreatLord = LordMaker.MakeNewLord(raidFrame.Faction,
                    new LordJob_ExitMapBest(LocomotionUrgency.Sprint, canDig: true, canDefendSelf: true),
                    Find.CurrentMap, new Pawn[] { raidFrame, raidInfantry });
                raidFrame.Cockpit.RememberOperationalLord(retreatLord);
                Check(raidFrame.Cockpit.OperationalLord == retreatLord && retreatLord.LordJob is LordJob_ExitMapBest,
                    "operational Frame receives vanilla exit-Lord control when retreat begins");
                Next(2, 1200);
            }
            else if (step == 2 && raidFrame.mindState.duty?.def == DutyDefOf.ExitMapBest && raidFrame.pather.Moving)
            {
                Check(raidFrame.Cockpit.OperationalLord?.LordJob is LordJob_ExitMapBest,
                    "vanilla retreat Lord drives the operational Frame through its exit-map job");
                SaveNext(10);
            }
        }

        private void EnemyRaidRetreatLoad()
        {
            if (step == 0)
            {
                Check(raidFrame != null && raidPilot != null && raidFrame.GetUniqueLoadID() == raidFrameId &&
                    raidPilot.GetUniqueLoadID() == raidPilotId && ReferenceEquals(raidFrame.Cockpit.Pilot, raidPilot),
                    "retreat fresh-process load preserves exact Frame and contained pilot ownership");
                UniqueFrame(raidFrame, "retreating Frame after load");
                Unique(raidPilot, "retreating contained pilot after load");
                if (!raidFrame.Spawned)
                {
                    FinishRetreatAssertions();
                    return;
                }
                Next(1, 6000);
            }
            else if (step == 1 && !raidFrame.Spawned)
            {
                FinishRetreatAssertions();
            }
            else if (step == 1 && raidFrame.Cockpit.OperationalLord?.LordJob is LordJob_ExitMapBest && raidFrame.Cockpit.CanUseHostileCombatAI)
            {
                Check(true, "retreat load restores the vanilla exit Lord and operational hostile Frame");
                Next(2, 6000);
            }
            else if (step == 2 && !raidFrame.Spawned)
            {
                FinishRetreatAssertions();
            }
        }

        private void FinishRetreatAssertions()
        {
            Check(Find.WorldPawns.AllPawnsAliveOrDead.Contains(raidFrame),
                "operational raid Frame exits the map through vanilla world-pawn handling");
            Check(ReferenceEquals(raidFrame.Cockpit.Pilot, raidPilot) && !raidPilot.Spawned &&
                raidPilot.holdingOwner == raidFrame.Cockpit.GetDirectlyHeldThings(),
                "exited Frame retains exclusive ownership of the exact living pilot");
            Check(raidFrame.GetUniqueLoadID() == raidFrameId && raidPilot.GetUniqueLoadID() == raidPilotId,
                "retreat exit preserves Frame and pilot identities without duplication");
            UniqueFrame(raidFrame, "retreated Frame world ownership");
            Unique(raidPilot, "retreated pilot cockpit ownership");
            SaveNext(11);
        }

        private void WorldPawnLoadAndPlayerExit()
        {
            if (step != 0) return;
            Check(raidFrame != null && raidPilot != null && Find.WorldPawns.Contains(raidFrame) &&
                ReferenceEquals(raidFrame.Cockpit.Pilot, raidPilot),
                "hostile escaped Frame and nested pilot survive a fresh-process world-pawn load");
            UniqueFrame(raidFrame, "hostile escaped Frame before deliberate GC");
            Unique(raidPilot, "hostile escaped pilot before deliberate GC");
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(raidFrame);
            Check(raidFrame.Discarded && raidPilot.Discarded && !raidFrame.Cockpit.HasPilot,
                "vanilla hostile world-pawn GC intentionally discards both escaped Frame and nested pilot without dangling cockpit state; frameDiscarded=" +
                raidFrame.Discarded + " pilotDiscarded=" + raidPilot.Discarded + " pilotDestroyed=" + raidPilot.Destroyed +
                " cockpit=" + raidFrame.Cockpit.HasPilot + " pilotOwner=" + (raidPilot.holdingOwner?.Owner?.GetType().Name ?? "null"));

            Map map = Find.CurrentMap;
            Check(CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map), map, 0f, out IntVec3 edge),
                "player world-frame fixture finds a valid map edge cell");
            worldFrame = SpawnFrame(edge);
            worldPilot = GenerateHuman(Faction.OfPlayer);
            GenSpawn.Spawn(worldPilot, CellFinder.RandomClosewalkCellNear(edge, map, 1), map);
            Check(worldFrame.Cockpit.TryBoard(worldPilot), "player world-frame fixture boards through the ordinary cockpit path");
            worldFrameId = worldFrame.GetUniqueLoadID();
            worldPilotId = worldPilot.GetUniqueLoadID();
            worldFrame.ExitMap(allowedToJoinOrCreateCaravan: false, Rot4.Invalid);
            Check(Find.WorldPawns.Contains(worldFrame) && ReferenceEquals(worldFrame.Cockpit.Pilot, worldPilot) &&
                Find.WorldPawns.ForcefullyKeptPawns.Contains(worldFrame),
                "player Frame ExitMap keeps the complete cockpit world pawn explicitly; world=" + Find.WorldPawns.Contains(worldFrame) +
                " cockpit=" + ReferenceEquals(worldFrame.Cockpit.Pilot, worldPilot) + " kept=" + Find.WorldPawns.ForcefullyKeptPawns.Contains(worldFrame) +
                " frameDestroyed=" + worldFrame.Destroyed + " pilotOwner=" + (worldPilot.holdingOwner?.Owner?.GetType().Name ?? "null"));
            UniqueFrame(worldFrame, "player Frame immediately after ExitMap");
            Unique(worldPilot, "player pilot immediately after Frame ExitMap");
            SaveNext(12);
        }

        private void PlayerWorldFrameLoad()
        {
            if (step != 0) return;
            Check(worldFrame != null && worldPilot != null && worldFrame.GetUniqueLoadID() == worldFrameId &&
                worldPilot.GetUniqueLoadID() == worldPilotId && Find.WorldPawns.Contains(worldFrame) &&
                ReferenceEquals(worldFrame.Cockpit.Pilot, worldPilot) && Find.WorldPawns.ForcefullyKeptPawns.Contains(worldFrame),
                "fresh-process load preserves the force-kept player world Frame and exact nested pilot");
            UniqueFrame(worldFrame, "player world Frame after load");
            Unique(worldPilot, "player world cockpit pilot after load");
            Find.WorldPawns.RemovePawn(worldFrame);
            worldFrame.Destroy();
            Check(worldFrame.Destroyed && !worldFrame.Cockpit.HasPilot &&
                worldPilot.holdingOwner == Current.Game.GetComponent<PilotRecovery>().GetDirectlyHeldThings(),
                "legitimate off-map player Frame destruction atomically transfers the exact pilot to PilotRecovery");
            Unique(worldPilot, "player pilot in recovery after world Frame destruction");
            SaveNext(13);
        }

        private void WorldRecoveryAndMapRemovalLoad()
        {
            if (step != 0) return;
            Check(worldPilot != null && worldPilot.GetUniqueLoadID() == worldPilotId && !worldPilot.Dead && !worldPilot.Destroyed,
                "PilotRecovery ownership survives a fresh-process save/load after world Frame destruction");
            Unique(worldPilot, "recovered player pilot after fresh load");

            Map home = Find.CurrentMap;
            MapParent abandonedParent;
            Map abandonedMap = CreateTemporaryMap(out abandonedParent);
            FramePawn abandonedFrame = (FramePawn)PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer);
            Pawn abandonedPilot = GenerateHuman(Faction.OfPlayer);
            Check(abandonedFrame.Cockpit.TryInstallGeneratedPilot(abandonedPilot),
                "temporary-map player fixture installs its exact generated pilot");
            GenSpawn.Spawn(abandonedFrame, abandonedMap.Center, abandonedMap);
            Check(ReferenceEquals(abandonedFrame.Cockpit.Pilot, abandonedPilot),
                "temporary-map player Frame contains its exact pilot before abandonment");
            string abandonedPilotId = abandonedPilot.GetUniqueLoadID();
            Current.Game.CurrentMap = home;
            Current.Game.DeinitAndRemoveMap(abandonedMap, notifyPlayer: false);
            abandonedParent.Destroy();
            Check(!Find.Maps.Contains(abandonedMap) && Find.WorldPawns.Contains(abandonedFrame) && !abandonedFrame.Cockpit.HasPilot,
                "map abandonment/removal passes the empty player chassis to world ownership after safe pilot detachment");
            Check(abandonedPilot.GetUniqueLoadID() == abandonedPilotId && !abandonedPilot.Destroyed && !abandonedPilot.Dead,
                "map abandonment cannot orphan or delete the contained player pilot");
            UniqueFrame(abandonedFrame, "player chassis after temporary-map removal");
            Unique(abandonedPilot, "player pilot after temporary-map removal");

            MapParent hostileParent;
            Map hostileMap = CreateTemporaryMap(out hostileParent);
            FramePawn cleanupHostile = (FramePawn)PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfAncientsHostile);
            Pawn cleanupHostilePilot = GenerateHuman(Faction.OfAncientsHostile);
            Check(cleanupHostile.Cockpit.TryInstallGeneratedPilot(cleanupHostilePilot),
                "temporary hostile fixture installs its exact generated pilot");
            GenSpawn.Spawn(cleanupHostile, hostileMap.Center, hostileMap);
            Check(ReferenceEquals(cleanupHostile.Cockpit.Pilot, cleanupHostilePilot),
                "temporary hostile Frame contains its exact pilot before cleanup");
            Current.Game.CurrentMap = home;
            Current.Game.DeinitAndRemoveMap(hostileMap, notifyPlayer: false);
            hostileParent.Destroy();
            Check(Find.WorldPawns.Contains(cleanupHostile) && Find.WorldPawns.Contains(cleanupHostilePilot) && !cleanupHostile.Cockpit.HasPilot,
                "temporary-map cleanup gives hostile Frame and detached pilot explicit independent world ownership");
            UniqueFrame(cleanupHostile, "hostile Frame after temporary-map cleanup");
            Unique(cleanupHostilePilot, "hostile pilot after temporary-map cleanup");
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(cleanupHostile);
            Find.WorldPawns.RemoveAndDiscardPawnViaGC(cleanupHostilePilot);

            MapParent ejectParent;
            Map ejectMap = CreateTemporaryMap(out ejectParent);
            FramePawn ejectFrame = (FramePawn)PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer);
            Pawn ejectPilot = GenerateHuman(Faction.OfPlayer);
            Check(ejectFrame.Cockpit.TryInstallGeneratedPilot(ejectPilot),
                "pre-cleanup ejection fixture installs its exact generated pilot");
            GenSpawn.Spawn(ejectFrame, ejectMap.Center, ejectMap);
            Check(ReferenceEquals(ejectFrame.Cockpit.Pilot, ejectPilot),
                "pre-cleanup ejection fixture starts with its exact pilot");
            DownByMissingLegs(ejectFrame);
            Check(ejectPilot.Spawned && !ejectFrame.Cockpit.HasPilot, "downing immediately before cleanup ejects into valid map ownership first");
            Current.Game.CurrentMap = home;
            Current.Game.DeinitAndRemoveMap(ejectMap, notifyPlayer: false);
            ejectParent.Destroy();
            Check(Find.WorldPawns.Contains(ejectPilot) && Find.WorldPawns.Contains(ejectFrame) && !ejectFrame.Cockpit.HasPilot,
                "ejection immediately before map shutdown leaves separate valid world owners and no ghost pilot");
            Unique(ejectPilot, "ejected player pilot after map shutdown");
            UniqueFrame(ejectFrame, "disabled empty chassis after map shutdown");
            Check(!abandonedFrame.Destroyed && !abandonedFrame.Discarded && Find.WorldPawns.Contains(abandonedFrame) &&
                abandonedFrame.GetLord() == null,
                "player-owned recovered or hacked chassis is retained and detached from hostile raid state during map cleanup");
            SaveNext(14);
        }

        private void PilotSkillBridgeStart()
        {
            if (step != 0) return;

            Map map = Find.CurrentMap;
            IntVec3 center = map.Center;
            ClearTestArea(CellRect.CenteredOn(center, 16), map);
            frame = SpawnFrame(center);
            frameId = frame.GetUniqueLoadID();
            ThingWithComps exactWeapon = frame.equipment.Primary;
            pilot = GenerateCombatCapableHuman(Faction.OfPlayer);
            other = GenerateCombatCapableHuman(Faction.OfPlayer);
            GenSpawn.Spawn(pilot, center + IntVec3.East, map);
            GenSpawn.Spawn(other, center + IntVec3.West * 3, map);
            pilotId = pilot.GetUniqueLoadID();

            SetCombatSkills(pilot, 0, 0);
            SetCombatSkills(other, 20, 20);
            float emptyShooting = frame.GetStatValue(StatDefOf.ShootingAccuracyPawn);
            float emptyMelee = frame.GetStatValue(StatDefOf.MeleeHitChance);
            Check(frame.skills == null, "Frame owns no mirrored SkillTracker or duplicate pilot SkillRecords");
            Check(emptyShooting > 0f && emptyMelee > 0f,
                "empty Frame combat stat queries remain safe and use ordinary chassis defaults");

            Check(frame.Cockpit.TryBoard(pilot), "low-skill pilot boards the skill-test Frame");
            float shooting0 = AssertPilotStat(frame, pilot, StatDefOf.ShootingAccuracyPawn, SkillDefOf.Shooting, "Shooting 0");
            float melee0 = AssertPilotStat(frame, pilot, StatDefOf.MeleeHitChance, SkillDefOf.Melee, "Melee 0");

            SetCombatSkills(pilot, 10, 10);
            float shooting10 = AssertPilotStat(frame, pilot, StatDefOf.ShootingAccuracyPawn, SkillDefOf.Shooting, "Shooting 10");
            float melee10 = AssertPilotStat(frame, pilot, StatDefOf.MeleeHitChance, SkillDefOf.Melee, "Melee 10");

            SetCombatSkills(pilot, 20, 20);
            float shooting20 = AssertPilotStat(frame, pilot, StatDefOf.ShootingAccuracyPawn, SkillDefOf.Shooting, "Shooting 20");
            float melee20 = AssertPilotStat(frame, pilot, StatDefOf.MeleeHitChance, SkillDefOf.Melee, "Melee 20");
            Check(shooting0 < shooting10 && shooting10 < shooting20,
                "vanilla Shooting skill contribution improves ranged accuracy monotonically at levels 0 10 and 20");
            Check(melee0 < melee10 && melee10 < melee20,
                "vanilla Melee skill contribution improves melee hit chance monotonically at levels 0 10 and 20");

            SetCombatSkills(pilot, 0, 0);
            Check(frame.Cockpit.TryDisembark(), "low pilot disembarks before the critical same-chassis swap");
            RelocatePawn(other, center + IntVec3.West, map);
            Check(frame.Cockpit.TryBoard(other), "high pilot boards the exact same Frame");
            float swappedShooting = frame.GetStatValue(StatDefOf.ShootingAccuracyPawn);
            float swappedMelee = frame.GetStatValue(StatDefOf.MeleeHitChance);
            Check(frame.GetUniqueLoadID() == frameId,
                "pilot swap preserves the exact Frame identity");
            Check(ReferenceEquals(frame.equipment.Primary, exactWeapon),
                "pilot swap preserves the exact equipped weapon instance");
            Check(Approximately(swappedShooting, shooting20) && Approximately(swappedMelee, melee20),
                "same Frame and weapon immediately use the replacement high pilot without copied chassis state; shooting=" +
                swappedShooting.ToString("0.#####") + "/" + shooting20.ToString("0.#####") + " melee=" +
                swappedMelee.ToString("0.#####") + "/" + melee20.ToString("0.#####"));

            Check(frame.Cockpit.TryDisembark(), "high pilot disembarks for low-pilot restoration proof");
            RelocatePawn(pilot, center + IntVec3.East, map);
            Check(frame.Cockpit.TryBoard(pilot), "original low pilot reboards the exact same Frame");
            Check(Approximately(frame.GetStatValue(StatDefOf.ShootingAccuracyPawn), shooting0) &&
                Approximately(frame.GetStatValue(StatDefOf.MeleeHitChance), melee0),
                "reboarding the original low pilot restores the original combat effectiveness");

            Check(frame.Cockpit.TryDisembark(), "low pilot disembarks for stale-contribution check");
            Check(Approximately(frame.GetStatValue(StatDefOf.ShootingAccuracyPawn), emptyShooting) &&
                Approximately(frame.GetStatValue(StatDefOf.MeleeHitChance), emptyMelee),
                "disembarking removes pilot contributions immediately with no cached value");

            SetCombatSkills(other, 15, 13);
            RelocatePawn(other, center + IntVec3.West, map);
            Check(frame.Cockpit.TryBoard(other) && frame.SetDrafted(true),
                "known-skill pilot boards and drafts before the fresh-process bridge save");
            savedPilotShootingStat = frame.GetStatValue(StatDefOf.ShootingAccuracyPawn);
            savedPilotMeleeStat = frame.GetStatValue(StatDefOf.MeleeHitChance);
            savedPilotShootingXp = other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;
            savedPilotMeleeXp = other.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel;
            Unique(other, "known-skill pilot before bridge save");
            SaveNext(15);
        }

        private void PilotSkillBridgeLoadAndCombat()
        {
            if (step == 0)
            {
                Check(frame != null && other != null && frame.GetUniqueLoadID() == frameId &&
                    ReferenceEquals(frame.Cockpit.Pilot, other),
                    "fresh-process load preserves exact Frame and known-skill cockpit pilot identities");
                Check(other.skills.GetSkill(SkillDefOf.Shooting).Level == 15 &&
                    other.skills.GetSkill(SkillDefOf.Melee).Level == 13 &&
                    Approximately(other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel, savedPilotShootingXp) &&
                    Approximately(other.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel, savedPilotMeleeXp),
                    "fresh-process load preserves pilot combat levels and XP on the pilot");
                Check(Approximately(frame.GetStatValue(StatDefOf.ShootingAccuracyPawn), savedPilotShootingStat) &&
                    Approximately(frame.GetStatValue(StatDefOf.MeleeHitChance), savedPilotMeleeStat),
                    "fresh-process load restores identical live Shooting and Melee bridge values");
                Check(frame.skills == null && frame.Cockpit.CanTakePlayerOrders,
                    "loaded Frame still has no SkillTracker and derives operation from the contained pilot");
                Unique(other, "known-skill pilot after bridge load");

                frame.SetDrafted(true);
                frame.drafter.FireAtWill = false;
                IntVec3 targetCell = frame.Position + IntVec3.East * 8;
                ClearTestArea(CellRect.FromLimits(frame.Position, targetCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), targetCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                combatXpBefore = other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;
                Action ranged = FloatMenuUtility.GetRangedAttackAction(frame, target, out string fail);
                Check(ranged != null, "loaded Frame receives a real manual ranged attack for pilot XP; failure=" + fail);
                ranged();
                Next(1, 1800);
            }
            else if (step == 1 && other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel > combatXpBefore && TargetDamaged())
            {
                Check(TargetDamaged(), "real post-load manual projectile combat still damages its target");
                Check(frame.skills == null, "manual Frame fire teaches only the actual pilot and never the chassis");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();

                IntVec3 targetCell = frame.Position + IntVec3.West * 8;
                ClearTestArea(CellRect.FromLimits(frame.Position, targetCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile), targetCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                combatXpBefore = other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;
                frame.drafter.FireAtWill = true;
                Next(2, 2400);
            }
            else if (step == 2 && other.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel > combatXpBefore && TargetDamaged())
            {
                Check(frame.CurJobDef == JobDefOf.Wait_Combat && TargetDamaged(),
                    "Fire At Will performs real combat and grants Shooting XP to the same contained pilot");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();

                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfAncientsHostile),
                    CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 4), frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, frame);
                combatXpBefore = other.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel;
                Action melee = FloatMenuUtility.GetMeleeAttackAction(frame, target, out string fail);
                Check(melee != null, "loaded Frame receives a real forced melee attack for pilot XP; failure=" + fail);
                melee();
                Next(3, 2400);
            }
            else if (step == 3 && other.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel > combatXpBefore && TargetDamaged())
            {
                Check(TargetDamaged(), "real forced melee damages the target while teaching the exact pilot");
                Check(frame.skills == null, "Frame melee teaches no duplicate chassis SkillRecord");
                frame.jobs.StopAll();
                frame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();

                hostileFrame = FrameDebugActions.SpawnHostilePilotedFrame(frame.Position + IntVec3.East * 12, frame.Map);
                hostilePilot = hostileFrame.Cockpit.Pilot;
                SetCombatSkills(hostilePilot, 17, 18);
                AssertPilotStat(hostileFrame, hostilePilot, StatDefOf.ShootingAccuracyPawn, SkillDefOf.Shooting, "hostile Shooting 17");
                AssertPilotStat(hostileFrame, hostilePilot, StatDefOf.MeleeHitChance, SkillDefOf.Melee, "hostile Melee 18");
                Check(hostileFrame.Cockpit.CanUseHostileCombatAI && !hostileFrame.CanTakeOrder,
                    "hostile skill bridge is faction-agnostic without exposing player orders");

                IntVec3 targetCell = hostileFrame.Position + IntVec3.West * 8;
                ClearTestArea(CellRect.FromLimits(hostileFrame.Position, targetCell).ExpandedBy(2), hostileFrame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer), targetCell, hostileFrame.Map);
                Pawn hostileRangedTarget = (Pawn)target;
                hostileRangedTarget.jobs.StopAll();
                hostileRangedTarget.drafter.Drafted = true;
                combatXpBefore = hostilePilot.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel;
                Next(4, 3000);
            }
            else if (step == 4 && hostilePilot.skills.GetSkill(SkillDefOf.Shooting).xpSinceLastLevel > combatXpBefore && TargetDamaged())
            {
                Check(TargetDamaged(), "hostile ranged AI grants Shooting XP to its exact contained hostile pilot");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                ThingWithComps equippedHostileRifle = hostileFrame.equipment?.Primary;
                Check(equippedHostileRifle != null,
                    "hostile skill fixture retains its generated ranged weapon before melee transition");
                Check(hostileFrame.equipment.TryDropEquipment(equippedHostileRifle, out hostileRifle,
                    hostileFrame.Position, forbid: false), "hostile skill fixture drops ranged weapon for real melee XP");
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer),
                    CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 4), hostileFrame.Map);
                Pawn hostileMeleeTarget = (Pawn)target;
                hostileMeleeTarget.jobs.StopAll();
                hostileMeleeTarget.drafter.Drafted = true;
                combatXpBefore = hostilePilot.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel;
                Next(5, 3000);
            }
            else if (step == 5 && hostilePilot.skills.GetSkill(SkillDefOf.Melee).xpSinceLastLevel > combatXpBefore)
            {
                Check(true, "hostile melee AI attempts real melee and grants Melee XP to its exact contained hostile pilot");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                float hostilePilotedShooting = hostileFrame.GetStatValue(StatDefOf.ShootingAccuracyPawn);
                DownByMissingLegs(hostileFrame);
                Check(!hostileFrame.Cockpit.HasPilot && hostilePilot.Spawned,
                    "downing ejects the hostile skill fixture pilot beside the chassis");
                Check(!Approximately(hostileFrame.GetStatValue(StatDefOf.ShootingAccuracyPawn), hostilePilotedShooting),
                    "downing removes the hostile pilot skill contribution immediately");
                Check(hostileFrame.CurJobDef == null || hostileFrame.CurJobDef == ZenithJobDefOf.ZF_FrameIdleJob,
                    "empty ejected hostile Frame remains inert after skill bridge removal");

                if (hostileRifle != null && !hostileRifle.Destroyed) hostileRifle.Destroy();
                hostilePilot.Destroy();
                hostileFrame.Destroy();

                DownByMissingLegs(frame);
                Check(frame.Downed && !frame.Cockpit.HasPilot && other.Spawned,
                    "player Frame downing ejects the known-skill pilot and clears the bridge");
                Check(frame.Recovery.TryRecover(), "recovering the chassis alone succeeds without restoring old pilot skill state");
                Check(!frame.Cockpit.HasPilot && !frame.CanTakeOrder,
                    "recovered empty chassis has no operator skill contribution or combat control");
                SetCombatSkills(pilot, 3, 4);
                RelocatePawn(pilot, CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 1), frame.Map);
                Check(frame.Cockpit.TryBoard(pilot), "different pilot boards the recovered same chassis");
                AssertPilotStat(frame, pilot, StatDefOf.ShootingAccuracyPawn, SkillDefOf.Shooting, "recovered Frame new Shooting 3");
                AssertPilotStat(frame, pilot, StatDefOf.MeleeHitChance, SkillDefOf.Melee, "recovered Frame new Melee 4");
                Check(frame.GetUniqueLoadID() == frameId && frame.skills == null,
                    "recovery and reboarding preserve chassis identity while only the new pilot owns skills");
                Unique(pilot, "new pilot in recovered skill-test Frame");
                SaveNext(16);
            }
        }

        private void FrameAbilityStart()
        {
            if (step == 0)
            {
                ClearCombatNoise();
                AbilityDef overdriveDef = OverdriveDef;
                Ability overdrive = Overdrive(frame);
                Check(frame.GetUniqueLoadID() == frameId && frame.abilities != null &&
                    frame.abilities.abilities.Count(ability => ability.def == overdriveDef) == 1,
                    "generated debug Frame owns exactly one intrinsic Frame Overdrive instance");
                Check(!OwnsOverdrive(pilot) && !HasOverdriveEffect(pilot),
                    "pilot owns neither Frame Overdrive nor its chassis effect before disembark");
                savedAbilityId = overdrive.Id;
                savedAbilityBaseMoveSpeed = frame.GetStatValue(StatDefOf.MoveSpeed);

                Check(frame.Cockpit.TryDisembark(), "ability fixture pilot disembarks to prove empty cast rejection");
                Check(!overdrive.CanCast.Accepted && !frame.CanTakeOrder,
                    "unpiloted Frame retains ability ownership but cannot cast or receive orders");
                RelocatePawn(pilot, CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 1), frame.Map);
                Check(frame.Cockpit.TryBoard(pilot), "pilot reboards the same ability-owning Frame");

                frame.SetDrafted(false);
                Command_Ability command = frame.GetGizmos().OfType<Command_Ability>()
                    .SingleOrDefault(candidate => candidate.Ability == overdrive);
                Check(command != null && command.GetType() == typeof(Command_Ability),
                    "Frame exposes one ordinary Command_Ability without a custom action surrogate");
                Check(command.Disabled, "Frame Overdrive command uses vanilla undrafted disabled state");
                Check(frame.SetDrafted(true) && !command.Disabled,
                    "drafting through the existing Frame controller enables the normal ability command");

                overdrive.QueueCastingJob(frame, LocalTargetInfo.Invalid);
                Check(frame.CurJob?.ability == overdrive,
                    "Frame Overdrive enters the normal vanilla ability casting job path; job=" +
                    (frame.CurJobDef?.defName ?? "null") + " abilityCasting=" +
                    (frame.CurJobDef?.abilityCasting.ToString() ?? "null"));
                Next(1, 900);
            }
            else if (step == 1 && HasOverdriveEffect(frame))
            {
                Ability overdrive = Overdrive(frame);
                HediffComp_Disappears effect = OverdriveEffect(frame).TryGetComp<HediffComp_Disappears>();
                float boostedMoveSpeed = frame.GetStatValue(StatDefOf.MoveSpeed);
                Check(boostedMoveSpeed > savedAbilityBaseMoveSpeed,
                    "Frame Overdrive deterministically increases chassis MoveSpeed; base=" +
                    savedAbilityBaseMoveSpeed.ToString("0.###") + " boosted=" + boostedMoveSpeed.ToString("0.###"));
                Check(!HasOverdriveEffect(pilot) && !OwnsOverdrive(pilot),
                    "casting applies no effect ability or cooldown state to the contained pilot");
                Check(overdrive.OnCooldown && overdrive.CooldownTicksRemaining > 0 &&
                    overdrive.CooldownTicksRemaining <= 900,
                    "successful vanilla cast starts the 900-tick machine cooldown");
                Check(!overdrive.CanCast.Accepted,
                    "immediate Frame Overdrive recast is rejected by vanilla cooldown state");
                savedAbilityCooldown = overdrive.CooldownTicksRemaining;
                savedAbilityEffectTicks = effect.ticksToDisappear;
                Check(savedAbilityEffectTicks > 0 && savedAbilityEffectTicks <= 300,
                    "successful cast creates the 300-tick machine-owned temporary effect");
                SaveNext(17);
            }
        }

        private void FrameAbilityActiveLoad()
        {
            if (step != 0) return;

            Ability overdrive = Overdrive(frame);
            HediffComp_Disappears effect = OverdriveEffect(frame)?.TryGetComp<HediffComp_Disappears>();
            Check(frame != null && pilot != null && frame.GetUniqueLoadID() == frameId &&
                ReferenceEquals(frame.Cockpit.Pilot, pilot),
                "active-effect fresh-process load preserves exact Frame and pilot ownership");
            Check(overdrive.Id == savedAbilityId &&
                frame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1,
                "fresh-process load preserves the same unique intrinsic ability instance");
            Check(overdrive.OnCooldown && overdrive.CooldownTicksRemaining > 0 &&
                overdrive.CooldownTicksRemaining <= savedAbilityCooldown,
                "fresh-process load preserves remaining vanilla machine cooldown");
            Check(effect != null && effect.ticksToDisappear > 0 &&
                effect.ticksToDisappear <= savedAbilityEffectTicks &&
                frame.GetStatValue(StatDefOf.MoveSpeed) > savedAbilityBaseMoveSpeed,
                "fresh-process load preserves the remaining chassis effect and boosted MoveSpeed");
            Check(!OwnsOverdrive(pilot) && !HasOverdriveEffect(pilot),
                "loaded contained pilot still owns no machine ability or effect");

            Check(frame.Cockpit.TryDisembark(), "pilot disembarks while machine effect and cooldown remain active");
            Check(overdrive.OnCooldown && HasOverdriveEffect(frame) && !overdrive.CanCast.Accepted,
                "empty Frame retains machine cooldown and effect while operation becomes impossible");
            Check(!OwnsOverdrive(pilot) && !HasOverdriveEffect(pilot),
                "disembarking transfers no Frame ability effect or cooldown to the pilot");
            SaveNext(18);
        }

        private void FrameAbilityEmptyLoadAndLifecycle()
        {
            if (step == 0)
            {
                Ability overdrive = Overdrive(frame);
                Check(frame.GetUniqueLoadID() == frameId && !frame.Cockpit.HasPilot &&
                    overdrive.Id == savedAbilityId && overdrive.OnCooldown && !overdrive.CanCast.Accepted,
                    "empty-machine fresh-process load preserves intrinsic ability state without enabling cast");
                Check(frame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1,
                    "repeated fresh-process load creates no duplicate ability or cooldown instance");
                RelocatePawn(other, CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 1), frame.Map);
                Check(frame.Cockpit.TryBoard(other) && frame.SetDrafted(true),
                    "new pilot boards the same Frame while its machine cooldown remains");
                Check(!OwnsOverdrive(other) && Overdrive(frame).Id == savedAbilityId,
                    "pilot swap preserves chassis ability identity without copying it to the new pilot");
                Next(1, 1800);
            }
            else if (step == 1 && !Overdrive(frame).OnCooldown)
            {
                Ability overdrive = Overdrive(frame);
                Check(!HasOverdriveEffect(frame), "temporary chassis effect expires through normal Hediff duration");
                Check(overdrive.CanCast.Accepted,
                    "machine cooldown expires normally and permits the valid new pilot to cast again");
                overdrive.QueueCastingJob(frame, LocalTargetInfo.Invalid);
                Next(2, 900);
            }
            else if (step == 2 && HasOverdriveEffect(frame))
            {
                Ability overdrive = Overdrive(frame);
                Check(overdrive.OnCooldown && overdrive.Id == savedAbilityId,
                    "second normal cast reuses the same ability instance and starts a new cooldown");
                DownByMissingLegs(frame);
                Check(frame.Downed && !frame.Cockpit.HasPilot && other.Spawned &&
                    HasOverdriveEffect(frame) && !overdrive.CanCast.Accepted,
                    "downing ejects the pilot and blocks casting without moving the active effect off the chassis");
                Check(frame.Recovery.TryRecover() &&
                    frame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1 &&
                    Overdrive(frame).Id == savedAbilityId && Overdrive(frame).OnCooldown,
                    "recovery preserves intrinsic ability ownership identity and cooldown");

                hostileFrame = FrameDebugActions.SpawnHostilePilotedFrame(
                    frame.Position + IntVec3.East * 12, frame.Map);
                hostileFrameId = hostileFrame.GetUniqueLoadID();
                hostilePilot = hostileFrame.Cockpit.Pilot;
                Check(Overdrive(hostileFrame).CanCast.Accepted && !hostileFrame.CanTakeOrder,
                    "hostile operational Frame can use the faction-agnostic machine ability without player control");
                IntVec3 targetCell = hostileFrame.Position + IntVec3.West * 8;
                ClearTestArea(CellRect.FromLimits(hostileFrame.Position, targetCell).ExpandedBy(2), frame.Map);
                target = GenSpawn.Spawn(GenerateCombatTarget(Faction.OfPlayer), targetCell, frame.Map);
                ((Pawn)target).stances.stunner.StunFor(5000, hostileFrame);
                Next(3, 3000);
            }
            else if (step == 3 && HasOverdriveEffect(hostileFrame) && Overdrive(hostileFrame).OnCooldown)
            {
                Check(true, "hostile ThinkTree naturally casts Frame Overdrive through vanilla ability AI; follow-up job=" +
                    (hostileFrame.CurJobDef?.defName ?? "null"));
                Check(!OwnsOverdrive(hostilePilot) && !HasOverdriveEffect(hostilePilot),
                    "hostile AI cast keeps ability effect and cooldown exclusively on the machine");
                Next(4, 3000);
            }
            else if (step == 4 && TargetDamaged())
            {
                Check(TargetDamaged(), "hostile Frame continues ordinary combat after its autonomous ability cast");
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                if (target != null && !target.Destroyed) target.Destroy();
                DownByMissingLegs(hostileFrame);
                Check(hostileFrame.Downed && !hostileFrame.Cockpit.HasPilot && hostilePilot.Spawned &&
                    hostileFrame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1,
                    "hostile downing ejects pilot while the same disabled chassis retains Overdrive");
                pilot = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer),
                    CellFinder.RandomClosewalkCellNear(hostileFrame.Position, hostileFrame.Map, 1), hostileFrame.Map);
                Check(hostileFrame.Hacking.CanHack(pilot, false),
                    "fresh player colonist is eligible to acquire the disabled ability-owning chassis");
                Check(hostileFrame.Hacking.TryHack(pilot) && hostileFrame.GetUniqueLoadID() == hostileFrameId,
                    "hacking converts the same ability-owning hostile chassis");
                Check(hostileFrame.Recovery.TryRecover() &&
                    hostileFrame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1,
                    "recovery preserves exactly one intrinsic ability on the captured chassis");
                Check(hostileFrame.Cockpit.TryBoard(pilot) && !OwnsOverdrive(pilot),
                    "player pilot boards captured chassis without receiving its machine ability");
                SaveNext(19);
            }
        }

        private void FrameAbilityCapturedLoad()
        {
            if (step != 0) return;
            Check(hostileFrame != null && hostilePilot != null && pilot != null &&
                hostileFrame.GetUniqueLoadID() == hostileFrameId &&
                hostileFrame.Faction == Faction.OfPlayer && ReferenceEquals(hostileFrame.Cockpit.Pilot, pilot),
                "captured Frame fresh-process load preserves chassis identity ownership and new pilot");
            Check(hostileFrame.abilities.abilities.Count(ability => ability.def == OverdriveDef) == 1 &&
                Overdrive(hostileFrame).pawn == hostileFrame,
                "hacking recovery boarding and save/load preserve one machine-owned ability instance");
            Check(!OwnsOverdrive(pilot) && !OwnsOverdrive(hostilePilot) &&
                !HasOverdriveEffect(pilot) && !HasOverdriveEffect(hostilePilot),
                "neither original hostile pilot nor replacement player pilot acquires machine ability state");
            Finish();
        }

        private static AbilityDef OverdriveDef =>
            DefDatabase<AbilityDef>.GetNamed("ZF_DebugFrameOverdrive");

        private static Ability Overdrive(Pawn targetFrame) =>
            targetFrame.abilities?.abilities.Single(ability => ability.def == OverdriveDef);

        private static bool OwnsOverdrive(Pawn pawn) =>
            pawn?.abilities?.abilities.Any(ability => ability.def == OverdriveDef) == true;

        private static Hediff OverdriveEffect(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("ZF_FrameOverdrive"));

        private static bool HasOverdriveEffect(Pawn pawn) => OverdriveEffect(pawn) != null;

        private void StartInertCase(Faction frameFaction, Faction targetFaction, string label, int nextStep)
        {
            IntVec3 cell = CellFinder.RandomClosewalkCellNear(pilot.Position, Find.CurrentMap, 8);
            frame = frameFaction == null ? SpawnFactionlessFrame(cell) : SpawnFrame(cell, frameFaction);
            Pawn adjacentTarget = GenerateHuman(targetFaction);
            foreach (Hediff_Injury injury in adjacentTarget.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                adjacentTarget.health.RemoveHediff(injury);
            target = GenSpawn.Spawn(adjacentTarget, CellFinder.RandomClosewalkCellNear(frame.Position, frame.Map, 1), frame.Map);
            adjacentTarget.jobs.StopAll();
            adjacentTarget.stances.stunner.StunFor(600, frame);
            inertCase = label;
            inertFrameStart = frame.Position;
            inertTargetInjurySeverity = InjurySeverity(adjacentTarget);
            inertUntil = Find.TickManager.TicksGame + 360;
            Next(nextStep, 900);
        }

        private void FinishInertCase()
        {
            Pawn adjacentTarget = (Pawn)target;
            Check(frame.Position == inertFrameStart && !frame.pather.Moving, inertCase + " remains stationary for 360 ticks beside an adjacent adversary");
            float finalInjurySeverity = InjurySeverity(adjacentTarget);
            Check(Math.Abs(finalInjurySeverity - inertTargetInjurySeverity) < 0.001f,
                inertCase + " causes no melee damage during inert observation; start=" + inertTargetInjurySeverity +
                " end=" + finalInjurySeverity + " FrameJob=" + (frame.CurJobDef?.defName ?? "null") +
                " targetJob=" + (adjacentTarget.CurJobDef?.defName ?? "null"));
            Check(frame.CurJobDef == null || frame.CurJobDef == ZenithJobDefOf.ZF_FrameIdleJob, inertCase + " runs only the Frame-specific inert idle job");
            Check(!frame.Cockpit.HasPilot && !frame.CanTakeOrder, inertCase + " remains empty and ineligible for orders");
            frame.Destroy();
            adjacentTarget.Destroy();
            target = null;
        }

        private float AssertPilotStat(FramePawn targetFrame, Pawn expectedPilot, StatDef stat, SkillDef skill, string stage)
        {
            float actual = targetFrame.GetStatValue(stat);
            float expected = ExpectedPilotStat(targetFrame, expectedPilot, stat, skill);
            Check(Approximately(actual, expected), stage + " uses the exact vanilla SkillNeed contribution; expected=" +
                expected.ToString("0.#####") + " actual=" + actual.ToString("0.#####"));
            StatPart_PilotSkill part = stat.parts.OfType<StatPart_PilotSkill>().Single(candidate => candidate.skill == skill);
            string explanation = part.ExplanationPart(StatRequest.For(targetFrame));
            Check(explanation != null && explanation.Contains("Pilot") && explanation.Contains(expectedPilot.skills.GetSkill(skill).Level.ToString()),
                stage + " exposes the live pilot contribution through normal stat explanation");
            return actual;
        }

        private static float ExpectedPilotStat(FramePawn targetFrame, Pawn expectedPilot, StatDef stat, SkillDef skill)
        {
            StatRequest request = StatRequest.For(targetFrame);
            float value = stat.Worker.GetValueUnfinalized(request);
            SkillNeed need = stat.skillNeedOffsets.Single(candidate => candidate.skill == skill);
            value += need.ValueFor(expectedPilot) - stat.noSkillOffset;
            if (stat.parts != null)
                foreach (StatPart part in stat.parts)
                    if (part != null && !(part is StatPart_PilotSkill)) part.TransformValue(request, ref value);
            if (stat.postProcessCurve != null) value = stat.postProcessCurve.Evaluate(value);
            if (stat.postProcessStatFactors != null)
                foreach (StatDef factor in stat.postProcessStatFactors)
                    if (factor != null) value *= targetFrame.GetStatValue(factor);
            if (Find.Scenario != null) value *= Find.Scenario.GetStatFactor(stat);
            if (Mathf.Abs(value) > stat.roundToFiveOver) value = Mathf.Round(value / 5f) * 5f;
            if (stat.roundValue) value = Mathf.RoundToInt(value);
            return Mathf.Clamp(value, stat.minValue, stat.maxValue);
        }

        private static void SetCombatSkills(Pawn targetPilot, int shooting, int melee)
        {
            SetSkill(targetPilot.skills.GetSkill(SkillDefOf.Shooting), shooting);
            SetSkill(targetPilot.skills.GetSkill(SkillDefOf.Melee), melee);
        }

        private static void SetSkill(SkillRecord record, int level)
        {
            record.Level = level;
            record.xpSinceLastLevel = 0f;
            record.xpSinceMidnight = 0f;
            record.passion = Passion.None;
        }

        private static bool Approximately(float left, float right) => Math.Abs(left - right) < 0.0001f;

        private static float InjurySeverity(Pawn pawn) => pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(injury => injury.Severity);
        private static Verb ManualVerb(FramePawn targetFrame) =>
            targetFrame.equipment.PrimaryEq.AllVerbs.Single(verb => verb is Verb_FrameShot);

        private static void ClearTestArea(CellRect area, Map map)
        {
            foreach (IntVec3 cell in area.ClipInsideMap(map))
            {
                foreach (Thing thing in cell.GetThingList(map).ToList())
                    if (!(thing is Pawn) && !(thing is IThingHolder) &&
                        (thing.def.category == ThingCategory.Plant || thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item))
                        thing.Destroy();
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                map.roofGrid.SetRoof(cell, null);
                map.fogGrid.Unfog(cell);
            }
        }

        private static void RelocatePawn(Pawn pawn, IntVec3 cell, Map map)
        {
            if (pawn.Spawned) pawn.DeSpawn();
            GenSpawn.Spawn(pawn, cell, map);
        }

        private sealed class HostileCombatProbe : JobGiver_AIFightEnemies
        {
            public HostileCombatProbe()
            {
                targetAcquireRadius = 65f;
                targetKeepRadius = 72f;
                chaseTarget = true;
                humanlikesOnly = true;
            }

            public Thing FindTarget(Pawn pawn) => FindAttackTarget(pawn);
        }

        private void ClearCombatNoise()
        {
            foreach (Thing thing in Find.CurrentMap.listerThings.AllThings.ToList())
            {
                bool disposablePawn = thing is Pawn pawn && pawn != pilot && pawn != other && pawn != frame &&
                    pawn != hostileFrame && pawn != hostilePilot;
                if (disposablePawn || thing is Projectile || thing is Fire || thing is Explosion)
                    thing.Destroy();
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
        private FramePawn SpawnFrame(IntVec3 cell, Faction faction = null) => (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), faction ?? Faction.OfPlayer), cell, Find.CurrentMap);
        private static FramePawn SpawnFrameOnMap(IntVec3 cell, Map map, Faction faction) =>
            (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), faction), cell, map);
        private static Map CreateTemporaryMap(out MapParent parent)
        {
            PlanetTile tile = TileFinder.RandomSettlementTileFor(Faction.OfPlayer, mustBeAutoChoosable: false, extraValidator: null);
            parent = (MapParent)WorldObjectMaker.MakeWorldObject(WorldObjectDefOf.Settlement);
            parent.Tile = tile;
            parent.SetFaction(Faction.OfPlayer);
            Find.WorldObjects.Add(parent);
            return MapGenerator.GenerateMap(new IntVec3(50, 1, 50), parent, MapGeneratorDefOf.Base_Player);
        }
        private FramePawn SpawnFactionlessFrame(IntVec3 cell)
        {
            FramePawn factionless = SpawnFrame(cell);
            factionless.SetFaction(null);
            return factionless;
        }
        private static Pawn GenerateHuman(Faction faction)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, faction, canGeneratePawnRelations: false));
            // DLC generation may attach disposable fixtures to off-map relatives.
            // Random relationships are unrelated to this ownership suite and can
            // otherwise leave unresolved references after the fixture is destroyed.
            pawn.relations.ClearAllRelations();
            return pawn;
        }
        private static Pawn GenerateCombatCapableHuman(Faction faction)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, faction, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true));
            pawn.relations.ClearAllRelations();
            return pawn;
        }
        private static Pawn GenerateCombatTarget(Faction faction)
        {
            Pawn pawn = GenerateHuman(faction);
            pawn.equipment?.DestroyAllEquipment(DestroyMode.Vanish);
            pawn.apparel?.DestroyAll(DestroyMode.Vanish);
            foreach (Hediff_Injury injury in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                pawn.health.RemoveHediff(injury);
            return pawn;
        }
        private static bool HasHackOption(Pawn actor, FramePawn targetFrame) =>
            FloatMenuMakerMap.GetOptions(new List<Pawn> { actor }, targetFrame.DrawPos, out var _)
                .Any(option => option.Label == "Hack Frame" && !option.Disabled);
        private void UniqueFrame(FramePawn targetFrame, string stage)
        {
            var owners = new List<string>();
            foreach (Map map in Find.Maps)
                if (targetFrame.Spawned && targetFrame.Map == map) owners.Add("map:" + map.uniqueID);
            if (Find.WorldPawns.Contains(targetFrame)) owners.Add("world-pawns");
            Check(owners.Count == 1, stage + ": exactly one Frame owner; found=" + string.Join(" | ", owners));
        }
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


