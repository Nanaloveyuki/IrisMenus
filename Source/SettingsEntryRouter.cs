using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace IrisMenus
{
    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Add))]
    internal static class SettingsEntryRouter
    {
        private static readonly FieldInfo ModField = AccessTools.Field(typeof(Dialog_ModSettings), "mod");
        private static bool installed;

        internal static void Install()
        {
            if (installed) return;
            new Harmony("Nanaloveyuki.IrisMenus.SettingsEntry").PatchAll(typeof(SettingsEntryRouter).Assembly);
            installed = true;
        }

        internal static bool Prefix(WindowStack __instance, ref Window window)
        {
            if (__instance != Find.WindowStack || window == null || window.GetType() != typeof(Dialog_ModSettings)) return true;
            Mod mod = (Mod)ModField.GetValue(window);
            MenuPage page = MenuRegistry.ResolveEntry(mod);
            if (page == null) return true;
            MenuWindow existing = __instance.WindowOfType<MenuWindow>();
            if (existing != null)
            {
                if (existing.OpenPage(page)) __instance.Notify_ManuallySetFocus(existing);
                return false;
            }
            window = new MenuWindow(page);
            return true;
        }
    }
}
