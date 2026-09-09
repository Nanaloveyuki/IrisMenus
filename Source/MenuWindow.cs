using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class MenuWindow : Window
    {
        private List<MenuPage> adapters;
        private List<MenuPage> pages;
        private List<MenuNavEntry> navigation;
        private MenuCatalog catalog;
        private readonly MenuBrowseState browse = new MenuBrowseState();
        private int catalogRevision = -1;
        private int catalogFrame = -1;
        private object catalogLanguage;
        private Vector2 resultScroll;
        private readonly FrostBackground background;
        private readonly MenuSession session = new MenuSession(ReportFailure);
        private Vector2 navigationScroll;
        private Vector2 adapterScroll;
        private bool showNavigation;
        private bool closeSaved;
        private MenuSettings Settings => IrisMenusMod.Instance.Settings;

        public MenuWindow() : this(new FrostBackground(), null) { }
        internal MenuWindow(MenuPage page) : this(new FrostBackground(), page) { }

        private MenuWindow(FrostBackground drawing, MenuPage requested) : base(drawing)
        {
            background = drawing;
            background.Owner = this;
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = true;
            resizeable = true;
            doCloseX = true;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            adapters = MenuRegistry.Adaptable();
            pages = MenuRegistry.Registered.Concat(Hosted()).ToList();
            MenuPage initial = requested == null
                ? pages.FirstOrDefault(page => page.Id == Settings.LastPage)
                : pages.FirstOrDefault(page => page.Id == requested.Id) ?? requested;
            if (initial != null) { session.Select(initial); browse.Navigate(initial); }
            RefreshCatalog();
        }

        public override Vector2 InitialSize => MenuLayout.Clamp(new Rect(0f, 0f,
            Settings.Width > 0f ? Settings.Width : UI.screenWidth * 0.92f,
            Settings.Height > 0f ? Settings.Height : UI.screenHeight * 0.88f), UI.screenWidth, UI.screenHeight).size;

        public override void PostOpen()
        {
            base.PostOpen();
            if (Settings.X >= 0f) windowRect.position = new Vector2(Settings.X, Settings.Y);
            ClampGeometry();
            background.Start();
        }

        public override void Notify_ResolutionChanged() { ClampGeometry(); }
        private void ClampGeometry() { windowRect = MenuLayout.Clamp(windowRect, UI.screenWidth, UI.screenHeight); }

        public override bool OnCloseRequest()
        {
            if (!session.Save() || !SaveWindowSettings()) return false;
            closeSaved = true;
            return true;
        }

        public override void PreClose()
        {
            try
            {
                // WindowStack can remove a window directly (e.g. return to main menu).
                if (!closeSaved) { session.Save(); SaveWindowSettings(); }
            }
            finally { catalog?.Dispose(); background.Dispose(); base.PreClose(); }
        }

        private bool SaveWindowSettings()
        {
            Settings.Width = windowRect.width;
            Settings.Height = windowRect.height;
            Settings.X = windowRect.x;
            Settings.Y = windowRect.y;
            Settings.LastPage = session.Selected?.Id;
            try { IrisMenusMod.Instance.WriteSettings(); return true; }
            catch (Exception exception) { ReportFailure("save window settings", exception); return false; }
        }

        public override void DoWindowContents(Rect inRect)
        {
            ClampGeometry();
            if (catalogRevision != MenuRegistry.Revision || !ReferenceEquals(catalogLanguage, LanguageDatabase.activeLanguage)) RefreshCatalog();
            catalog.Query(browse.Query, browse.Scope);
            if (catalogFrame != Time.frameCount) { catalog.Advance(); catalogFrame = Time.frameCount; }
            var state = new GuiState();
            try
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = true;
                Text.Font = GameFont.Medium;
                GUI.color = MenuControls.Accent;
                Widgets.Label(new Rect(4f, 0f, inRect.width - 40f, 30f), "IrisMenus");
                Text.Font = GameFont.Tiny;
                GUI.color = new Color(0.72f, 0.72f, 0.72f);
                string title = browse.Managing ? "IM_Manage".Translate().ToString()
                    : browse.Global && !browse.ViewingTarget ? "IM_Global".Translate().ToString() : session.Selected?.DisplayTitle ?? "IrisMenus";
                Widgets.Label(new Rect(4f, 30f, inRect.width - 12f, 16f), title.Truncate(inRect.width - 12f));
                TooltipHandler.TipRegion(new Rect(4f, 30f, inRect.width - 12f, 16f), title);
                GUI.color = new Color(1f, 0.85f, 0.55f, 0.35f);
                Widgets.DrawLineHorizontal(0f, 52f, inRect.width);
                GUI.color = Color.white;
                Text.Font = GameFont.Small;
                MenuLayout layout = MenuLayout.Calculate(inRect.width, inRect.height);
                Rect body = DrawSearch(layout.Content);
                Rect navRect = layout.Navigation;
                if (layout.Compact) navRect.yMin += 40f;
                if (!layout.Compact || showNavigation) DrawNavigation(navRect);
                if (!layout.Compact)
                {
                    GUI.color = new Color(1f, 0.85f, 0.55f, 0.18f);
                    Widgets.DrawLineVertical(layout.Navigation.xMax + 12f, layout.Content.y, layout.Content.height);
                    GUI.color = Color.white;
                }
                if (!layout.Compact || !showNavigation)
                {
                    if (browse.Managing) DrawAdapters(body);
                    else if (browse.ShowResults) DrawResults(body);
                    else if (session.Selected != null) DrawSelected(body);
                }
                DrawFooter(layout);
            }
            finally { state.Restore(); }
        }

        private void DrawFooter(MenuLayout layout)
        {
            Rect footer = layout.Footer;
            GUI.color = new Color(1f, 0.85f, 0.55f, 0.18f);
            Widgets.DrawLineHorizontal(0f, footer.y, footer.width);
            GUI.color = Color.white;
            float controlX = layout.Compact ? 0f : layout.Content.x;
            float controlsY = footer.y + (layout.Compact ? 68f : 14f);
            float buttonWidth = Mathf.Min(164f, (footer.width - controlX - 32f) / 2f);
            if (Widgets.ButtonText(new Rect(controlX, controlsY, buttonWidth, 26f), "IM_Manage".Translate()) && session.Select(null))
            {
                browse.Manage();
                resultScroll = Vector2.zero;
                RebuildNavigation();
            }
            if (layout.Compact && Widgets.ButtonText(new Rect(controlX + buttonWidth + 12f, controlsY, buttonWidth, 26f), "IM_Pages".Translate()))
                showNavigation = !showNavigation;
            float toggleX = layout.Compact ? footer.width / 2f + 8f : controlX + buttonWidth + 18f;
            float toggleY = layout.Compact ? footer.y + 8f : controlsY;
            bool enabled = Settings.FrostEnabled;
            Widgets.CheckboxLabeled(new Rect(toggleX, toggleY, Mathf.Min(180f, footer.width - toggleX - 24f), 24f), "IM_Frost".Translate(), ref Settings.FrostEnabled);
            if (enabled != Settings.FrostEnabled) background.Retry();
            float sliderWidth = layout.Compact ? footer.width / 2f - 16f : Mathf.Max(120f, layout.Navigation.width - 20f);
            GUI.enabled = Settings.FrostEnabled;
            Settings.Blur = FooterSlider(new Rect(10f, footer.y + 4f, sliderWidth, 28f), "IM_Blur", Settings.Blur);
            GUI.enabled = true;
            Settings.Transparency = FooterSlider(new Rect(10f, footer.y + 34f, sliderWidth, 28f), "IM_Transparency", Settings.Transparency);
        }

        private static float FooterSlider(Rect rect, string key, float value)
        {
            Text.Font = GameFont.Tiny;
            GUI.color = new Color(0.72f, 0.72f, 0.72f);
            Widgets.Label(new Rect(rect.x, rect.y, rect.width, 14f), key.Translate(Mathf.RoundToInt(value * 100f)));
            GUI.color = Color.white;
            float result = Widgets.HorizontalSlider(new Rect(rect.x, rect.y + 14f, rect.width, 14f), value, 0f, 1f);
            Text.Font = GameFont.Small;
            return result;
        }

        private IEnumerable<MenuPage> Hosted() => adapters.Where(page => !MenuRegistry.HasPages(page.Owner) && Settings.EnabledAdapters.Contains(page.Id));

        private void DrawNavigation(Rect rect)
        {
            Rect global = new Rect(rect.x, rect.y, rect.width - 20f, 32f);
            if (browse.Global && !browse.ViewingTarget) Widgets.DrawHighlightSelected(global);
            Widgets.DrawHighlightIfMouseover(global);
            Widgets.Label(global.ContractedBy(4f), "IM_Global".Translate());
            if (Widgets.ButtonInvisible(global)) Select(null);
            rect.yMin += 42f;
            float width = rect.width - 20f;
            float height = navigation.Sum(entry => NavigationHeight(entry, width));
            Widgets.BeginScrollView(rect, ref navigationScroll, new Rect(0f, 0f, width, Mathf.Max(rect.height, height)));
            try
            {
                float y = 0f;
                foreach (MenuNavEntry entry in navigation)
                {
                    float rowHeight = NavigationHeight(entry, width);
                    float indent = entry.Child ? 18f : 0f;
                    Rect row = new Rect(indent, y, width - indent, rowHeight);
                    bool selected = !browse.Global && !browse.Managing && session.Selected?.Id == entry.Page.Id && !entry.OwnerHeader;
                    if (browse.ViewingTarget) selected = session.Selected?.Id == entry.Page.Id && !entry.OwnerHeader;
                    if (selected) Widgets.DrawHighlightSelected(row);
                    Widgets.DrawHighlightIfMouseover(row);
                    Rect label = row.ContractedBy(4f);
                    label.width -= 28f;
                    GUI.color = entry.Child ? MenuControls.Accent : Color.white;
                    Widgets.Label(label, entry.Title);
                    GUI.color = Color.white;
                    TooltipHandler.TipRegion(row, entry.Page.Owner.Content.Name);
                    bool clicked = Widgets.RadioButton(row.xMax - 24f, row.y + (rowHeight - 24f) / 2f, selected);
                    if (clicked || Widgets.ButtonInvisible(row)) Select(entry.Page);
                    y += rowHeight;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private static float RowHeight(MenuPage page, float width) => Mathf.Max(32f, Text.CalcHeight(page.DisplayTitle, Mathf.Max(1f, width - 12f)) + 8f);
        private static float NavigationHeight(MenuNavEntry entry, float width) => Mathf.Max(32f,
            Text.CalcHeight(entry.Title, Mathf.Max(1f, width - (entry.Child ? 58f : 40f))) + 8f);

        private void DrawAdapters(Rect rect)
        {
            var filtered = adapters.Where(page => !MenuRegistry.HasPages(page.Owner) &&
                (page.DisplayTitle.IndexOf(browse.Query, StringComparison.CurrentCultureIgnoreCase) >= 0 ||
                 page.Owner.Content.Name.IndexOf(browse.Query, StringComparison.CurrentCultureIgnoreCase) >= 0)).ToList();
            Rect viewport = rect;
            float width = viewport.width - 20f;
            float height = filtered.Sum(page => RowHeight(page, width - 28f));
            if (filtered.Count == 0)
            {
                Widgets.Label(viewport, "IM_Empty".Translate());
                return;
            }
            Widgets.BeginScrollView(viewport, ref adapterScroll, new Rect(0f, 0f, width, Mathf.Max(viewport.height, height)));
            try
            {
                float y = 0f;
                foreach (MenuPage page in filtered)
                {
                    bool enabled = Settings.EnabledAdapters.Contains(page.Id);
                    bool previous = enabled;
                    float rowHeight = RowHeight(page, width - 28f);
                    Rect row = new Rect(0f, y, width, rowHeight);
                    Widgets.CheckboxLabeled(row, page.DisplayTitle, ref enabled);
                    TooltipHandler.TipRegion(row, page.Owner.Content.Name);
                    if (previous != enabled)
                    {
                        if (enabled) Settings.EnabledAdapters.Add(page.Id);
                        else Settings.EnabledAdapters.RemoveAll(id => id == page.Id);
                        catalogRevision = -1;
                    }
                    y += rowHeight;
                }
            }
            finally { Widgets.EndScrollView(); }
        }

        private void DrawSelected(Rect rect)
        {
            if (session.DrawFailed)
            {
                Widgets.Label(new Rect(rect.x, rect.y, rect.width, 56f), "IM_PageFailed".Translate());
                if (Widgets.ButtonText(new Rect(rect.x, rect.y + 64f, Mathf.Min(160f, rect.width), 30f), "IM_Retry".Translate())) session.Retry();
                return;
            }
            var state = new GuiState();
            GUI.BeginGroup(rect);
            try { session.Draw(new Rect(0f, 0f, rect.width, rect.height)); }
            finally { GUI.EndGroup(); state.Restore(); }
        }

        private void Select(MenuPage page)
        {
            if (!session.Select(page)) return;
            browse.Navigate(page);
            resultScroll = Vector2.zero;
            RememberPage(page);
            showNavigation = false;
            GUI.FocusControl(null);
        }

        internal bool OpenPage(MenuPage page)
        {
            MenuPage owned = MenuRegistry.Registered.Concat(adapters).FirstOrDefault(candidate => candidate.Id == page.Id) ?? page;
            if (!session.Select(owned)) return false;
            browse.Navigate(owned);
            resultScroll = Vector2.zero;
            RememberPage(owned);
            showNavigation = false;
            return true;
        }

        private void RememberPage(MenuPage page)
        {
            if (page != null) Settings.LastSubItems[MenuRegistry.OwnerId(page.Owner)] = page.Id;
            RebuildNavigation();
        }

        private void RebuildNavigation()
        {
            Mod owner = browse.Global && !browse.ViewingTarget || browse.Managing ? null : session.Selected?.Owner;
            navigation = MenuNavigation.Build(pages, owner, Settings.LastSubItems);
        }

        private void RefreshCatalog()
        {
            catalog?.Dispose();
            adapters = MenuRegistry.Adaptable();
            pages = MenuRegistry.Registered.Concat(Hosted()).ToList();
            catalog = new MenuCatalog(pages);
            catalog.Query(browse.Query, browse.Scope);
            catalogRevision = MenuRegistry.Revision;
            catalogLanguage = LanguageDatabase.activeLanguage;
            catalogFrame = -1;
            RebuildNavigation();
        }

        private Rect DrawSearch(Rect rect)
        {
            Rect icon = new Rect(rect.x, rect.y + 3f, 24f, 24f);
            if (browse.ViewingTarget)
            {
                if (Widgets.ButtonImage(icon, TexUI.ArrowTexLeft, tooltip: "IM_BackResults".Translate()) && session.Save())
                { browse.Back(); RebuildNavigation(); }
            }
            else GUI.DrawTexture(icon, TexButton.Search);
            Rect field = new Rect(rect.x + 30f, rect.y, Mathf.Max(1f, rect.width - 60f), 30f);
            GUI.SetNextControlName("IrisMenus.Search");
            string query = Widgets.TextField(field, browse.Query);
            if (query.Length > 0 && Widgets.ButtonImage(new Rect(rect.xMax - 24f, rect.y + 3f, 24f, 24f),
                TexButton.CloseXSmall, tooltip: "IM_ClearSearch".Translate())) query = "";
            string scope = browse.Managing ? "IM_Manage".Translate().ToString() : browse.Scope == null
                ? "IM_SearchGlobal".Translate().ToString() : "IM_SearchMod".Translate(session.Selected?.Owner.Content.Name ?? "").ToString();
            TooltipHandler.TipRegion(field, scope);
            if (query.Length == 0 && GUI.GetNameOfFocusedControl() != "IrisMenus.Search")
            {
                GUI.color = new Color(0.65f, 0.65f, 0.65f);
                Widgets.Label(field.ContractedBy(5f), scope.Truncate(field.width - 10f));
                GUI.color = Color.white;
            }
            if (browse.Query != query)
            {
                browse.Search(query);
                adapterScroll = resultScroll = Vector2.zero;
                catalog.Query(browse.Query, browse.Scope);
                RebuildNavigation();
            }
            return new Rect(rect.x, rect.y + 40f, rect.width, Mathf.Max(1f, rect.height - 40f));
        }

        private void DrawResults(Rect rect)
        {
            int count = catalog.Results.Count;
            if (!catalog.Pending) browse.ResultPage = Math.Min(browse.ResultPage, Math.Max(0, (count - 1) / MenuCatalog.PageSize));
            int start = browse.ResultPage * MenuCatalog.PageSize;
            int end = Math.Min(count, start + MenuCatalog.PageSize);
            Rect viewport = new Rect(rect.x, rect.y, rect.width, Mathf.Max(1f, rect.height - 36f));
            const float rowHeight = 76f;
            float width = viewport.width - 20f;
            if (end <= start) Widgets.Label(viewport, (catalog.Pending ? "IM_Indexing" : "IM_NoResults").Translate());
            else
            {
                Widgets.BeginScrollView(viewport, ref resultScroll, new Rect(0f, 0f, width, Math.Max(viewport.height, (end - start) * rowHeight)));
                try
                {
                    for (int i = start; i < end; i++)
                    {
                        MenuSearchHit hit = catalog.Results[i];
                        Rect row = new Rect(0f, (i - start) * rowHeight, width, rowHeight - 4f);
                        Widgets.DrawHighlightIfMouseover(row);
                        Widgets.Label(new Rect(4f, row.y, width - 8f, 24f), hit.Title.Truncate(width - 8f));
                        Text.Font = GameFont.Tiny;
                        GUI.color = MenuControls.Accent;
                        Widgets.Label(new Rect(4f, row.y + 25f, width - 8f, 18f), hit.Path.Truncate(width - 8f));
                        GUI.color = new Color(0.72f, 0.72f, 0.72f);
                        Widgets.Label(new Rect(4f, row.y + 44f, width - 8f, 20f), hit.Context.Truncate(width - 8f));
                        Text.Font = GameFont.Small;
                        GUI.color = Color.white;
                        TooltipHandler.TipRegion(row, hit.Title + "\n" + hit.Path + "\n" + hit.Context);
                        if (Widgets.ButtonInvisible(row) && session.Select(hit.Page))
                        {
                            browse.OpenTarget();
                            RememberPage(hit.Page);
                            if (hit.Anchor != null && hit.Page.Focus != null)
                            {
                                try { hit.Page.Focus(hit.Anchor); }
                                catch (Exception exception) { ReportFailure("focus " + hit.Page.Id + "/" + hit.Anchor, exception); }
                            }
                        }
                    }
                }
                finally { Widgets.EndScrollView(); }
            }
            float y = rect.yMax - 28f;
            GUI.enabled = browse.ResultPage > 0;
            if (Widgets.ButtonImage(new Rect(rect.x, y, 24f, 24f), TexUI.ArrowTexLeft, tooltip: "IM_Previous".Translate()))
            { browse.ResultPage--; resultScroll = Vector2.zero; }
            GUI.enabled = end < count;
            if (Widgets.ButtonImage(new Rect(rect.xMax - 24f, y, 24f, 24f), TexUI.ArrowTexRight, tooltip: "IM_Next".Translate()))
            { browse.ResultPage++; resultScroll = Vector2.zero; }
            GUI.enabled = true;
            string status = "IM_ResultPage".Translate(browse.ResultPage + 1, Math.Max(1, (count + MenuCatalog.PageSize - 1) / MenuCatalog.PageSize), count);
            if (catalog.Pending) status += "  " + "IM_Indexing".Translate();
            Widgets.Label(new Rect(rect.x + 34f, y, Math.Max(1f, rect.width - 68f), 26f), status.Truncate(rect.width - 68f));
        }

        private static void ReportFailure(string operation, Exception exception)
        {
            Log.Error("[IrisMenus] Failed to " + operation + ": " + exception);
            Messages.Message("IM_OperationFailed".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
