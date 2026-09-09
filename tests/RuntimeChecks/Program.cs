using System;
using System.Linq;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class Program
{
    private static int assertions;
    private static void Main()
    {
        Registry();
        Session();
        Scrolling();
        BackgroundLifecycle();
        SettingsRoundTrip();
        Routing();
        CatalogChecks.Run(Check);
        Console.WriteLine("PASS: " + assertions + " runtime assertions (engine doubles; no game rendering).");
    }

    private static void Registry()
    {
        var owner = new Mod();
        Throws<ArgumentNullException>(() => MenuRegistry.Register(null, "x", () => "X", _ => { }));
        Throws<ArgumentException>(() => MenuRegistry.Register(owner, " ", () => "X", _ => { }));
        Throws<ArgumentNullException>(() => MenuRegistry.Register(owner, "x", null, _ => { }));
        Throws<ArgumentNullException>(() => MenuRegistry.Register(owner, "x", () => "X", null));
        string title = "First";
        MenuRegistry.Register(owner, "first", () => title, owner.DoSettingsWindowContents);
        MenuPage page = MenuRegistry.Registered.Single();
        Check(page.DisplayTitle == "First", "Registered title");
        title = "Translated";
        Check(page.DisplayTitle == title, "Dynamic title");
        page.Draw(default); page.Save();
        Check(owner.Draws == 1 && owner.Saves == 1, "Owner callbacks");
        Throws<ArgumentException>(() => MenuRegistry.Register(owner, "first", () => "Duplicate", _ => { }));
        Check(MenuRegistry.Registered.Count() == 1, "Duplicate registration is atomic");
        MenuRegistry.Register(owner, "second", () => throw new Exception("title"), _ => { });
        MenuPage brokenTitle = MenuRegistry.Registered.Last();
        int errors = Log.Errors.Count;
        Check(brokenTitle.DisplayTitle == owner.Content.Name, "Title fallback");
        Check(brokenTitle.DisplayTitle == owner.Content.Name && Log.Errors.Count == errors + 1, "Title failure logged once");
        var adapter = new OtherMod();
        var noSettings = new NoSettings();
        var badCategory = new BadCategory();
        LoadedModManager.ModHandles.AddRange(new Mod[] { owner, adapter, noSettings, badCategory, new IrisMenusMod(new ModContentPack()) });
        var adapters = MenuRegistry.Adaptable();
        Check(adapters.Count == 1 && ReferenceEquals(adapters[0].Owner, adapter), "Adapters exclude registered, empty, failed and self");
        adapters[0].Draw(default); adapters[0].Save();
        Check(adapter.Draws == 1 && adapter.Saves == 1, "Adapter invokes original draw/save");
        Check(MenuRegistry.OwnerId(owner) != MenuRegistry.OwnerId(adapter), "Different mod types retain distinct identity");
        MenuRegistry.RegisterListing(adapter, "listing", () => "Listing", listing => listing.CurHeight = 100);
        Check(MenuRegistry.Adaptable().Count == 0, "Native registration supersedes manual host");
        MenuRegistry.Open(); MenuRegistry.Open();
        Check(Find.WindowStack.Windows.Count == 1, "Open is idempotent");
        int saves = 0;
        var customOwner = new Mod { Content = new ModContentPack { PackageIdPlayerFacing = "test.custom" } };
        MenuRegistry.Register(customOwner, "custom", () => "Custom", _ => { }, () => saves++);
        MenuRegistry.Registered.Last().Save();
        Check(saves == 1 && customOwner.Saves == 0, "Custom save replaces default");
    }

    private static void Session()
    {
        int errors = 0, saves = 0, draws = 0;
        bool failSave = true, failDraw = true;
        var owner = new Mod();
        var first = new MenuPage("a", owner, () => "A", _ =>
        {
            draws++;
            if (failDraw) throw new Exception("draw");
        }, () => { saves++; if (failSave) throw new Exception("save"); }, false);
        var second = new MenuPage("b", owner, () => "B", _ => { }, () => { }, true);
        var session = new MenuSession((_, exception) => errors++);
        Check(session.Save(), "Empty session save");
        Check(session.Select(first), "Initial selection");
        Check(session.Select(first) && saves == 0, "Reselect does not save");
        session.Draw(default); session.Draw(default);
        Check(draws == 1 && session.DrawFailed && errors == 1, "Failed draw does not repeat every event");
        Check(!session.Select(second) && ReferenceEquals(session.Selected, first), "Save failure retains selection");
        Check(!session.Save(), "Close can be refused on save failure");
        failDraw = false;
        session.Retry(); session.Draw(default);
        Check(draws == 2 && !session.DrawFailed, "Retry redraws");
        failSave = false;
        Check(session.Select(second) && ReferenceEquals(session.Selected, second), "Successful save permits switch");
        Check(session.Select(null) && session.Selected == null, "Manage page saves and clears selection");
        var exit = new MenuPage("exit", owner, () => "Exit", _ => throw new ExitGUIException(), () => { }, false);
        session.Select(exit);
        Throws<ExitGUIException>(() => session.Draw(default));
        Check(!session.DrawFailed, "ExitGUI is not a page failure");
    }

    private static void Scrolling()
    {
        var scroll = new MenuScrollView();
        Rect viewport = new Rect(0, 0, 400, 300);
        Throws<ArgumentNullException>(() => scroll.Draw(viewport, null));
        scroll.Draw(viewport, listing => { Check(listing.maxOneColumn, "Listing is single column"); listing.CurHeight = 1200; });
        Check(Widgets.Groups == 0, "Scroll groups balanced");
        scroll.Draw(viewport, listing => listing.CurHeight = 1200);
        Check(Widgets.LastContent.height == 1204 && Widgets.LastContent.width == 380, "Measured scroll extent");
        Throws<InvalidOperationException>(() => scroll.Draw(viewport, _ => throw new InvalidOperationException()));
        Check(Widgets.Groups == 0, "Exception restores groups");
        scroll.Draw(viewport, listing => listing.CurHeight = 10);
        scroll.Draw(viewport, listing => listing.CurHeight = 10);
        Check(Widgets.LastContent.height == 300, "Shrinking content fits viewport");
        scroll.Reset();
        scroll.Draw(viewport, _ => { });
        Check(Widgets.LastPosition.y == 0, "Reset clears scroll");
    }

    private static void BackgroundLifecycle()
    {
        Current.ProgramState = ProgramState.Playing;
        IrisMenusMod.Instance.Content.assetBundles.loadedAssetBundles.Add(new AssetBundle());
        Find.Camera = new Camera();
        Find.WorldCamera = new Camera();
        var owner = new MenuWindow();
        Find.WindowStack.Windows.Clear();
        Find.WindowStack.Add(owner);
        var background = new FrostBackground { Owner = owner };
        background.Start(); background.Start();
        Check(Camera.Subscribers == 2, "Background subscribes once");
        new Camera().Render();
        Check(FrostPipeline.Created == 0, "Preview camera ignored");
        Find.Camera.Render();
        Check(FrostPipeline.Created == 1 && FrostPipeline.Binds == 1, "Map camera binds");
        background.DoWindowBackground(default);
        Check(GUI.TextureDraws == 1, "Fresh frame uses frost");
        Time.frameCount += 3;
        int native = DefaultWindowDrawing.Backgrounds;
        background.DoWindowBackground(default);
        Check(DefaultWindowDrawing.Backgrounds == native && GUI.TextureDraws == 1, "Stale frame does not draw stale texture");
        IrisMenusMod.Instance.Settings.FrostEnabled = false;
        Find.Camera.Render();
        background.DoWindowBackground(default);
        Check(DefaultWindowDrawing.Backgrounds == native && Math.Abs(GUI.OverlayAlpha - 0.35f) < 0.001f, "Transparency still works with frost disabled");
        IrisMenusMod.Instance.Settings.FrostEnabled = true;
        RimWorld.Planet.WorldRendererUtility.WorldSelected = true;
        int binds = FrostPipeline.Binds;
        Find.Camera.Render();
        Check(FrostPipeline.Binds == binds, "Map camera ignored in world view");
        Find.WorldCamera.Render();
        Check(FrostPipeline.Binds == binds + 1, "World camera binds");
        Find.WindowStack.Windows.Clear();
        Find.WindowStack.Add(new MenuWindow());
        Find.WorldCamera.Render();
        Check(Camera.Subscribers == 0 && FrostPipeline.Disposed == 1, "Replaced window releases its own callbacks");
        background.Dispose();
        Check(FrostPipeline.Disposed == 1, "Dispose is idempotent");
        var second = new FrostBackground { Owner = (Window)Find.WindowStack.Windows[0] };
        AssetBundle.FailLoad = true;
        second.Start();
        int errors = Log.Errors.Count;
        Find.WorldCamera.Render(); Find.WorldCamera.Render();
        Check(Log.Errors.Count == errors + 1, "Missing borrowed shader is reported once");
        AssetBundle.FailLoad = false;
        second.Retry(); Find.WorldCamera.Render();
        Check(FrostPipeline.Created == 2, "Explicit retry recovers bundle load");
        Current.ProgramState = ProgramState.Entry;
        Find.WorldCamera.Render();
        native = DefaultWindowDrawing.Backgrounds;
        second.DoWindowBackground(default);
        Check(DefaultWindowDrawing.Backgrounds == native && GUI.TextureDraws == 1, "Main menu does not reuse a game frame");
        second.Dispose();
        Check(Camera.Subscribers == 0 && AssetBundle.Unloads == 0 && AssetBundle.Loads == 0, "Game-owned bundle is neither reloaded nor unloaded");
    }

    private static void SettingsRoundTrip()
    {
        var original = new MenuSettings
        {
            Width = 920, Height = 640, X = 55, Y = 75, Blur = 0.8f,
            Transparency = 0.4f, FrostEnabled = false, LastPage = "test:page"
        };
        original.EnabledAdapters.Add("unloaded.mod:Settings");
        original.ExposeData();
        Scribe_Values.Loading = true;
        var restored = new MenuSettings();
        restored.ExposeData();
        Check(restored.Width == 920 && restored.Height == 640 && restored.X == 55 && restored.Y == 75, "Geometry serialization contract");
        Check(restored.Blur == 0.8f && restored.Transparency == 0.4f && !restored.FrostEnabled, "Appearance serialization contract");
        Check(restored.LastPage == "test:page", "Last-page serialization contract");
        Check(restored.EnabledAdapters.Single() == "unloaded.mod:Settings", "Unloaded manual selections retained");
        Scribe_Values.Data.Clear();
        restored.ExposeData();
        Check(restored.EnabledAdapters != null && restored.EnabledAdapters.Count == 0, "Missing collection initialized");
        Check(restored.FrostEnabled && restored.Blur == 0.5f && restored.Transparency == 0.65f, "Missing appearance uses defaults");
        Scribe_Values.Data["blur"] = float.NaN;
        Scribe_Values.Data["transparency"] = 5f;
        restored.ExposeData();
        Check(restored.Blur == 0.5f && restored.Transparency == 1f, "Invalid appearance values normalized");
        Scribe_Values.Loading = false;
    }

    private static void Routing()
    {
        var mod = new NoSettings();
        Find.WindowStack.Windows.Clear();
        var original = new RimWorld.Dialog_ModSettings(mod);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Unselected mod keeps original entry");
        Find.WindowStack.Windows.Clear();
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(MenuRegistry.OwnerId(mod));
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        var host = Find.WindowStack.WindowOfType<MenuWindow>();
        Check(host != null && ReferenceEquals(host.Page.Owner, mod), "Actual Harmony patch routes selected mod");
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        Check(Find.WindowStack.Windows.Count == 1 && Find.WindowStack.Focused == host, "Existing host reused and focused");
        host.AcceptPage = false;
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(mod));
        Check(Find.WindowStack.Windows.Count == 1, "Failed selection does not leak an original dialog");
        var independentStack = new WindowStack();
        original = new RimWorld.Dialog_ModSettings(mod);
        independentStack.Add(original);
        Check(ReferenceEquals(independentStack.Windows.Single(), original), "Unrelated window stacks are untouched");
        var custom = new CustomDialog(mod);
        Find.WindowStack.Add(custom);
        Check(Find.WindowStack.Windows.Contains(custom), "Custom dialog subclasses remain untouched");
        IrisMenusMod.Instance.Settings.EnabledAdapters.Clear();
        Find.WindowStack.Windows.Clear();
        original = new RimWorld.Dialog_ModSettings(mod);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Unselect restores original entry");
        Find.WindowStack.Windows.Clear();
        original = new RimWorld.Dialog_ModSettings(IrisMenusMod.Instance);
        Find.WindowStack.Add(original);
        Check(ReferenceEquals(Find.WindowStack.Windows.Single(), original), "Own settings entry does not recurse");
        Find.WindowStack.Windows.Clear();
        var native = MenuRegistry.Registered.First();
        Find.WindowStack.Add(new RimWorld.Dialog_ModSettings(native.Owner));
        Check(Find.WindowStack.WindowOfType<MenuWindow>()?.Page.Id == native.Id, "Registered owner routes to its native page");
    }

    private static void Check(bool value, string name)
    {
        if (!value) throw new Exception("FAIL: " + name);
        assertions++;
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { assertions++; return; }
        throw new Exception("Expected " + typeof(T).Name);
    }

    private sealed class OtherMod : Mod { }
    private sealed class CustomDialog : RimWorld.Dialog_ModSettings { public CustomDialog(Mod mod) : base(mod) { } }
    private sealed class NoSettings : Mod { public override string SettingsCategory() => ""; }
    private sealed class BadCategory : Mod { public override string SettingsCategory() => throw new Exception("category"); }
}
