using System;

namespace ZenithFrame
{
    // Rot4 uses North=0, East=1, South=2, West=3. The artist exports one side.
    public enum FrameVisualSource
    {
        North,
        Sides,
        South
    }

    public static class FrameVisualDirection
    {
        public static FrameVisualSource SourceFor(int rotation)
        {
            switch (rotation)
            {
                case 0: return FrameVisualSource.North;
                case 1: return FrameVisualSource.Sides;
                case 2: return FrameVisualSource.South;
                case 3: return FrameVisualSource.Sides;
                default: throw new ArgumentOutOfRangeException(nameof(rotation));
            }
        }

        // The Test Frame side art faces right: East is authored, West is mirrored.
        public static bool MirrorFor(int rotation)
        {
            if (rotation < 0 || rotation > 3) throw new ArgumentOutOfRangeException(nameof(rotation));
            return rotation == 3;
        }
    }
}
