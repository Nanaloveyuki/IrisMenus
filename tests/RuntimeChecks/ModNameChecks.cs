using System;
using System.Collections.Generic;
using System.Linq;
using IrisMenus;
using Verse;

internal static class ModNameChecks
{
    internal static void Run(Action<bool, string> check)
    {
        LoadedLanguage previousLanguage = LanguageDatabase.activeLanguage;
        var pack = new ModContentPack { PackageIdPlayerFacing = "names.target", Name = "原始名称" };
        var owner = new Mod(pack);
        string ownerId = MenuRegistry.OwnerId(owner);
        try
        {
            // Translation mods can declare a package before the target has a Mod handle.
            MenuRegistry.RegisterModNameAlias(" NAMES.TARGET ", " English ", " Hunger and Havoc ");
            MenuRegistry.RegisterModNameAlias(owner, "ChineseSimplified", "饥与祸");
            LanguageDatabase.activeLanguage = new LoadedLanguage("English");
            check(MenuRegistry.GetModDisplayName(owner) == "Hunger and Havoc" && pack.Name == "原始名称",
                "Package aliases resolve without renaming game content");
            LanguageDatabase.activeLanguage = new LoadedLanguage("ChineseSimplified");
            check(MenuRegistry.GetModDisplayName(pack) == "饥与祸", "Self registration selects only the active language");
            LanguageDatabase.activeLanguage = new LoadedLanguage("French");
            check(MenuRegistry.GetModDisplayName(pack) == "原始名称", "Missing language alias preserves the original name");
            LanguageDatabase.activeLanguage = null;
            check(MenuRegistry.GetModDisplayName(pack) == "原始名称", "Names are available before language initialization");

            LanguageDatabase.activeLanguage = new LoadedLanguage("english");
            var category = new MenuPage("names.category", owner, () => "Overview", _ => { }, () => { }, false) { IsSubItem = true };
            var namedPage = new MenuPage("names.raw", owner, () => pack.Name, _ => { }, () => { }, false);
            var fallback = new MenuPage("names.fallback", owner, () => " ", _ => { }, () => { }, false);
            var broken = new MenuPage("names.broken", owner, () => throw new InvalidOperationException(), _ => { }, () => { }, false);
            var adapter = SettingsAdapter.Create(owner, () => "Untranslated category");
            check(adapter.DisplayTitle == "Hunger and Havoc" && namedPage.DisplayTitle == "Hunger and Havoc" &&
                fallback.DisplayTitle == "Hunger and Havoc" && broken.DisplayTitle == "Hunger and Havoc",
                "Aliases reach adapters, name-based pages and fallback titles");
            var rows = MenuNavigation.Build(new[] { category }, pack, new Dictionary<string, string>());
            check(rows[0].Title == "Hunger and Havoc" && rows[1].Title == "Overview" && category.DisplayTitle == "Overview",
                "Parent name is localized without replacing explicit category titles");
            check(MenuNavigation.Build(new[] { namedPage }, null, new Dictionary<string, string>()).Single().Title == "Hunger and Havoc",
                "Single-page Mod navigation uses its alias");
            CheckSearch(category, "Hunger Havoc", "Hunger and Havoc / Overview", check);
            CheckSearch(category, "原始名称", "Hunger and Havoc / Overview", check);

            MenuRegistry.RegisterModNameAlias(pack, "ENGLISH", "Translated replacement");
            check(MenuRegistry.GetModDisplayName(owner) == "Translated replacement" && MenuRegistry.OwnerId(owner) == ownerId,
                "Last registration replaces the language alias without changing persistent identity");
            CheckSearch(category, "Translated replacement", "Translated replacement / Overview", check);
            LanguageDatabase.activeLanguage = new LoadedLanguage("ChineseSimplified");
            check(rows[0].Title == "饥与祸" && adapter.DisplayTitle == "饥与祸", "Existing navigation and adapters follow language changes");

            var actionPack = new ModContentPack { PackageIdPlayerFacing = "names.actions", Name = "Actions" };
            MenuRegistry.RegisterModNameAlias(actionPack, "ChineseSimplified", "操作 Mod");
            MenuRegistry.RegisterOwnerAction(actionPack, "open", () => "Open", () => { });
            MenuPage launcher = MenuRegistry.Registered.Single(page => ReferenceEquals(page.Content, actionPack));
            check(launcher.DisplayTitle == "操作 Mod", "Action-only Mod launchers use localized names");

            Throws<ArgumentNullException>(() => MenuRegistry.RegisterModNameAlias((Mod)null, "English", "Alias"), check);
            Throws<ArgumentNullException>(() => MenuRegistry.RegisterModNameAlias((ModContentPack)null, "English", "Alias"), check);
            Throws<ArgumentException>(() => MenuRegistry.RegisterModNameAlias(" ", "English", "Alias"), check);
            Throws<ArgumentException>(() => MenuRegistry.RegisterModNameAlias(pack, " ", "Alias"), check);
            Throws<ArgumentException>(() => MenuRegistry.RegisterModNameAlias(pack, "English", " "), check);
            LanguageDatabase.activeLanguage = new LoadedLanguage("English");
            check(MenuRegistry.GetModDisplayName(pack) == "Translated replacement", "Rejected registrations do not replace a valid alias");
        }
        finally { LanguageDatabase.activeLanguage = previousLanguage; }
    }

    private static void CheckSearch(MenuPage page, string query, string path, Action<bool, string> check)
    {
        using (var catalog = new MenuCatalog(new[] { page }))
        {
            catalog.Query(query, null);
            while (catalog.Pending) catalog.Advance();
            check(catalog.Results.Count == 1 && catalog.Results[0].Path == path,
                "Search resolves localized paths for query: " + query);
        }
    }

    private static void Throws<T>(Action action, Action<bool, string> check) where T : Exception
    {
        try { action(); }
        catch (T) { check(true, "Alias registration rejects " + typeof(T).Name); return; }
        check(false, "Alias registration must reject " + typeof(T).Name);
    }
}
