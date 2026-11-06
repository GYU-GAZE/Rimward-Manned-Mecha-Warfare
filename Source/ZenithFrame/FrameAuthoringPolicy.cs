using System.Collections.Generic;

namespace ZenithFrame
{
    public static class FrameAuthoringPolicy
    {
        public static bool AllowsFamily(string chassisFamily, IList<string> allowedFamilies, bool hasOverlay)
        {
            if (allowedFamilies == null || allowedFamilies.Count == 0) return !hasOverlay;
            return !string.IsNullOrEmpty(chassisFamily) && allowedFamilies.Contains(chassisFamily);
        }
    }
}
