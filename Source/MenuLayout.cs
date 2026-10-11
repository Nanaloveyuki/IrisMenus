using UnityEngine;

namespace IrisMenus
{
    internal struct MenuLayout
    {
        internal Rect Navigation;
        internal Rect Content;
        internal Rect Footer;
        internal Rect MainButton;
        internal bool Compact;

        internal static MenuLayout Calculate(float width, float height, float mainButtonLabelWidth)
        {
            const float top = 58f;
            float leftWidth = Mathf.Clamp((width - 62f) * 0.2f, 170f, 340f);
            bool compact = width < 560f;
            float controlX = compact ? 0f : leftWidth + 28f;
            float buttonWidth = Mathf.Min(164f, (width - controlX - 32f) / 2f);
            float mainButtonX = controlX + buttonWidth + 18f + 238f;
            float mainButtonWidth = Mathf.Min(420f, width - mainButtonX - 24f);
            bool inlineMainButton = !compact && mainButtonWidth >= mainButtonLabelWidth + 32f;
            float footerHeight = compact ? 152f : inlineMainButton ? 72f : 120f;
            float footerY = height - footerHeight;
            float bodyHeight = Mathf.Max(1f, height - top - footerHeight - 12f);
            return new MenuLayout
            {
                Compact = compact,
                Navigation = new Rect(0f, top, compact ? width : leftWidth, bodyHeight),
                Content = new Rect(compact ? 0f : leftWidth + 28f, top,
                    compact ? width : Mathf.Max(1f, width - leftWidth - 28f), bodyHeight),
                Footer = new Rect(0f, footerY, width, footerHeight),
                MainButton = inlineMainButton
                    ? new Rect(mainButtonX, footerY + 14f, mainButtonWidth, 24f)
                    : new Rect(controlX, footerY + (compact ? 100f : 68f),
                        Mathf.Min(420f, Mathf.Max(1f, width - controlX - 8f)), 44f)
            };
        }

        internal static Rect Clamp(Rect rect, float width, float height)
        {
            rect.width = Mathf.Clamp(Finite(rect.width, width * 0.92f), Mathf.Min(620f, width), width);
            rect.height = Mathf.Clamp(Finite(rect.height, height * 0.88f), Mathf.Min(440f, height), height);
            rect.x = Mathf.Clamp(Finite(rect.x, 0f), 0f, width - rect.width);
            rect.y = Mathf.Clamp(Finite(rect.y, 0f), 0f, height - rect.height);
            return rect;
        }

        internal static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        internal static Rect BackgroundUv(Rect window, float screenWidth, float screenHeight)
        {
            return new Rect(window.x / screenWidth, 1f - window.yMax / screenHeight,
                window.width / screenWidth, window.height / screenHeight);
        }
    }
}
