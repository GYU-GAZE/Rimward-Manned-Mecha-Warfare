using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace ZenithFrame
{
    public sealed partial class FramePawn
    {
        private bool draftedOnLoad;
        private bool fireAtWillOnLoad = true;
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                draftedOnLoad = Drafted;
                fireAtWillOnLoad = drafter?.FireAtWill ?? true;
            }
            base.ExposeData();
            Scribe_Values.Look(ref draftedOnLoad, "zenithDrafted");
            Scribe_Values.Look(ref fireAtWillOnLoad, "zenithFireAtWill", true);
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (drafter == null) drafter = new Pawn_DraftController(this);
            // Vanilla does not retain a drafter for our custom non-humanlike,
            // non-mechanoid Pawn classification. Persist control intent and
            // restore the ordinary Pawn_DraftController explicitly.
            if (respawningAfterLoad)
            {
                if (draftedOnLoad && Cockpit.CanTakePlayerOrders) drafter.Drafted = true;
                drafter.FireAtWill = Cockpit.CanUseHostileCombatAI || fireAtWillOnLoad;
            }
            else if (Cockpit.CanUseHostileCombatAI)
            {
                drafter.FireAtWill = true;
            }
            if (!respawningAfterLoad && equipment.Primary == null)
                equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugRifle")));
        }

        protected override void Tick()
        {
            if (!Spawned)
            {
                base.Tick();
                return;
            }
            TryFollowLordExitDuty();
            if (!Cockpit.CanOperate)
            {
                if (Drafted || pather.Moving || (CurJob != null && CurJob.def != ZenithJobDefOf.ZF_FrameIdleJob)) Cockpit.StopFrame();
                if (Cockpit.HasPilot) Cockpit.EmergencyEject();
            }
            base.Tick();
        }

        private void TryFollowLordExitDuty()
        {
            DutyDef duty = mindState?.duty?.def;
            Lord operationalLord = Cockpit.OperationalLord;
            bool exitLord = operationalLord?.LordJob is LordJob_ExitMapBest ||
                operationalLord?.CurLordToil is LordToil_ExitMap ||
                operationalLord?.CurLordToil is LordToil_ExitMapAndDefendSelf;
            if (exitLord && duty != DutyDefOf.ExitMapBest && duty != DutyDefOf.ExitMapBestAndDefendSelf &&
                duty != DutyDefOf.ExitMapRandom && duty != DutyDefOf.ExitMapNearDutyTarget)
            {
                mindState.duty = new PawnDuty(DutyDefOf.ExitMapBest)
                {
                    locomotion = LocomotionUrgency.Sprint,
                    canDig = true
                };
                duty = mindState.duty.def;
            }
            bool shouldExit = Cockpit.CanUseHostileCombatAI && (exitLord ||
                duty == DutyDefOf.ExitMapBest || duty == DutyDefOf.ExitMapBestAndDefendSelf ||
                duty == DutyDefOf.ExitMapRandom || duty == DutyDefOf.ExitMapNearDutyTarget);
            if (!shouldExit || (CurJob != null && pather.Moving)) return;

            ThinkResult result = new JobGiver_ExitMapBest().TryIssueJobPackage(this, default(JobIssueParams));
            if (result.IsValid)
                jobs.StartJob(result.Job, JobCondition.InterruptForced);
        }

        public bool SetDrafted(bool value)
        {
            if (drafter == null) return false;
            if (value && !Cockpit.CanTakePlayerOrders) return false;
            drafter.Drafted = value;
            return true;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
                // Pawn_AbilityTracker hides ordinary ability commands for our
                // deliberately non-colonist/non-mech classification. Suppress
                // its Dev-mode copy here; the normal command is bridged below.
                if (!(gizmo is Command_Ability)) yield return gizmo;

            if (Faction != Faction.OfPlayer || !Spawned || !Cockpit.HasPilot) yield break;
            yield return new Command_Action { defaultLabel = "Disembark", defaultDesc = "Safely return the pilot to the map.", icon = TexCommand.Install,
                action = () => { if (!Cockpit.TryDisembark()) Messages.Message("No safe exit available.", MessageTypeDefOf.RejectInput, false); } };

            if (Cockpit.CanTakePlayerOrders && abilities != null)
            {
                foreach (Ability ability in abilities.AllAbilitiesForReading)
                {
                    if (!ability.GizmosVisible()) continue;
                    foreach (Command command in ability.GetGizmos())
                        yield return command;
                }
            }

            foreach (Gizmo pilotAbilityGizmo in PilotAbilityGizmos.VanillaFor(this))
                yield return pilotAbilityGizmo;

            foreach (Gizmo attackGizmo in PawnAttackGizmoUtility.GetAttackGizmos(this))
                yield return attackGizmo;
            var draft = new Command_Toggle
            {
                hotKey = KeyBindingDefOf.Command_ColonistDraft,
                defaultLabel = (Drafted ? "CommandUndraftLabel" : "CommandDraftLabel").Translate(),
                defaultDesc = "CommandToggleDraftDesc".Translate(),
                icon = TexCommand.Draft,
                turnOnSound = SoundDefOf.DraftOn,
                turnOffSound = SoundDefOf.DraftOff,
                groupKeyIgnoreContent = 81729172,
                tutorTag = (!Drafted ? "Draft" : "Undraft"),
                isActive = () => Drafted,
                toggleAction = () =>
                {
                    if (!SetDrafted(!Drafted)) return;
                    PlayerKnowledgeDatabase.KnowledgeDemonstrated(ConceptDefOf.Drafting, KnowledgeAmount.SpecificInteraction);
                    if (Drafted) LessonAutoActivator.TeachOpportunity(ConceptDefOf.QueueOrders, OpportunityType.GoodToKnow);
                }
            };
            if (!Cockpit.CanTakePlayerOrders) draft.Disable("Frame or pilot incapacitated.");
            yield return draft;

            if (Drafted && equipment.Primary?.def.IsRangedWeapon == true)
            {
                yield return new Command_Toggle
                {
                    hotKey = KeyBindingDefOf.Misc6,
                    isActive = () => drafter.FireAtWill,
                    toggleAction = () => drafter.FireAtWill = !drafter.FireAtWill,
                    icon = TexCommand.FireAtWill,
                    defaultLabel = "CommandFireAtWillLabel".Translate(),
                    defaultDesc = "CommandFireAtWillDesc".Translate(),
                    tutorTag = "FireAtWillToggle"
                };
            }
        }
    }

    public abstract class Verb_FrameShootBase : Verb_Shoot
    {
        public override void WarmupComplete()
        {
            base.WarmupComplete();
            FramePilotCombatExperience.LearnShooting(this);
        }
    }

    public sealed class Verb_FrameShot : Verb_FrameShootBase
    {
        public override bool Available() => base.Available() && CasterPawn is FramePawn frame &&
            frame.Cockpit.CanTakePlayerOrders && frame.Drafted;
        protected override bool TryCastShot() => Available() && base.TryCastShot();
    }

    public sealed class Verb_HostileFrameShot : Verb_FrameShootBase
    {
        public override bool Available()
        {
            if (!base.Available() || !(CasterPawn is FramePawn frame)) return false;
            bool playerAutomaticFire = frame.Cockpit.CanTakePlayerOrders && frame.Drafted && frame.drafter.FireAtWill;
            bool executingPlayerAttack = frame.Cockpit.CanTakePlayerOrders && frame.CurJob?.playerForced == true &&
                frame.CurJob.verbToUse is Verb_FrameShot;
            return frame.Cockpit.CanUseHostileCombatAI || playerAutomaticFire || executingPlayerAttack;
        }
        protected override bool TryCastShot() => Available() && base.TryCastShot();
    }

    public static class FrameDebugActions
    {
        [DebugAction("ZENITH FRAME", "Spawn debug Frame", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void Spawn()
        {
            DebugTools.curTool = new DebugTool("Spawn debug Frame", () => GenSpawn.Spawn(
                PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), UI.MouseCell(), Find.CurrentMap));
        }

        [DebugAction("ZENITH FRAME", "Spawn hostile piloted Frame", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void SpawnHostilePiloted()
        {
            DebugTools.curTool = new DebugTool("Spawn hostile piloted Frame", () =>
                SpawnHostilePilotedFrame(UI.MouseCell(), Find.CurrentMap));
        }

        internal static FramePawn SpawnHostilePilotedFrame(IntVec3 cell, Map map)
        {
            Faction faction = Faction.OfAncientsHostile ?? Find.FactionManager.AllFactions
                .FirstOrDefault(candidate => candidate != null && !candidate.defeated && candidate.HostileTo(Faction.OfPlayer));
            if (faction == null)
                throw new System.InvalidOperationException("ZENITH: no hostile faction is available for the debug fixture.");

            FramePawn hostileFrame = (FramePawn)PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), faction);
            Pawn hostilePilot = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, faction, canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true));
            hostilePilot.relations.ClearAllRelations();
            if (!hostileFrame.Cockpit.TryInstallGeneratedPilot(hostilePilot))
            {
                if (!hostilePilot.Destroyed) hostilePilot.Destroy();
                if (!hostileFrame.Destroyed) hostileFrame.Destroy();
                throw new System.InvalidOperationException("ZENITH: hostile debug pilot could not enter the Frame.");
            }

            return (FramePawn)GenSpawn.Spawn(hostileFrame, cell, map);
        }
    }
}

