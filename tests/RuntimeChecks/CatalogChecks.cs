using System;
using System.Collections.Generic;
using System.Linq;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class CatalogChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var owner = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "catalog.a", Name = "Alpha" } };
        var other = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "catalog.b", Name = "Beta" } };
        int draws = 0;
        MenuRegistry.Register(owner, "legacy-one", () => "Legacy 1", _ => draws++);
        MenuRegistry.Register(owner, "legacy-two", () => "Legacy 2", _ => draws++);
        var legacy = MenuRegistry.Registered.Where(p => ReferenceEquals(p.Owner, owner)).ToList();
        var remembered = new Dictionary<string, string>();
        var rows = MenuNavigation.Build(legacy, owner, remembered);
        check(rows.Count == 3 && rows[0].OwnerHeader && rows.Skip(1).All(r => r.Child), "Multiple legacy pages group without changing registration API");
        check(MenuNavigation.Build(new[] { legacy[0] }, owner, remembered).Single().OwnerHeader == false, "Single legacy page opens directly");
        remembered[MenuRegistry.OwnerId(owner)] = legacy[1].Id;
        check(MenuNavigation.Build(legacy, null, remembered).Single().Page == legacy[1], "Legacy group remembers selected category");
        IrisMenusMod.Instance.Settings.LastSubItems[MenuRegistry.OwnerId(owner)] = legacy[1].Id;
        check(MenuRegistry.ResolveEntry(owner) == legacy[1], "Original entry restores legacy category");
        MenuRegistry.RegisterSubItemListing(owner, "general", () => "General", _ => draws++);
        MenuRegistry.RegisterSubItemListing(owner, "advanced", () => "Advanced", _ => draws++);
        MenuRegistry.RegisterSubItem(other, "other", () => "Other", _ => draws++);
        var pages = MenuRegistry.Registered.Where(p => p.IsSubItem).ToList();
        MenuPage general = pages.First(p => p.Id.EndsWith(":general"));
        MenuPage advanced = pages.First(p => p.Id.EndsWith(":advanced"));
        rows = MenuNavigation.Build(pages, null, remembered);
        check(rows.Count == 2 && rows.All(r => r.OwnerHeader), "Explicit categories collapse initially");
        rows = MenuNavigation.Build(pages, owner, remembered);
        check(rows.Count(r => r.Child) == 2 && rows.Where(r => r.Child).All(r => ReferenceEquals(r.Page.Owner, owner)), "Only selected owner's children expand");
        remembered[MenuRegistry.OwnerId(owner)] = advanced.Id;
        check(MenuNavigation.Build(pages, null, remembered).First().Page == advanced, "Owner restores last category");
        IrisMenusMod.Instance.Settings.LastSubItems[MenuRegistry.OwnerId(owner)] = advanced.Id;
        check(MenuRegistry.ResolveEntry(owner) == advanced, "Original entry restores explicitly selected category");

        int yielded = 0, disposed = 0;
        MenuRegistry.RegisterSearchProvider(owner, "general", () => Entries(300, () => yielded++, () => disposed++));
        MenuRegistry.RegisterSearchProvider(owner, "advanced", () => new[]
        { new MenuSearchEntry("advanced", () => "Shadow strength", () => "night darkness", () => "Requires lighting") });
        using (var catalog = new MenuCatalog(pages))
        {
            check(yielded == 0 && draws == 0, "Construction does not enumerate or draw pages");
            catalog.Advance(4);
            check(yielded <= 4 && catalog.Pending && draws == 0, "Index work is bounded per advance");
            for (int i = 0; i < 200 && catalog.Pending; i++) catalog.Advance(4);
            check(!catalog.Pending && yielded == 300 && disposed == 1, "Index completes incrementally and disposes provider");
            check(catalog.Results.Count == 304, "Directory includes pages and declared setting entries");
            catalog.Query("NIGHT darkness", MenuRegistry.OwnerId(owner));
            while (catalog.Pending) catalog.Advance(7);
            check(catalog.Results.Count == 1 && catalog.Results[0].Page == advanced, "Case-insensitive AND keywords search across categories");
            catalog.Query("requires", MenuRegistry.OwnerId(other));
            while (catalog.Pending) catalog.Advance(7);
            check(catalog.Results.Count == 0, "Current mod search cannot escape owner scope");
            catalog.Query("requires", null);
            while (catalog.Pending) catalog.Advance(7);
            check(catalog.Results.Count == 1 && catalog.Results[0].Context == "Requires lighting", "Global search returns context and destination");
            check(yielded == 300 && draws == 0, "Changing queries neither rebuilds metadata nor executes UI");
            catalog.Query("", null);
            while (catalog.Pending) catalog.Advance(7);
            check(catalog.Results.Skip(MenuCatalog.PageSize).Take(MenuCatalog.PageSize).Count() == 20, "Directory page is bounded to 20 entries");
        }
        using (var partial = new MenuCatalog(new[] { general })) { partial.Advance(2); }
        check(disposed == 2, "Closing during indexing disposes an unfinished iterator");
        int errors = Log.Errors.Count;
        MenuRegistry.RegisterSearchProvider(owner, "advanced", () => throw new InvalidOperationException("provider"));
        using (var broken = new MenuCatalog(new[] { advanced, general }))
        {
            while (broken.Pending) broken.Advance();
            check(broken.Results.Count == 302 && Log.Errors.Count == errors + 1, "Provider failure preserves page result and other providers");
        }
        var adapterOwner = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "catalog.adapter", Name = "Adapter" } };
        LoadedModManager.ModHandles.Add(adapterOwner);
        int adapterReads = 0;
        MenuRegistry.RegisterAdapterSearch(adapterOwner, () => { adapterReads++; return new MenuSearchEntry[0]; });
        check(MenuRegistry.ResolveEntry(adapterOwner) == null && adapterReads == 0, "Adapter metadata does not opt in or execute hooks");
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(MenuRegistry.OwnerId(adapterOwner));
        MenuPage adapted = MenuRegistry.ResolveEntry(adapterOwner);
        check(!adapted.IsSubItem && adapted.SearchProvider != null, "Adapted page gains only explicitly declared search metadata");

        var state = new MenuBrowseState();
        state.Search("shadow"); state.ResultPage = 3; state.OpenTarget(); state.Back();
        check(state.Global && state.Query == "shadow" && state.ResultPage == 3 && state.ShowResults, "Back restores global query and page");
        state.Navigate(advanced);
        check(state.Scope == MenuRegistry.OwnerId(owner) && state.Query == "" && !state.ShowResults, "Explicit navigation changes search scope and opens content");
        state.Search("night");
        check(state.ShowResults && state.ResultPage == 0, "Editing current-mod search opens results");

        Anchors(check);
        Sections(check);
    }

    private static IEnumerable<MenuSearchEntry> Entries(int count, Action next, Action dispose)
    {
        try
        {
            for (int i = 0; i < count; i++)
            {
                int index = i;
                next();
                yield return new MenuSearchEntry("entry-" + index, () => "Setting " + index, () => "keyword");
            }
        }
        finally { dispose(); }
    }

    private static void Anchors(Action<bool, string> check)
    {
        var scroll = new MenuScrollView();
        Rect viewport = new Rect(0, 0, 400, 300);
        Action<Listing_Standard> draw = list =>
        {
            MenuScrollView.Active.Anchor("target", new Rect(0, 700, 300, 30));
            list.CurHeight = 1000;
        };
        scroll.Focus("target"); scroll.Draw(viewport, draw); scroll.Draw(viewport, draw);
        check(Widgets.LastPosition.y == 688, "Anchor scroll is measured from current layout");
        check(Widgets.Highlights > 0, "Focused anchor highlights in its actual page");
        check(MenuScrollView.Active == null && Widgets.Groups == 0, "Anchor context and groups are restored");
        scroll.Focus("target");
        scroll.Draw(viewport, list => { MenuScrollView.Active.Anchor("target", new Rect(0, 500, 300, 30)); list.CurHeight = 1000; });
        scroll.Draw(viewport, draw);
        check(Widgets.LastPosition.y == 488, "Focus does not reuse stale pixel coordinates");
        int warnings = Log.Errors.Count;
        scroll.Focus("conditional-hidden"); scroll.Draw(viewport, draw);
        check(Log.Errors.Count == warnings + 1, "Unavailable conditional anchor is diagnosed");
    }

    private static void Sections(Action<bool, string> check)
    {
        int measures = 0;
        var drawn = new List<int>();
        var sections = Enumerable.Range(0, 100).Select(i => new MenuSection("s" + i,
            width => { measures++; return 100; }, rect => drawn.Add(i))).ToArray();
        var view = new MenuSectionView(sections);
        Rect viewport = new Rect(0, 0, 400, 250);
        view.Draw(viewport);
        check(measures == 100 && drawn.SequenceEqual(new[] { 0, 1, 2 }), "Only viewport-intersecting sections draw");
        drawn.Clear(); view.Draw(viewport);
        check(measures == 100 && drawn.Count == 3, "Section measurements are cached");
        drawn.Clear(); view.Focus("s80"); view.Draw(viewport);
        check(drawn.Contains(80) && !drawn.Contains(0) && drawn.Count <= 4, "Jump draws target sections without drawing skipped content");
        view.InvalidateLayout(); view.Draw(viewport);
        check(measures == 200, "Explicit layout invalidation remeasures");
        check(Widgets.Groups == 0, "Section drawing balances groups");
    }
}
