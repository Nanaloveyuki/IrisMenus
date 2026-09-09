using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class IrisMenusMod : Mod
    {
        internal static IrisMenusMod Instance;
        internal readonly MenuSettings Settings;

        public IrisMenusMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<MenuSettings>();
            SettingsEntryRouter.Install();
        }

        public override string SettingsCategory() => "IrisMenus";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y, Mathf.Min(inRect.width, 240f), 32f), "IM_Open".Translate()))
                MenuRegistry.Open();
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 44f, Mathf.Min(inRect.width, 240f), 32f), "IM_ResetWindow".Translate()))
            {
                Settings.Width = Settings.Height = 0f;
                Settings.X = Settings.Y = -1f;
                WriteSettings();
            }
        }
    }

    public sealed class MenuSettings : ModSettings
    {
        internal List<string> EnabledAdapters = new List<string>();
        internal float Width;
        internal float Height;
        internal float X = -1f;
        internal float Y = -1f;
        internal bool FrostEnabled = true;
        internal float Blur = 0.5f;
        internal float Transparency = 0.65f;
        internal string LastPage;
        internal Dictionary<string, string> LastSubItems = new Dictionary<string, string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref EnabledAdapters, "enabledAdapters", LookMode.Value);
            Scribe_Values.Look(ref Width, "width");
            Scribe_Values.Look(ref Height, "height");
            Scribe_Values.Look(ref X, "x", -1f);
            Scribe_Values.Look(ref Y, "y", -1f);
            Scribe_Values.Look(ref FrostEnabled, "frostEnabled", true);
            Scribe_Values.Look(ref Blur, "blur", 0.5f);
            Scribe_Values.Look(ref Transparency, "transparency", 0.65f);
            Scribe_Values.Look(ref LastPage, "lastPage");
            Scribe_Collections.Look(ref LastSubItems, "lastSubItems", LookMode.Value, LookMode.Value);
            if (EnabledAdapters == null) EnabledAdapters = new List<string>();
            if (LastSubItems == null) LastSubItems = new Dictionary<string, string>();
            Blur = Mathf.Clamp01(MenuLayout.Finite(Blur, 0.5f));
            Transparency = Mathf.Clamp01(MenuLayout.Finite(Transparency, 0.65f));
        }
    }

    public sealed class MainButtonWorker_IrisMenus : MainButtonWorker
    {
        public override void Activate() => MenuRegistry.Open();
    }
}
