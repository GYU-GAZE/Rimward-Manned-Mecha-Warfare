using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace ZenithFrame
{
    public sealed class FrameServiceExtension : DefModExtension
    {
        public ThingDef reconstructionMaterial;
        public float reconstructionMaterialPerPartHp = 0.1f;
        public float reconstructionWorkPerPartHp = 30f;
        public float repairWorkPerDamage = 60f;
        public float serviceWork = 600f;
        public float hackingDefence = 1200f;
        public int normalBoardingTicks = 300;
        public float dockingSpeedMultiplier = 5f;
        public int dockingStorageRadius = 12;
    }

    public enum FrameServiceOrderKind { None, Install, Remove }

    public sealed class CompProperties_FrameService : CompProperties
    {
        public CompProperties_FrameService() { compClass = typeof(CompFrameService); }
    }

    public sealed class CompFrameService : ThingComp
    {
        private FrameServiceOrderKind pendingKind;
        private FrameAddonCategory pendingCategory;
        private Thing pendingModule;
        private Pawn activeWorker;

        public FramePawn Frame => (FramePawn)parent;
        public FrameServiceExtension Profile => Frame.def.GetModExtension<FrameServiceExtension>();
        public bool HasPendingOrder => pendingKind != FrameServiceOrderKind.None;
        public FrameServiceOrderKind PendingKind => pendingKind;
        public FrameAddonCategory PendingCategory => pendingCategory;
        public Thing PendingModule => pendingModule;
        public Pawn ActiveWorker => IsServiceLocked ? activeWorker : null;
        public bool IsServiceLocked => activeWorker != null && !activeWorker.Destroyed &&
            activeWorker.CurJob != null && IsFrameServiceJob(activeWorker.CurJob.def) &&
            ReferenceEquals(activeWorker.CurJob.targetA.Thing, Frame);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref pendingKind, "frameServiceOrderKind", FrameServiceOrderKind.None);
            Scribe_Values.Look(ref pendingCategory, "frameServiceOrderCategory", FrameAddonCategory.Weapon);
            Scribe_References.Look(ref pendingModule, "frameServiceExactModule");
            Scribe_References.Look(ref activeWorker, "frameServiceWorker");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                // Pawn jobs and carried Things finish their own load reconciliation after
                // ThingComps.  Preserve exact references here and validate them on tick.
                if (!HasPendingOrder) ClearPendingOrder();
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (activeWorker != null && !IsServiceLocked) activeWorker = null;
            if (pendingKind == FrameServiceOrderKind.Install && !PendingOrderStillValid(false)) ClearPendingOrder();
        }

        public bool CanBeServicedNow(out string reason)
        {
            reason = null;
            if (!Frame.Spawned || Frame.Dead || Frame.Destroyed || Frame.Faction != Faction.OfPlayer)
                reason = "Frame is not an available player chassis.";
            else if (Frame.Drafted) reason = "Drafted Frames cannot be serviced.";
            else if (Frame.pather?.Moving == true) reason = "Moving Frames cannot be serviced.";
            else if (Frame.stances?.FullBodyBusy == true) reason = "Busy Frames cannot be serviced.";
            else if (Frame.CurJob != null && Frame.CurJob.def != ZenithJobDefOf.ZF_FrameIdleJob)
                reason = "Frame must be idle before service begins.";
            else if (IsServiceLocked) reason = "Frame is already being serviced.";
            return reason == null;
        }

        public bool TryBeginService(Pawn worker)
        {
            if (worker == null || worker.Dead || worker.Downed || !CanBeServicedNow(out _)) return false;
            Frame.Cockpit?.StopFrame();
            activeWorker = worker;
            return IsServiceLocked;
        }

        public void EndService(Pawn worker)
        {
            if (worker == null || ReferenceEquals(activeWorker, worker)) activeWorker = null;
        }

        public bool QueueInstall(FrameAddonCategory category, Thing exact)
        {
            if (!IsStoredCandidate(exact, category) || Frame.Faction != Faction.OfPlayer) return false;
            pendingKind = FrameServiceOrderKind.Install;
            pendingCategory = category;
            pendingModule = exact;
            return true;
        }

        public bool QueueRemove(FrameAddonCategory category)
        {
            if (Frame.Faction != Faction.OfPlayer || Installed(category) == null) return false;
            pendingKind = FrameServiceOrderKind.Remove;
            pendingCategory = category;
            pendingModule = null;
            return true;
        }

        public void ClearPendingOrder()
        {
            pendingKind = FrameServiceOrderKind.None;
            pendingCategory = FrameAddonCategory.Weapon;
            pendingModule = null;
        }

        public bool PendingOrderStillValid(bool requireReachable, Pawn worker = null)
        {
            if (!HasPendingOrder || Frame.Dead || Frame.Destroyed || Frame.Faction != Faction.OfPlayer) return false;
            if (pendingKind == FrameServiceOrderKind.Remove) return Installed(pendingCategory) != null;
            bool stored = IsStoredCandidate(pendingModule, pendingCategory);
            Pawn carrier = PendingModuleCarrier();
            bool carried = carrier != null;
            if (!stored && !carried) return false;
            if (!requireReachable) return true;
            if (worker == null) return false;
            if (carried) return ReferenceEquals(worker, carrier);
            return worker.CanReserveAndReach(pendingModule, PathEndMode.ClosestTouch, Danger.Some);
        }

        public IEnumerable<Thing> StoredCandidates(FrameAddonCategory category)
        {
            if (!Frame.Spawned) yield break;
            foreach (Thing thing in Frame.Map.listerThings.AllThings
                .Where(thing => IsStoredCandidate(thing, category))
                .OrderBy(thing => thing.LabelCap).ThenBy(thing => thing.thingIDNumber))
                yield return thing;
        }

        public bool CompletePendingOrder(Pawn worker, out Thing outgoing)
        {
            outgoing = null;
            if (!ReferenceEquals(activeWorker, worker) || !PendingOrderStillValid(false)) return false;
            FrameAddonCategory category = pendingCategory;
            if (pendingKind == FrameServiceOrderKind.Remove)
            {
                bool removed = TryRemove(category, out outgoing);
                if (removed) ClearPendingOrder();
                return removed;
            }

            Thing incoming = pendingModule;
            if (!ReferenceEquals(worker.carryTracker?.CarriedThing, incoming)) return false;
            Thing installed = Installed(category);
            if (installed != null && !TryRemove(category, out outgoing)) return false;
            if (!worker.carryTracker.TryDropCarriedThing(Frame.Position, ThingPlaceMode.Near, out Thing dropped) ||
                !ReferenceEquals(dropped, incoming) || !TryInstall(category, incoming))
            {
                if (outgoing != null && outgoing.Spawned) TryInstall(category, outgoing);
                return false;
            }
            ClearPendingOrder();
            return true;
        }

        public Thing Installed(FrameAddonCategory category) => category == FrameAddonCategory.Weapon
            ? Frame.Loadout?.InstalledWeapon : category == FrameAddonCategory.Utility
                ? Frame.Loadout?.InstalledUtility : Frame.Loadout?.InstalledPower;

        private bool TryInstall(FrameAddonCategory category, Thing thing) => category == FrameAddonCategory.Weapon
            ? thing is ThingWithComps weapon && Frame.Loadout.TryInstallWeapon(weapon)
            : category == FrameAddonCategory.Utility ? Frame.Loadout.TryInstallUtility(thing) : Frame.Loadout.TryInstallPower(thing);

        private bool TryRemove(FrameAddonCategory category, out Thing removed)
        {
            removed = null;
            if (category == FrameAddonCategory.Weapon)
            {
                bool result = Frame.Loadout.TryRemoveWeapon(out ThingWithComps weapon);
                removed = weapon;
                return result;
            }
            return category == FrameAddonCategory.Utility
                ? Frame.Loadout.TryRemoveUtility(out removed)
                : Frame.Loadout.TryRemovePower(out removed);
        }

        private bool IsStoredCandidate(Thing thing, FrameAddonCategory category)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != Frame.Map || thing.IsForbidden(Faction.OfPlayer)) return false;
            if (thing.def.GetModExtension<FrameAddonExtension>()?.category != category || thing.stackCount != 1) return false;
            return StoreUtility.IsInValidStorage(thing);
        }

        private Pawn PendingModuleCarrier()
        {
            if (pendingKind != FrameServiceOrderKind.Install || pendingModule == null || !Frame.Spawned) return null;
            IEnumerable<Pawn> candidates = activeWorker != null
                ? new[] { activeWorker }.Concat(Frame.Map.mapPawns.AllPawnsSpawned.Where(pawn => !ReferenceEquals(pawn, activeWorker)))
                : Frame.Map.mapPawns.AllPawnsSpawned;
            return candidates.FirstOrDefault(worker => !worker.Destroyed &&
                ReferenceEquals(worker.carryTracker?.CarriedThing, pendingModule) &&
                worker.CurJob != null && IsFrameServiceJob(worker.CurJob.def) &&
                ReferenceEquals(worker.CurJob.targetA.Thing, Frame) &&
                ReferenceEquals(worker.CurJob.targetB.Thing, pendingModule));
        }

        public string PendingLabel => !HasPendingOrder ? null : pendingKind == FrameServiceOrderKind.Remove
            ? "Remove " + pendingCategory : "Install " + (pendingModule?.LabelCap ?? "invalid module");

        public override string CompInspectStringExtra()
        {
            string result = "Docking: " + (DockingBayUtility.IsDocked(Frame) ? "Docked" : "Not docked");
            if (HasPendingOrder) result += "\nPending service: " + PendingLabel;
            if (IsServiceLocked) result += "\nBeing serviced by " + activeWorker.LabelShortCap;
            return result;
        }

        public static bool IsFrameServiceJob(JobDef def) => def == ZenithJobDefOf.ZF_RepairFrame ||
            def == ZenithJobDefOf.ZF_ReconstructFramePart || def == ZenithJobDefOf.ZF_ServiceFrameModule;
    }

    public static class DockingBayUtility
    {
        public const string DockingBayDefName = "ZF_FrameDockingBay";

        public static Building DockingBayFor(FramePawn frame)
        {
            if (frame?.Spawned != true) return null;
            return frame.Position.GetThingList(frame.Map).OfType<Building>()
                .FirstOrDefault(building => building.def.defName == DockingBayDefName);
        }

        public static bool IsDocked(FramePawn frame) => DockingBayFor(frame) != null;

        public static float SpeedMultiplier(FramePawn frame) => IsDocked(frame)
            ? frame.def.GetModExtension<FrameServiceExtension>()?.dockingSpeedMultiplier ?? 5f : 1f;

        public static int BoardingTicks(FramePawn frame) => Mathf.Max(1, Mathf.RoundToInt(
            (frame.def.GetModExtension<FrameServiceExtension>()?.normalBoardingTicks ?? 300) / SpeedMultiplier(frame)));

        public static IntVec3 NearbyStorageCell(FramePawn frame, Thing thing)
        {
            int radius = frame.def.GetModExtension<FrameServiceExtension>()?.dockingStorageRadius ?? 12;
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(frame.Position, radius, true))
            {
                if (!cell.InBounds(frame.Map) || cell.GetSlotGroup(frame.Map)?.Settings.AllowedToAccept(thing) != true) continue;
                if (cell.Standable(frame.Map) && !cell.GetThingList(frame.Map).Any(existing => existing.def.category == ThingCategory.Item)) return cell;
            }
            return IntVec3.Invalid;
        }
    }

    public static class FrameRepairUtility
    {
        public static Hediff_Injury NextRepairableInjury(FramePawn frame) => frame?.health?.hediffSet?.hediffs
            .OfType<Hediff_Injury>().Where(injury => injury.Severity > 0f && injury.Part != null && !frame.health.hediffSet.PartIsMissing(injury.Part))
            .OrderByDescending(injury => injury.Severity)
            .ThenBy(injury => frame.RaceProps.body.AllParts.IndexOf(injury.Part)).FirstOrDefault();

        public static Hediff_MissingPart NextReconstructablePart(FramePawn frame) => frame?.health?.hediffSet
            .GetMissingPartsCommonAncestors().Where(missing => FrameDamageRules.IsRecoverablePart(missing.Part))
            .OrderByDescending(missing => missing.Part.def.GetMaxHealth(frame))
            .ThenBy(missing => frame.RaceProps.body.AllParts.IndexOf(missing.Part)).FirstOrDefault();

        public static int ReconstructionCost(FramePawn frame, BodyPartRecord part)
        {
            float scale = frame.def.GetModExtension<FrameServiceExtension>()?.reconstructionMaterialPerPartHp ?? 0.1f;
            return Math.Max(1, Mathf.CeilToInt(part.def.GetMaxHealth(frame) * scale));
        }

        public static bool BaseEligible(FramePawn frame) => frame?.Spawned == true && !frame.Dead && !frame.Destroyed &&
            frame.Faction == Faction.OfPlayer && frame.Service?.CanBeServicedNow(out _) == true;

        public static bool HomeAreaAllows(FramePawn frame, bool forced) => forced || frame.Map.areaManager.Home[frame.Position];
    }

    public abstract class WorkGiver_FrameServiceBase : WorkGiver_Scanner
    {
        public override PathEndMode PathEndMode => PathEndMode.Touch;
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);
        public override bool ShouldSkip(Pawn pawn, bool forced = false) => !pawn.Map.mapPawns.AllPawnsSpawned.OfType<FramePawn>().Any();
    }

    public sealed class WorkGiver_ReconstructFrame : WorkGiver_FrameServiceBase
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            t is FramePawn frame && FrameRepairUtility.BaseEligible(frame) && FrameRepairUtility.HomeAreaAllows(frame, forced) &&
            FrameRepairUtility.NextReconstructablePart(frame) != null && FindMaterial(pawn, frame) != null && pawn.CanReserve(frame);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            FramePawn frame = t as FramePawn;
            Hediff_MissingPart missing = FrameRepairUtility.NextReconstructablePart(frame);
            Thing material = FindMaterial(pawn, frame);
            if (missing == null || material == null) return null;
            Job job = JobMaker.MakeJob(ZenithJobDefOf.ZF_ReconstructFramePart, frame, material);
            job.count = FrameRepairUtility.ReconstructionCost(frame, missing.Part);
            job.playerForced = forced;
            return job;
        }

        private static Thing FindMaterial(Pawn worker, FramePawn frame)
        {
            ThingDef material = frame.Service.Profile?.reconstructionMaterial ?? ThingDefOf.Steel;
            Hediff_MissingPart missing = FrameRepairUtility.NextReconstructablePart(frame);
            if (missing == null) return null;
            int count = FrameRepairUtility.ReconstructionCost(frame, missing.Part);
            return GenClosest.ClosestThingReachable(frame.Position, frame.Map, ThingRequest.ForDef(material),
                PathEndMode.ClosestTouch, TraverseParms.For(worker), 9999f,
                thing => thing.stackCount >= count && !thing.IsForbidden(worker) && worker.CanReserve(thing, 1, count));
        }
    }

    public sealed class WorkGiver_RepairFrame : WorkGiver_FrameServiceBase
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
            t is FramePawn frame && FrameRepairUtility.BaseEligible(frame) && FrameRepairUtility.HomeAreaAllows(frame, forced) &&
            FrameRepairUtility.NextRepairableInjury(frame) != null && pawn.CanReserve(frame);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            Job job = JobMaker.MakeJob(ZenithJobDefOf.ZF_RepairFrame, t);
            job.playerForced = forced;
            return job;
        }
    }

    public sealed class WorkGiver_ServiceFrameModule : WorkGiver_FrameServiceBase
    {
        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) => t is FramePawn frame &&
            FrameRepairUtility.BaseEligible(frame) && frame.Service.HasPendingOrder && frame.Service.PendingOrderStillValid(true, pawn) && pawn.CanReserve(frame);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            FramePawn frame = (FramePawn)t;
            Job job = frame.Service.PendingKind == FrameServiceOrderKind.Install
                ? JobMaker.MakeJob(ZenithJobDefOf.ZF_ServiceFrameModule, frame, frame.Service.PendingModule)
                : JobMaker.MakeJob(ZenithJobDefOf.ZF_ServiceFrameModule, frame);
            job.count = 1;
            job.playerForced = forced;
            return job;
        }
    }

    public abstract class JobDriver_FrameServiceBase : JobDriver
    {
        protected FramePawn Frame => job.targetA.Thing as FramePawn;
        protected float Speed => pawn.GetStatValue(StatDefOf.ConstructionSpeed) * DockingBayUtility.SpeedMultiplier(Frame);
        public override bool TryMakePreToilReservations(bool errorOnFailed) => pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        protected void RegisterServiceFinish() => AddFinishAction(condition => Frame?.Service?.EndService(pawn));
        protected bool BeginService() => Frame?.Service?.TryBeginService(pawn) == true;
        protected void LearnConstruction(float amount = 0.11f) => pawn.skills?.Learn(SkillDefOf.Construction, amount);
    }

    public sealed class JobDriver_RepairFrame : JobDriver_FrameServiceBase
    {
        protected override IEnumerable<Toil> MakeNewToils()
        {
            RegisterServiceFinish();
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => !FrameRepairUtility.BaseEligible(Frame) && !ReferenceEquals(Frame?.Service?.ActiveWorker, pawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil repair = ToilMaker.MakeToil("RepairFrame");
            repair.initAction = () => { if (!BeginService()) EndJobWith(JobCondition.Incompletable); };
            repair.tickAction = () =>
            {
                Hediff_Injury injury = FrameRepairUtility.NextRepairableInjury(Frame);
                if (injury == null) { ReadyForNextToil(); return; }
                float workPerDamage = Frame.Service.Profile?.repairWorkPerDamage ?? 60f;
                injury.Severity -= Speed / Math.Max(1f, workPerDamage);
                LearnConstruction();
            };
            repair.defaultCompleteMode = ToilCompleteMode.Never;
            yield return repair;
        }
    }

    public sealed class JobDriver_ReconstructFramePart : JobDriver_FrameServiceBase
    {
        private int partIndex = -1;
        private float workLeft;
        private int materialCount;
        private BodyPartRecord Part => Frame?.RaceProps?.body?.AllParts.ElementAtOrDefault(partIndex);

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref partIndex, "frameReconstructionPartIndex", -1);
            Scribe_Values.Look(ref workLeft, "frameReconstructionWorkLeft");
            Scribe_Values.Look(ref materialCount, "frameReconstructionMaterialCount");
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (materialCount <= 0) materialCount = job.count;
            return base.TryMakePreToilReservations(errorOnFailed) &&
                pawn.Reserve(job.targetB, job, 1, materialCount, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            RegisterServiceFinish();
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
            this.FailOn(() => !job.targetB.Thing.Destroyed && !ReferenceEquals(pawn.carryTracker?.CarriedThing, job.targetB.Thing) &&
                job.targetB.Thing.stackCount < materialCount);
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil reconstruct = ToilMaker.MakeToil("ReconstructFramePart");
            reconstruct.initAction = () =>
            {
                if (materialCount <= 0) materialCount = pawn.carryTracker?.CarriedThing?.stackCount ?? job.count;
                if (partIndex < 0)
                {
                    Hediff_MissingPart missing = FrameRepairUtility.NextReconstructablePart(Frame);
                    partIndex = missing == null ? -1 : Frame.RaceProps.body.AllParts.IndexOf(missing.Part);
                    workLeft = missing == null ? 0f : missing.Part.def.GetMaxHealth(Frame) * (Frame.Service.Profile?.reconstructionWorkPerPartHp ?? 30f);
                }
                if (Part == null || !Frame.health.hediffSet.PartIsMissing(Part) || !BeginService()) EndJobWith(JobCondition.Incompletable);
            };
            reconstruct.tickAction = () => { workLeft -= Speed; LearnConstruction(); if (workLeft <= 0f) ReadyForNextToil(); };
            reconstruct.defaultCompleteMode = ToilCompleteMode.Never;
            yield return reconstruct;
            Toil complete = ToilMaker.MakeToil("CompleteFrameReconstruction");
            complete.initAction = () =>
            {
                Thing material = pawn.carryTracker?.CarriedThing;
                if (materialCount <= 0) materialCount = material?.stackCount ?? 0;
                if (Part == null || !Frame.health.hediffSet.PartIsMissing(Part) || material == null ||
                    material.def != (Frame.Service.Profile?.reconstructionMaterial ?? ThingDefOf.Steel) || material.stackCount < materialCount)
                { EndJobWith(JobCondition.Incompletable); return; }
                if (material.stackCount == materialCount) material.Destroy(); else material.stackCount -= materialCount;
                Frame.health.RestorePart(Part);
                Frame.Loadout?.TryReclaimRememberedWeapon();
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }

    public sealed class JobDriver_ServiceFrameModule : JobDriver_FrameServiceBase
    {
        private float workLeft;

        public override void ExposeData() { base.ExposeData(); Scribe_Values.Look(ref workLeft, "frameModuleServiceWorkLeft"); }
        public override bool TryMakePreToilReservations(bool errorOnFailed) => base.TryMakePreToilReservations(errorOnFailed) &&
            (!job.targetB.IsValid || pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed));

        protected override IEnumerable<Toil> MakeNewToils()
        {
            RegisterServiceFinish();
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Frame?.Service?.PendingOrderStillValid(job.targetB.IsValid, pawn) != true);
            if (job.targetB.IsValid)
            {
                this.FailOnDestroyedNullOrForbidden(TargetIndex.B);
                yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
                yield return Toils_Haul.StartCarryThing(TargetIndex.B, false, true, false);
            }
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            Toil service = ToilMaker.MakeToil("ServiceFrameModule");
            service.initAction = () =>
            {
                if (!BeginService()) { EndJobWith(JobCondition.Incompletable); return; }
                if (workLeft <= 0f) workLeft = Frame.Service.Profile?.serviceWork ?? 600f;
            };
            service.tickAction = () => { workLeft -= Speed; LearnConstruction(); if (workLeft <= 0f) ReadyForNextToil(); };
            service.defaultCompleteMode = ToilCompleteMode.Never;
            yield return service;
            Toil complete = ToilMaker.MakeToil("CompleteFrameModuleService");
            complete.initAction = () =>
            {
                if (Frame.Service.CompletePendingOrder(pawn, out Thing outgoing))
                {
                    if (outgoing?.Spawned == true && DockingBayUtility.IsDocked(Frame))
                    {
                        IntVec3 cell = DockingBayUtility.NearbyStorageCell(Frame, outgoing);
                        if (cell.IsValid)
                        {
                            Job haul = HaulAIUtility.HaulToCellStorageJob(pawn, outgoing, cell, false);
                            haul.playerForced = true;
                            pawn.jobs.jobQueue.EnqueueFirst(haul);
                        }
                    }
                    return;
                }
                EndJobWith(JobCondition.Incompletable);
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }
}
