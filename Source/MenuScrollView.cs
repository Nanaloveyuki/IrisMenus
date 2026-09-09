using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class MenuScrollView
    {
        private Vector2 position;
        private float contentHeight;
        private string pendingFocus;
        private string highlighted;
        private float highlightUntil;
        private readonly Dictionary<string, Rect> anchors = new Dictionary<string, Rect>();
        internal static MenuScrollView Active { get; private set; }

        public void Reset() { position = Vector2.zero; }

        public void Focus(string anchorId)
        {
            if (string.IsNullOrWhiteSpace(anchorId)) throw new ArgumentException("An anchor ID is required.", nameof(anchorId));
            pendingFocus = highlighted = anchorId;
            highlightUntil = Time.realtimeSinceStartup + 4f;
        }

        internal void Anchor(string id, Rect rect)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An anchor ID is required.", nameof(id));
            if (anchors.ContainsKey(id)) throw new ArgumentException("Duplicate anchor ID in this page: " + id);
            anchors.Add(id, rect);
            if (id == highlighted && Time.realtimeSinceStartup <= highlightUntil) Widgets.DrawHighlight(rect);
        }

        public void Draw(Rect viewport, Action<Listing_Standard> draw)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            float height = Mathf.Max(viewport.height, contentHeight);
            Rect content = new Rect(0f, 0f, Mathf.Max(1f, viewport.width - 20f), height);
            position.y = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, height - viewport.height));
            Widgets.BeginScrollView(viewport, ref position, content);
            MenuScrollView previous = Active;
            Active = this;
            anchors.Clear();
            try
            {
                var listing = new Listing_Standard { maxOneColumn = true };
                listing.Begin(content);
                try { draw(listing); }
                finally
                {
                    contentHeight = listing.CurHeight + 4f;
                    listing.End();
                }
                if (pendingFocus != null)
                {
                    if (anchors.TryGetValue(pendingFocus, out Rect target))
                        position.y = Mathf.Clamp(target.y - 12f, 0f, Mathf.Max(0f, contentHeight - viewport.height));
                    else Log.Warning("[IrisMenus] Anchor is not currently visible in page content: " + pendingFocus);
                    pendingFocus = null;
                }
            }
            finally { Active = previous; Widgets.EndScrollView(); }
        }
    }
}
