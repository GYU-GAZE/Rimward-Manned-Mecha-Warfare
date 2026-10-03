using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using System.Linq;

namespace ZenithFrame
{
    public sealed class CompProperties_PilotableMech : CompProperties
    {
        public CompProperties_PilotableMech() { compClass = typeof(CompPilotableMech); }
    }

    public sealed class CompPilotableMech : ThingComp, IThingHolder
    {
        private ThingOwner<Pawn> pilots;
        private Lord operationalLord;
        public CompPilotableMech() { pilots = new ThingOwner<Pawn>(this, oneStackOnly: true, contentsLookMode: LookMode.Deep); }
        public Pawn Frame => (Pawn)parent;
        public Pawn Pilot => pilots.Count == 0 ? null : pilots[0];
        public bool HasPilot => pilots.Count != 0;
        internal Lord OperationalLord => operationalLord ?? Frame.GetLord();
        public bool CanOperate => PilotRules.CanOperate(HasPilot,
            Frame.Spawned && !Frame.Dead && !Frame.Downed && !Frame.Destroyed && !Frame.InMentalState,
            Pilot != null && !Pilot.Dead && !Pilot.Downed && !Pilot.InMentalState,
            Pilot != null && Frame.Faction != null && Pilot.Faction == Frame.Faction);
        // IsColonistPlayerControlled becomes false while the pilot is held in
        // the cockpit because that Pawn is no longer spawned. Colonist status
        // and faction remain stable across containment and save/load.
        public bool CanTakePlayerOrders => CanOperate && Frame.Faction == Faction.OfPlayer &&
            Pilot?.IsColonist == true && !Pilot.IsPrisoner && !Pilot.IsSlave;
        public bool CanUseHostileCombatAI => CanOperate && Frame.Faction != null &&
            Frame.Faction.HostileTo(Faction.OfPlayer);
        public ThingOwner GetDirectlyHeldThings() => pilots;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, pilots);

        public bool CanBoard(Pawn pawn, bool requireAdjacent = true)
        {
            return pawn != null && pawn != Frame && !(pawn is FramePawn) && PilotRules.CanBoard(HasPilot,
                Frame.Spawned && !Frame.Dead && !Frame.Downed && !Frame.Destroyed,
                pawn.Spawned && pawn.RaceProps.Humanlike && pawn.IsColonistPlayerControlled && !pawn.Dead && !pawn.Downed && !pawn.InMentalState && !pawn.IsPrisoner && !pawn.IsSlave && pawn.carryTracker.CarriedThing == null,
                pawn.Faction == Frame.Faction && Frame.Faction == Faction.OfPlayer,
                pawn.Map == Frame.Map, pawn.holdingOwner != null && !(pawn.holdingOwner.Owner is Map),
                !requireAdjacent || pawn.Position.AdjacentTo8WayOrInside(Frame.Position));
        }

        public bool TryBoard(Pawn pawn)
        {
            if (!CanBoard(pawn)) return false;
            return TryStorePilot(pawn);
        }

        internal bool CanInstallPilot(Pawn pawn, bool requireAdjacent = true)
        {
            return pawn != null && pawn != Frame && !(pawn is FramePawn) && PilotRules.CanBoard(HasPilot,
                Frame.Spawned && !Frame.Dead && !Frame.Downed && !Frame.Destroyed,
                pawn.Spawned && pawn.RaceProps.Humanlike && !pawn.Dead && !pawn.Downed && !pawn.InMentalState && pawn.carryTracker.CarriedThing == null,
                Frame.Faction != null && pawn.Faction == Frame.Faction,
                pawn.Map == Frame.Map, pawn.holdingOwner != null && !(pawn.holdingOwner.Owner is Map),
                !requireAdjacent || pawn.Position.AdjacentTo8WayOrInside(Frame.Position));
        }

        internal bool TryInstallPilot(Pawn pawn)
        {
            if (!CanInstallPilot(pawn)) return false;
            return TryStorePilot(pawn);
        }

        internal bool TryInstallGeneratedPilot(Pawn pawn)
        {
            if (pawn == null || pawn == Frame || pawn is FramePawn || HasPilot ||
                Frame.Spawned || pawn.Spawned || Frame.Destroyed || pawn.Destroyed ||
                pawn.holdingOwner != null || !pawn.RaceProps.Humanlike || pawn.Dead ||
                Frame.Faction == null || pawn.Faction != Frame.Faction)
                return false;

            return pilots.TryAdd(pawn, false);
        }

        internal void RememberOperationalLord(Lord lord)
        {
            bool ownedAssaultFrame = Frame.GetLord() == lord;
            bool compatibleExitControl = lord?.LordJob is LordJob_ExitMapBest && Frame.Spawned &&
                Frame.Map == lord.Map && Frame.Faction == lord.faction;
            if (lord == null || !HasPilot || (!ownedAssaultFrame && !compatibleExitControl))
                throw new System.InvalidOperationException("ZENITH: cannot remember an incompatible operational Lord.");
            operationalLord = lord;
        }

        internal void Notify_ExitedMap() { operationalLord = null; }

        private bool TryStorePilot(Pawn pawn)
        {
            Map map = pawn.Map;
            IntVec3 position = pawn.Position;
            Rot4 rotation = pawn.Rotation;
            if (pawn.drafter != null) pawn.drafter.Drafted = false;
            pawn.pather.StopDead();
            pawn.DeSpawn();
            try
            {
                return pilots.TryAdd(pawn, false);
            }
            finally
            {
                // A rejected insertion or an exception must leave a reachable owner.
                // Exceptions still propagate; there is no silent catch-and-continue.
                if (!pawn.Spawned && pawn.holdingOwner == null && !pawn.Destroyed)
                    GenSpawn.Spawn(pawn, position, map, rotation);
            }
        }

        public bool TryDisembark()
        {
            if (!HasPilot || !Frame.Spawned) return false;
            if (!pilots.TryDrop(Pilot, Frame.Position, Frame.Map, ThingPlaceMode.Near, out Pawn _)) return false;
            StopFrame();
            return true;
        }

        public void StopFrame()
        {
            FrameAbilityContext.Cancel(Pilot);
            if (Frame.drafter != null) Frame.drafter.Drafted = false;
            Frame.pather?.StopDead();
            Frame.jobs?.StopAll();
            Frame.stances?.CancelBusyStanceHard();
            if (Frame.equipment?.PrimaryEq != null)
                foreach (Verb verb in Frame.equipment.PrimaryEq.AllVerbs)
                    verb.Reset();
        }

        public void EmergencyEject()
        {
            if (!HasPilot) return;
            if (Find.WorldPawns?.IsBeingDiscarded(Frame) == true && Frame.Faction != Faction.OfPlayer)
            {
                DiscardEscapedHostilePilot();
                return;
            }
            Lord raidLord = OperationalLord;
            Pawn exactPilot = Pilot;
            StopFrame();
            if (TryDisembark())
            {
                TransferRaidControlToEjectedPilot(raidLord, exactPilot);
                return;
            }
            // Off-map destruction has no spawn destination. Transfer to an independent,
            // deep-scribed game owner BEFORE the Frame's destruction is allowed to proceed.
            var recovery = Current.Game?.GetComponent<PilotRecovery>();
            if (recovery == null || !recovery.Accept(Pilot))
                throw new System.InvalidOperationException("ZENITH: cannot detach pilot safely; aborting Frame destruction.");
        }

        internal void PrepareForMapRemoval()
        {
            if (!HasPilot) return;
            Pawn exactPilot = Pilot;
            StopFrame();
            operationalLord = null;
            if (Frame.Faction == Faction.OfPlayer && exactPilot.Faction == Faction.OfPlayer && exactPilot.RaceProps.Humanlike &&
                !exactPilot.IsPrisoner && !exactPilot.IsSlave)
            {
                var recovery = Current.Game?.GetComponent<PilotRecovery>();
                if (recovery == null || !recovery.Accept(exactPilot))
                    throw new System.InvalidOperationException("ZENITH: map removal could not transfer the player pilot to recovery ownership.");
                return;
            }

            if (!pilots.Remove(exactPilot))
                throw new System.InvalidOperationException("ZENITH: map removal could not detach the non-player pilot.");
            Find.WorldPawns.PassToWorld(exactPilot);
        }

        private void DiscardEscapedHostilePilot()
        {
            Pawn exactPilot = Pilot;
            operationalLord = null;
            if (!pilots.Remove(exactPilot))
                throw new System.InvalidOperationException("ZENITH: hostile world cleanup could not detach its contained pilot.");
            if (!exactPilot.Destroyed) exactPilot.Destroy();
            if (Find.WorldPawns.Contains(exactPilot)) Find.WorldPawns.RemovePawn(exactPilot);
            if (!exactPilot.Discarded) exactPilot.Discard(silentlyRemoveReferences: true);
        }

        private void TransferRaidControlToEjectedPilot(Lord lord, Pawn pilot)
        {
            if (lord != null)
            {
                if (Frame.GetLord() == lord) lord.RemovePawn(Frame);
                if (pilot?.Spawned == true && !pilot.Downed && !pilot.Dead && pilot.Map == lord.Map && pilot.Faction == Frame.Faction && lord.CanAddPawn(pilot))
                    lord.AddPawn(pilot);
            }
            operationalLord = null;
        }

        public override void CompTick()
        {
            base.CompTick();
            Pawn exactPilot = Pilot;
            if (exactPilot != null && !exactPilot.Dead)
            {
                exactPilot.health.HealthTick();
                if (!exactPilot.Dead)
                    exactPilot.health.HealthTickInterval(1);
            }
            Lord currentLord = Frame.GetLord();
            bool preservingExplicitExitControl = operationalLord?.LordJob is LordJob_ExitMapBest &&
                currentLord != operationalLord;
            if (HasPilot && currentLord != null && !preservingExplicitExitControl)
                operationalLord = currentLord;
        }

        public override IEnumerable<Gizmo> CompGetGizmosExtra()
        {
            if (!(Frame is FramePawn frame) || frame.Faction != Faction.OfPlayer ||
                !frame.Spawned || !CanTakePlayerOrders || Pilot?.health?.HasHediffsNeedingTend() != true)
                yield break;

            Pawn exactPilot = Pilot;
            Medicine medicine = exactPilot.inventory?.innerContainer.OfType<Medicine>()
                .Where(item => !item.Destroyed && item.stackCount > 0)
                .OrderByDescending(item => item.def.GetStatValueAbstract(StatDefOf.MedicalPotency))
                .FirstOrDefault();
            var command = new Command_Action
            {
                defaultLabel = "Pilot self-tend",
                defaultDesc = medicine == null
                    ? "The contained pilot tends their real wounds without medicine."
                    : "The contained pilot tends their real wounds using " + medicine.LabelCap + " from their own inventory.",
                icon = TexCommand.Install,
                action = () => StartPilotSelfTend(frame, exactPilot, medicine)
            };
            yield return command;
        }

        private static void StartPilotSelfTend(FramePawn frame, Pawn exactPilot, Medicine medicine)
        {
            if (!ReferenceEquals(frame.Cockpit?.Pilot, exactPilot) || !frame.Cockpit.CanTakePlayerOrders ||
                exactPilot.health?.HasHediffsNeedingTend() != true ||
                (medicine != null && (medicine.Destroyed || exactPilot.inventory?.innerContainer.Contains(medicine) != true)))
                return;
            frame.Cockpit.StopFrame();
            Job job = JobMaker.MakeJob(ZenithJobDefOf.ZF_FramePilotSelfTend, frame);
            job.targetC = exactPilot;
            if (medicine != null) job.targetB = medicine;
            job.playerForced = true;
            frame.jobs.TryTakeOrderedJob(job);
        }

        public override void Notify_Downed() { EmergencyEject(); }
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Deep.Look(ref pilots, "zenithPilot", this);
            Scribe_References.Look(ref operationalLord, "zenithOperationalLord");
        }
        public override string CompInspectStringExtra() => HasPilot ? "Pilot: " + Pilot.LabelShortCap : "No pilot - Frame inactive";
    }
}
