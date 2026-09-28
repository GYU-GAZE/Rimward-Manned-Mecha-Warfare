using System.Collections.Generic;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public sealed partial class FramePawn
    {
        private bool draftedOnLoad;
        public override void ExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving) draftedOnLoad = Drafted;
            base.ExposeData();
            Scribe_Values.Look(ref draftedOnLoad, "zenithDrafted");
        }
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (drafter == null) drafter = new Pawn_DraftController(this);
            // Vanilla recreates/removes mech drafters without Biotech during load.
            // Persist our own control intent, then restore the ordinary draft controller.
            if (respawningAfterLoad && draftedOnLoad && Cockpit.CanOperate) drafter.Drafted = true;
            drafter.FireAtWill = false;
            if (!respawningAfterLoad && equipment.Primary == null)
                equipment.AddEquipment((ThingWithComps)ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugRifle")));
        }

        protected override void Tick()
        {
            if (!Cockpit.CanOperate)
            {
                if (Drafted || pather.Moving || (CurJob != null && CurJob.def != JobDefOf.Wait)) Cockpit.StopFrame();
                if (Cockpit.HasPilot) Cockpit.EmergencyEject();
            }
            if (drafter != null && drafter.FireAtWill) drafter.FireAtWill = false;
            base.Tick();
        }

        public bool SetDrafted(bool value)
        {
            if (value && !Cockpit.CanOperate) return false;
            drafter.Drafted = value;
            drafter.FireAtWill = false;
            return true;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            if (Faction != Faction.OfPlayer || !Spawned || !Cockpit.HasPilot) yield break;
            yield return new Command_Action { defaultLabel = "Disembark", defaultDesc = "Safely return the pilot to the map.", icon = TexCommand.Install,
                action = () => { if (!Cockpit.TryDisembark()) Messages.Message("No safe exit available.", MessageTypeDefOf.RejectInput, false); } };
            var draft = new Command_Toggle
            {
                hotKey = KeyBindingDefOf.Command_ColonistDraft,
                defaultLabel = (Drafted ? "CommandUndraftLabel" : "CommandDraftLabel").Translate(),
                defaultDesc = "CommandToggleDraftDesc".Translate(),
                icon = TexCommand.Draft,
                turnOnSound = SoundDefOf.DraftOn,
                turnOffSound = SoundDefOf.DraftOff,
                groupKeyIgnoreContent = 81729172,
                isActive = () => Drafted,
                toggleAction = () => SetDrafted(!Drafted)
            };
            if (!Cockpit.CanOperate) draft.Disable("Frame or pilot incapacitated.");
            yield return draft;
        }
    }

    public sealed class Verb_FrameShot : Verb_Shoot
    {
        public override bool Available() => base.Available() && CasterPawn is FramePawn frame && frame.Cockpit.CanOperate && frame.Drafted;
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
    }
}

