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

    public sealed class FrameServiceOrder : IExposable
    {
        public int id;
        public FrameServiceOrderKind kind;
        public FrameAddonCategory category;
        public Thing incoming;
        public Thing expectedInstalled;

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref kind, "kind");
            Scribe_Values.Look(ref category, "category");
            Scribe_References.Look(ref incoming, "incoming");
            Scribe_References.Look(ref expectedInstalled, "expectedInstalled");
        }
    }

    public sealed class CompProperties_FrameService : CompProperties
    {
        public CompProperties_FrameService() { compClass = typeof(CompFrameService); }
    }

    public sealed class CompFrameService : ThingComp
    {
        private List<FrameServiceOrder> orders = new List<FrameServiceOrder>();
        private int nextOrderId = 1;
        private int activeOrderId;
        private Pawn activeWorker;
        private int continuationOrderId;
        private Pawn continuationWorker;
        private int continuationUntilTick;
        private FrameServiceOrderKind legacyKind;
        private FrameAddonCategory legacyCategory;
        private Thing legacyModule;

        public FramePawn Frame => (FramePawn)parent;
        public FrameServiceExtension Profile => Frame.def.GetModExtension<FrameServiceExtension>();
        public IReadOnlyList<FrameServiceOrder> Orders => orders;
        public bool HasPendingOrder => orders.Count != 0;
        public FrameServiceOrderKind PendingKind => orders.FirstOrDefault()?.kind ?? FrameServiceOrderKind.None;
        public FrameAddonCategory PendingCategory => orders.FirstOrDefault()?.category ?? FrameAddonCategory.Weapon;
        public Thing PendingModule => orders.FirstOrDefault()?.incoming;
        public Pawn ActiveWorker => IsServiceLocked ? activeWorker : null;
        public FrameServiceOrder ActiveOrder => IsServiceLocked ? orders.FirstOrDefault(order => order.id == activeOrderId) : null;
        public bool IsServiceLocked => activeWorker != null && !activeWorker.Destroyed &&
            activeWorker.CurJob != null && IsFrameServiceJob(activeWorker.CurJob.def) &&
            ReferenceEquals(activeWorker.CurJob.targetA.Thing, Frame);

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Collections.Look(ref orders, "frameServiceOrders", LookMode.Deep);
            Scribe_Values.Look(ref nextOrderId, "frameServiceNextOrderId", 1);
            Scribe_Values.Look(ref activeOrderId, "frameServiceActiveOrderId");
            Scribe_References.Look(ref activeWorker, "frameServiceWorker");
            Scribe_Values.Look(ref continuationOrderId, "frameServiceContinuationOrderId");
            Scribe_References.Look(ref continuationWorker, "frameServiceContinuationWorker");
            Scribe_Values.Look(ref continuationUntilTick, "frameServiceContinuationUntilTick");
            // Old saves contained one order. Migrate it once without recreating Things.
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref legacyKind, "frameServiceOrderKind", FrameServiceOrderKind.None);
                Scribe_Values.Look(ref legacyCategory, "frameServiceOrderCategory", FrameAddonCategory.Weapon);
            }
            if (Scribe.mode == LoadSaveMode.LoadingVars || Scribe.mode == LoadSaveMode.ResolvingCrossRefs)
            {
                Scribe_References.Look(ref legacyModule, "frameServiceExactModule");
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (orders == null) orders = new List<FrameServiceOrder>();
                if (orders.Count == 0 && legacyKind != FrameServiceOrderKind.None)
                    orders.Add(new FrameServiceOrder { id = nextOrderId++, kind = legacyKind,
                        category = legacyCategory, incoming = legacyModule,
                        expectedInstalled = Installed(legacyCategory) });
                nextOrderId = Math.Max(nextOrderId, orders.Count == 0 ? 1 : orders.Max(order => order.id) + 1);
                legacyKind = FrameServiceOrderKind.None;
                legacyModule = null;
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (activeWorker != null && !IsServiceLocked) { activeWorker = null; activeOrderId = 0; }
            if (continuationWorker != null && (continuationWorker.Destroyed || continuationWorker.Dead ||
                continuationWorker.Downed || Find.TickManager.TicksGame > continuationUntilTick ||
                !orders.Any(order => order.id == continuationOrderId))) ClearContinuation();
            foreach (FrameServiceOrder order in orders.ToList())
                if (!OrderStillValid(order, false)) CancelOrder(order.category);
        }

        private void ClearContinuation()
        {
            continuationOrderId = 0;
            continuationWorker = null;
            continuationUntilTick = 0;
        }

        public void PreferContinuation(Pawn worker, FrameServiceOrder order)
        {
            if (worker == null || order == null) { ClearContinuation(); return; }
            continuationWorker = worker;
            continuationOrderId = order.id;
            continuationUntilTick = Find.TickManager.TicksGame + 1200;
        }

        private bool ReservedForContinuation(FrameServiceOrder order, Pawn worker) =>
            order != null && continuationWorker != null && !ReferenceEquals(continuationWorker, worker) &&
            continuationOrderId == order.id && Find.TickManager.TicksGame <= continuationUntilTick;

        public bool CanBeServicedNow(out string reason)
        {
            reason = null;
            if (!Frame.Spawned || Frame.Dead || Frame.Destroyed || Frame.Faction != Faction.OfPlayer)
                reason = "ZF_NotPlayerChassis".Translate();
            else if (Frame.Drafted) reason = "ZF_ServiceDrafted".Translate();
            else if (Frame.pather?.Moving == true) reason = "ZF_ServiceMoving".Translate();
            else if (Frame.stances?.FullBodyBusy == true) reason = "ZF_ServiceBusy".Translate();
            // Vanilla may replace our idle job with a passive wait while the Frame is downed.
            else if (Frame.CurJob != null && Frame.CurJob.def != ZenithJobDefOf.ZF_FrameIdleJob &&
                !(Frame.Downed && (Frame.CurJob.def == JobDefOf.Wait_MaintainPosture ||
                    Frame.CurJob.def == JobDefOf.Wait_Downed)))
                reason = "ZF_ServiceNeedsIdle".Translate();
            else if (IsServiceLocked) reason = "ZF_ServiceAlreadyActive".Translate();
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
            if (worker == null || ReferenceEquals(activeWorker, worker)) { activeWorker = null; activeOrderId = 0; }
        }

        public void EndModuleService(Pawn worker, int orderId)
        {
            if (ReferenceEquals(activeWorker, worker) && activeOrderId == orderId)
                EndService(worker);
        }

        public FrameServiceOrder OrderFor(FrameAddonCategory category) => orders.FirstOrDefault(order => order.category == category);

        private bool CanQueue(FrameAddonCategory category) => Frame.Faction == Faction.OfPlayer &&
            Frame.Spawned && !Frame.Dead && !Frame.Destroyed &&
            !(OrderFor(category)?.id == activeOrderId && IsServiceLocked);

        private bool PutOrder(FrameServiceOrderKind kind, FrameAddonCategory category, Thing incoming)
        {
            if (!CanQueue(category)) return false;
            FrameServiceOrder order = OrderFor(category);
            if (order == null)
            {
                if (orders.Count >= 6) return false;
                order = new FrameServiceOrder { id = nextOrderId++, category = category };
                orders.Add(order);
            }
            order.kind = kind;
            order.incoming = incoming;
            order.expectedInstalled = Installed(category);
            return true;
        }

        public bool QueueInstall(FrameAddonCategory category, Thing exact)
        {
            return IsStoredCandidate(exact, category) && PutOrder(FrameServiceOrderKind.Install, category, exact);
        }

        public bool QueueRemove(FrameAddonCategory category)
        {
            return Installed(category) != null && PutOrder(FrameServiceOrderKind.Remove, category, null);
        }

        public bool CancelOrder(FrameAddonCategory category)
        {
            FrameServiceOrder order = OrderFor(category);
            if (order == null) return false;
            if (order.id == continuationOrderId) ClearContinuation();
            bool active = order.id == activeOrderId && IsServiceLocked;
            orders.Remove(order);
            if (active)
            {
                Pawn worker = activeWorker;
                if (ReferenceEquals(worker.carryTracker?.CarriedThing, order.incoming))
                    worker.carryTracker.TryDropCarriedThing(worker.Position, ThingPlaceMode.Near, out _);
                EndService(worker);
                worker.jobs.EndCurrentJob(JobCondition.Incompletable);
            }
            return true;
        }

        public void ClearPendingOrder() { if (orders.Count != 0) CancelOrder(orders[0].category); }

        public bool PendingOrderStillValid(bool requireReachable, Pawn worker = null)
        {
            return orders.Count != 0 && OrderStillValid(orders[0], requireReachable, worker);
        }

        public bool OrderStillValid(FrameServiceOrder order, bool requireReachable, Pawn worker = null)
        {
            if (order == null || !orders.Contains(order) || Frame.Dead || Frame.Destroyed ||
                Frame.Faction != Faction.OfPlayer || !ReferenceEquals(Installed(order.category), order.expectedInstalled)) return false;
            if (order.kind == FrameServiceOrderKind.Remove) return order.expectedInstalled != null;
            if (order.kind != FrameServiceOrderKind.Install) return false;
            bool stored = IsStoredCandidate(order.incoming, order.category);
            Pawn carrier = IncomingCarrier(order);
            if (carrier == null && order.id == activeOrderId && IsServiceLocked &&
                ReferenceEquals(activeWorker.carryTracker?.CarriedThing, order.incoming))
                carrier = activeWorker;
            bool carried = carrier != null;
            if (!stored && !carried) return false;
            if (!requireReachable) return true;
            if (worker == null) return false;
            if (carried) return ReferenceEquals(worker, carrier);
            return worker.CanReserveAndReach(order.incoming, PathEndMode.ClosestTouch, Danger.Some);
        }

        public FrameServiceOrder FirstAvailableOrder(Pawn worker)
        {
            if (worker == null || !worker.CanReserveAndReach(Frame, PathEndMode.Touch, Danger.Some)) return null;
            return orders.FirstOrDefault(order => !ReservedForContinuation(order, worker) &&
                OrderStillValid(order, true, worker));
        }

        public bool IsBlocked(FrameServiceOrder order) => order != null && OrderStillValid(order, false) &&
            !Frame.Map.mapPawns.FreeColonistsSpawned.Any(worker => !worker.Dead && !worker.Downed &&
                worker.CanReserveAndReach(Frame, PathEndMode.Touch, Danger.Some) &&
                !ReservedForContinuation(order, worker) && OrderStillValid(order, true, worker));

        public Job JobFor(FrameServiceOrder order)
        {
            if (order == null || !orders.Contains(order)) return null;
            Job job = JobMaker.MakeJob(ZenithJobDefOf.ZF_ServiceFrameModule, Frame);
            if (order.kind == FrameServiceOrderKind.Install) job.targetB = order.incoming;
            if (order.expectedInstalled != null) job.targetC = order.expectedInstalled;
            job.count = 1;
            return job;
        }

        public FrameServiceOrder OrderForJob(Job job) => orders.FirstOrDefault(order =>
            (order.kind == FrameServiceOrderKind.Install) == job.targetB.IsValid &&
            ReferenceEquals(order.incoming, job.targetB.Thing) &&
            ReferenceEquals(order.expectedInstalled, job.targetC.Thing));

        public bool ClaimModuleJob(Pawn worker, Job job, out int orderId)
        {
            orderId = 0;
            FrameServiceOrder order = OrderForJob(job);
            // Verse can request pre-toil reservations again for the same
            // running job after the first claim. That is not a new worker or
            // a competing operation, and CanBeServicedNow would see our own
            // lock and reject it.
            if (order != null && ReferenceEquals(activeWorker, worker) &&
                activeOrderId == order.id && IsServiceLocked)
            {
                orderId = order.id;
                return true;
            }
            if (order == null || ReservedForContinuation(order, worker) ||
                !OrderStillValid(order, false) || !CanBeServicedNow(out _)) return false;
            activeWorker = worker;
            activeOrderId = order.id;
            orderId = order.id;
            if (order.id == continuationOrderId) ClearContinuation();
            Frame.Cockpit?.StopFrame();
            return true;
        }

        public bool OwnsModuleJob(Pawn worker, int orderId) =>
            ReferenceEquals(activeWorker, worker) && activeOrderId == orderId &&
            OrderStillValid(orders.FirstOrDefault(order => order.id == orderId), false);

        public IEnumerable<Thing> StoredCandidates(FrameAddonCategory category)
        {
            if (!Frame.Spawned) yield break;
            foreach (Thing thing in Frame.Map.listerThings.AllThings
                .Where(thing => IsStoredCandidate(thing, category))
                .OrderBy(thing => thing.LabelCap).ThenBy(thing => thing.thingIDNumber))
                yield return thing;
        }

        public bool CompleteOrder(Pawn worker, int orderId, out Thing outgoing)
        {
            outgoing = null;
            FrameServiceOrder order = orders.FirstOrDefault(entry => entry.id == orderId);
            if (!OwnsModuleJob(worker, orderId)) return false;
            FrameAddonCategory category = order.category;
            if (order.kind == FrameServiceOrderKind.Remove)
            {
                bool removed = TryRemove(category, out outgoing);
                if (removed && ReferenceEquals(outgoing, order.expectedInstalled)) orders.Remove(order);
                return removed;
            }

            Thing incoming = order.incoming;
            if (!ReferenceEquals(worker.carryTracker?.CarriedThing, incoming)) return false;
            Thing installed = order.expectedInstalled;
            if (installed != null && !TryRemove(category, out outgoing)) return false;
            bool completed = false;
            try
            {
                if (!worker.carryTracker.TryDropCarriedThing(Frame.Position, ThingPlaceMode.Near, out Thing dropped) ||
                    !ReferenceEquals(dropped, incoming) || !TryInstall(category, incoming)) return false;
                orders.Remove(order);
                completed = true;
                return true;
            }
            finally
            {
                if (!completed && Installed(category) == null && outgoing?.Spawned == true)
                    TryInstall(category, outgoing);
            }
        }

        public Thing Installed(FrameAddonCategory category) => Frame.Loadout?.Installed(category);

        private bool TryInstall(FrameAddonCategory category, Thing thing) =>
            Frame.Loadout?.TryInstall(category, thing) == true;

        private bool TryRemove(FrameAddonCategory category, out Thing removed)
        {
            removed = null;
            return Frame.Loadout?.TryRemove(category, out removed) == true;
        }

        private bool IsStoredCandidate(Thing thing, FrameAddonCategory category)
        {
            if (thing == null || thing.Destroyed || !thing.Spawned || thing.Map != Frame.Map || thing.IsForbidden(Faction.OfPlayer)) return false;
            if (thing.def.GetModExtension<FrameAddonExtension>()?.category != category || thing.stackCount != 1) return false;
            if (Frame.Loadout?.AcceptsDefinition(thing.def, category, out _) != true) return false;
            return StoreUtility.IsInValidStorage(thing);
        }

        private Pawn IncomingCarrier(FrameServiceOrder order)
        {
            if (order.kind != FrameServiceOrderKind.Install || order.incoming == null || !Frame.Spawned) return null;
            IEnumerable<Pawn> candidates = activeWorker != null
                ? new[] { activeWorker }.Concat(Frame.Map.mapPawns.AllPawnsSpawned.Where(pawn => !ReferenceEquals(pawn, activeWorker)))
                : Frame.Map.mapPawns.AllPawnsSpawned;
            return candidates.FirstOrDefault(worker => !worker.Destroyed &&
                ReferenceEquals(worker.carryTracker?.CarriedThing, order.incoming) &&
                worker.CurJob != null && IsFrameServiceJob(worker.CurJob.def) &&
                ReferenceEquals(worker.CurJob.targetA.Thing, Frame) &&
                ReferenceEquals(worker.CurJob.targetB.Thing, order.incoming));
        }

        public string PendingLabel => orders.Count == 0 ? null : string.Join(", ", orders.Select(order =>
            order.kind == FrameServiceOrderKind.Remove ? "ZF_RemoveCategory".Translate(FrameText.Category(order.category)).ToString() :
            "ZF_PendingInstall".Translate(order.incoming?.LabelCap ?? "ZF_InvalidModule".Translate()).ToString()));

        public override string CompInspectStringExtra()
        {
            string result = "ZF_InspectDocking".Translate(DockingBayUtility.IsDocked(Frame) ? "ZF_Docked".Translate() : "ZF_NotDocked".Translate());
            if (HasPendingOrder) result += "\n" + "ZF_InspectPendingService".Translate(PendingLabel);
            if (IsServiceLocked) result += "\n" + "ZF_InspectServiceWorker".Translate(activeWorker.LabelShortCap);
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
            Thing material = FindMaterial(pawn, frame, missing);
            if (missing == null || material == null) return null;
            Job job = JobMaker.MakeJob(ZenithJobDefOf.ZF_ReconstructFramePart, frame, material);
            job.count = FrameRepairUtility.ReconstructionCost(frame, missing.Part);
            // BodyDef is immutable chassis identity. This job-specific countQueue
            // stores its exact AllParts index; count owns the corresponding cost.
            job.countQueue = new List<int> { frame.RaceProps.body.AllParts.IndexOf(missing.Part) };
            job.playerForced = forced;
            return job;
        }

        private static Thing FindMaterial(Pawn worker, FramePawn frame, Hediff_MissingPart intended = null)
        {
            ThingDef material = frame.Service.Profile?.reconstructionMaterial ?? ThingDefOf.Steel;
            Hediff_MissingPart missing = intended ?? FrameRepairUtility.NextReconstructablePart(frame);
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
            FrameRepairUtility.BaseEligible(frame) && frame.Service.FirstAvailableOrder(pawn) != null && pawn.CanReserve(frame);

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            FramePawn frame = (FramePawn)t;
            Job job = frame.Service.JobFor(frame.Service.FirstAvailableOrder(pawn));
            if (job == null) return null;
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
            repair.handlingFacing = true;
            repair.activeSkill = () => SkillDefOf.Construction;
            repair.WithEffect(EffecterDefOf.ConstructMetal, TargetIndex.A);
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
            if (job.countQueue == null || job.countQueue.Count != 1) return false;
            if (partIndex < 0) partIndex = job.countQueue[0];
            if (materialCount <= 0) materialCount = job.count;
            if (Part == null || !FrameDamageRules.IsRecoverablePart(Part) ||
                !Frame.health.hediffSet.PartIsMissing(Part) || materialCount <= 0 ||
                materialCount != FrameRepairUtility.ReconstructionCost(Frame, Part)) return false;
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
                if (Part == null || !Frame.health.hediffSet.PartIsMissing(Part) ||
                    materialCount != FrameRepairUtility.ReconstructionCost(Frame, Part) || !BeginService())
                { EndJobWith(JobCondition.Incompletable); return; }
                if (workLeft <= 0f) workLeft = Part.def.GetMaxHealth(Frame) *
                    (Frame.Service.Profile?.reconstructionWorkPerPartHp ?? 30f);
            };
            reconstruct.tickAction = () => { workLeft -= Speed; LearnConstruction(); if (workLeft <= 0f) ReadyForNextToil(); };
            reconstruct.defaultCompleteMode = ToilCompleteMode.Never;
            yield return reconstruct;
            Toil complete = ToilMaker.MakeToil("CompleteFrameReconstruction");
            complete.initAction = () =>
            {
                Thing material = pawn.carryTracker?.CarriedThing;
                if (Part == null || !Frame.health.hediffSet.PartIsMissing(Part) || material == null ||
                    material.def != (Frame.Service.Profile?.reconstructionMaterial ?? ThingDefOf.Steel) || material.stackCount < materialCount)
                { EndJobWith(JobCondition.Incompletable); return; }
                if (material.stackCount == materialCount) material.Destroy(); else material.stackCount -= materialCount;
                Frame.health.RestorePart(Part);
                Frame.Loadout?.RefreshModuleEffects();
                Frame.Loadout?.TryReclaimRememberedWeapon();
            };
            complete.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return complete;
        }
    }

    public sealed class JobDriver_ServiceFrameModule : JobDriver_FrameServiceBase
    {
        private float workLeft;
        private float totalWork;
        private int orderId;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref workLeft, "frameModuleServiceWorkLeft");
            Scribe_Values.Look(ref totalWork, "frameModuleServiceTotalWork");
            Scribe_Values.Look(ref orderId, "frameModuleServiceOrderId");
        }
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (!base.TryMakePreToilReservations(errorOnFailed) ||
                (job.targetB.IsValid && !pawn.Reserve(job.targetB, job, 1, 1, null, errorOnFailed))) return false;
            if (orderId != 0 && Frame.Service.OwnsModuleJob(pawn, orderId)) return true;
            return Frame.Service.ClaimModuleJob(pawn, job, out orderId);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            AddFinishAction(condition => Frame?.Service?.EndModuleService(pawn, orderId));
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => Frame?.Service?.OwnsModuleJob(pawn, orderId) != true);
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
                if (!Frame.Service.OwnsModuleJob(pawn, orderId)) { EndJobWith(JobCondition.Incompletable); return; }
                if (totalWork <= 0f) totalWork = Frame.Service.Profile?.serviceWork ?? 600f;
                if (workLeft <= 0f) workLeft = totalWork;
            };
            service.tickAction = () => { workLeft -= Speed; LearnConstruction(); if (workLeft <= 0f) ReadyForNextToil(); };
            service.defaultCompleteMode = ToilCompleteMode.Never;
            service.WithProgressBar(TargetIndex.A, () => totalWork <= 0f ? 0f :
                Mathf.Clamp01(1f - workLeft / totalWork));
            yield return service;
            Toil complete = ToilMaker.MakeToil("CompleteFrameModuleService");
            complete.initAction = () =>
            {
                if (Frame.Service.CompleteOrder(pawn, orderId, out Thing outgoing))
                {
                    // The completed order no longer owns the service lock. Release it
                    // before the next queued job tries its pre-toil claim; otherwise
                    // CanBeServicedNow rejects the same worker's continuation.
                    Frame.Service.EndService(pawn);
                    FrameServiceOrder nextOrder = Frame.Service.FirstAvailableOrder(pawn);
                    Frame.Service.PreferContinuation(pawn, nextOrder);
                    Job next = Frame.Service.JobFor(nextOrder);
                    if (next != null)
                    {
                        // Pawn_JobTracker keeps manually queued continuations only
                        // when they are ordered work, even if the first job came
                        // from a WorkGiver.
                        next.playerForced = true;
                        pawn.jobs.jobQueue.EnqueueFirst(next);
                    }
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
