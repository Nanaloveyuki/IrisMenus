using System;
using UnityEngine;

namespace IrisMenus
{
    internal sealed class MenuSession
    {
        private readonly Action<string, Exception> report;
        internal MenuPage Selected { get; private set; }
        internal bool DrawFailed { get; private set; }

        internal MenuSession(Action<string, Exception> report) { this.report = report; }

        internal bool Select(MenuPage page)
        {
            if (page == Selected) return true;
            if (!Save()) return false;
            Selected = page;
            DrawFailed = false;
            return true;
        }

        internal bool Save()
        {
            if (Selected == null) return true;
            try { Selected.Save(); return true; }
            catch (Exception exception) { report("save " + Selected.Id, exception); return false; }
        }

        internal void Draw(Rect rect)
        {
            if (Selected == null || DrawFailed) return;
            try { Selected.Draw(rect); }
            catch (ExitGUIException) { throw; }
            catch (Exception exception)
            {
                DrawFailed = true;
                report("draw " + Selected.Id, exception);
            }
        }

        internal void Retry() { DrawFailed = false; }
    }
}
