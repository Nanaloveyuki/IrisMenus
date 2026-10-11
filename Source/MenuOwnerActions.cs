using System;
using System.Collections.Generic;
using Verse;

namespace IrisMenus
{
    public static class MenuOwnerActions
    {
        internal const string LauncherPageId = "actions";

        private static readonly List<MenuOwnerAction> registered = new List<MenuOwnerAction>();
        private static readonly Dictionary<ModContentPack, List<MenuOwnerAction>> byContent = new Dictionary<ModContentPack, List<MenuOwnerAction>>();
        private static readonly List<MenuOwnerAction> none = new List<MenuOwnerAction>();

        public static void RegisterOwnerAction(Mod owner, string id, Func<string> label, Action execute,
            Func<bool> visible = null, Func<bool> enabled = null, Func<string> confirmation = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Register(new MenuOwnerAction(ContentOwnerId(owner.Content), owner.Content, id, label, execute, visible, enabled, confirmation));
        }

        public static void RegisterOwnerAction(ModContentPack owner, string id, Func<string> label, Action execute,
            Func<bool> visible = null, Func<bool> enabled = null, Func<string> confirmation = null)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            Register(new MenuOwnerAction(ContentOwnerId(owner), owner, id, label, execute, visible, enabled, confirmation));
        }

        internal static string ContentOwnerId(ModContentPack content) =>
            (content.PackageIdPlayerFacing ?? "") + ":" + (content.RootDir ?? "");

        internal static List<MenuOwnerAction> For(MenuPage page)
        {
            ModContentPack content = page?.Content;
            if (content == null) return none;
            return byContent.TryGetValue(content, out List<MenuOwnerAction> actions) ? actions : none;
        }

        internal static void EnsureLaunchers()
        {
            var seen = new HashSet<ModContentPack>();
            foreach (MenuOwnerAction action in registered)
            {
                if (action.Content == null || !seen.Add(action.Content)) continue;
                if (MenuRegistry.HasContentPages(action.Content)) continue;
                MenuRegistry.RegisterLauncher(action.Content, ContentOwnerId(action.Content) + ":" + LauncherPageId);
            }
        }

        private static void Register(MenuOwnerAction action)
        {
            if (!byContent.TryGetValue(action.Content, out List<MenuOwnerAction> actions))
            {
                actions = new List<MenuOwnerAction>();
                byContent[action.Content] = actions;
            }
            for (int i = 0; i < actions.Count; i++)
            {
                if (!string.Equals(actions[i].Id, action.Id, StringComparison.Ordinal)) continue;
                actions[i] = action;
                for (int j = 0; j < registered.Count; j++)
                    if (ReferenceEquals(registered[j].Content, action.Content) && string.Equals(registered[j].Id, action.Id, StringComparison.Ordinal))
                    { registered[j] = action; break; }
                MenuRegistry.InvalidateSearchIndex();
                return;
            }
            actions.Add(action);
            registered.Add(action);
            EnsureLaunchers();
            MenuRegistry.InvalidateSearchIndex();
        }
    }

    internal sealed class MenuOwnerAction
    {
        internal readonly string OwnerKey;
        internal readonly ModContentPack Content;
        internal readonly string Id;
        internal readonly Func<string> Label;
        internal readonly Action Execute;
        internal readonly Func<bool> Visible;
        internal readonly Func<bool> Enabled;
        internal readonly Func<string> Confirmation;

        internal MenuOwnerAction(string ownerKey, ModContentPack content, string id, Func<string> label,
            Action execute, Func<bool> visible, Func<bool> enabled, Func<string> confirmation)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("An action ID is required.", nameof(id));
            OwnerKey = ownerKey ?? throw new ArgumentNullException(nameof(ownerKey));
            Content = content ?? throw new ArgumentNullException(nameof(content));
            Id = id;
            Label = label ?? throw new ArgumentNullException(nameof(label));
            Execute = execute ?? throw new ArgumentNullException(nameof(execute));
            Visible = visible;
            Enabled = enabled;
            Confirmation = confirmation;
        }

        internal bool IsVisible()
        {
            try { return Visible == null || Visible(); }
            catch (Exception exception)
            {
                Log.Error("[IrisMenus] Owner action visibility failed for " + OwnerKey + ":" + Id + ": " + exception);
                return false;
            }
        }

        internal bool IsEnabled()
        {
            try { return Enabled == null || Enabled(); }
            catch (Exception exception)
            {
                Log.Error("[IrisMenus] Owner action enabled check failed for " + OwnerKey + ":" + Id + ": " + exception);
                return false;
            }
        }

        internal bool TryConfirmText(out string text)
        {
            if (Confirmation == null) { text = null; return true; }
            text = Confirmation();
            return true;
        }

        internal string DisplayLabel()
        {
            try
            {
                string label = Label();
                return string.IsNullOrWhiteSpace(label) ? Id : label;
            }
            catch (Exception exception)
            {
                Log.Error("[IrisMenus] Owner action label failed for " + OwnerKey + ":" + Id + ": " + exception);
                return Id;
            }
        }
    }
}
