using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace ZenithFrame
{
    public sealed class CompProperties_FrameHacking : CompProperties
    {
        public CompProperties_FrameHacking() { compClass = typeof(CompFrameHacking); }
    }

    public sealed class CompFrameHacking : ThingComp
    {
        private float hackProgress;
        private bool completionApplied;

        public FramePawn Frame => (FramePawn)parent;
        public float HackDefence => Frame.def.GetModExtension<FrameServiceExtension>()?.hackingDefence ?? 1200f;
        public float HackProgress => hackProgress;
        public float ProgressFraction => HackDefence <= 0f ? 1f : Mathf.Clamp01(hackProgress / HackDefence);
        public bool IsHackableDisabledChassis => Frame.Spawned && !Frame.Dead && !Frame.Destroyed &&
            Frame.Cockpit != null && !Frame.Cockpit.HasPilot && Frame.Faction != Faction.OfPlayer &&
            (Frame.Faction == null || Frame.Faction.HostileTo(Faction.OfPlayer));

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref hackProgress, "frameHackProgress");
            Scribe_Values.Look(ref completionApplied, "frameHackCompletionApplied");
        }

        public static float SpeedFor(Pawn hacker)
        {
            return hacker?.GetStatValue(StatDefOf.HackingSpeed) ?? 0f;
        }

        public bool CanHack(Pawn hacker, bool requireReachAndReserve)
        {
            if (!IsHackableDisabledChassis || hacker == null || hacker == Frame) return false;
            bool eligibleColonist = hacker.Spawned && hacker.Map == Frame.Map &&
                hacker.Faction == Faction.OfPlayer && hacker.RaceProps.Humanlike &&
                hacker.IsColonistPlayerControlled && !hacker.Dead && !hacker.Downed &&
                !hacker.InMentalState && !hacker.IsPrisoner && !hacker.IsSlave;
            if (!eligibleColonist) return false;
            return !requireReachAndReserve || hacker.CanReserveAndReach(Frame, PathEndMode.Touch, Danger.Deadly);
        }

        public bool ApplyHackWork(Pawn hacker)
        {
            if (!CanHack(hacker, false) || completionApplied) return false;
            hackProgress = Mathf.Min(HackDefence, hackProgress + SpeedFor(hacker));
            hacker.skills?.Learn(SkillDefOf.Intellectual, 0.08f);
            return true;
        }

        public bool TryCompleteHack(Pawn hacker)
        {
            if (completionApplied || hackProgress < HackDefence || !CanHack(hacker, false)) return false;
            completionApplied = true;
            Frame.Cockpit.StopFrame();
            Lord hostileLord = Frame.GetLord();
            if (hostileLord != null) hostileLord.RemovePawn(Frame);
            Frame.SetFaction(Faction.OfPlayer);
            Frame.Cockpit.StopFrame();
            return Frame.Faction == Faction.OfPlayer && !Frame.Cockpit.HasPilot && !Frame.Drafted;
        }

        public override string CompInspectStringExtra() => IsHackableDisabledChassis
            ? "ZF_HackProgress".Translate(ProgressFraction.ToStringPercent()) : null;
    }

    public sealed class JobDriver_HackFrame : JobDriver
    {
        private FramePawn TargetFrame => job.targetA.Thing as FramePawn;
        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => TargetFrame?.Hacking?.CanHack(pawn, false) != true);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil hack = ToilMaker.MakeToil("HackFrame");
            hack.initAction = () => SoundDefOf.Hacking_Started.PlayOneShot(new TargetInfo(TargetFrame.Position, TargetFrame.Map));
            hack.tickAction = () =>
            {
                if (TargetFrame?.Hacking?.ApplyHackWork(pawn) != true)
                { EndJobWith(JobCondition.Incompletable); return; }
                if (TargetFrame.Hacking.ProgressFraction >= 1f) ReadyForNextToil();
            };
            hack.defaultCompleteMode = ToilCompleteMode.Never;
            hack.WithProgressBar(TargetIndex.A, () => TargetFrame?.Hacking?.ProgressFraction ?? 0f);
            hack.WithEffect(EffecterDefOf.Hacking, TargetIndex.A);
            hack.PlaySustainerOrSound(SoundDefOf.Hacking_InProgress);
            AddFinishAction(condition =>
            {
                if (condition != JobCondition.Succeeded && TargetFrame?.Hacking?.IsHackableDisabledChassis == true)
                    SoundDefOf.Hacking_Suspended.PlayOneShot(new TargetInfo(TargetFrame.Position, TargetFrame.Map));
            });
            yield return hack;
            Toil complete = ToilMaker.MakeToil("CompleteHackFrame");
            complete.initAction = () =>
            {
                if (TargetFrame?.Hacking?.TryCompleteHack(pawn) != true) EndJobWith(JobCondition.Incompletable);
                else SoundDefOf.Hacking_Completed.PlayOneShot(new TargetInfo(TargetFrame.Position, TargetFrame.Map));
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }

    public sealed class FloatMenuOptionProvider_HackFrame : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;
        protected override bool Undrafted => true;
        protected override bool Multiselect => false;
        protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (!(clickedPawn is FramePawn frame)) return null;
            Pawn hacker = context.FirstSelectedPawn;
            if (frame.Hacking?.CanHack(hacker, true) != true) return null;
            return new FloatMenuOption("ZF_HackFrame".Translate(), () => hacker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(ZenithJobDefOf.ZF_HackFrame, frame)));
        }
    }
}
