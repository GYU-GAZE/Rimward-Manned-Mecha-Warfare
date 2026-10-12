using System;
using System.IO;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace ZenithFrame.Tests
{
    // One disposable RimWorld session covers live recache and a same-process save/load.
    public sealed class LiverySuite : GameComponent
    {
        private int stage;
        private FramePawn first;
        private FramePawn second;
        private string firstId;
        private string secondId;
        private string helmetId;
        private string shoulderId;
        private string weaponId;
        private bool finished;

        private static readonly Color Red = new Color32(230, 40, 55, 255);
        private static readonly Color Cyan = new Color32(40, 210, 225, 255);
        private static readonly Color Blue = new Color32(50, 90, 230, 255);
        private static readonly Color Yellow = new Color32(245, 185, 35, 255);
        private static readonly Color Purple = new Color32(180, 55, 230, 255);
        private static readonly Color Orange = new Color32(245, 120, 25, 255);

        private bool Enabled => File.Exists(Path.Combine(GenFilePaths.SaveDataFolderPath, "livery-suite.txt"));
        private string ReportPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "livery-phase-0.txt");

        public LiverySuite(Game game) { }

        public override void ExposeData()
        {
            Scribe_Values.Look(ref stage, "liveryStage");
            Scribe_References.Look(ref first, "liveryFirstFrame");
            Scribe_References.Look(ref second, "liverySecondFrame");
            Scribe_Values.Look(ref firstId, "liveryFirstId");
            Scribe_Values.Look(ref secondId, "liverySecondId");
            Scribe_Values.Look(ref helmetId, "liveryHelmetId");
            Scribe_Values.Look(ref shoulderId, "liveryShoulderId");
            Scribe_Values.Look(ref weaponId, "liveryWeaponId");
        }

        private void Check(bool condition, string name)
        {
            if (!condition) throw new InvalidOperationException(name);
            File.AppendAllText(ReportPath, "PASS " + name + Environment.NewLine);
        }

        private static bool SameColor(Color actual, Color expected) =>
            ((Color32)actual).Equals((Color32)expected);

        private static FrameRenderNode Node(FramePawn frame, FrameVisualPart part, bool visor = false) =>
            frame.Drawer.renderer.renderTree.rootNode.children.OfType<FrameRenderNode>()
                .Single(node => node.Part == part && node.VisorOverlay == visor);

        private void CheckPaint(FramePawn frame, Color primary, Color secondary, Color visor, string stageName)
        {
            Check(SameColor(frame.Livery.Primary, primary) && SameColor(frame.Livery.Secondary, secondary) &&
                SameColor(frame.Livery.Visor, visor), stageName + " persists three chassis-owned paint colors");
            frame.Drawer.renderer.EnsureGraphicsInitialized();
            foreach (Rot4 facing in Rot4.AllRotations)
            {
                foreach (FrameVisualPart part in new[] { FrameVisualPart.Torso, FrameVisualPart.Head,
                    FrameVisualPart.Helmet, FrameVisualPart.Shoulder })
                {
                    FrameRenderNode node = Node(frame, part);
                    var parms = new PawnDrawParms { pawn = frame, facing = facing, posture = PawnPosture.Standing };
                    Graphic graphic = node.GraphicAt(facing);
                    Material material = node.Worker.GetFinalizedMaterial(node, parms);
                    Vector3 pivot;
                    Vector3 offset = node.Worker.OffsetFor(node, parms, out pivot);
                    Check(graphic?.path == FrameVisualRules.Source(frame, part).PathFor(facing) &&
                        graphic.Shader == ShaderDatabase.CutoutComplex && material != null &&
                        SameColor(material.color, primary) &&
                        SameColor(material.GetColor(ShaderPropertyIDs.ColorTwo), secondary) &&
                        material.GetTexture(ShaderPropertyIDs.MaskTex) is Texture2D mask &&
                        mask.width == 1024 && mask.height == 1024 &&
                        MeshPool.GetMetaData(node.GetMesh(parms)).flipped == (facing == Rot4.West) &&
                        Mathf.Approximately(node.Worker.ScaleFor(node, parms).x, 3.5f) &&
                        Mathf.Approximately(node.Worker.ScaleFor(node, parms).z, 3.5f) &&
                        offset.x == 0f && offset.z == 0f && pivot == Vector3.zero,
                        stageName + " " + part + " masked material, shared canvas and facing " + facing);
                    FrameVisualSet visorSource = FrameVisualRules.Source(frame, part, true);
                    if (visorSource != null)
                    {
                        FrameRenderNode overlay = Node(frame, part, true);
                        Graphic visorGraphic = overlay.GraphicAt(facing);
                        Material visorMaterial = overlay.Worker.GetFinalizedMaterial(overlay, parms);
                        Check(visorGraphic?.path == visorSource.PathFor(facing) &&
                            visorGraphic.Shader == ShaderDatabase.Cutout && visorMaterial != null &&
                            SameColor(visorMaterial.color, visor) &&
                            MeshPool.GetMetaData(overlay.GetMesh(parms)).flipped == (facing == Rot4.West) &&
                            Mathf.Approximately(overlay.Worker.ScaleFor(overlay, parms).x, 3.5f),
                            stageName + " " + part + " independent visor layer facing " + facing);
                    }
                }
            }
        }

        private static FramePawn SpawnFrame(IntVec3 cell, Map map) => (FramePawn)GenSpawn.Spawn(
            PawnGenerator.GeneratePawn(DefDatabase<PawnKindDef>.GetNamed("ZF_DebugFrame"), Faction.OfPlayer), cell, map);

        private static Pawn SpawnPilot(IntVec3 cell, Map map) => (Pawn)GenSpawn.Spawn(
            PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                canGeneratePawnRelations: false)), cell, map);

        private void Start()
        {
            File.WriteAllText(ReportPath, "ZENITH focused livery probe" + Environment.NewLine);
            Map map = Find.CurrentMap;
            first = SpawnFrame(map.Center, map);
            second = SpawnFrame(map.Center + IntVec3.East * 5, map);
            firstId = first.GetUniqueLoadID();
            secondId = second.GetUniqueLoadID();
            Thing helmet = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugSensorHelmet"));
            Thing shoulder = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("ZF_DebugShoulderSystem"));
            helmetId = helmet.GetUniqueLoadID();
            shoulderId = shoulder.GetUniqueLoadID();
            Check(first.Loadout.TryInstallHead(helmet) && first.Loadout.TryInstallShoulder(shoulder) &&
                second.Loadout.TryInstallHead(ThingMaker.MakeThing(helmet.def)) &&
                second.Loadout.TryInstallShoulder(ThingMaker.MakeThing(shoulder.def)),
                "both Frames have Helmet and Shoulder on exact physical mounts");
            Check(first.Livery != null && second.Livery != null && !ReferenceEquals(first.Livery, second.Livery),
                "each physical chassis owns its own livery component");
            CheckPaint(first, Color.white, Color.white, Color.white, "default white");
            ThingWithComps originalWeapon = first.equipment.Primary;
            weaponId = originalWeapon.GetUniqueLoadID();
            Material normalWeaponMaterial = originalWeapon.Graphic.MatSingle;

            Check(first.def.inspectorTabs.Contains(typeof(ITab_FrameVisual)) &&
                first.Livery.Defaults != null, "Frame exposes its visual tab and chassis color defaults");

            second.Drawer.renderer.EnsureGraphicsInitialized();
            Material untouchedSecond = Node(second, FrameVisualPart.Torso).GraphicAt(Rot4.South).MatSingle;
            IntVec3 stationaryCell = first.Position;
            first.Livery.SetColors(Red, Cyan, Purple);
            Check(first.Position == stationaryCell, "livery change leaves first Frame completely stationary");
            CheckPaint(first, Red, Cyan, Purple, "red/cyan/purple after stationary recache");
            second.Drawer.renderer.EnsureGraphicsInitialized();
            Check(ReferenceEquals(untouchedSecond, Node(second, FrameVisualPart.Torso).GraphicAt(Rot4.South).MatSingle) &&
                SameColor(second.Livery.Primary, Color.white),
                "changing first Frame does not change second Frame's cached white material");
            second.Livery.SetColors(Blue, Yellow, Orange);
            CheckPaint(second, Blue, Yellow, Orange, "blue/yellow/orange on second Frame");
            Check(!ReferenceEquals(Node(first, FrameVisualPart.Torso).GraphicAt(Rot4.South).MatSingle,
                Node(second, FrameVisualPart.Torso).GraphicAt(Rot4.South).MatSingle),
                "same source texture gives different cached materials for different Frame colors");

            Check(first.Loadout.TryRemoveHead(out Thing removedHelmet) &&
                removedHelmet.GetUniqueLoadID() == helmetId && first.Loadout.TryInstallHead(removedHelmet) &&
                first.Loadout.TryRemoveShoulder(out Thing removedShoulder) &&
                removedShoulder.GetUniqueLoadID() == shoulderId && first.Loadout.TryInstallShoulder(removedShoulder),
                "module removal and reinstall preserve exact items");
            CheckPaint(first, Red, Cyan, Purple, "after module recache");

            Pawn oldPilot = SpawnPilot(first.Position + IntVec3.West, map);
            Check(first.Cockpit.TryBoard(oldPilot) && first.Cockpit.TryDisembark(),
                "first pilot boards and leaves exact Frame");
            Pawn newPilot = SpawnPilot(first.Position + IntVec3.South, map);
            Check(first.Cockpit.TryBoard(newPilot) && first.Cockpit.TryDisembark() &&
                SameColor(first.Livery.Primary, Red), "pilot replacement cannot change chassis paint");

            var injuries = first.RaceProps.body.AllParts
                .Where(part => part.def.tags.Contains(BodyPartTagDefOf.MovingLimbCore))
                .Select(part =>
                {
                    var injury = (Hediff_Injury)HediffMaker.MakeHediff(HediffDefOf.Cut, first, part);
                    injury.Severity = part.def.GetMaxHealth(first) - 1f;
                    first.health.AddHediff(injury, part);
                    return injury;
                }).ToList();
            Check(first.Downed && SameColor(first.Livery.Primary, Red),
                "downing does not clear physical Frame paint");
            foreach (Hediff_Injury injury in injuries) first.health.RemoveHediff(injury);
            Check(!first.Downed && SameColor(first.Livery.Secondary, Cyan),
                "repairing injuries retains Frame paint");
            if (!ReferenceEquals(first.equipment.Primary, originalWeapon))
                Check(originalWeapon.Spawned &&
                    (first.Loadout.TryReclaimRememberedWeapon() || first.Loadout.TryInstallWeapon(originalWeapon)) &&
                    ReferenceEquals(first.equipment.Primary, originalWeapon),
                    "fixture restores the exact weapon that vanilla downing dropped");

            first.SetFaction(Faction.OfAncientsHostile);
            Check(SameColor(first.Livery.Primary, Red), "faction transfer keeps the same chassis paint");
            first.SetFaction(Faction.OfPlayer);
            CheckPaint(first, Red, Cyan, Purple, "after faction transfer");
            CheckPaint(second, Blue, Yellow, Orange, "second Frame remains isolated");
            Check(ReferenceEquals(first.equipment.Primary, originalWeapon) &&
                ReferenceEquals(originalWeapon.Graphic.MatSingle, normalWeaponMaterial) &&
                second.equipment.Primary?.def.defName == "ZF_DebugRifle",
                "livery does not recolor the exact ordinary rifle material");
            ThingWithComps heavy = (ThingWithComps)ThingMaker.MakeThing(
                DefDatabase<ThingDef>.GetNamed("ZF_DebugHeavyRifle"));
            Material heavyMaterial = heavy.Graphic.MatSingle;
            Check(heavy.def.graphicData.drawSize == new Vector2(2.5f, 2.5f) &&
                first.Loadout.TryRemoveWeapon(out ThingWithComps removedNormal) &&
                ReferenceEquals(removedNormal, originalWeapon) && first.Loadout.TryInstallWeapon(heavy),
                "oversized weapon uses the same exact normal equipment slot");
            first.Livery.SetColors(Blue, Yellow, Orange);
            Check(ReferenceEquals(first.equipment.Primary, heavy) &&
                ReferenceEquals(heavy.Graphic.MatSingle, heavyMaterial) &&
                heavy.def.graphicData.drawSize == new Vector2(2.5f, 2.5f),
                "changing chassis paint leaves oversized weapon material and size untouched");
            first.Livery.SetColors(Red, Cyan, Purple);
            Check(first.Loadout.TryRemoveWeapon(out ThingWithComps removedHeavy) &&
                ReferenceEquals(removedHeavy, heavy) && first.Loadout.TryInstallWeapon(originalWeapon),
                "fixture restores its exact control rifle after weapon paint boundary check");
            heavy.Destroy();

            // The disposable quicktest scenario sometimes has relations to
            // temporary world pawns that are not serialized with its map.
            foreach (Pawn mapPawn in map.mapPawns.AllPawnsSpawned.ToList())
                mapPawn.relations?.ClearAllRelations();
            stage = 1;
            GameDataSaveLoader.SaveGame("ZenithLivery");
            Check(File.Exists(GenFilePaths.FilePathForSavedGame("ZenithLivery")),
                "three-color two-Frame save written in the same process");
            finished = true;
            GameDataSaveLoader.LoadGame("ZenithLivery");
        }

        private void AfterLoad()
        {
            Check(first != null && second != null && first.Spawned && second.Spawned &&
                first.GetUniqueLoadID() == firstId && second.GetUniqueLoadID() == secondId &&
                !ReferenceEquals(first, second), "save/load retains two exact physical Frame identities");
            Check(first.Loadout.InstalledHead?.GetUniqueLoadID() == helmetId &&
                first.Loadout.InstalledShoulder?.GetUniqueLoadID() == shoulderId &&
                first.equipment.Primary?.GetUniqueLoadID() == weaponId,
                "save/load retains exact Helmet, Shoulder and ordinary weapon");
            CheckPaint(first, Red, Cyan, Purple, "red/cyan/purple after reload");
            CheckPaint(second, Blue, Yellow, Orange, "blue/yellow/orange after reload");
            first.Livery.ResetColors();
            CheckPaint(first, Color.white, Color.white, Color.white, "chassis defaults after reload");
            CheckPaint(second, Blue, Yellow, Orange, "second Frame unaffected by repaint after reload");
            File.AppendAllText(ReportPath, "PASS focused livery session complete" + Environment.NewLine);
            finished = true;
            Application.Quit(0);
        }

        public override void GameComponentUpdate()
        {
            if (!Enabled || finished || Current.ProgramState != ProgramState.Playing ||
                Find.CurrentMap == null || LongEventHandler.ShouldWaitForEvent || Find.TickManager.TicksGame < 1000)
                return;
            try
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Superfast;
                if (stage == 0) Start();
                else AfterLoad();
            }
            catch (Exception error)
            {
                File.AppendAllText(ReportPath, "FAIL " + error + Environment.NewLine);
                Log.Error("ZENITH LIVERY TEST FAILURE " + error);
                finished = true;
                Application.Quit(1);
            }
        }
    }
}
