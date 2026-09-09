using UnityEngine;

namespace IrisMenus
{
    internal struct MenuLayout
    {
        internal Rect Navigation;
        internal Rect Content;
        internal Rect Footer;
        internal bool Compact;

        internal static MenuLayout Calculate(float width, float height)
        {
            const float top = 58f;
            float footerHeight = width < 560f ? 104f : 64f;
            float bodyHeight = Mathf.Max(1f, height - top - footerHeight - 12f);
            float leftWidth = Mathf.Clamp((width - 62f) * 0.2f, 170f, 340f);
            bool compact = width < 560f;
            return new MenuLayout
            {
                Compact = compact,
                Navigation = new Rect(0f, top, compact ? width : leftWidth, bodyHeight),
                Content = new Rect(compact ? 0f : leftWidth + 28f, top,
                    compact ? width : Mathf.Max(1f, width - leftWidth - 28f), bodyHeight),
                Footer = new Rect(0f, height - footerHeight, width, footerHeight)
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
