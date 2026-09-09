using IrisMenus;
using Verse;

namespace IrisMenusApiConsumer
{
    // Compile-only API consumer, not included in the shipped mod.
    public sealed class ExampleMod : Mod
    {
        private readonly ExampleSettings settings;
        private string numberBuffer;
        private string floatBuffer;

        public ExampleMod(ModContentPack content) : base(content)
        {
            settings = GetSettings<ExampleSettings>();
            MenuRegistry.RegisterSubItemListing(this, "general", () => "General", Draw);
            MenuRegistry.RegisterSearchProvider(this, "general", () => new[]
            {
                new MenuSearchEntry("enabled", () => "Enabled", () => "activate toggle", () => "Enable the feature")
            });
            var sections = new MenuSectionView(new[]
            {
                new MenuSection("advanced", width => 60f, rect => Widgets.Label(rect, "Advanced options"))
            });
            MenuRegistry.RegisterSubItem(this, "advanced", () => "Advanced", sections.Draw);
            MenuRegistry.RegisterSearchProvider(this, "advanced", () => new[]
            {
                new MenuSearchEntry("advanced", () => "Advanced options")
            }, sections.Focus);
        }

        private void Draw(Listing_Standard listing)
        {
            MenuControls.Section(listing, "General");
            MenuControls.Anchor(listing, "enabled");
            MenuControls.Checkbox(listing, "Enabled", ref settings.Enabled);
            settings.Strength = MenuControls.Slider(listing, "Strength", settings.Strength, 0f, 1f);
            MenuControls.Number(listing, "Samples", ref settings.SampleCount, ref numberBuffer, 1, 16);
            MenuControls.Number(listing, "Threshold", ref settings.Threshold, ref floatBuffer, 0f, 1f);
            MenuControls.Select(listing, "Mode", settings.Mode, new[] { "Default", "Custom" }, value => value, value => settings.Mode = value);
        }
    }

    public sealed class ExampleSettings : ModSettings
    {
        public bool Enabled = true;
        public float Strength = 0.5f;
        public int SampleCount = 4;
        public float Threshold = 0.5f;
        public string Mode = "Default";

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Enabled, "enabled", true);
            Scribe_Values.Look(ref Strength, "strength", 0.5f);
            Scribe_Values.Look(ref SampleCount, "sampleCount", 4);
            Scribe_Values.Look(ref Threshold, "threshold", 0.5f);
            Scribe_Values.Look(ref Mode, "mode", "Default");
        }
    }
}
