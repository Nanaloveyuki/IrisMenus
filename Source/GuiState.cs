using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class GuiState
    {
        private readonly Color color = GUI.color;
        private readonly Color background = GUI.backgroundColor;
        private readonly Color content = GUI.contentColor;
        private readonly bool enabled = GUI.enabled;
        private readonly bool changed = GUI.changed;
        private readonly Matrix4x4 matrix = GUI.matrix;
        private readonly GameFont font = Text.Font;
        private readonly TextAnchor anchor = Text.Anchor;
        private readonly bool wordWrap = Text.WordWrap;
        private readonly GUISkin skin = GUI.skin;
        private readonly int depth = GUI.depth;

        internal void Restore()
        {
            GUI.color = color;
            GUI.backgroundColor = background;
            GUI.contentColor = content;
            GUI.enabled = enabled;
            GUI.changed = changed;
            GUI.matrix = matrix;
            GUI.skin = skin;
            GUI.depth = depth;
            Text.Font = font;
            Text.Anchor = anchor;
            Text.WordWrap = wordWrap;
        }
    }
}
