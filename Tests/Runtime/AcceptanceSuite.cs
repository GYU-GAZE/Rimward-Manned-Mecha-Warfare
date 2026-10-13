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
    public interface IFrameworkProbe
    {
        void Give(Pawn pilot);
        bool Start(Pawn pilot, Pawn target);
        bool Applied(Pawn target);
        bool Owned(Pawn pilot, FramePawn frame);
        bool Blocked(Pawn pilot);
    }

    public sealed class TemporalProbe : ThingComp
    {
        public int ticks, intervalTicks;
        public override void CompTick() => ticks++;
        public override void CompTickInterval(int delta) => intervalTicks += delta;
    }
    public sealed class TemporalProbeProps : CompProperties
    { public TemporalProbeProps() { compClass = typeof(TemporalProbe); } }
    public sealed class TemporalGene : Gene
    {
        public int ticks;
        public override void Tick() { base.Tick(); ticks++; }
    }
    public sealed class TransferProbeException : Exception { }
    public sealed class ThrowOnceOnDespawn : ThingComp
    {
        public bool armed = true;
        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        { if (armed) { armed = false; throw new TransferProbeException(); } }
    }
    public sealed class CompProperties_RecordOrigin : CompProperties_AbilityEffect
    { public CompProperties_RecordOrigin() { compClass = typeof(CompAbilityEffect_RecordOrigin); } }
    public sealed class CompAbilityEffect_RecordOrigin : CompAbilityEffect
    {
        public static Pawn owner, actor;
        public static IntVec3 origin, target;
        public override void Apply(LocalTargetInfo chosen, LocalTargetInfo dest)
        {
            owner = parent.pawn;
            actor = parent.verb.CasterPawn;
            origin = actor.Position;
            target = chosen.Cell;
        }
    }

    // One sequential, real-game architecture acceptance. No phase replay/matrix.
    public sealed class AcceptanceSuite : GameComponent
    {
        public static IFrameworkProbe Framework;
        private FramePawn frame, spare, hostile;
        private Pawn pilot, otherPilot, worker, enemy;
        private Map map;
        private Ability self, targeted, ground, moduleAbility;
        private Thing module, shield, sword, cancelled, blocked;
        private ThingWithComps removedWeapon;
        private float meleeBefore, frameBefore, spareBefore;
        private FrameServiceOrder blockedOrder;
        private TemporalProbe pilotProbe, inventoryProbe;
        private TemporalGene gene;
        private int enteredAt;
        private float initialFood, initialEntropy, initialFocus, initialBloodLoss;
        private Func<bool> ready;
        private Action continuation, gui;
        private int deadline;
        private string phase;
        private bool started, finished;
        private int uiPhase, guiFrames;
        private Window popup;
        private bool closePopup;
        private bool Enabled => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance-suite.txt"));
        private string Report => Path.Combine(GenFilePaths.SaveDataFolderPath, "acceptance.txt");
        public AcceptanceSuite(Game game) { }

        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message + " [" + phase + "]");
            File.AppendAllText(Report, "PASS " + message + Environment.NewLine);
        }
        private void Wait(string name, Func<bool> condition, Action next)
        {
            phase = name; ready = condition; continuation = next;
            deadline = Find.TickManager.TicksGame + 12000;
        }
        private FramePawn NewFrame(IntVec3 cell) => (FramePawn)GenSpawn.Spawn(
            PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), cell, map);
        private Pawn NewColonist(IntVec3 cell) => (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(
            new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                canGeneratePawnRelations: false, mustBeCapableOfViolence: true)), cell, map);
        private Thing Item(string name, IntVec3 cell) => GenSpawn.Spawn(ThingMaker.MakeThing(
            DefDatabase<ThingDef>.GetNamed(name)), cell, map);
        private bool Has(Pawn pawn, string name) => pawn.health.hediffSet.HasHediff(DefDatabase<HediffDef>.GetNamed(name));
        private void Clear(Pawn pawn, string name)
        {
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.Where(h => h.def.defName == name).ToList())
                pawn.health.RemoveHediff(hediff);
        }
        private void Stop(FramePawn machine)
        {
            machine.Cockpit.StopFrame();
            machine.jobs.StartJob(JobMaker.MakeJob(ZenithJobDefOf.ZF_FrameIdleJob), JobCondition.InterruptForced);
        }

        private void Start()
        {
            started = true;
            File.WriteAllText(Report, "ZENITH compatibility-first architecture acceptance" + Environment.NewLine);
            map = Find.CurrentMap;
            IntVec3 center = map.Center;
            // Dispose only generated disposable arena obstacles, never user saves.
            foreach (IntVec3 cell in CellRect.CenteredOn(center, 27).Cells)
            {
                foreach (Thing thing in cell.GetThingList(map).ToList())
                    if (!(thing is Pawn) && (thing.def.category == ThingCategory.Building || thing is Plant))
                        thing.Destroy(DestroyMode.Vanish);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Concrete);
                map.roofGrid.SetRoof(cell, null);
                map.fogGrid.Unfog(cell);
            }
            foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned.ToList())
                if (pawn.Position.InHorDistOf(center, 27f))
                {
                    pawn.jobs?.StopAll();
                    pawn.DeSpawn();
                    GenSpawn.Spawn(pawn, CellFinder.StandableCellNear(center + IntVec3.West * 45, map, 15), map);
                    if (pawn.drafter != null) { pawn.drafter.Drafted = true; pawn.drafter.FireAtWill = false; }
                }
            FrameChassisVisualExtension defaults = DefDatabase<ThingDef>.GetNamed("ZF_DebugFrame")
                .GetModExtension<FrameChassisVisualExtension>();
            Color oldPrimary = defaults.defaultPrimary;
            try
            {
                defaults.defaultPrimary = new Color(0.2f, 0.4f, 0.6f);
                frame = NewFrame(center);
                Check(((Color32)frame.Livery.Primary).Equals((Color32)defaults.defaultPrimary),
                    "fresh chassis initializes non-white authored paint");
            }
            finally { defaults.defaultPrimary = oldPrimary; }
            Color customized = new Color(0.6f, 0.2f, 0.4f);
            frame.Livery.SetColors(customized, Color.white, Color.cyan);
            frame.Livery.PostSpawnSetup(true);
            Check(((Color32)frame.Livery.Primary).Equals((Color32)customized), "spawn/load initialization preserves customized paint");
            File.AppendAllText(Report, "Physical Frame comps: " + string.Join(", ", frame.AllComps.Select(c => c.GetType().FullName)) + Environment.NewLine);
            spare = NewFrame(center + IntVec3.East * 5);
            pilot = NewColonist(center + IntVec3.South);
            otherPilot = NewColonist(spare.Position + IntVec3.South);
            worker = NewColonist(center + IntVec3.North * 3);
            worker.skills.GetSkill(SkillDefOf.Construction).Level = 12;
            foreach (WorkTypeDef type in DefDatabase<WorkTypeDef>.AllDefsListForReading)
                worker.workSettings.SetPriority(type, 0);
            foreach (BodyPartRecord part in pilot.health.hediffSet.GetNotMissingParts().Where(p => p.def == BodyPartDefOf.Arm).Take(1))
            {
                Hediff cut = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Cut"), pilot, part);
                cut.Severity = 1f; pilot.health.AddHediff(cut, part);
            }
            pilotProbe = new TemporalProbe { parent = pilot, props = new TemporalProbeProps() };
            pilot.AllComps.Add(pilotProbe);
            Thing clock = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_TestTemporalItem"));
            Check(pilot.inventory.innerContainer.TryAdd(clock), "inventory temporal item has exact pilot owner");
            inventoryProbe = clock.TryGetComp<TemporalProbe>();
            if (ModsConfig.BiotechActive)
                gene = (TemporalGene)pilot.genes.AddGene(DefDatabase<GeneDef>.GetNamed("ZF_TestTemporalGene"), false);
            if (ModsConfig.RoyaltyActive)
            {
                Hediff_Psylink link = (Hediff_Psylink)HediffMaker.MakeHediff(HediffDefOf.PsychicAmplifier, pilot);
                pilot.health.AddHediff(link, pilot.health.hediffSet.GetBrain());
                link.ChangeLevel(1);
                pilot.psychicEntropy.OffsetPsyfocusDirectly(0.5f - pilot.psychicEntropy.CurrentPsyfocus);
                Check(pilot.psychicEntropy.TryAddEntropy(10f), "pilot psychic entropy seeded through normal tracker");
            }
            self = Give(pilot, "ZF_TestPilotSelf");
            targeted = Give(pilot, "ZF_TestPilotTarget");
            ground = Give(pilot, "ZF_TestPilotLocation");
            Framework?.Give(pilot);
            pilot.needs.food.CurLevel = 0.8f;
            Check(frame.Cockpit.TryBoard(pilot) && spare.Cockpit.TryBoard(otherPilot), "two exact real pilots board physical Frames");
            enteredAt = Find.TickManager.TicksGame;
            initialFood = pilot.needs.food.CurLevel;
            initialEntropy = pilot.psychicEntropy?.EntropyValue ?? 0f;
            initialFocus = pilot.psychicEntropy?.CurrentPsyfocus ?? 0f;
            initialBloodLoss = pilot.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
            Check(frame.SetDrafted(true) && spare.SetDrafted(true), "ordinary Frame drafter operates without drafting contained pilots");
            frame.drafter.FireAtWill = false; spare.drafter.FireAtWill = false;
            self.QueueCastingJob(pilot, LocalTargetInfo.Invalid);
            Check(ReferenceEquals(frame.CurJob?.ability, self) && ReferenceEquals(self.verb.CasterPawn, frame),
                "original pilot Ability job and verb execute on Frame");
            Wait("pilot first cast", () => Has(pilot, "ZF_TestPilotSelfEffect"), PilotFirst);
        }
        private Ability Give(Pawn owner, string name)
        {
            if (owner.abilities == null) owner.abilities = new Pawn_AbilityTracker(owner);
            owner.abilities.GainAbility(DefDatabase<AbilityDef>.GetNamed(name));
            Ability ability = owner.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed(name));
            if (FrameAbilityContext.TryGetOperationalFrame(owner, out _)) FrameAbilityContext.BindVanillaAbility(ability);
            return ability;
        }
        private void PilotFirst()
        {
            Check(!Has(frame, "ZF_TestPilotSelfEffect") && self.CooldownTicksRemaining > 0, "self effect and cooldown belong only to exact pilot");
            Wait("pilot temporal simulation", () => Find.TickManager.TicksGame - enteredAt >= 450 &&
                self.CanCast.Accepted, PilotSecond);
        }
        private void PilotSecond()
        {
            int elapsed = Find.TickManager.TicksGame - enteredAt;
            Check(ReferenceEquals(frame.Cockpit.Pilot, pilot) && !pilot.Spawned && pilot.Map == null &&
                pilot.CurJob == null && !pilot.pather.Moving && !pilot.Drafted &&
                !map.mapPawns.AllPawnsSpawned.Contains(pilot), "contained pilot remains unspawned, non-pathing, non-job-running and single-owned");
            Check(pilotProbe.ticks >= elapsed - 2 && pilotProbe.ticks <= elapsed + 2 && pilotProbe.intervalTicks > 0,
                "ordinary unknown Pawn comp ticks once and receives interval simulation while contained");
            Check(inventoryProbe.ticks > 0, "pilot inventory-held Thing receives normal ticking");
            Check(pilot.needs.food.CurLevel < initialFood, "ordinary pilot food need progresses while contained");
            Check((pilot.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f) > initialBloodLoss,
                "real pilot bleeding progresses through normal Pawn health");
            if (gene != null) Check(gene.ticks > 0, "pilot gene receives normal tick while contained");
            if (ModsConfig.RoyaltyActive)
            {
                Check(pilot.psychicEntropy.EntropyValue < initialEntropy, "psychic entropy recovers on exact contained pilot");
                Check(Mathf.Abs(pilot.psychicEntropy.CurrentPsyfocus - initialFocus) < 0.0001f &&
                    !pilot.psychicEntropy.NeedsPsyfocus, "psyfocus follows vanilla unspawned-container pause rule");
            }
            Clear(pilot, "ZF_TestPilotSelfEffect");
            self.QueueCastingJob(pilot, LocalTargetInfo.Invalid);
            Wait("pilot second cast", () => Has(pilot, "ZF_TestPilotSelfEffect"), () =>
            {
                Check(self.CooldownTicksRemaining > 0 && ReferenceEquals(self.pawn, pilot), "pilot cooldown actually expires and second cast executes");
                enemy = (Pawn)GenSpawn.Spawn(PawnGenerator.GeneratePawn(new PawnGenerationRequest(
                    PawnKindDefOf.Colonist, Faction.OfPlayer, canGeneratePawnRelations: false)),
                    frame.Position + IntVec3.North * 8, map);
                FrameAbilityContext.BindVanillaAbility(targeted);
                targeted.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
                Wait("targeted pilot cast", () => Has(enemy, "ZF_TestPilotTargetEffect"), RoyaltyCast);
            });
        }
        private void RoyaltyCast()
        {
            if (!ModsConfig.RoyaltyActive) { Ground(); return; }
            Ability psycast = Give(pilot, "Stun");
            float entropy = pilot.psychicEntropy.EntropyValue;
            psycast.QueueCastingJob(enemy, LocalTargetInfo.Invalid);
            Wait("Royalty physical cast", () => enemy.stances.stunner.Stunned, () =>
            {
                Check(psycast is Psycast && ReferenceEquals(psycast.pawn, pilot) &&
                    ReferenceEquals(psycast.verb.CasterPawn, frame) && pilot.Map == null &&
                    pilot.psychicEntropy.EntropyValue > entropy,
                    "real Royalty psycast originates at Frame and charges exact contained pilot entropy");
                Ground();
            });
        }
        private void Ground()
        {
            Check(ReferenceEquals(targeted.pawn, pilot) && ReferenceEquals(targeted.verb.CasterPawn, frame) &&
                !frame.abilities.AllAbilitiesForReading.Contains(targeted), "targeted ability uses physical Frame origin without copying pilot ability");
            FrameAbilityContext.BindVanillaAbility(ground);
            IntVec3 cell = frame.Position + IntVec3.North * 4;
            ground.QueueCastingJob(cell, LocalTargetInfo.Invalid);
            Wait("ground pilot cast", () => CompAbilityEffect_RecordOrigin.owner != null, () =>
            {
                Check(ReferenceEquals(CompAbilityEffect_RecordOrigin.owner, pilot) &&
                    ReferenceEquals(CompAbilityEffect_RecordOrigin.actor, frame) &&
                    CompAbilityEffect_RecordOrigin.origin == frame.Position && CompAbilityEffect_RecordOrigin.target == cell,
                    "ground ability records exact logical owner and real Frame origin");
                if (Framework != null)
                {
                    Check(Framework.Owned(pilot, frame) && Framework.Start(pilot, enemy), "VEF framework ability keeps exact pilot state and starts Frame job");
                    Wait("VEF cast", () => Framework.Applied(enemy), Modules);
                }
                else Modules();
            });
        }
        private void Modules()
        {
            Stop(frame); frame.SetDrafted(true);
            module = Item("ZF_DebugShoulderSystem", frame.Position + IntVec3.West);
            Check(frame.Loadout.TryInstallShoulder(module), "install exact ticking Shoulder module");
            frame.SetDrafted(true);
            moduleAbility = module.TryGetComp<CompFrameModuleAbility>().Ability;
            moduleAbility.QueueCastingJob(frame, LocalTargetInfo.Invalid);
            Wait("module first cast", () => Has(frame, "ZF_ShoulderBurstEffect"), () =>
            {
                int cooldown = moduleAbility.CooldownTicksRemaining;
                Check(cooldown > 0 && !frame.abilities.AllAbilitiesForReading.Contains(moduleAbility),
                    "module owns exact Ability outside chassis tracker");
                Check(frame.Loadout.TryRemoveShoulder(out Thing removed) && ReferenceEquals(removed, module) &&
                    spare.Loadout.TryInstallShoulder(module), "physical module transfer preserves exact Thing identity");
                spare.SetDrafted(true);
                Check(ReferenceEquals(module.TryGetComp<CompFrameModuleAbility>().Ability, moduleAbility) &&
                    moduleAbility.CooldownTicksRemaining <= cooldown, "module transfer retains original Ability and cooldown timestamp");
                Wait("module cooldown expiration", () => moduleAbility.CanCast.Accepted, () =>
                {
                    moduleAbility.QueueCastingJob(spare, LocalTargetInfo.Invalid);
                    Wait("module second cast", () => Has(spare, "ZF_ShoulderBurstEffect"), Transactions);
                });
            });
        }
        private Zone_Stockpile Storage(IntVec3 cell)
        {
            Zone_Stockpile zone = new Zone_Stockpile(StorageSettingsPreset.DefaultStockpile, map.zoneManager);
            zone.settings.filter.SetAllowAll(null);
            map.zoneManager.RegisterZone(zone); zone.AddCell(cell);
            return zone;
        }
        private void BeginOrder()
        {
            FrameServiceOrder next = frame.Service.FirstAvailableOrder(worker);
            Check(next != null, "service finds next executable exact queued order");
            worker.jobs.TryTakeOrderedJob(frame.Service.JobFor(next));
        }
        private void Transactions()
        {
            Check(moduleAbility.CooldownTicksRemaining > 0 && ReferenceEquals(moduleAbility.pawn, spare), "module second cast really executes on new Frame");
            Check(spare.Loadout.TryRemoveShoulder(out Thing removed) && ReferenceEquals(removed, module) &&
                frame.Loadout.TryInstallShoulder(module), "exact module returns from transfer with its state");
            Stop(frame);
            Thing faulty = Item("ZF_DebugSensorHelmet", frame.Position + IntVec3.West * 3);
            ((ThingWithComps)faulty).AllComps.Add(new ThrowOnceOnDespawn { parent = (ThingWithComps)faulty });
            bool threw = false;
            try { frame.Loadout.TryInstallHead(faulty); }
            catch (TransferProbeException) { threw = true; }
            Check(threw && faulty.Spawned && faulty.Map == map && frame.Loadout.InstalledHead == null,
                "external despawn exception restores exact module to map without swallowing exception");
            ThingWithComps humanWeapon = (ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("Gun_Autopistol"));
            bool rejected = false;
            try { frame.equipment.AddEquipment(humanWeapon); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && humanWeapon.holdingOwner == null && frame.equipment.Primary.def.defName != "Gun_Autopistol",
                "direct equipment API rejects unmarked human weapon before ownership mutation");
            humanWeapon.Destroy();
            IntVec3 sealedCell = frame.Position + IntVec3.West * 15;
            Storage(sealedCell);
            blocked = Item("ZF_DebugSensorHelmet", sealedCell);
            foreach (IntVec3 wallCell in CellRect.CenteredOn(sealedCell, 2).EdgeCells)
                GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.BlocksGranite), wallCell, map).SetFaction(Faction.OfPlayer);
            IntVec3 storageCell = frame.Position + IntVec3.North * 5;
            Storage(storageCell);
            shield = Item("ZF_DebugShieldOffhand", storageCell);
            Check(frame.Service.QueueInstall(FrameAddonCategory.Head, blocked) &&
                frame.Service.QueueInstall(FrameAddonCategory.Offhand, shield), "queue preserves two distinct exact module requests");
            blockedOrder = frame.Service.OrderFor(FrameAddonCategory.Head);
            Wait("blocked queue geometry", () => Find.TickManager.TicksGame % 120 == 0, () =>
            {
                Check(!worker.CanReach(blocked, PathEndMode.ClosestTouch, Danger.Some) &&
                    frame.Service.FirstAvailableOrder(worker)?.incoming == shield,
                    "inaccessible earlier order stays queued while later executable order is selected");
                BeginOrder();
                Wait("install service", () => frame.Loadout.InstalledOffhand == shield, () =>
                {
                    Check(frame.Service.Orders.Contains(blockedOrder) && ReferenceEquals(blockedOrder.incoming, blocked),
                        "completed later service preserves blocked order identity and enqueue preference");
                    sword = Item("ZF_DebugBeamSwordOffhand", storageCell);
                    Check(frame.Service.QueueInstall(FrameAddonCategory.Offhand, sword), "queue exact replacement module");
                    Wait("replacement readiness", () => frame.Service.CanBeServicedNow(out _), () =>
                    {
                        BeginOrder(); Wait("replace service", () => frame.Loadout.InstalledOffhand == sword, () =>
                        {
                            Check(!shield.Destroyed && (shield.Spawned || shield.holdingOwner != null),
                                "replacement preserves outgoing exact module in a physical owner");
                            Check(frame.Service.QueueRemove(FrameAddonCategory.Offhand), "queue exact module removal");
                            Wait("remove readiness", () => frame.Service.CanBeServicedNow(out _), () =>
                            {
                                BeginOrder(); Wait("remove service", () => frame.Loadout.InstalledOffhand == null, CancelOrder);
                            });
                        });
                    });
                });
            });
        }
        private void CancelOrder()
        {
            Check(!sword.Destroyed && (sword.Spawned || sword.holdingOwner != null), "removal preserves exact physical module");
            IntVec3 cell = frame.Position + IntVec3.North * 6; Storage(cell);
            cancelled = Item("ZF_DebugMobilityBooster", cell);
            Check(frame.Service.QueueInstall(FrameAddonCategory.Utility, cancelled), "queue cancellable exact module");
            Wait("cancel readiness", () => frame.Service.CanBeServicedNow(out _), () =>
            {
                BeginOrder(); Wait("active cancellable service", () => frame.Service.IsServiceLocked, () =>
                {
                    Check(frame.Service.CancelOrder(FrameAddonCategory.Utility) &&
                        !cancelled.Destroyed && (cancelled.Spawned || cancelled.holdingOwner != null),
                        "active service cancellation preserves exact incoming Thing and releases lock");
                    frame.Service.CancelOrder(FrameAddonCategory.Head);
                    ServiceLock();
                });
            });
        }
        private void ServiceLock()
        {
            Stop(frame);
            ReconstructionBinding();
            Hediff injury = HediffMaker.MakeHediff(DefDatabase<HediffDef>.GetNamed("Bruise"), frame,
                FrameDamageRules.FindPart(frame, ZenithBodyPartDefOf.ZF_FrameTorso));
            injury.Severity = 10f; frame.health.AddHediff(injury, injury.Part);
            worker.jobs.TryTakeOrderedJob(JobMaker.MakeJob(ZenithJobDefOf.ZF_RepairFrame, frame));
            Wait("real repair lock", () => frame.Service.IsServiceLocked, () =>
            {
                Check(frame.Cockpit.CanOperate && !frame.Cockpit.CanExecuteActions &&
                    ReferenceEquals(frame.Cockpit.Pilot, pilot), "service separates lifecycle validity from action authority and preserves pilot");
                Check(!frame.abilities.AllAbilitiesForReading.First().CanCast.Accepted &&
                    !moduleAbility.CanCast.Accepted && !self.CanCast.Accepted &&
                    !frame.meleeVerbs.TryMeleeAttack(enemy) &&
                    !frame.equipment.PrimaryEq.PrimaryVerb.TryStartCastOn(enemy) &&
                    !frame.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Goto, frame.Position + IntVec3.East)),
                    "service blocks intrinsic module pilot ability melee ranged and movement execution families");
                if (Framework != null) Check(Framework.Blocked(pilot), "service blocks framework ability without ejecting logical owner");
                worker.jobs.EndCurrentJob(JobCondition.InterruptForced);
                Wait("repair cancellation release", () => !frame.Service.IsServiceLocked, SwapAndCombat);
            });
        }
        private void ReconstructionBinding()
        {
            BodyPartRecord[] parts = frame.RaceProps.body.AllParts
                .Where(part => part.def.defName == "ZF_FrameManipulator").Take(2).ToArray();
            Check(parts.Length == 2, "reconstruction fixture has two independent hand parts");
            foreach (BodyPartRecord part in parts) frame.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            Thing material = GenSpawn.Spawn(ThingMaker.MakeThing(ThingDefOf.Steel), frame.Position + IntVec3.North * 4, map);
            material.stackCount = 100;
            Job reconstruction = new WorkGiver_ReconstructFrame().JobOnThing(worker, frame, true);
            Check(reconstruction != null && reconstruction.countQueue.Count == 1,
                "reconstruction job binds exact target body index at material selection");
            BodyPartRecord intended = frame.RaceProps.body.AllParts[reconstruction.countQueue[0]];
            Check(reconstruction.count == FrameRepairUtility.ReconstructionCost(frame, intended),
                "bound reconstruction part owns the exact material cost");
            frame.health.RestorePart(intended);
            Check(!reconstruction.GetCachedDriver(worker).TryMakePreToilReservations(false),
                "invalidated reconstruction target fails instead of choosing another missing part");
            foreach (BodyPartRecord part in parts) frame.health.RestorePart(part);
        }
        private void MovePilot(Pawn pawn, IntVec3 cell)
        {
            pawn.DeSpawn(); GenSpawn.Spawn(pawn, cell, map);
        }
        private void SwapAndCombat()
        {
            Check(frame.Cockpit.CanExecuteActions && ReferenceEquals(frame.Cockpit.Pilot, pilot) &&
                self.CanCast.Accepted && moduleAbility.CanCast.Accepted && frame.abilities.AllAbilitiesForReading.First().CanCast.Accepted,
                "service completion releases shared action lock with original pilot intact");
            Check(!frame.jobs.TryTakeOrderedJob(JobMaker.MakeJob(JobDefOf.Ingest, cancelled)),
                "ordinary survival/labor job remains rejected at authoritative boundary");
            gui = () =>
            {
                frame.SetDrafted(true);
                frame.GetGizmos().ToList();
                Find.Targeter.BeginTargeting(self.verb);
                Check(frame.Cockpit.TryDisembark() && !Find.Targeter.IsTargeting &&
                    ReferenceEquals(self.verb.CasterPawn, pilot), "disembark safely cancels targeting and restores original ability caster");
                Check(spare.Cockpit.TryDisembark(), "second exact pilot disembarks for swap");
                MovePilot(otherPilot, frame.Position + IntVec3.South);
                MovePilot(pilot, spare.Position + IntVec3.South);
                Check(frame.Cockpit.TryBoard(otherPilot) && spare.Cockpit.TryBoard(pilot), "pilot swap preserves both original identities");
                frame.SetDrafted(true); spare.SetDrafted(true);
                Check(!frame.GetGizmos().OfType<Command_Ability>().Any(c => ReferenceEquals(c.Ability, self)) &&
                    spare.GetGizmos().OfType<Command_Ability>().Any(c => ReferenceEquals(c.Ability, self)),
                    "accessible pilot ability commands follow exact cockpit ownership immediately");
                Check(frame.Loadout.TryRemoveWeapon(out removedWeapon) && frame.equipment.Primary == null &&
                    frame.meleeVerbs.TryGetMeleeVerb(enemy) != null, "unarmed Frame retains ordinary native melee verb");
                frame.SetDrafted(true);
                meleeBefore = enemy.health.summaryHealth.SummaryHealthPercent;
                Job attack = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
                attack.playerForced = true;
                Check(frame.jobs.TryTakeOrderedJob(attack), "unarmed Frame accepts ordinary AttackMelee job");
                Wait("physical unarmed melee", () => enemy.health.summaryHealth.SummaryHealthPercent < meleeBefore, BeginHostile);
            };
        }
        private void BeginHostile()
        {
                Check(frame.Loadout.TryInstallWeapon(removedWeapon), "unarmed melee deals damage and exact primary weapon reinstalls");
                frame.SetDrafted(true);
                hostile = FrameDebugActions.SpawnHostilePilotedFrame(frame.Position + IntVec3.South * 10, map);
                Give(hostile.Cockpit.Pilot, "ZF_TestPilotTarget");
                IAttackTarget frameTarget = AttackTargetFinder.BestAttackTarget(hostile,
                    TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                    target => ReferenceEquals(target, frame), 0f, 65f);
                IAttackTarget infantryTarget = AttackTargetFinder.BestAttackTarget(hostile,
                    TargetScanFlags.NeedThreat | TargetScanFlags.NeedAutoTargetable,
                    target => ReferenceEquals(target, worker), 0f, 65f);
                Check(frameTarget?.Thing == frame && infantryTarget?.Thing == worker,
                    "ordinary hostile target finder recognizes player Frame and infantry");
                Wait("hostile pilot ability", () => Has(frame, "ZF_TestPilotTargetEffect") || Has(spare, "ZF_TestPilotTargetEffect"), () =>
                {
                    Check(hostile.Cockpit.Pilot.abilities.GetAbility(DefDatabase<AbilityDef>.GetNamed("ZF_TestPilotTarget")).CooldownTicksRemaining > 0,
                        "hostile AI executes exact pilot-owned ability against a physical player Frame");
                    frameBefore = frame.health.summaryHealth.SummaryHealthPercent;
                    spareBefore = spare.health.summaryHealth.SummaryHealthPercent;
                    Wait("hostile physical ranged combat", () => frame.health.summaryHealth.SummaryHealthPercent < frameBefore ||
                        spare.health.summaryHealth.SummaryHealthPercent < spareBefore, BeginUI);
                });
        }
        private void BeginUI()
        {
            Check(hostile.Cockpit.CanUseHostileCombatAI && hostile.CurJob?.ability == null,
                "hostile Frame enters ordinary physical combat job/verb pipeline");
            hostile.Cockpit.StopFrame();
            // UI sees the original pilot now in spare. Core layout is unchanged.
            gui = () => { Find.Selector.ClearSelection(); Find.Selector.Select(spare);
                InspectPaneUtility.OpenTab(typeof(ITab_FrameVisual)); uiPhase = 1; };
            ready = null; continuation = null;
        }
        public override void GameComponentOnGUI()
        {
            if (!Enabled || finished) return;
            try
            {
                if (gui != null) { Action action = gui; gui = null; action(); }
                if (Event.current.type != EventType.Repaint || uiPhase == 0 || ++guiFrames < 5) return;
                guiFrames = 0;
                if (closePopup) { popup.Close(); popup = null; closePopup = false; }
                if (uiPhase == 9) { Complete(); return; }
                if (uiPhase == 1) popup = new Dialog_FrameColorPicker(spare, FrameColorChannel.Primary);
                else if (uiPhase == 3) popup = new Dialog_FramePawnInfo(pilot, true);
                else if (uiPhase == 5) popup = new Dialog_FramePawnInfo(spare, false);
                else if (uiPhase == 7) popup = new Dialog_FramePawnInfo(pilot, true, FrameInfoPage.Health);
                else
                {
                    ScreenCapture.CaptureScreenshot(Path.Combine(GenFilePaths.SaveDataFolderPath, "architecture-popup-" + uiPhase + ".png"));
                    closePopup = true; uiPhase++; return;
                }
                Find.WindowStack.Add(popup);
                Rect tab = ITab_FrameVisual.CurrentScreenRect();
                Check(popup.windowRect.x >= tab.xMax + FramePopupPlacement.Gap &&
                    popup.windowRect.xMax <= UI.screenWidth - 8f && popup.windowRect.yMax <= UI.screenHeight - 8f,
                    "popup " + uiPhase + " anchors beside actual rendered Frame tab within screen");
                uiPhase++;
            }
            catch (Exception ex) { Fail(ex); }
        }
        private void Complete()
        {
            File.AppendAllText(Report, "PASS acceptance complete" + Environment.NewLine);
            finished = true; Application.Quit(0);
        }
        public override void GameComponentUpdate()
        {
            if (!Enabled || finished || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || Find.TickManager.TicksGame < 1000) return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (!started) { Start(); return; }
                if (ready != null && Find.TickManager.TicksGame > deadline)
                    throw new TimeoutException(phase + "; job=" + frame.CurJob + "; worker=" + worker.CurJob);
                if (ready?.Invoke() == true)
                { Action next = continuation; ready = null; continuation = null; next(); }
            }
            catch (Exception ex) { Fail(ex); }
        }
        private void Fail(Exception ex)
        {
            File.AppendAllText(Report, "FAIL " + ex + Environment.NewLine);
            Log.Error("ZENITH ACCEPTANCE FAILURE " + ex);
            finished = true; Application.Quit(1);
        }
    }
}
