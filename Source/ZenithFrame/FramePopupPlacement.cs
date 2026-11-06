using UnityEngine;

namespace ZenithFrame
{
    internal static class FramePopupPlacement
    {
        internal const float Gap = 12f;
        private const float ScreenMargin = 8f;

        internal static Rect Beside(Rect frameTab, Vector2 requestedSize, float screenWidth,
            float screenHeight, float minimumWidth)
        {
            float width = Mathf.Min(requestedSize.x, screenWidth - ScreenMargin * 2f);
            float height = Mathf.Min(requestedSize.y, screenHeight - ScreenMargin * 2f);
            float rightX = frameTab.xMax + Gap;
            float rightSpace = screenWidth - ScreenMargin - rightX;
            float leftSpace = frameTab.x - Gap - ScreenMargin;
            float x;

            if (rightSpace >= width)
                x = rightX;
            else if (leftSpace >= width)
                x = frameTab.x - Gap - width;
            else if (rightSpace >= minimumWidth)
            {
                width = rightSpace;
                x = rightX;
            }
            else if (leftSpace >= minimumWidth)
            {
                width = leftSpace;
                x = frameTab.x - Gap - width;
            }
            else
                x = rightSpace >= leftSpace ? rightX : frameTab.x - Gap - width;

            x = Mathf.Clamp(x, ScreenMargin, screenWidth - ScreenMargin - width);
            float y = Mathf.Clamp(frameTab.y, ScreenMargin, screenHeight - ScreenMargin - height);
            return new Rect(Mathf.Round(x), Mathf.Round(y), Mathf.Round(width), Mathf.Round(height));
        }
    }
}
