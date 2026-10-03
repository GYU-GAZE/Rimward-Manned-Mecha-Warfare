using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using ZenithFrame;

namespace ZenithFrame.AbilityTests
{
    public sealed class CompProperties_RecordLocation : CompProperties_AbilityEffect
    {
        public CompProperties_RecordLocation() { compClass = typeof(CompAbilityEffect_RecordLocation); }
    }

    public sealed class CompAbilityEffect_RecordLocation : CompAbilityEffect
    {
        public static IntVec3 LastCell = IntVec3.Invalid;
        public static Pawn LastOwner;
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            LastCell = target.Cell;
            LastOwner = parent.pawn;
        }
    }

    public sealed class AbilitySuite : GameComponent
    {
        private int phase;
        private int step;
        private int deadline;
        private bool finished;
        private FramePawn frame;
        private Pawn pilot;
        private Pawn target;
        private Pawn secondPilot;
        private FramePawn hostileFrame;
        private Pawn hostilePilot;
        private Pawn hostileTarget;
        private string frameId;
        private string pilotId;
        private int selfAbilityId;
        private int targetAbilityId;
        private int targetCooldown;
        private float psyfocus;
        private float entropy;
        private float desiredPsyfocus;
        private string vefAbilityId;
        private int vefCooldown;
        private string vpeAbilityId;
        private int vpeCooldown;
        private float vpePsyfocus;
        private float vpeEntropy;
        private IntVec3 locationTarget;
        private Precept_Role ideologyRole;
        private int roleAbilityId;
        private int roleCooldown;
        private Pawn serviceWorker;
        private readonly List<string> passed = new List<string>();

        public AbilitySuite(Game game) { }
        private string ReportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "ability-phase-" + phase + ".txt");
        private static AbilityDef SelfDef => DefDatabase<AbilityDef>.GetNamed("ZF_TestPilotSelf");
        private static AbilityDef TargetDef => DefDatabase<AbilityDef>.GetNamed("ZF_TestPilotTarget");
        private static AbilityDef LocationDef => DefDatabase<AbilityDef>.GetNamed("ZF_TestPilotLocation");

        public override void ExposeData()
        {
            Scribe_Values.Look(ref phase, "abilityTestPhase");
            Scribe_References.Look(ref frame, "abilityTestFrame");
            Scribe_References.Look(ref pilot, "abilityTestPilot");
            Scribe_References.Look(ref target, "abilityTestTarget");
            Scribe_References.Look(ref secondPilot, "abilityTestSecondPilot");
            Scribe_References.Look(ref hostileFrame, "abilityTestHostileFrame");
            Scribe_References.Look(ref hostilePilot, "abilityTestHostilePilot");
            Scribe_References.Look(ref hostileTarget, "abilityTestHostileTarget");
            Scribe_Values.Look(ref frameId, "abilityTestFrameId");
            Scribe_Values.Look(ref pilotId, "abilityTestPilotId");
            Scribe_Values.Look(ref selfAbilityId, "abilityTestSelfId");
            Scribe_Values.Look(ref targetAbilityId, "abilityTestTargetId");
            Scribe_Values.Look(ref targetCooldown, "abilityTestCooldown");
            Scribe_Values.Look(ref psyfocus, "abilityTestPsyfocus");
            Scribe_Values.Look(ref entropy, "abilityTestEntropy");
            Scribe_Values.Look(ref desiredPsyfocus, "abilityTestDesiredPsyfocus");
            Scribe_Values.Look(ref vefAbilityId, "abilityTestVEFId");
            Scribe_Values.Look(ref vefCooldown, "abilityTestVEFCooldown");
            Scribe_Values.Look(ref vpeAbilityId, "abilityTestVPEId");
            Scribe_Values.Look(ref vpeCooldown, "abilityTestVPECooldown");
            Scribe_Values.Look(ref vpePsyfocus, "abilityTestVPEPsyfocus");
            Scribe_Values.Look(ref vpeEntropy, "abilityTestVPEEntropy");
            Scribe_References.Look(ref ideologyRole, "abilityTestIdeologyRole");
            Scribe_Values.Look(ref roleAbilityId, "abilityTestRoleAbilityId");
            Scribe_Values.Look(ref roleCooldown, "abilityTestRoleCooldown");
            Scribe_References.Look(ref serviceWorker, "abilityTestServiceWorker");
        }

        public override void GameComponentUpdate()
        {
            if (finished || Current.ProgramState != ProgramState.Playing || Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (step != 0 && Find.TickManager.TicksGame > deadline) throw new TimeoutException("ability phase " + phase + " step " + step);
                if (phase == 0) OwnershipTargetingAndResources();
                else if (phase == 1) ReloadSwapAndEjection();
                else if (phase == 2) HostileAbilityAI();
                else HostileReload();
            }
            catch (Exception e) { Fail(e); }
        }

        private void OwnershipTargetingAndResources()
        {
            if (Find.TickManager.TicksGame < 1000) return;
            if (step == 0)
            {
                Map map = Find.CurrentMap;
                IntVec3 center = map.Center;
                ClearArea(CellRect.CenteredOn(center, 20), map);
                if (frame != null && pilot != null && target != null && frame.Spawned && !frame.Destroyed)
                {
                    if (frame.Cockpit.HasPilot) Check(frame.Cockpit.TryDisembark(), "bootstrap Frame releases its saved pilot for a clean rerun");
                    Relocate(frame, center, map);
                    Relocate(pilot, center + IntVec3.East, map);
                    Relocate(target, center + IntVec3.East * 8, map);
                    pilot.abilities.RemoveAbility(SelfDef);
                    pilot.abilities.RemoveAbility(TargetDef);
                    pilot.abilities.RemoveAbility(LocationDef);
                    Hediff oldMarker = target.health.hediffSet.GetFirstHediffOfDef(DefDatabase<HediffDef>.GetNamed("ZF_TestPilotTargetEffect"));
                    if (oldMarker != null) target.health.RemoveHediff(oldMarker);
                }
                else
                {
                    frame = (FramePawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), center, map);
                    pilot = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), center + IntVec3.East, map);
                    target = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfAncientsHostile), center + IntVec3.East * 8, map);
                }
                target.jobs.StopAll();
                target.stances.stunner.StunFor(10000, frame);
                pilot.abilities.GainAbility(SelfDef);
                pilot.abilities.GainAbility(TargetDef);
                pilot.abilities.GainAbility(LocationDef);
                serviceWorker = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), frame.Position + IntVec3.West, map);
                serviceWorker.skills.GetSkill(SkillDefOf.Construction).Level = 0;
                if (ModsConfig.IdeologyActive)
                {
                    Ideo ideo = Faction.OfPlayer.ideos.PrimaryIdeo;
                    pilot.ideo.SetIdeo(ideo);
                    ideologyRole = ideo.RolesListForReading.FirstOrDefault(r => r.def.defName == "IdeoRole_Leader");
                    if (ideologyRole == null)
                    {
                        ideologyRole = (Precept_Role)PreceptMaker.MakePrecept(DefDatabase<PreceptDef>.GetNamed("IdeoRole_Leader"));
                        ideo.AddPrecept(ideologyRole);
                    }
                    ideologyRole.Assign(pilot, false);
                    ideologyRole.RecacheActivity();
                    ideologyRole.FillOrUpdateAbilities();
                    pilot.abilities.Notify_TemporaryAbilitiesChanged();
                }
                if (HasVEF) Check(VEF<bool>("Give", pilot), "pilot receives one real VEF ability in the original CompAbilities");
                if (HasVPE)
                {
                    PrepareRoyaltyPsycaster(pilot);
                    Check(VEF<bool>("GiveVPE", pilot), "pilot receives a real VPE psycast in the original CompAbilities");
                }
                frameId = frame.GetUniqueLoadID();
                pilotId = pilot.GetUniqueLoadID();
                Check(frame.Cockpit.TryBoard(pilot) && frame.SetDrafted(true), "player pilot boards and operates the test Frame");
                Ability self = pilot.abilities.GetAbility(SelfDef);
                Ability aimed = pilot.abilities.GetAbility(TargetDef);
                selfAbilityId = self.Id;
                targetAbilityId = aimed.Id;
                Check(!pilot.Spawned && ReferenceEquals(frame.Cockpit.Pilot, pilot), "pilot remains contained before every cast");
                Check(frame.abilities.GetAbility(SelfDef) == null && frame.abilities.GetAbility(TargetDef) == null, "Frame receives no copied pilot abilities");
                List<Command_Ability> commands = frame.GetGizmos().OfType<Command_Ability>().ToList();
                Check(commands.Count(c => c.Ability == self) == 1 && commands.Count(c => c.Ability == aimed) == 1,
                    "Frame forwards each original pilot Command_Ability exactly once");
                Check(commands.Any(c => c.Ability.def.defName == "ZF_DebugFrameOverdrive"), "Frame-owned Overdrive stays independently visible");
                frame.SetDrafted(false);
                Check(frame.Service.QueueRemove(FrameAddonCategory.Weapon),
                    "ability matrix fixture queues a real physical service request");
                Job serviceJob = new WorkGiver_ServiceFrameModule().JobOnThing(serviceWorker, frame, true);
                Check(serviceJob != null && serviceWorker.jobs.TryTakeOrderedJob(serviceJob),
                    "ability matrix fixture starts the real Construction service job");
                Next(8, 1200);
            }
            else if (step == 8 && frame.Service.IsServiceLocked)
            {
                Ability self = pilot.abilities.GetAbility(SelfDef);
                Ability aimed = pilot.abilities.GetAbility(TargetDef);
                List<Command_Ability> lockedCommands = frame.GetGizmos().OfType<Command_Ability>().ToList();
                Check(!frame.Cockpit.CanTakePlayerOrders && lockedCommands.All(command => command.Ability.pawn != pilot),
                    "active service lock hides every forwarded vanilla pilot ability and blocks Frame orders");
                Ability lockedOverdrive = frame.abilities.AllAbilitiesForReading.Single(ability => ability.def.defName == "ZF_DebugFrameOverdrive");
                Check(!lockedOverdrive.CanCast.Accepted, "active service lock blocks the machine-owned Frame ability without moving its ownership");
                if (HasVEF) Check(!VEF<bool>("HasExactCommand", frame, pilot),
                    "active service lock hides the exact forwarded VEF pilot ability");
                if (HasVPE) Check(!VEF<bool>("HasExactVPECommand", frame, pilot),
                    "active service lock hides the exact forwarded VPE psycast");
                serviceWorker.jobs.StopAll();
                frame.Service.EndService(serviceWorker);
                frame.Service.ClearPendingOrder();
                serviceWorker.Destroy();
                Check(!frame.Service.IsServiceLocked && frame.SetDrafted(true) && self.Id == selfAbilityId && aimed.Id == targetAbilityId,
                    "ending service restores operation with unchanged pilot ability instances");
                BeginInitialCast();
            }
            else if (step == 7 && pilot.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("CombatCommand")))
            {
                Ability roleAbility = pilot.abilities.AllAbilitiesForReading.Single(a => a.Id == roleAbilityId);
                roleCooldown = roleAbility.CooldownTicksRemaining;
                Check(roleAbility.OnCooldown && roleCooldown > 0 && !frame.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("CombatCommand")),
                    "Ideology role effect and cooldown remain on original pilot after Frame-hosted cast");
                Ability self = pilot.abilities.GetAbility(SelfDef);
                self.QueueCastingJob(pilot, LocalTargetInfo.Invalid);
                Check(frame.CurJob?.ability == self && ReferenceEquals(frame.CurJob.targetA.Thing, frame) && ReferenceEquals(frame.CurJob.targetC.Thing, pilot),
                    "self cast keeps pilot as logical target while the spawned Frame runs the vanilla job");
                Next(1, 1200);
            }
            else if (step == 1 && pilot.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestPilotSelfEffect")))
            {
                Ability self = pilot.abilities.GetAbility(SelfDef);
                Check(!frame.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestPilotSelfEffect")), "self effect applies to the pilot and never the chassis");
                Check(self.OnCooldown && selfAbilityId == self.Id, "self cooldown remains on the original pilot ability instance");
                Ability aimed = pilot.abilities.GetAbility(TargetDef);
                aimed.QueueCastingJob(target, target);
                Check(frame.CurJob?.ability == aimed && !pilot.Spawned, "targeted warmup uses a valid spawned Frame actor without spawning the pilot");
                Next(2, 1800);
            }
            else if (step == 2 && target.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed("ZF_TestPilotTargetEffect")))
            {
                Ability aimed = pilot.abilities.GetAbility(TargetDef);
                Check(aimed.OnCooldown && aimed.Id == targetAbilityId, "targeted cast cooldown remains pilot-owned");
                targetCooldown = aimed.CooldownTicksRemaining;
                Check(aimed.verb.Caster == frame && aimed.verb.CanHitTarget(target), "caster range and LOS originate from the exact Frame");
                frame.jobs.StopAll();
                IntVec3 old = frame.Position;
                frame.Position = old + IntVec3.North * 4;
                frame.Notify_Teleported(false, false);
                Check(aimed.verb.Caster.Position == frame.Position, "moving the Frame moves the pilot ability origin immediately");
                locationTarget = frame.Position + IntVec3.West * 6;
                Ability location = pilot.abilities.GetAbility(LocationDef);
                CompAbilityEffect_RecordLocation.LastCell = IntVec3.Invalid;
                location.QueueCastingJob(locationTarget, LocalTargetInfo.Invalid);
                Next(3, 1200);
            }
            else if (step == 3 && CompAbilityEffect_RecordLocation.LastCell.IsValid && frame.CurJob?.ability == null)
            {
                Check(CompAbilityEffect_RecordLocation.LastCell == locationTarget && ReferenceEquals(CompAbilityEffect_RecordLocation.LastOwner, pilot),
                    "ground ability preserves its location target and original pilot owner");
                if (ModsConfig.RoyaltyActive)
                {
                    PrepareRoyaltyPsycaster(pilot);
                    Ability psycast = pilot.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("Stun"));
                    psyfocus = pilot.psychicEntropy.CurrentPsyfocus;
                    entropy = pilot.psychicEntropy.EntropyValue;
                    desiredPsyfocus = 0.66f;
                    pilot.psychicEntropy.SetPsyfocusTarget(desiredPsyfocus);
                    psycast.QueueCastingJob(target, target);
                    Check(frame.CurJob?.ability == psycast && !pilot.Spawned,
                        "real Royalty Psycast enters the Frame-hosted vanilla cast job (job=" +
                        (frame.CurJobDef?.defName ?? "null") + ", jobAbility=" +
                        (frame.CurJob?.ability?.def?.defName ?? "null") + ", psycast=" +
                        (psycast?.def?.defName ?? "null") + ", canCast=" + psycast?.CanCast.Accepted + ")");
                    Next(4, 1800);
                }
                else if (HasVEF) StartVEFCast();
                else SaveNext(1);
            }
            else if (step == 4 && pilot.psychicEntropy.CurrentPsyfocus < psyfocus)
            {
                Check(pilot.psychicEntropy.CurrentPsyfocus < psyfocus, "real Royalty Psycast consumes exact pilot psyfocus");
                Check(pilot.psychicEntropy.EntropyValue > entropy, "real Royalty Psycast adds entropy to the exact pilot tracker");
                Check(target.stances.stunner.Stunned, "real Royalty Psycast applies its effect to the real target");
                Check(frame.psychicEntropy == null, "Frame owns no duplicate psychic entropy tracker");
                psyfocus = pilot.psychicEntropy.CurrentPsyfocus;
                entropy = pilot.psychicEntropy.EntropyValue;
                if (HasVEF) StartVEFCast();
                else SaveNext(1);
            }
            else if (step == 5 && VEF<bool>("EffectApplied", target))
            {
                vefAbilityId = VEF<string>("LoadId", pilot);
                vefCooldown = VEF<int>("Cooldown", pilot);
                Check(vefCooldown > 0, "VEF cooldown remains on the original pilot ability");
                Check(VEF<bool>("UsesFrameHost", frame, pilot), "VEF cast creates no CompAbilities proxy on the chassis");
                if (HasVPE) StartVPECast();
                else SaveNext(1);
            }
            else if (step == 6 && VEF<bool>("VPEEffectApplied", pilot))
            {
                vpeAbilityId = VEF<string>("VPELoadId", pilot);
                vpeCooldown = VEF<int>("VPECooldown", pilot);
                Check(vpeCooldown > 0, "real VPE cooldown remains on the original pilot psycast");
                Check(pilot.psychicEntropy.CurrentPsyfocus < vpePsyfocus, "real VPE psycast consumes pilot psyfocus");
                Check(pilot.psychicEntropy.EntropyValue > vpeEntropy, "real VPE psycast adds entropy to the pilot tracker");
                Check(VEF<bool>("VPEUsesFrameHost", frame, pilot) && frame.psychicEntropy == null,
                    "VPE cast keeps resources on the pilot and creates no chassis proxy");
                vpePsyfocus = pilot.psychicEntropy.CurrentPsyfocus;
                vpeEntropy = pilot.psychicEntropy.EntropyValue;
                SaveNext(1);
            }
        }

        private void BeginInitialCast()
        {
            Ability self = pilot.abilities.GetAbility(SelfDef);
            List<Command_Ability> commands = frame.GetGizmos().OfType<Command_Ability>().ToList();
            if (ModsConfig.IdeologyActive)
            {
                Check(ideologyRole.IsAssigned(pilot), "real Ideology Precept_Role is assigned to the exact pilot");
                Ability roleAbility = pilot.abilities.AllAbilitiesForReading.Single(a => a.def.defName == "CombatCommand");
                roleAbilityId = roleAbility.Id;
                Check(roleAbility.pawn == pilot && commands.Count(c => c.Ability == roleAbility) == 1,
                    "real Ideology role Ability belongs to pilot and appears exactly once through embarked Frame");
                roleAbility.QueueCastingJob(pilot, LocalTargetInfo.Invalid);
                Check(frame.CurJob?.ability == roleAbility && ReferenceEquals(frame.CurJob.targetC.Thing, pilot) && roleAbility.verb.Caster == frame,
                    "Ideology role Ability uses Frame spatial host while pilot remains logical owner and self target");
                Next(7, 1200);
                return;
            }
            if (HasVEF)
            {
                Check(VEF<bool>("HasExactCommand", frame, pilot), "Frame forwards the exact original VEF Command_Ability once");
                Check(VEF<bool>("UsesFrameHost", frame, pilot), "VEF ability keeps pilot ownership while using the exact Frame host");
                if (HasVPE)
                {
                    Check(VEF<bool>("HasExactVPECommand", frame, pilot), "Frame forwards the exact VPE Command_Ability_Psycast once");
                    Check(VEF<bool>("VPEUsesFrameHost", frame, pilot), "VPE psycast keeps pilot ownership while using the exact Frame host");
                }
            }
            self.QueueCastingJob(pilot, LocalTargetInfo.Invalid);
            Check(frame.CurJob?.ability == self && ReferenceEquals(frame.CurJob.targetA.Thing, frame) && ReferenceEquals(frame.CurJob.targetC.Thing, pilot),
                "self cast keeps pilot as logical target while the spawned Frame runs the vanilla job");
            Next(1, 1200);
        }

        private void ReloadSwapAndEjection()
        {
            if (step != 0) return;
            Ability self = pilot.abilities.GetAbility(SelfDef);
            Ability aimed = pilot.abilities.GetAbility(TargetDef);
            Check(frame.GetUniqueLoadID() == frameId && pilot.GetUniqueLoadID() == pilotId && ReferenceEquals(frame.Cockpit.Pilot, pilot),
                "fresh process preserves exact Frame cockpit and pilot identities");
            Check(self.Id == selfAbilityId && aimed.Id == targetAbilityId && aimed.OnCooldown && aimed.CooldownTicksRemaining <= targetCooldown,
                "fresh process preserves exact pilot ability instances and cooldown");
            Check(frame.abilities.GetAbility(SelfDef) == null && !pilot.Spawned, "reload creates no copied ability and no fake spawned pilot");
            if (HasVEF)
            {
                Check(VEF<string>("LoadId", pilot) == vefAbilityId && VEF<int>("Cooldown", pilot) > 0 &&
                    VEF<int>("Cooldown", pilot) <= vefCooldown,
                    "fresh process preserves the exact VEF pilot ability and cooldown");
                Check(VEF<bool>("HasExactCommand", frame, pilot) && VEF<bool>("UsesFrameHost", frame, pilot),
                    "fresh process restores the original VEF command with no chassis component copy");
                if (HasVPE)
                {
                    Check(VEF<string>("VPELoadId", pilot) == vpeAbilityId && VEF<int>("VPECooldown", pilot) > 0 &&
                        VEF<int>("VPECooldown", pilot) <= vpeCooldown,
                        "fresh process preserves the exact VPE psycast and cooldown");
                    Check(VEF<bool>("VPESetContains", pilot), "fresh process preserves the VPE psyset assignment on the pilot");
                    Check(VEF<bool>("HasExactVPECommand", frame, pilot) && VEF<bool>("VPEUsesFrameHost", frame, pilot),
                        "fresh process restores the real VPE command and pilot-owned caster state; " +
                        VEF<string>("VPEDiagnostic", frame, pilot));
                }
            }
            if (ModsConfig.RoyaltyActive)
            {
                float expectedPsyfocus = HasVPE ? vpePsyfocus : psyfocus;
                float expectedEntropy = HasVPE ? vpeEntropy : entropy;
                Check(Math.Abs(pilot.psychicEntropy.CurrentPsyfocus - expectedPsyfocus) < 0.01f && pilot.psychicEntropy.EntropyValue <= expectedEntropy &&
                    Math.Abs(pilot.psychicEntropy.TargetPsyfocus - desiredPsyfocus) < 0.001f,
                    "fresh process preserves pilot psyfocus entropy and desired psyfocus state");
                Check(frame.GetGizmos().Any(g => g.GetType().Name.Contains("Psychic") && g.GetType().Name.Contains("Gizmo")),
                    "selected Frame forwards the real psychic status gizmo");
            }
            int remaining = aimed.CooldownTicksRemaining;
            Check(frame.Cockpit.TryDisembark(), "Pilot A disembarks with their ability state");
            if (ModsConfig.IdeologyActive)
            {
                Ability roleAbility = pilot.abilities.AllAbilitiesForReading.Single(a => a.Id == roleAbilityId);
                Check(roleAbility.pawn == pilot && ReferenceEquals(roleAbility.ConstantCaster, pilot) && roleAbility.OnCooldown,
                    "disembark restores ordinary Pawn use of the exact role-granted Ability and pilot cooldown");
            }
            secondPilot = frame.Map.mapPawns.FreeColonistsSpawned.FirstOrDefault(candidate => candidate != pilot);
            if (secondPilot == null) secondPilot = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), frame.Position + IntVec3.East, frame.Map);
            else Relocate(secondPilot, frame.Position + IntVec3.East, frame.Map);
            secondPilot.abilities.GainAbility(SelfDef);
            Check(frame.Cockpit.TryBoard(secondPilot) && frame.SetDrafted(true), "Pilot B boards the same chassis");
            Check(frame.GetGizmos().OfType<Command_Ability>().All(c => c.Ability != aimed) && aimed.CooldownTicksRemaining <= remaining,
                "Pilot B does not inherit Pilot A ability or cooldown");
            if (HasVEF) Check(!VEF<bool>("HasExactCommand", frame, pilot), "Pilot B does not inherit Pilot A VEF ability or cooldown");
            if (HasVPE) Check(!VEF<bool>("HasExactVPECommand", frame, pilot), "Pilot B does not inherit Pilot A VPE psycast or cooldown");
            Check(frame.Cockpit.TryDisembark(), "Pilot B disembarks cleanly");
            Relocate(pilot, frame.Position + IntVec3.East, frame.Map);
            Check(frame.Cockpit.TryBoard(pilot) && frame.SetDrafted(true), "Pilot A reboards and restores their original ability state");
            Check(frame.GetGizmos().OfType<Command_Ability>().Any(c => c.Ability == aimed) && aimed.OnCooldown,
                "Pilot A cooldown follows Pilot A across the pilot swap");
            if (HasVEF) Check(VEF<bool>("HasExactCommand", frame, pilot) && VEF<int>("Cooldown", pilot) > 0,
                "Pilot A VEF ability and cooldown follow Pilot A across the pilot swap");
            if (HasVPE) Check(VEF<bool>("HasExactVPECommand", frame, pilot) && VEF<int>("VPECooldown", pilot) > 0,
                "Pilot A VPE psycast and cooldown follow Pilot A across the pilot swap");
            Find.Targeter.BeginTargeting(aimed.verb);
            Check(Find.Targeter.IsTargeting, "pilot ability enters the real targeter from Frame selection");
            Check(frame.Cockpit.TryDisembark() && !Find.Targeter.IsTargeting, "disembark cancels targeting without a stale caster");
            Relocate(pilot, frame.Position + IntVec3.East, frame.Map);
            Check(frame.Cockpit.TryBoard(pilot), "Pilot A reboards before ejection proof");
            DownByMissingLegs(frame);
            Check(frame.Downed && !frame.Cockpit.HasPilot && pilot.Spawned, "downing ejects the exact psycaster safely");
            Check(pilot.abilities.GetAbility(TargetDef).Id == targetAbilityId && frame.abilities.GetAbility(TargetDef) == null,
                "ejection preserves pilot abilities without transferring them to the chassis");
            if (HasVEF) Check(VEF<string>("LoadId", pilot) == vefAbilityId && VEF<int>("Cooldown", pilot) > 0,
                "ejection preserves the original VEF ability on the pilot");
            if (HasVPE) Check(VEF<string>("VPELoadId", pilot) == vpeAbilityId && VEF<int>("VPECooldown", pilot) > 0,
                "ejection preserves the real VPE psycast and resources on the pilot");
            if (ModsConfig.RoyaltyActive)
                Check(Math.Abs(pilot.psychicEntropy.TargetPsyfocus - desiredPsyfocus) < 0.001f, "ejected pilot retains desired psyfocus");
            SaveNext(2);
        }

        private void HostileAbilityAI()
        {
            if (step == 0)
            {
                Check(pilot.Spawned && pilot.abilities.GetAbility(TargetDef).Id == targetAbilityId,
                    "fresh process preserves ejected pilot ability ownership");
                hostileFrame = FrameDebugActions.SpawnHostilePilotedFrame(frame.Position + IntVec3.East * 15, frame.Map);
                hostilePilot = hostileFrame.Cockpit.Pilot;
                hostilePilot.abilities.GainAbility(TargetDef);
                hostileTarget = (Pawn)GenSpawn.Spawn(GenerateHuman(Faction.OfPlayer), hostileFrame.Position + IntVec3.West * 7, frame.Map);
                hostileTarget.jobs.StopAll();
                hostileTarget.stances.stunner.StunFor(10000, hostileFrame);
                hostileFrame.jobs.StopAll();
                hostileFrame.stances.CancelBusyStanceHard();
                Check(!hostileFrame.CanTakeOrder && hostileFrame.GetGizmos().OfType<Command_Ability>().All(c => c.Ability.pawn != hostilePilot),
                    "hostile pilot gizmos are never exposed to player control");
                Next(1, 3600);
            }
            else if (step == 1 && FindPlayerPawnWithHediff("ZF_TestPilotTargetEffect") != null)
            {
                hostileTarget = FindPlayerPawnWithHediff("ZF_TestPilotTargetEffect");
                Ability hostileAbility = hostilePilot.abilities.GetAbility(TargetDef);
                Check(hostileAbility.OnCooldown && !hostilePilot.Spawned && ReferenceEquals(hostileFrame.Cockpit.Pilot, hostilePilot),
                    "hostile Frame naturally uses an AI-valid pilot ability while ownership remains in cockpit");
                Check(hostileFrame.abilities.GetAbility(TargetDef) == null, "hostile AI creates no chassis ability copy");
                if (HasVEF)
                {
                    Check(VEF<bool>("Give", hostilePilot), "hostile pilot receives a controlled VEF ability in the original CompAbilities");
                    VEF<object>("ClearTarget", hostileTarget);
                    hostileFrame.jobs.StopAll();
                    Next(2, 4800);
                }
                else SaveNext(3);
            }
            else if (step == 2 && FindPlayerPawnWithHediff("ZF_TestVEFTargetEffect") != null)
            {
                hostileTarget = FindPlayerPawnWithHediff("ZF_TestVEFTargetEffect");
                Check(VEF<int>("Cooldown", hostilePilot) > 0 && !hostilePilot.Spawned &&
                    ReferenceEquals(hostileFrame.Cockpit.Pilot, hostilePilot),
                    "hostile Frame naturally casts the pilot-owned VEF ability through its ThinkTree");
                Check(VEF<bool>("UsesFrameHost", hostileFrame, hostilePilot),
                    "hostile VEF cast creates no chassis component or ability copy");
                SaveNext(3);
            }
        }

        private void HostileReload()
        {
            if (step != 0) return;
            Check(hostileFrame != null && hostilePilot != null && ReferenceEquals(hostileFrame.Cockpit.Pilot, hostilePilot) &&
                hostilePilot.abilities.GetAbility(TargetDef).OnCooldown && !hostilePilot.Spawned,
                "hostile pilot ability and cooldown survive fresh-process cockpit reload");
            Check(hostileFrame.abilities.GetAbility(TargetDef) == null, "hostile reload still contains no duplicated ability");
            if (HasVEF) Check(VEF<int>("Cooldown", hostilePilot) > 0 && VEF<bool>("UsesFrameHost", hostileFrame, hostilePilot),
                "hostile VEF ability and cooldown survive fresh-process cockpit reload");
            Finish();
        }

        private static void PrepareRoyaltyPsycaster(Pawn pawn)
        {
            if (!pawn.health.hediffSet.HasHediff(HediffDefOf.PsychicAmplifier))
            {
                Hediff_Psylink link = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pawn);
                link.suppressPostAddLetter = true;
                pawn.health.AddHediff(link, pawn.health.hediffSet.GetBrain());
            }
            pawn.psychicEntropy.RechargePsyfocus();
            pawn.psychicEntropy.RemoveAllEntropy();
            pawn.abilities.GainAbility(DefDatabase<AbilityDef>.GetNamed("Stun"));
        }

        private static Type VEFApi => AccessTools.TypeByName("ZenithFrame.AbilityTests.VEF.VEFAbilityTestApi");
        private static bool HasVEF => VEFApi != null;
        private static bool HasVPE => HasVEF && VEF<bool>("HasVPE");

        private static T VEF<T>(string method, params object[] args) =>
            (T)AccessTools.Method(VEFApi, method).Invoke(null, args);

        private void StartVEFCast()
        {
            frame.jobs.StopAll();
            VEF<object>("ClearTarget", target);
            Check(VEF<bool>("Start", pilot, target), "VEF starts the original pilot ability through its real job pipeline");
            Check(VEF<bool>("FrameRunsVEFJob", frame), "spawned Frame runs the real VEF cast driver without spawning the pilot");
            Next(5, 1800);
        }

        private void StartVPECast()
        {
            frame.jobs.StopAll();
            VEF<object>("ClearVPEEffect", pilot);
            pilot.psychicEntropy.RechargePsyfocus();
            pilot.psychicEntropy.RemoveAllEntropy();
            vpePsyfocus = pilot.psychicEntropy.CurrentPsyfocus;
            vpeEntropy = pilot.psychicEntropy.EntropyValue;
            Check(VEF<bool>("StartVPE", pilot), "real VPE psycast starts through its original ability pipeline");
            Check(VEF<bool>("FrameRunsVEFJob", frame), "spawned Frame runs the real VPE cast driver with pilot as logical self target; " +
                VEF<string>("FrameJobState", frame));
            Next(6, 1800);
        }

        private static Pawn FindPlayerPawnWithHediff(string defName)
        {
            HediffDef marker = DefDatabase<HediffDef>.GetNamed(defName);
            return Find.CurrentMap.mapPawns.AllPawnsSpawned.FirstOrDefault(candidate =>
                candidate.Faction == Faction.OfPlayer && candidate.health?.hediffSet.HasHediff(marker) == true);
        }

        private static Pawn GenerateHuman(Faction faction)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, faction, canGeneratePawnRelations: false));
            pawn.relations.ClearAllRelations();
            return pawn;
        }

        private static void ClearArea(CellRect rect, Map map)
        {
            foreach (IntVec3 cell in rect)
            {
                foreach (Thing thing in cell.GetThingList(map).ToList())
                    if (!(thing is Pawn) && !(thing is IThingHolder) && (thing.def.category == ThingCategory.Plant || thing.def.category == ThingCategory.Building || thing.def.category == ThingCategory.Item)) thing.Destroy();
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                map.roofGrid.SetRoof(cell, null);
                map.fogGrid.Unfog(cell);
            }
        }

        private static void Relocate(Pawn pawn, IntVec3 cell, Map map)
        {
            if (pawn.Spawned) pawn.DeSpawn();
            GenSpawn.Spawn(pawn, cell, map);
        }

        private static void DownByMissingLegs(FramePawn targetFrame)
        {
            foreach (BodyPartRecord leg in targetFrame.RaceProps.body.AllParts.Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)))
                if (!targetFrame.health.hediffSet.PartIsMissing(leg)) targetFrame.health.AddHediff(HediffDefOf.MissingBodyPart, leg);
        }

        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            passed.Add("PASS " + name);
        }

        private void Next(int nextStep, int ticks) { step = nextStep; deadline = Find.TickManager.TicksGame + ticks; }
        private void SaveNext(int nextPhase)
        {
            string report = ReportPath;
            phase = nextPhase;
            GameDataSaveLoader.SaveGame("ZenithAbilities");
            File.WriteAllLines(report, passed.Concat(new[] { "PASS phase complete; saved for fresh-process reload" }));
            finished = true;
            Application.Quit(0);
        }
        private void Finish()
        {
            File.WriteAllLines(ReportPath, passed.Concat(new[] { "PASS ALL ABILITY PHASES COMPLETE" }));
            finished = true;
            Application.Quit(0);
        }
        private void Fail(Exception e)
        {
            File.WriteAllLines(ReportPath, passed.Concat(new[] { "FAIL " + e }));
            finished = true;
            Log.Error("ZENITH ABILITY TEST FAILURE " + e);
            Application.Quit(1);
        }
    }
}
