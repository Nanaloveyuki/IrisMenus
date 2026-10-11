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
        private static readonly Dictionary<string, HashSet<string>> subItemRegistrations = new Dictionary<string, HashSet<string>>();
        private static readonly Dictionary<string, SearchDeclaration> adapterSearch = new Dictionary<string, SearchDeclaration>();
        private static readonly Dictionary<string, Dictionary<string, string>> modNameAliases =
            new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
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

        public static void RegisterOwnerAction(Mod owner, string id, Func<string> label, Action execute,
            Func<bool> visible = null, Func<bool> enabled = null, Func<string> confirmation = null) =>
            MenuOwnerActions.RegisterOwnerAction(owner, id, label, execute, visible, enabled, confirmation);

        public static void RegisterOwnerAction(ModContentPack owner, string id, Func<string> label, Action execute,
            Func<bool> visible = null, Func<bool> enabled = null, Func<string> confirmation = null) =>
            MenuOwnerActions.RegisterOwnerAction(owner, id, label, execute, visible, enabled, confirmation);

        // Language is the RimWorld language folder name (e.g. English or ChineseSimplified).
        public static void RegisterModNameAlias(Mod owner, string language, string alias)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            RegisterModNameAlias(owner.Content, language, alias);
        }

        public static void RegisterModNameAlias(ModContentPack content, string language, string alias)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            RegisterModNameAlias(content.PackageIdPlayerFacing, language, alias);
        }

        // A translation mod may register a target package before that target creates its Mod instance.
        public static void RegisterModNameAlias(string packageId, string language, string alias)
        {
            if (string.IsNullOrWhiteSpace(packageId)) throw new ArgumentException("A package ID is required.", nameof(packageId));
            if (string.IsNullOrWhiteSpace(language)) throw new ArgumentException("A language folder name is required.", nameof(language));
            if (string.IsNullOrWhiteSpace(alias)) throw new ArgumentException("A Mod name alias is required.", nameof(alias));
            packageId = packageId.Trim();
            language = language.Trim();
            alias = alias.Trim();
            if (!modNameAliases.TryGetValue(packageId, out Dictionary<string, string> names))
            {
                names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                modNameAliases.Add(packageId, names);
            }
            if (names.TryGetValue(language, out string previous) && previous == alias) return;
            names[language] = alias;
            Revision++;
        }

        public static string GetModDisplayName(Mod owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            return GetModDisplayName(owner.Content);
        }

        public static string GetModDisplayName(ModContentPack content)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));
            return TryGetModNameAlias(content, out string alias) ? alias : content.Name;
        }

        internal static bool TryGetModNameAlias(ModContentPack content, out string alias)
        {
            alias = null;
            string language = LanguageDatabase.activeLanguage?.folderName;
            return language != null && modNameAliases.TryGetValue(content.PackageIdPlayerFacing, out Dictionary<string, string> names)
                && names.TryGetValue(language, out alias);
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
            MenuPage page = FindRegistered(owner, pageId);
            page.Focus = scroll.Focus;
            page.ListingScroll = scroll;
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

        // Call on the game thread. Replaces only pages previously declared under this registration id.
        public static void ReplaceSubItemListings(Mod owner, string registrationId, IEnumerable<MenuSubItemListing> listings)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (string.IsNullOrWhiteSpace(registrationId)) throw new ArgumentException("A registration ID is required.", nameof(registrationId));
            if (listings == null) throw new ArgumentNullException(nameof(listings));
            var next = new List<MenuSubItemListing>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (MenuSubItemListing listing in listings)
            {
                if (listing == null) throw new ArgumentNullException(nameof(listings), "A listing is required.");
                if (!ids.Add(listing.PageId))
                    throw new ArgumentException("Duplicate page ID in replacement: " + listing.PageId, nameof(listings));
                next.Add(listing);
            }
            string ownerId = OwnerId(owner);
            string key = SubItemRegistrationKey(owner, registrationId);
            subItemRegistrations.TryGetValue(key, out HashSet<string> ownedIds);
            ownedIds = ownedIds ?? new HashSet<string>(StringComparer.Ordinal);
            foreach (MenuSubItemListing listing in next)
            {
                string id = ownerId + ":" + listing.PageId;
                MenuPage collision = registered.FirstOrDefault(page => page.Id == id);
                if (collision != null && (!ReferenceEquals(collision.Owner, owner) || !ownedIds.Contains(listing.PageId)))
                    throw new ArgumentException("Page already registered: " + id, nameof(listings));
            }
            var retained = new List<MenuPage>();
            for (int i = registered.Count - 1; i >= 0; i--)
            {
                MenuPage page = registered[i];
                if (!ReferenceEquals(page.Owner, owner) || !ownedIds.Contains(PageLocalId(page.Id, ownerId))) continue;
                retained.Add(page);
                registered.RemoveAt(i);
            }
            retained.Reverse();
            int insertAt = registered.FindIndex(page => ReferenceEquals(page.Owner, owner));
            if (insertAt < 0) insertAt = registered.Count;
            var updated = new HashSet<string>(StringComparer.Ordinal);
            foreach (MenuSubItemListing listing in next)
            {
                string id = ownerId + ":" + listing.PageId;
                MenuPage existing = retained.FirstOrDefault(page => page.Id == id);
                Action save = listing.Save ?? owner.WriteSettings;
                MenuScrollView scroll = existing?.ListingScroll ?? new MenuScrollView();
                MenuPage page = existing ?? new MenuPage(id, owner, listing.Title, null, save, false);
                page.Title = listing.Title;
                page.Draw = rect => scroll.Draw(rect, listing.Draw);
                page.Save = save;
                page.IsSubItem = true;
                page.RegistrationId = registrationId;
                page.RegistrationIndex = updated.Count;
                page.Focus = scroll.Focus;
                page.ListingScroll = scroll;
                page.SearchProvider = listing.SearchProvider;
                registered.Insert(insertAt, page);
                insertAt++;
                updated.Add(listing.PageId);
            }

            if (updated.Count == 0) subItemRegistrations.Remove(key);
            else subItemRegistrations[key] = updated;
            Revision++;
        }

        internal static MenuPage ReplacementFor(MenuPage selected, IReadOnlyList<MenuPage> pages)
        {
            if (selected == null || pages == null) return selected;
            foreach (MenuPage page in pages)
                if (ReferenceEquals(page, selected) || page.Id == selected.Id) return page;
            if (selected.RegistrationId != null)
            {
                MenuPage fallback = null;
                int distance = int.MaxValue;
                foreach (MenuPage page in pages)
                {
                    if (!ReferenceEquals(page.Owner, selected.Owner) || page.RegistrationId != selected.RegistrationId) continue;
                    int candidateDistance = Math.Abs(page.RegistrationIndex - selected.RegistrationIndex);
                    if (candidateDistance >= distance) continue;
                    fallback = page;
                    distance = candidateDistance;
                }
                if (fallback != null) return fallback;
            }

            foreach (MenuPage page in pages)
                if (ReferenceEquals(page.Owner, selected.Owner) && ReferenceEquals(page.Content, selected.Content)) return page;
            return null;
        }

        private static string SubItemRegistrationKey(Mod owner, string registrationId) => OwnerId(owner) + "\n" + registrationId;

        private static string PageLocalId(string fullId, string ownerId)
        {
            string prefix = ownerId + ":";
            return fullId.StartsWith(prefix, StringComparison.Ordinal) ? fullId.Substring(prefix.Length) : fullId;
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
        internal static bool HasContentPages(ModContentPack content) =>
            registered.Any(page => !page.ActionLauncher && ReferenceEquals(page.Content, content));

        internal static void RegisterLauncher(ModContentPack content, string id)
        {
            if (content == null) return;
            MenuPage launcher = registered.FirstOrDefault(page => page.ActionLauncher && ReferenceEquals(page.Content, content));
            Mod owner = null;
            foreach (Mod mod in LoadedModManager.ModHandles)
                if (ReferenceEquals(mod.Content, content)) { owner = mod; break; }
            if (launcher != null)
            {
                if (launcher.Owner == null) launcher.Owner = owner;
                return;
            }
            if (registered.Any(page => ReferenceEquals(page.Content, content))) return;
            registered.Add(new MenuPage(id, content, owner));
            Revision++;
        }

        internal static void AdoptActionOwners()
        {
            foreach (MenuPage page in registered)
            {
                if (!page.ActionLauncher || page.Owner != null) continue;
                foreach (Mod mod in LoadedModManager.ModHandles)
                {
                    if (!ReferenceEquals(mod.Content, page.Content)) continue;
                    page.Owner = mod;
                    break;
                }
            }
        }

        internal static void ResetListingScroll()
        {
            foreach (MenuPage page in registered) page.ListingScroll?.Reset();
        }

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
        internal static bool HasPages(Mod mod) => registered.Any(page => !page.ActionLauncher && ReferenceEquals(page.Owner, mod));
        internal static IEnumerable<MenuPage> Registered => registered;

        internal static MenuPage ResolveEntry(Mod owner)
        {
            if (owner == null || owner is IrisMenusMod) return null;
            var owned = registered.Where(page => !page.ActionLauncher && ReferenceEquals(page.Owner, owner)).ToList();
            MenuPage native = owned.FirstOrDefault();
            if (IrisMenusMod.Instance.Settings.RememberState && IrisMenusMod.Instance.Settings.LastSubItems.TryGetValue(OwnerId(owner), out string last))
                native = owned.FirstOrDefault(page => page.Id == last) ?? native;
            if (native != null) return native;
            if (!IrisMenusMod.Instance.Settings.EnabledAdapters.Contains(OwnerId(owner))) return null;
            return AttachAdapterSearch(SettingsAdapter.Create(owner, owner.SettingsCategory));
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
                    pages.Add(AttachAdapterSearch(SettingsAdapter.Create(mod, () => title)));
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
        internal Mod Owner;
        internal readonly ModContentPack Content;
        internal string OwnerId => Owner == null ? MenuOwnerActions.ContentOwnerId(Content) : MenuRegistry.OwnerId(Owner);
        internal Func<string> Title;
        internal Action<Rect> Draw;
        internal Action Save;
        internal readonly bool Adapted;
        internal bool IsSubItem;
        internal bool ActionLauncher;
        internal string RegistrationId;
        internal int RegistrationIndex;
        internal Func<IEnumerable<MenuSearchEntry>> SearchProvider;
        internal Action<string> Focus;
        internal MenuScrollView ListingScroll;
        internal string DisplayModName => MenuRegistry.GetModDisplayName(Content);
        private bool titleFailed;

        internal string DisplayTitle
        {
            get
            {
                try
                {
                    if (Adapted && MenuRegistry.TryGetModNameAlias(Content, out string alias)) return alias;
                    string title = Title();
                    return string.IsNullOrWhiteSpace(title) || title == Content.Name ? DisplayModName : title;
                }
                catch (Exception exception)
                {
                    if (!titleFailed) Log.Error("[IrisMenus] Cannot read page title " + Id + ": " + exception);
                    titleFailed = true;
                    return DisplayModName;
                }
            }
        }

        internal MenuPage(string id, Mod owner, Func<string> title, Action<Rect> draw, Action save, bool adapted)
        {
            Id = id;
            Owner = owner;
            Content = owner?.Content;
            Title = title;
            Draw = draw;
            Save = save;
            Adapted = adapted;
        }

        internal MenuPage(string id, ModContentPack content, Mod owner)
            : this(id, owner, () => MenuRegistry.GetModDisplayName(content), _ => { }, () => { }, false)
        {
            Content = content;
            ActionLauncher = true;
        }
    }

    public sealed class MenuSubItemListing
    {
        public string PageId { get; }
        public Func<string> Title { get; }
        public Action<Listing_Standard> Draw { get; }
        public Action Save { get; }
        public Func<IEnumerable<MenuSearchEntry>> SearchProvider { get; }


        public MenuSubItemListing(string pageId, Func<string> title, Action<Listing_Standard> draw,
            Action save = null, Func<IEnumerable<MenuSearchEntry>> searchProvider = null)
        {
            if (string.IsNullOrWhiteSpace(pageId)) throw new ArgumentException("A page ID is required.", nameof(pageId));
            PageId = pageId;
            Title = title ?? throw new ArgumentNullException(nameof(title));
            Draw = draw ?? throw new ArgumentNullException(nameof(draw));
            Save = save;
            SearchProvider = searchProvider;
        }
    }


}
