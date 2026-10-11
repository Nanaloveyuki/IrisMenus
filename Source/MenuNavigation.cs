using System.Collections.Generic;
using Verse;

namespace IrisMenus
{
    internal sealed class MenuNavEntry
    {
        internal MenuPage Page;
        internal bool OwnerHeader;
        internal bool Child;
        internal bool ActionOnly;
        internal string Title => OwnerHeader ? Page.DisplayModName : !Child &&
            MenuRegistry.TryGetModNameAlias(Page.Content, out string alias) ? alias : Page.DisplayTitle;
    }

    internal static class MenuNavigation
    {
        internal static List<MenuNavEntry> Build(IReadOnlyList<MenuPage> pages, ModContentPack selectedContent,
            IReadOnlyDictionary<string, string> remembered)
        {
            var result = new List<MenuNavEntry>();
            var owners = new HashSet<string>();
            foreach (MenuPage page in pages)
            {
                string owner = MenuOwnerActions.ContentOwnerId(page.Content);
                if (!owners.Add(owner)) continue;
                int count = 0;
                bool launcherOnly = true;
                foreach (MenuPage candidate in pages)
                {
                    if (!SameContent(candidate, page)) continue;
                    count++;
                    if (!candidate.ActionLauncher) launcherOnly = false;
                }
                if (launcherOnly)
                { result.Add(new MenuNavEntry { Page = page, OwnerHeader = true, ActionOnly = true }); continue; }
                if (count == 1 && !page.IsSubItem)
                { result.Add(new MenuNavEntry { Page = page }); continue; }
                MenuPage first = page;
                string rememberedId = null;
                if (!remembered.TryGetValue(page.OwnerId, out rememberedId))
                    remembered.TryGetValue(owner, out rememberedId);
                if (rememberedId != null)
                    foreach (MenuPage candidate in pages)
                        if (candidate.Id == rememberedId && SameContent(candidate, page)) first = candidate;
                result.Add(new MenuNavEntry { Page = first, OwnerHeader = true });
                if (!ReferenceEquals(selectedContent, page.Content)) continue;
                foreach (MenuPage child in pages)
                    if (SameContent(child, page) && !child.ActionLauncher)
                        result.Add(new MenuNavEntry { Page = child, Child = true });
            }
            return result;
        }

        private static bool SameContent(MenuPage left, MenuPage right) =>
            ReferenceEquals(left.Content, right.Content);

    }
}
