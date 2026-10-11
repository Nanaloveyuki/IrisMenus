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
        var rows = MenuNavigation.Build(legacy, owner.Content, remembered);
        check(rows.Count == 3 && rows[0].OwnerHeader && rows.Skip(1).All(r => r.Child), "Multiple legacy pages group without changing registration API");
        check(MenuNavigation.Build(new[] { legacy[0] }, owner.Content, remembered).Single().OwnerHeader == false, "Single legacy page opens directly");
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
        rows = MenuNavigation.Build(pages, owner.Content, remembered);
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
        var captured = new Dictionary<string, string>();
        state.Capture(captured, null);
        var restored = new MenuBrowseState();
        restored.Restore(captured, null, true);
        check(restored.Global && restored.Query == "shadow" && restored.ResultPage == 3 && restored.ShowResults, "Restored global search keeps query and page");
        state.OpenTarget(); captured.Clear(); state.Capture(captured, advanced);
        restored.Restore(captured, advanced, true); restored.Back();
        check(restored.Global && restored.Query == "shadow" && restored.ResultPage == 3, "Restored global target returns to global results");
        state.Navigate(advanced); captured.Clear(); state.Capture(captured, advanced);
        restored.Restore(captured, advanced, true);
        check(restored.Scope == MenuRegistry.OwnerId(owner) && !restored.ShowResults, "Reopen restores a selected page without turning it into the global catalog");
        state.Manage(); state.Search("adapter"); captured.Clear(); state.Capture(captured, null);
        restored.Restore(captured, null, true);
        check(restored.Managing && restored.Query == "adapter", "Adapter management restores its filter");
        state.Navigate(advanced); state.Search("night"); state.ResultPage = 2; state.OpenTarget();
        captured.Clear(); state.Capture(captured, advanced);
        restored.Restore(captured, advanced, true);
        check(restored.ViewingTarget && restored.Scope == MenuRegistry.OwnerId(owner) && restored.Query == "night" && restored.ResultPage == 2, "Restored target keeps owner search");
        restored.Back();
        check(restored.ShowResults && restored.Query == "night" && restored.ResultPage == 2, "Back returns to restored owner results");
        restored.Restore(captured, null, false);
        check(restored.Global && !restored.ViewingTarget && restored.Query == "night", "Missing scope and target fall back to global results");
        captured["mode"] = "target"; captured.Remove("target");
        restored.Restore(captured, null, true);
        check(!restored.ViewingTarget && restored.ShowResults, "Target mode without a target does not open a page");
        state.Navigate(advanced);
        check(state.Scope == MenuRegistry.OwnerId(owner) && state.Query == "" && !state.ShowResults, "Explicit navigation changes search scope and opens content");
        state.Search("night");
        check(state.ShowResults && state.ResultPage == 0, "Editing current-mod search opens results");
        var reloaded = new MenuScrollView { PositionY = 240f };
        Rect restoredViewport = new Rect(0, 0, 400, 300);
        reloaded.Draw(restoredViewport, list => list.CurHeight = 1000);
        reloaded.Draw(restoredViewport, list => list.CurHeight = 1000);
        check(Widgets.LastPosition.y == 240f, "Restored listing offset survives its first content measurement");
        reloaded.Reset(); reloaded.Draw(restoredViewport, list => list.CurHeight = 1000);
        check(Widgets.LastPosition.y == 0f, "Disabled memory resets visible listing scroll");
        Anchors(check);
        ReplaceableListings(check);
        Sections(check);
    }

    private static void ReplaceableListings(Action<bool, string> check)
    {
        var owner = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "catalog.replace", Name = "Replace" } };
        var other = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "catalog.other", Name = "Other" } };
        int draws = 0, saves = 0;
        MenuRegistry.RegisterSubItemListing(other, "kept", () => "Kept", _ => draws++);
        MenuRegistry.Register(owner, "manual", () => "Manual", _ => draws++);
        MenuPage otherPage = MenuRegistry.Registered.Single(page => ReferenceEquals(page.Owner, other));
        MenuPage manual = MenuRegistry.Registered.Single(page => page.Id.EndsWith(":manual"));
        int revision = MenuRegistry.Revision;
        int before = MenuRegistry.Registered.Count();
        Action<Listing_Standard> stale = _ => draws += 100;
        try
        {
            MenuRegistry.ReplaceSubItemListings(owner, "dsl", new[]
            {
                new MenuSubItemListing("general", () => "General", stale),
                new MenuSubItemListing("general", () => "Duplicate", _ => draws++)
            });
            check(false, "Duplicate replacement IDs throw");
        }
        catch (ArgumentException) { check(true, "Duplicate replacement IDs throw"); }
        check(MenuRegistry.Revision == revision && MenuRegistry.Registered.Count() == before, "Invalid replacement does not mutate registry");
        try
        {
            MenuRegistry.ReplaceSubItemListings(owner, "dsl", new[]
            {
                new MenuSubItemListing("manual", () => "Collision", _ => draws++)
            });
            check(false, "Replacement cannot take a page outside its registration");
        }
        catch (ArgumentException) { check(true, "Replacement cannot take a page outside its registration"); }
        check(ReferenceEquals(MenuRegistry.Registered.Single(page => page.Id.EndsWith(":manual")), manual), "Rejected collision keeps the original page");

        MenuRegistry.ReplaceSubItemListings(owner, "dsl", new[]
        {
            new MenuSubItemListing("general", () => "General", stale, () => saves++, () => new[] { new MenuSearchEntry("old", () => "Old") }),
            new MenuSubItemListing("advanced", () => "Advanced", _ => draws++)
        });
        var owned = MenuRegistry.Registered.Where(page => ReferenceEquals(page.Owner, owner) && page.IsSubItem).ToList();
        MenuPage general = owned.Single(page => page.Id.EndsWith(":general"));
        MenuPage advanced = owned.Single(page => page.Id.EndsWith(":advanced"));
        check(MenuRegistry.Revision == revision + 1, "Successful replacement increments revision once");
        check(ReferenceEquals(MenuRegistry.Registered.Single(page => ReferenceEquals(page.Owner, other)), otherPage), "Other registrations stay registered");
        general.ListingScroll.PositionY = 180f;
        int updatedSaves = 0;
        MenuRegistry.ReplaceSubItemListings(owner, "dsl", new[]
        {
            new MenuSubItemListing("general", () => "Updated", _ => draws++, () => updatedSaves++),
            new MenuSubItemListing("extra", () => "Extra", _ => draws++)
        });
        MenuPage updated = MenuRegistry.Registered.Single(page => page.Id == general.Id);
        check(ReferenceEquals(updated, general) && ReferenceEquals(updated.ListingScroll, general.ListingScroll), "Stable page retains page and listing scroll");
        check(updated.ListingScroll.PositionY == 180f && updated.DisplayTitle == "Updated", "Retained page keeps scroll and updates callbacks");
        updated.Save();
        check(updatedSaves == 1 && saves == 0, "Updated save replaces the previous callback");
        check(MenuRegistry.Registered.All(page => page.Id != advanced.Id), "Omitted page is removed");
        check(MenuRegistry.Registered.Count(page => page.Id.EndsWith(":extra")) == 1, "Replacement adds the new page once");
        var openPages = MenuRegistry.Registered.Where(page => ReferenceEquals(page.Owner, owner) || ReferenceEquals(page.Owner, other)).ToList();
        MenuPage fallback = MenuSession.ResolveReplacement(advanced, openPages);
        MenuPage retained = MenuSession.ResolveReplacement(general, openPages);
        check(fallback != null && fallback.Id.EndsWith(":extra") && ReferenceEquals(fallback.Owner, owner), "Removed selection falls back within the same registration");

        check(ReferenceEquals(retained, general), "Same page ID remains selected");
        check(MenuSession.ResolveReplacement(otherPage, openPages.Where(page => !ReferenceEquals(page, otherPage)).ToList()) == null, "Removing the last owner page clears selection");
        MenuRegistry.ReplaceSubItemListings(owner, "dsl", new MenuSubItemListing[0]);
        check(!MenuRegistry.Registered.Any(page => ReferenceEquals(page.Owner, owner) && (page.Id == general.Id || page.Id.EndsWith(":extra"))), "Empty replacement removes this registration");
        check(MenuRegistry.Registered.Any(page => page.Id == manual.Id), "Empty replacement does not remove unrelated pages");
        OwnerActions(check);

    }
    private static void OwnerActions(Action<bool, string> check)
    {
        var pack = new ModContentPack { PackageIdPlayerFacing = "actions.pack", Name = "Actions", RootDir = "actions-root" };
        var settings = new Mod { Content = pack };
        MenuRegistry.RegisterOwnerAction(pack, "edit", () => "Edit", () => { });
        MenuPage launcher = MenuRegistry.Registered.Single(page => page.ActionLauncher && ReferenceEquals(page.Content, pack));
        check(launcher.Id == MenuOwnerActions.ContentOwnerId(pack) + ":actions", "Content-only pack gets a stable action launcher");
        var browse = new MenuBrowseState();
        browse.Navigate(launcher);
        var captured = new Dictionary<string, string>();
        browse.Capture(captured, launcher);
        var restored = new MenuBrowseState();
        restored.Restore(captured, launcher, true);
        check(restored.Scope == MenuOwnerActions.ContentOwnerId(pack) && !restored.Global && !restored.ShowResults,
            "Content-only launcher reopens as its own page, not the global catalog");
        check(!MenuRegistry.HasPages(settings), "Action launcher does not count as native settings");
        LoadedModManager.ModHandles.Add(settings);
        check(MenuRegistry.Adaptable().Any(page => ReferenceEquals(page.Owner, settings)), "Settings adapter stays available beside an action launcher");
        MenuRegistry.AdoptActionOwners();
        check(ReferenceEquals(launcher.Owner, settings), "Launcher adopts the real mod handle for the same content pack");
        MenuRegistry.Register(settings, "general", () => "General", _ => { });
        var owned = MenuRegistry.Registered.Where(page => ReferenceEquals(page.Content, pack)).ToList();
        check(owned.Count(page => page.ActionLauncher) == 1 && MenuRegistry.HasPages(settings), "Settings registration keeps the existing launcher");
        var rows = MenuNavigation.Build(owned, pack, new Dictionary<string, string>());
        check(rows.Any(row => row.OwnerHeader) && rows.Count(row => row.Child && !row.Page.ActionLauncher) == 1 && rows.All(row => !row.ActionOnly), "Settings and actions share one group without listing the launcher as a settings page");
        MenuOwnerAction edit = MenuOwnerActions.For(launcher).Single(action => action.Id == "edit");
        using (var catalog = new MenuCatalog(owned))
        {
            while (catalog.Pending) catalog.Advance();
            check(catalog.Results.All(hit => !hit.Page.ActionLauncher) && catalog.Results.Any(hit => hit.Page.Id.EndsWith(":general")), "Action launcher is excluded from search while the settings page remains");
        }
        MenuRegistry.RegisterOwnerAction(pack, "edit", () => "Edit again", () => { });
        MenuOwnerAction replaced = MenuOwnerActions.For(launcher).Single(action => action.Id == "edit");
        check(!ReferenceEquals(replaced, edit) && MenuOwnerActions.For(launcher).Count(action => action.Id == "edit") == 1, "Same action id replaces the previous registration");
        var only = new ModContentPack { PackageIdPlayerFacing = "actions.only", Name = "Only", RootDir = "only-root" };
        MenuRegistry.RegisterOwnerAction(only, "open", () => "Open", () => { });
        MenuPage onlyPage = MenuRegistry.Registered.Single(page => ReferenceEquals(page.Content, only));
        var onlyRows = MenuNavigation.Build(new[] { onlyPage }, null, new Dictionary<string, string>());
        check(onlyRows.Count == 1 && onlyRows[0].ActionOnly && onlyRows[0].OwnerHeader, "Action-only content keeps one non-settings header");
        var session = new MenuSession((operation, exception) => throw new Exception(operation, exception));
        session.Select(onlyPage);
        int executions = 0;
        MenuRegistry.RegisterOwnerAction(only, "run", () => "Run", () => executions++);
        MenuOwnerActions.For(session.Selected).Single(action => action.Id == "run").Execute();
        session.Draw(default);
        session.Select(null);
        check(executions == 1 && !session.DrawFailed && session.Selected == null,
            "Content-only launcher supports execution, drawing and leaving the page");
        check(MenuSession.ResolveReplacement(onlyPage, new[] { launcher }) == null,
            "Removed content-only launcher never selects an unrelated ownerless page");
        MenuRegistry.RegisterSubItem(settings, "advanced", () => "Advanced", _ => { });
        owned = MenuRegistry.Registered.Where(page => ReferenceEquals(page.Content, pack)).ToList();
        MenuPage advanced = owned.Single(page => page.Id.EndsWith(":advanced"));
        var remembered = new Dictionary<string, string> { [MenuRegistry.OwnerId(settings)] = advanced.Id };
        rows = MenuNavigation.Build(owned, pack, remembered);
        check(rows[0].Page == advanced && rows.Count(row => row.Child) == 2,
            "Adopted action launcher preserves remembered settings and content-group expansion");
        rows = MenuNavigation.Build(owned.Concat(new[] { onlyPage }).ToList(), only, remembered);
        check(rows.All(row => !row.Child), "Selecting content-only actions does not expand another Mod's settings");
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
