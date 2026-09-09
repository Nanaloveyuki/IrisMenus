using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public static class MenuControls
    {
        internal static readonly Color Accent = new Color(1f, 0.85f, 0.55f);

        public static void Anchor(Listing_Standard list, string id, float height = 30f)
        {
            if (MenuScrollView.Active == null) throw new InvalidOperationException("Anchors require a MenuScrollView or registered listing page.");
            MenuScrollView.Active.Anchor(id, new Rect(0f, list.CurHeight, list.ColumnWidth, height));
        }

        public static void Section(Listing_Standard list, string title)
        {
            Color color = GUI.color;
            try
            {
                GUI.color = Accent;
                list.Label(title);
                list.GapLine(6f);
            }
            finally { GUI.color = color; }
        }

        public static void Checkbox(Listing_Standard list, string label, ref bool value, string tooltip = null)
        {
            list.CheckboxLabeled(label, ref value, tooltip);
        }

        public static float Slider(Listing_Standard list, string label, float value,
            float min, float max, string format = "F2", string tooltip = null)
        {
            float height = Mathf.Max(30f, Text.CalcHeight(label, list.ColumnWidth * 0.4f));
            Rect row = list.GetRect(height);
            Rect labelRect = new Rect(row.x, row.y, row.width * 0.4f, row.height);
            Widgets.Label(labelRect, label);
            if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(labelRect, tooltip);
            Rect slider = new Rect(labelRect.xMax + 8f, row.y, Mathf.Max(1f, row.width * 0.6f - 72f), row.height);
            float result = Widgets.HorizontalSlider(slider, value, min, max);
            Widgets.Label(new Rect(row.xMax - 60f, row.y, 60f, row.height), result.ToString(format));
            list.Gap(4f);
            return result;
        }

        public static void Number(Listing_Standard list, string label, ref int value,
            ref string editBuffer, int min = int.MinValue, int max = int.MaxValue)
        {
            Rect field = FieldRow(list, label);
            Widgets.TextFieldNumeric(field, ref value, ref editBuffer, min, max);
        }

        public static void Number(Listing_Standard list, string label, ref float value,
            ref string editBuffer, float min = float.MinValue, float max = float.MaxValue)
        {
            Rect field = FieldRow(list, label);
            Widgets.TextFieldNumeric(field, ref value, ref editBuffer, min, max);
        }

        public static void Select<T>(Listing_Standard list, string label, T selected,
            IEnumerable<T> options, Func<T, string> optionLabel, Action<T> onSelected)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (optionLabel == null) throw new ArgumentNullException(nameof(optionLabel));
            if (onSelected == null) throw new ArgumentNullException(nameof(onSelected));
            Rect field = FieldRow(list, label);
            string current = optionLabel(selected);
            if (!Widgets.ButtonText(field, current.Truncate(field.width - 16f))) return;
            var menu = new List<FloatMenuOption>();
            foreach (T option in options)
            {
                T captured = option;
                menu.Add(new FloatMenuOption(optionLabel(captured), () => onSelected(captured)));
            }
            if (menu.Count > 0) Find.WindowStack.Add(new FloatMenu(menu));
        }

        private static Rect FieldRow(Listing_Standard list, string label)
        {
            float height = Mathf.Max(30f, Text.CalcHeight(label, list.ColumnWidth * 0.4f));
            Rect row = list.GetRect(height);
            Widgets.Label(new Rect(row.x, row.y, row.width * 0.4f, height), label);
            list.Gap(4f);
            return new Rect(row.x + row.width * 0.4f + 8f, row.y, row.width * 0.6f - 8f, 30f);
        }
    }
}
