using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public static class MenuRegistry
    {
        private static readonly List<MenuPage> registered = new List<MenuPage>();
        private static readonly Dictionary<string, SearchDeclaration> adapterSearch = new Dictionary<string, SearchDeclaration>();
        internal static int Revision { get; private set; }

        // Call on the game thread. The owner keeps ownership of its settings.
        public static void Register(Mod owner, string pageId, Func<string> title,
            Action<Rect> draw, Action save = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(pageId)) throw new ArgumentException("A page ID is required.", nameof(pageId));
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            string id = OwnerId(owner) + ":" + pageId;
            if (registered.Any(page => page.Id == id))
                throw new ArgumentException("Page already registered: " + id, nameof(pageId));
            registered.Add(new MenuPage(id, owner, title, draw, save ?? owner.WriteSettings, false));
            Revision++;
        }

        public static void Open()
        {
            if (!Find.WindowStack.IsOpen<MenuWindow>()) Find.WindowStack.Add(new MenuWindow());
        }

        public static void RegisterListing(Mod owner, string pageId, Func<string> title,
            Action<Listing_Standard> draw, Action save = null)
        {
            if (draw == null) throw new ArgumentNullException(nameof(draw));
            var scroll = new MenuScrollView();
            Register(owner, pageId, title, rect => scroll.Draw(rect, draw), save);
            FindRegistered(owner, pageId).Focus = scroll.Focus;
        }

        public static void RegisterSubItem(Mod owner, string pageId, Func<string> title, Action<Rect> draw, Action save = null)
        {
            Register(owner, pageId, title, draw, save);
            FindRegistered(owner, pageId).IsSubItem = true;
        }

        public static void RegisterSubItemListing(Mod owner, string pageId, Func<string> title,
            Action<Listing_Standard> draw, Action save = null)
        {
            RegisterListing(owner, pageId, title, draw, save);
            FindRegistered(owner, pageId).IsSubItem = true;
        }

        public static void RegisterSearchProvider(Mod owner, string pageId,
            Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null)
        {
            MenuPage page = FindRegistered(owner, pageId);
            page.SearchProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            if (focus != null) page.Focus = focus;
            Revision++;
        }

        public static void RegisterAdapterSearch(Mod owner, Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (provider == null) throw new ArgumentNullException(nameof(provider));
            adapterSearch[OwnerId(owner)] = new SearchDeclaration { Provider = provider, Focus = focus };
            Revision++;
        }

        public static void InvalidateSearchIndex() { Revision++; }

        private static MenuPage FindRegistered(Mod owner, string pageId)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            string id = OwnerId(owner) + ":" + pageId;
            return registered.FirstOrDefault(page => page.Id == id)
                ?? throw new ArgumentException("Register the page before declaring its search provider: " + id);
        }

        private static MenuPage AttachAdapterSearch(MenuPage page)
        {
            if (adapterSearch.TryGetValue(OwnerId(page.Owner), out SearchDeclaration declaration))
            {
                page.SearchProvider = declaration.Provider;
                page.Focus = declaration.Focus;
            }
            return page;
        }

        private sealed class SearchDeclaration
        {
            internal Func<IEnumerable<MenuSearchEntry>> Provider;
            internal Action<string> Focus;
        }

        internal static string OwnerId(Mod mod) => mod.Content.PackageIdPlayerFacing + ":" + mod.GetType().FullName;
        internal static bool HasPages(Mod mod) => registered.Any(page => ReferenceEquals(page.Owner, mod));
        internal static IEnumerable<MenuPage> Registered => registered;

        internal static MenuPage ResolveEntry(Mod owner)
        {
            if (owner == null || owner is IrisMenusMod) return null;
            var owned = registered.Where(page => ReferenceEquals(page.Owner, owner)).ToList();
            MenuPage native = owned.FirstOrDefault();
            if (IrisMenusMod.Instance.Settings.LastSubItems.TryGetValue(OwnerId(owner), out string last))
                native = owned.FirstOrDefault(page => page.Id == last) ?? native;
            if (native != null) return native;
            if (!IrisMenusMod.Instance.Settings.EnabledAdapters.Contains(OwnerId(owner))) return null;
            return AttachAdapterSearch(new MenuPage(OwnerId(owner), owner, owner.SettingsCategory,
                owner.DoSettingsWindowContents, owner.WriteSettings, true));
        }

        internal static List<MenuPage> Adaptable()
        {
            var pages = new List<MenuPage>();
            foreach (Mod mod in LoadedModManager.ModHandles)
            {
                if (mod is IrisMenusMod || HasPages(mod)) continue;
                try
                {
                    string title = mod.SettingsCategory();
                    if (string.IsNullOrWhiteSpace(title)) continue;
                    pages.Add(AttachAdapterSearch(new MenuPage(OwnerId(mod), mod, () => title,
                        mod.DoSettingsWindowContents, mod.WriteSettings, true)));
                }
                catch (Exception exception)
                {
                    Log.Error("[IrisMenus] Cannot read settings category for " + mod.GetType().FullName + ": " + exception);
                }
            }
            return pages.OrderBy(page => page.DisplayTitle).ToList();
        }
    }

    internal sealed class MenuPage
    {
        internal readonly string Id;
        internal readonly Mod Owner;
        internal readonly Func<string> Title;
        internal readonly Action<Rect> Draw;
        internal readonly Action Save;
        internal readonly bool Adapted;
        internal bool IsSubItem;
        internal Func<IEnumerable<MenuSearchEntry>> SearchProvider;
        internal Action<string> Focus;
        private bool titleFailed;

        internal string DisplayTitle
        {
            get
            {
                try
                {
                    string title = Title();
                    return string.IsNullOrWhiteSpace(title) ? Owner.Content.Name : title;
                }
                catch (Exception exception)
                {
                    if (!titleFailed) Log.Error("[IrisMenus] Cannot read page title " + Id + ": " + exception);
                    titleFailed = true;
                    return Owner.Content.Name;
                }
            }
        }

        internal MenuPage(string id, Mod owner, Func<string> title, Action<Rect> draw, Action save, bool adapted)
        {
            Id = id;
            Owner = owner;
            Title = title;
            Draw = draw;
            Save = save;
            Adapted = adapted;
        }
    }
}
