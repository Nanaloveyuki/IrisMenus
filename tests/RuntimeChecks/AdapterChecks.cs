using System;
using System.Linq;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class AdapterChecks
{
    internal static void Run(Action<bool, string> check)
    {
        var owner = new CharacterEditor.CharacterEditorMod();
        LoadedModManager.ModHandles.Add(owner);
        string id = MenuRegistry.OwnerId(owner);
        IrisMenusMod.Instance.Settings.EnabledAdapters.Add(id);
        Find.WindowStack.Windows.Clear();
        try
        {
            MenuPage page = MenuRegistry.Adaptable().Single(candidate => candidate.Id == id);
            Rect rect = new Rect(0f, 0f, 800f, 600f);
            for (int i = 0; i < 6; i++) page.Draw(rect);
            check(Find.WindowStack.Windows.Count == 0,
                "CharacterEditor page rendering never opens windows without a click");
            Widgets.ClickNextButton = true;
            page.Draw(rect);
            var dialog = Find.WindowStack.Windows.Single() as CharacterEditor.CEditor.ModOptions.DialogConfigurate;
            check(dialog != null, "CharacterEditor launcher opens its original settings window");
            for (int i = 0; i < 6; i++) page.Draw(rect);
            Widgets.ClickNextButton = true;
            page.Draw(rect);
            check(Find.WindowStack.Windows.Count == 1 && ReferenceEquals(Find.WindowStack.Focused, dialog),
                "CharacterEditor launcher focuses an existing window instead of reopening it");
            // The original window owns closing and saving; the host must not auto-reopen it.
            Find.WindowStack.Windows.Remove(dialog);
            for (int i = 0; i < 6; i++) page.Draw(rect);
            check(Find.WindowStack.Windows.Count == 0, "Closing CharacterEditor does not create an opening loop");
            MenuPage routed = MenuRegistry.ResolveEntry(owner);
            for (int i = 0; i < 6; i++) routed.Draw(rect);
            check(Find.WindowStack.Windows.Count == 0, "Original entry routing uses the same safe adapter");
            Widgets.ClickNextButton = true;
            routed.Draw(rect);
            check(Find.WindowStack.Windows.Single() is CharacterEditor.CEditor.ModOptions.DialogConfigurate,
                "CharacterEditor settings can be reopened explicitly after closing");
            MenuRegistry.Register(owner, "native", () => "Native", _ => { });
            check(!MenuRegistry.ResolveEntry(owner).Adapted &&
                MenuRegistry.Adaptable().All(candidate => candidate.Id != id),
                "Explicit CharacterEditor registration supersedes the launcher");
        }
        finally
        {
            Widgets.ClickNextButton = false;
            Find.WindowStack.Windows.Clear();
            IrisMenusMod.Instance.Settings.EnabledAdapters.Remove(id);
            LoadedModManager.ModHandles.Remove(owner);
        }
    }
}

namespace CharacterEditor
{
    // Mirrors the installed 1.6.3 entry: each draw opens DialogConfigurate and closes ModSettings.
    internal sealed class CharacterEditorMod : Mod
    {
        public override string SettingsCategory() => "CharacterEditor";
        public override void DoSettingsWindowContents(Rect rect)
        {
            Find.WindowStack.Add(new CEditor.ModOptions.DialogConfigurate());
            foreach (Window window in Find.WindowStack.Windows.OfType<RimWorld.Dialog_ModSettings>().ToArray())
                Find.WindowStack.Windows.Remove(window);
        }
    }

    internal static class CEditor
    {
        internal static class ModOptions
        {
            internal sealed class DialogConfigurate : Window { }
        }
    }
}
