using System;
using System.Collections.Generic;
using Verse;

namespace IrisMenus
{
    internal sealed class MenuSearchHit
    {
        internal readonly MenuPage Page;
        internal readonly string Anchor;
        internal readonly string Title;
        internal readonly string Context;
        internal readonly string Path;
        internal readonly string Text;

        internal MenuSearchHit(MenuPage page, string anchor, string title, string context, string keywords)
        {
            Page = page;
            Anchor = anchor;
            Title = title;
            Context = context ?? "";
            Path = page.Owner.Content.Name + " / " + page.DisplayTitle;
            Text = Title + " " + Context + " " + keywords + " " + Path;
        }
    }

    // Incremental metadata enumeration and matching. No page draw callbacks are invoked here.
    internal sealed class MenuCatalog : IDisposable
    {
        internal const int PageSize = 20;
        private readonly IReadOnlyList<MenuPage> pages;
        private readonly List<MenuSearchHit> records = new List<MenuSearchHit>();
        private readonly List<MenuSearchHit> results = new List<MenuSearchHit>();
        private readonly HashSet<string> entryIds = new HashSet<string>(StringComparer.Ordinal);
        private IEnumerator<MenuSearchEntry> entries;
        private MenuPage current;
        private int pageCursor;
        private int matchCursor;
        private string query = "";
        private string scope;
        private string[] terms = new string[0];
        internal IReadOnlyList<MenuSearchHit> Results => results;
        internal bool Pending => pageCursor < pages.Count || current != null || matchCursor < records.Count;

        internal MenuCatalog(IReadOnlyList<MenuPage> pages) { this.pages = pages; }

        internal void Query(string text, string ownerId)
        {
            text = (text ?? "").Trim();
            if (query == text && scope == ownerId) return;
            query = text;
            scope = ownerId;
            terms = text.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            results.Clear();
            matchCursor = 0;
        }

        internal void Advance(int budget = 32)
        {
            for (int i = 0; i < budget && (pageCursor < pages.Count || current != null); i++)
            {
                try
                {
                    if (current == null)
                    {
                        current = pages[pageCursor++];
                        entryIds.Clear();
                        records.Add(new MenuSearchHit(current, null, current.DisplayTitle, "", ""));
                        entries = current.SearchProvider?.Invoke()?.GetEnumerator();
                        if (entries == null) EndPage();
                    }
                    else if (entries.MoveNext())
                    {
                        MenuSearchEntry entry = entries.Current;
                        if (entry == null) throw new InvalidOperationException("Search provider returned a null entry.");
                        if (!entryIds.Add(entry.Id)) throw new InvalidOperationException("Duplicate search entry ID: " + entry.Id);
                        records.Add(new MenuSearchHit(current, entry.Id, entry.Title() ?? current.DisplayTitle,
                            entry.Context?.Invoke(), entry.Keywords?.Invoke()));
                    }
                    else EndPage();
                }
                catch (Exception exception)
                {
                    Log.Error("[IrisMenus] Search index failed for " + current?.Id + ": " + exception);
                    EndPage();
                }
            }
            for (int i = 0; i < budget && matchCursor < records.Count; i++)
            {
                MenuSearchHit hit = records[matchCursor++];
                if (scope != null && MenuRegistry.OwnerId(hit.Page.Owner) != scope) continue;
                bool matches = true;
                foreach (string term in terms)
                    if (hit.Text.IndexOf(term, StringComparison.CurrentCultureIgnoreCase) < 0) { matches = false; break; }
                if (matches) results.Add(hit);
            }
        }

        private void EndPage()
        {
            try { entries?.Dispose(); }
            catch (Exception exception) { Log.Error("[IrisMenus] Search provider disposal failed: " + exception); }
            entries = null;
            current = null;
        }

        public void Dispose() { EndPage(); }
    }
}
