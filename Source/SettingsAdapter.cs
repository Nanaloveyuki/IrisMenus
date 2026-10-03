using System;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal static class SettingsAdapter
    {
        private const string CharacterEditorMod = "CharacterEditor.CharacterEditorMod";
        private const string CharacterEditorDialog = "CharacterEditor.CEditor+ModOptions+DialogConfigurate";

        internal static MenuPage Create(Mod owner, Func<string> title)
        {
            Action<Rect> draw = owner.GetType().FullName == CharacterEditorMod
                ? rect => DrawCharacterEditor(owner, rect)
                : owner.DoSettingsWindowContents;
            return new MenuPage(MenuRegistry.OwnerId(owner), owner, title, draw, owner.WriteSettings, true);
        }

        private static void DrawCharacterEditor(Mod owner, Rect rect)
        {
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 72f), "IM_ExternalSettings".Translate());
            if (!Widgets.ButtonText(new Rect(rect.x, rect.y + 80f, Mathf.Min(rect.width, 280f), 32f),
                "IM_OpenOriginalSettings".Translate())) return;
            WindowStack stack = Find.WindowStack;
            for (int i = 0; i < stack.Count; i++)
            {
                Window window = stack[i];
                if (window.GetType().FullName != CharacterEditorDialog) continue;
                stack.Notify_ManuallySetFocus(window);
                return;
            }
            // CharacterEditor's draw is a launcher: it opens a window and closes Dialog_ModSettings.
            // Calling it on every IMGUI event continually reopens that window.
            owner.DoSettingsWindowContents(rect);
        }
    }
}
