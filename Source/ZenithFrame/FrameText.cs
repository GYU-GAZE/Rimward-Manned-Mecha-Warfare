using Verse;

namespace ZenithFrame
{
    public static class FrameText
    {
        public static string Category(FrameAddonCategory category) => ("ZF_Category_" + category).Translate();
        public static string Paint(FrameColorChannel channel) => ("ZF_Paint_" + channel).Translate();
    }
}
