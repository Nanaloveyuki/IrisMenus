using System.Collections.Generic;

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
            Scope = page?.OwnerId;
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

        internal void Capture(Dictionary<string, string> view, MenuPage selected)
        {
            view["mode"] = Managing ? "manage" : ViewingTarget ? "target" : ShowResults ? (Query.Length > 0 ? "search" : "global") : "page";
            if (Query.Length > 0) view["query"] = Query;
            else view.Remove("query");
            if (Scope != null) view["scope"] = Scope;
            else view.Remove("scope");
            if (ResultPage > 0) view["resultPage"] = ResultPage.ToString();
            else view.Remove("resultPage");
            if (ViewingTarget && selected != null) view["target"] = selected.Id;
            else view.Remove("target");
        }

        internal void Restore(IReadOnlyDictionary<string, string> view, MenuPage selected, bool scopeAvailable)
        {
            view.TryGetValue("mode", out string mode);
            view.TryGetValue("query", out string query);
            view.TryGetValue("scope", out string scope);
            int.TryParse(view.TryGetValue("resultPage", out string page) ? page : "0", out int resultPage);
            if (resultPage < 0) resultPage = 0;
            Query = query ?? "";
            ResultPage = resultPage;
            Managing = ViewingTarget = false;
            Scope = null;
            if (mode == "manage") { Managing = true; return; }
            if (mode == "page") { Navigate(selected); return; }
            if (mode == "target" && selected != null)
            {
                Scope = scopeAvailable ? scope : null;
                ViewingTarget = true;
                return;
            }
            if (scopeAvailable && mode == "search")
            {
                Scope = scope;
                return;
            }
            if (mode == "global" || mode == "search" || mode == "target" || string.IsNullOrEmpty(mode))
            {
                Scope = null;
                ViewingTarget = false;
                return;
            }
            Query = "";
            ResultPage = 0;
        }
    }
}
