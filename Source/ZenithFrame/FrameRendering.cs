using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace ZenithFrame
{
    public sealed class FrameVisualSet
    {
        public string south;
        public string sides;
        public string north;

        public string PathFor(Rot4 facing)
        {
            switch (FrameVisualDirection.SourceFor(facing.AsInt))
            {
                case FrameVisualSource.South: return south;
                case FrameVisualSource.North: return north;
                default: return sides;
            }
        }
    }

    public sealed class FrameChassisVisualExtension : DefModExtension
    {
        public FrameVisualSet torso;
        public FrameVisualSet head;
        public float worldDrawSize = 2f;
    }

    public sealed class FrameModuleVisualExtension : DefModExtension
    {
        public FrameVisualSet visual;
    }

    public enum FrameVisualPart
    {
        Torso,
        Head,
        Helmet,
        Shoulder
    }

    public enum FrameHeadVisualState
    {
        Present,
        Missing
    }

    public static class FrameVisualRules
    {
        public static FrameHeadVisualState HeadState(FramePawn frame) =>
            FrameDamageRules.IsPartDestroyed(frame, ZenithBodyPartDefOf.ZF_FrameHeadAssembly)
                ? FrameHeadVisualState.Missing : FrameHeadVisualState.Present;

        public static Thing PhysicalModule(FramePawn frame, FrameVisualPart part)
        {
            if (part == FrameVisualPart.Helmet) return frame?.Loadout?.InstalledHead;
            if (part == FrameVisualPart.Shoulder) return frame?.Loadout?.InstalledShoulder;
            return null;
        }

        public static FrameVisualSet Source(FramePawn frame, FrameVisualPart part)
        {
            if (frame == null) return null;
            FrameChassisVisualExtension chassis = frame.def.GetModExtension<FrameChassisVisualExtension>();
            if (part == FrameVisualPart.Torso) return chassis?.torso;
            if (part == FrameVisualPart.Head) return chassis?.head;
            return PhysicalModule(frame, part)?.def.GetModExtension<FrameModuleVisualExtension>()?.visual;
        }

        public static bool Visible(FramePawn frame, FrameVisualPart part)
        {
            if (frame == null || Source(frame, part) == null) return false;
            if (part == FrameVisualPart.Torso) return true;
            if (part == FrameVisualPart.Head) return HeadState(frame) == FrameHeadVisualState.Present;
            Thing module = PhysicalModule(frame, part);
            if (module == null || module.Destroyed || frame.Loadout == null || !frame.Loadout.IsMountValid(module))
                return false;
            return part != FrameVisualPart.Helmet || HeadState(frame) == FrameHeadVisualState.Present;
        }
    }

    public sealed class FrameRenderNodeProperties : PawnRenderNodeProperties
    {
        public FrameVisualPart visualPart;

        public FrameRenderNodeProperties()
        {
            nodeClass = typeof(FrameRenderNode);
            workerClass = typeof(FrameRenderNodeWorker);
            pawnType = RenderNodePawnType.NonHumanlikeOnly;
            overrideMeshSize = Vector2.one;
        }
    }

    public sealed class FrameRenderNode : PawnRenderNode
    {
        private Thing cachedModule;
        private bool cachedVisible;
        private FrameVisualSet loadedVisualSet;
        private Graphic southGraphic;
        private Graphic sidesGraphic;
        private Graphic northGraphic;

        public FrameVisualPart Part => ((FrameRenderNodeProperties)Props).visualPart;

        public FrameRenderNode(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override bool RecacheRequested => base.RecacheRequested ||
            !ReferenceEquals(cachedModule, FrameVisualRules.PhysicalModule(tree.pawn as FramePawn, Part)) ||
            cachedVisible != FrameVisualRules.Visible(tree.pawn as FramePawn, Part);

        protected override IEnumerable<Graphic> GraphicsFor(Pawn pawn)
        {
            FramePawn frame = pawn as FramePawn;
            cachedModule = FrameVisualRules.PhysicalModule(frame, Part);
            cachedVisible = FrameVisualRules.Visible(frame, Part);
            RefreshGraphics();
            if (southGraphic != null) yield return southGraphic;
            if (sidesGraphic != null) yield return sidesGraphic;
            if (northGraphic != null) yield return northGraphic;
        }

        private static Graphic Load(string path) => path.NullOrEmpty() ? null :
            GraphicDatabase.Get<Graphic_Single>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);

        private void RefreshGraphics()
        {
            FrameVisualSet source = FrameVisualRules.Source(tree.pawn as FramePawn, Part);
            if (ReferenceEquals(source, loadedVisualSet)) return;
            loadedVisualSet = source;
            southGraphic = Load(source?.south);
            sidesGraphic = Load(source?.sides);
            northGraphic = Load(source?.north);
        }

        public Graphic GraphicAt(Rot4 facing)
        {
            // Service may change the physical Thing between graph initialization and draw.
            // GraphicDatabase reuses loaded materials; this does not decode PNGs per draw.
            RefreshGraphics();
            switch (FrameVisualDirection.SourceFor(facing.AsInt))
            {
                case FrameVisualSource.South: return southGraphic;
                case FrameVisualSource.North: return northGraphic;
                default: return sidesGraphic;
            }
        }

        // GraphicMeshSet already supplies a horizontally flipped West mesh.
        // Toggling FlipGraphic again cancels that mesh's UV mirror.
        public override bool FlipGraphic(PawnDrawParms parms) => base.FlipGraphic(parms);
    }

    public sealed class FrameRenderNodeWorker : PawnRenderNodeWorker_Body
    {
        public override bool CanDrawNow(PawnRenderNode node, PawnDrawParms parms) =>
            base.CanDrawNow(node, parms) &&
            FrameVisualRules.Visible(parms.pawn as FramePawn, ((FrameRenderNode)node).Part);

        protected override Graphic GetGraphic(PawnRenderNode node, PawnDrawParms parms) =>
            ((FrameRenderNode)node).GraphicAt(parms.facing);

        public override Vector3 ScaleFor(PawnRenderNode node, PawnDrawParms parms)
        {
            Vector3 scale = base.ScaleFor(node, parms);
            float size = parms.pawn.def.GetModExtension<FrameChassisVisualExtension>()?.worldDrawSize ?? 2f;
            scale.x *= size;
            scale.z *= size;
            return scale;
        }
    }
}
