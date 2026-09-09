using System.Collections.Generic;
using Verse;

namespace IrisMenus
{
    internal sealed class MenuNavEntry
    {
        internal MenuPage Page;
        internal bool OwnerHeader;
        internal bool Child;
        internal string Title => OwnerHeader ? Page.Owner.Content.Name : Page.DisplayTitle;
    }

    internal static class MenuNavigation
    {
        internal static List<MenuNavEntry> Build(IReadOnlyList<MenuPage> pages, Mod selectedOwner,
            IReadOnlyDictionary<string, string> remembered)
        {
            var result = new List<MenuNavEntry>();
            var owners = new HashSet<string>();
            foreach (MenuPage page in pages)
            {
                string owner = MenuRegistry.OwnerId(page.Owner);
                if (!owners.Add(owner)) continue;
                int count = 0;
                foreach (MenuPage candidate in pages)
                    if (ReferenceEquals(candidate.Owner, page.Owner)) count++;
                if (count == 1 && !page.IsSubItem)
                { result.Add(new MenuNavEntry { Page = page }); continue; }
                MenuPage first = page;
                if (remembered.TryGetValue(owner, out string id))
                    foreach (MenuPage candidate in pages)
                        if (candidate.Id == id && ReferenceEquals(candidate.Owner, page.Owner)) first = candidate;
                result.Add(new MenuNavEntry { Page = first, OwnerHeader = true });
                if (!ReferenceEquals(selectedOwner, page.Owner)) continue;
                foreach (MenuPage child in pages)
                    if (ReferenceEquals(child.Owner, page.Owner))
                        result.Add(new MenuNavEntry { Page = child, Child = true });
            }
            return result;
        }
    }
}
