using System;
using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace ZenithFrame.Tests
{
    public sealed partial class LifecycleSuite
    {
        public override void GameComponentTick()
        {
            if (!RepairMode || finished || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent) return;
            try { RepairUpdate(); }
            catch (Exception e) { Fail(e); }
        }

        private int repairScenario;
        private Hediff_Injury repairFirst;
        private Hediff_Injury repairSecond;
        private bool repairReachedToil;
        private bool repairSawProgress;
        private bool repairSawTransition;
        private bool repairSawUndown;
        private float repairFirstStart;
        private float repairSecondStart;

        private void StartRepairScenario(bool interrupt = false)
        {
            var legs = frame.RaceProps.body.AllParts
                .Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore)).ToList();
            Check(legs.Count == 2, "repair fixture has two mechanical legs");
            repairFirst = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, frame, legs[0]);
            repairFirst.Severity = legs[0].def.GetMaxHealth(frame) - 1f;
            frame.health.AddHediff(repairFirst, legs[0]);
            repairSecond = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, frame, legs[1]);
            repairSecond.Severity = legs[1].def.GetMaxHealth(frame) - 1f;
            frame.health.AddHediff(repairSecond, legs[1]);
            Check(frame.Downed && !frame.Dead && !frame.health.hediffSet.GetMissingPartsCommonAncestors().Any(),
                "repair fixture starts downed from repairable injuries only");
            repairFirstStart = repairFirst.Severity;
            repairSecondStart = repairSecond.Severity;
            repairReachedToil = false;
            repairSawProgress = false;
            repairSawTransition = false;
            repairSawUndown = false;

            var giver = new WorkGiver_RepairFrame();
            Check(giver.HasJobOnThing(other, frame, true), "forced Repair Frame WorkGiver accepts scenario " + repairScenario);
            Job job = giver.JobOnThing(other, frame, true);
            Check(job != null && job.playerForced && ReferenceEquals(job.targetA.Thing, frame) &&
                other.jobs.TryTakeOrderedJob(job),
                "player-forced WorkGiver repair job accepted in scenario " + repairScenario);
            Next(interrupt ? 2 : 1, 20000);
        }

        private string RepairState()
        {
            string reason = null;
            bool serviceEligible = frame?.Service?.CanBeServicedNow(out reason) == true;
            return
            " scenario=" + repairScenario +
            " job=" + (other?.CurJobDef?.defName ?? "null") +
            " toil=" + (other?.jobs?.curDriver == null ? "null" : CurrentToil(other.jobs.curDriver)?.debugName ?? "null") +
            " first=" + (repairFirst?.Severity.ToString() ?? "null") +
            " second=" + (repairSecond?.Severity.ToString() ?? "null") +
            " lock=" + (frame?.Service?.IsServiceLocked.ToString() ?? "null") +
            " worker=" + (frame?.Service?.ActiveWorker?.LabelShort ?? "null") +
            " eligible=" + serviceEligible + " reason=" + (reason ?? "none") +
            " frameJob=" + (frame?.CurJobDef?.defName ?? "null");
        }

        private void RepairUpdate()
        {
            if (Find.TickManager.TicksGame < 1000) return;
            if (step == 0)
            {
                Map map = Find.CurrentMap;
                foreach (Pawn colonist in map.mapPawns.FreeColonistsSpawned.ToList())
                    colonist.drafter.Drafted = true;
                frame = SpawnFrame(map.Center);
                Thing helmet = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
                Thing shoulder = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShoulderSystem"));
                Thing shield = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShieldOffhand"));
                Check(frame.Loadout.TryInstallHead(helmet) && frame.Loadout.TryInstallShoulder(shoulder) &&
                    frame.Loadout.TryInstallOffhand(shield),
                    "downed repair fixture equips Helmet, Shoulder and shield Offhand");
                other = GenerateHuman(Faction.OfPlayer);
                other.skills.GetSkill(SkillDefOf.Construction).Level = 20;
                GenSpawn.Spawn(other, frame.Position + IntVec3.East * 3, map);
                map.areaManager.Home[frame.Position] = true;
                StartRepairScenario();
                return;
            }

            bool firstPresent = frame.health.hediffSet.hediffs.Contains(repairFirst);
            bool secondPresent = frame.health.hediffSet.hediffs.Contains(repairSecond);
            bool remaining = firstPresent || secondPresent;
            if (remaining && other.CurJobDef != ZenithJobDefOf.ZF_RepairFrame)
                throw new InvalidOperationException("Repair job left while damage remained:" + RepairState());

            if (other.CurJobDef == ZenithJobDefOf.ZF_RepairFrame &&
                CurrentToil(other.jobs.curDriver)?.debugName == "RepairFrame")
            {
                if (!frame.Service.IsServiceLocked || !ReferenceEquals(frame.Service.ActiveWorker, other))
                    throw new InvalidOperationException("Active repair lost its service lock:" + RepairState());
                if (!repairReachedToil)
                {
                    repairReachedToil = true;
                    Check(CurrentToil(other.jobs.curDriver).handlingFacing &&
                        CurrentToil(other.jobs.curDriver).activeSkill() == SkillDefOf.Construction,
                        "repair toil retains facing and Construction feedback in scenario " + repairScenario);
                }
                if (firstPresent && repairFirst.Severity < repairFirstStart - 0.05f)
                    repairSawProgress = true;
            }

            if (!frame.Downed && remaining && !repairSawUndown)
            {
                repairSawUndown = true;
                Check(other.CurJobDef == ZenithJobDefOf.ZF_RepairFrame && frame.Service.IsServiceLocked,
                    "repair stays active when Frame stands up with injuries remaining in scenario " + repairScenario);
            }

            if (step == 2)
            {
                if (!repairSawProgress) return;
                other.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Check(!frame.Service.IsServiceLocked &&
                    other.CurJobDef != ZenithJobDefOf.ZF_RepairFrame &&
                    FrameRepairUtility.NextRepairableInjury(frame) != null,
                    "explicit interruption releases lock and active repair toil with damage remaining");
                Finish();
                return;
            }

            if (firstPresent != secondPresent && !repairSawTransition)
            {
                repairSawTransition = true;
                Check(other.CurJobDef == ZenithJobDefOf.ZF_RepairFrame && frame.Service.IsServiceLocked,
                    "repair continues through first-injury completion in scenario " + repairScenario);
            }

            if (remaining) return;
            if (other.CurJobDef == ZenithJobDefOf.ZF_RepairFrame) return;
            Check(repairReachedToil && repairSawProgress && repairSawTransition && repairSawUndown &&
                repairSecondStart > 0f && !frame.Service.IsServiceLocked,
                "player-forced repair completes both injuries and releases lock in scenario " + repairScenario +
                RepairState());
            repairScenario++;
            StartRepairScenario(repairScenario == 3);
        }
    }
}
