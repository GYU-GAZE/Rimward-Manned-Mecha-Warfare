using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;
using Verse.AI.Group;

namespace ZenithFrame
{
    [DefOf]
    public static class ZenithIncidentDefOf
    {
        public static IncidentDef ZF_DevelopmentEnemyFrameRaid;

        static ZenithIncidentDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(ZenithIncidentDefOf)); }
    }

    public sealed class IncidentWorker_DevelopmentEnemyFrameRaid : IncidentWorker_RaidEnemy
    {
        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            Map map = parms.target as Map;
            if (map == null) return false;

            Faction faction = parms.faction ?? Faction.OfAncientsHostile ?? Find.FactionManager.AllFactions
                .FirstOrDefault(candidate => candidate != null && !candidate.defeated && candidate.HostileTo(Faction.OfPlayer));
            if (faction == null) return false;

            FramePawn frame = (FramePawn)PawnGenerator.GeneratePawn(
                DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), faction);
            Pawn pilot = GenerateHumanRaider(faction);
            Pawn infantry = GenerateHumanRaider(faction);
            if (!frame.Cockpit.TryInstallGeneratedPilot(pilot))
                throw new System.InvalidOperationException("ZENITH: generated raid pilot could not enter the Frame.");

            var raiders = new List<Pawn> { infantry, frame };
            parms.faction = faction;
            parms.raidStrategy = RaidStrategyDefOf.ImmediateAttack;
            parms.raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn;
            parms.canTimeoutOrFlee = true;
            parms.canKidnap = false;
            parms.canSteal = false;

            if (!parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
                return false;

            parms.raidArrivalMode.Worker.Arrive(raiders, parms);
            if (raiders.Any(pawn => !pawn.Spawned || pawn.Map != map))
                throw new System.InvalidOperationException("ZENITH: development raid arrival did not spawn every raider.");

            parms.raidStrategy.Worker.MakeLords(parms, raiders);
            if (frame.GetLord() == null || infantry.GetLord() != frame.GetLord())
                throw new System.InvalidOperationException("ZENITH: development raid did not create one shared assault Lord.");
            frame.Cockpit.RememberOperationalLord(frame.GetLord());

            if (parms.sendLetter && !parms.silent)
                SendStandardLetter(parms, raiders);
            return true;
        }

        private static Pawn GenerateHumanRaider(Faction faction)
        {
            Pawn pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                PawnKindDefOf.Colonist, faction, mustBeCapableOfViolence: true));
            pawn.relations.ClearAllRelations();
            return pawn;
        }
    }

    public static class EnemyFrameRaidDebugActions
    {
        [DebugAction("ZENITH FRAME", "Start mixed enemy Frame raid", allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartMixedEnemyFrameRaid()
        {
            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, Find.CurrentMap);
            parms.forced = true;
            if (!ZenithIncidentDefOf.ZF_DevelopmentEnemyFrameRaid.Worker.TryExecute(parms))
                Messages.Message("ZENITH: development Frame raid could not start.", MessageTypeDefOf.RejectInput, false);
        }
    }
}
