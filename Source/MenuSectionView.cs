using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class MenuSection
    {
        public string Id { get; }
        internal Func<float, float> Measure { get; }
        internal Action<Rect> Draw { get; }
        internal Func<string> LayoutKey { get; }

        public MenuSection(string id, Func<float, float> measure, Action<Rect> draw)
            : this(id, measure, draw, null)
        {
        }

        public MenuSection(string id, Func<float, float> measure, Action<Rect> draw, Func<string> layoutKey)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("A section ID is required.", nameof(id));
            Id = id;
            Measure = measure ?? throw new ArgumentNullException(nameof(measure));
            Draw = draw ?? throw new ArgumentNullException(nameof(draw));
            LayoutKey = layoutKey;
        }
    }

    public sealed class MenuSectionView
    {
        private readonly MenuSection[] sections;
        private readonly Dictionary<string, int> indices = new Dictionary<string, int>(StringComparer.Ordinal);
        private readonly Rect[] rectangles;
        private readonly string[] layoutKeys;
        private readonly bool[] layoutKeyInitialized;
        private Vector2 position;
        private float layoutWidth = -1f;
        private float height;
        private object language;
        private int pending = -1;
        private int highlighted = -1;
        private float highlightUntil;

        public MenuSectionView(IEnumerable<MenuSection> sections)
        {
            this.sections = sections?.ToArray() ?? throw new ArgumentNullException(nameof(sections));
            rectangles = new Rect[this.sections.Length];
            layoutKeys = new string[this.sections.Length];
            layoutKeyInitialized = new bool[this.sections.Length];
            for (int i = 0; i < this.sections.Length; i++)
            {
                if (this.sections[i] == null) throw new ArgumentException("A section must not be null.", nameof(sections));
                indices.Add(this.sections[i].Id, i);
            }
        }

        public void InvalidateLayout() { layoutWidth = -1f; }

        public void Focus(string sectionId)
        {
            if (sectionId == null || !indices.TryGetValue(sectionId, out int index))
                throw new ArgumentException("Unknown section: " + sectionId, nameof(sectionId));
            pending = highlighted = index;
            highlightUntil = Time.realtimeSinceStartup + 4f;
        }

        public void Draw(Rect viewport)
        {
            float width = Mathf.Max(1f, viewport.width - 20f);
            bool needsLayout = layoutWidth != width || !ReferenceEquals(language, LanguageDatabase.activeLanguage);
            if (!needsLayout)
            {
                for (int i = 0; i < sections.Length; i++)
                {
                    if (sections[i].LayoutKey == null) continue;
                    string key = sections[i].LayoutKey();
                    if (!layoutKeyInitialized[i] ||
                        !string.Equals(layoutKeys[i], key, StringComparison.Ordinal))
                    {
                        needsLayout = true;
                        break;
                    }
                }
            }
            if (needsLayout)
            {
                height = 0f;
                for (int i = 0; i < sections.Length; i++)
                {
                    string key = sections[i].LayoutKey == null ? null : sections[i].LayoutKey();
                    float measured = sections[i].Measure(width);
                    if (float.IsNaN(measured) || float.IsInfinity(measured) || measured < 0f)
                        throw new InvalidOperationException("Invalid section height: " + sections[i].Id);
                    rectangles[i] = new Rect(0f, height, width, measured);
                    height += measured + 8f;
                    layoutKeys[i] = key;
                    layoutKeyInitialized[i] = sections[i].LayoutKey != null;
                }
                layoutWidth = width;
                language = LanguageDatabase.activeLanguage;
            }
            if (pending >= 0) { position.y = rectangles[pending].y - 8f; pending = -1; }
            position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref position, new Rect(0f, 0f, width, Mathf.Max(height, viewport.height)));
            try
            {
                for (int i = 0; i < sections.Length; i++)
                {
                    Rect rect = rectangles[i];
                    if (rect.height <= 0f || rect.yMax <= position.y || rect.y >= position.y + viewport.height) continue;
                    if (i == highlighted && Time.realtimeSinceStartup <= highlightUntil) Widgets.DrawHighlight(rect);
                    var state = new GuiState();
                    GUI.BeginGroup(rect);
                    try { sections[i].Draw(new Rect(0f, 0f, rect.width, rect.height)); }
                    finally { GUI.EndGroup(); state.Restore(); }
                }
            }
            finally { Widgets.EndScrollView(); }
        }
    }
}
