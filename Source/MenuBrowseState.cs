namespace IrisMenus
{
    internal sealed class MenuBrowseState
    {
        internal string Query { get; private set; } = "";
        internal string Scope { get; private set; }
        internal bool Managing { get; private set; }
        internal bool ViewingTarget { get; private set; }
        internal int ResultPage;
        internal bool Global => !Managing && Scope == null;
        internal bool ShowResults => !Managing && !ViewingTarget && (Global || Query.Length > 0);

        internal void Navigate(MenuPage page)
        {
            Scope = page == null ? null : MenuRegistry.OwnerId(page.Owner);
            Query = "";
            Managing = ViewingTarget = false;
            ResultPage = 0;
        }

        internal void Manage()
        {
            Navigate(null);
            Managing = true;
        }

        internal void Search(string query)
        {
            if (Query == query) return;
            Query = query;
            ViewingTarget = false;
            ResultPage = 0;
        }

        internal void OpenTarget() { ViewingTarget = true; }
        internal void Back() { ViewingTarget = false; }
    }
}
