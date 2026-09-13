using System;
using System.Collections.Generic;

// Only engine boundaries are doubled. Registry, session, and scroll code are linked unchanged.
namespace UnityEngine
{
    public struct Rect
    {
        public float x, y, width, height;
        public float xMax => x + width;
        public float yMax => y + height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Overlaps(Rect other) => x < other.xMax && xMax > other.x && y < other.yMax && yMax > other.y;
    }
    public struct Vector2 { public float x, y; public static Vector2 zero => default; }
    public static class Mathf
    {
        public static float Max(float a, float b) => Math.Max(a, b);
        public static float Min(float a, float b) => Math.Min(a, b);
        public static float Clamp(float value, float min, float max) => Math.Clamp(value, min, max);
        public static float Clamp01(float value) => Math.Clamp(value, 0, 1);
    }
    public sealed class ExitGUIException : Exception { }
}

namespace Verse
{
    public class ModContentPack
    {
        public string PackageIdPlayerFacing = "test.mod";
        public string Name = "Test Mod";
        public string RootDir = "test-root";
        public ModAssetBundlesHandler assetBundles = new ModAssetBundlesHandler();
    }
    public class Mod
    {
        public ModContentPack Content = new ModContentPack();
        public Mod(ModContentPack content = null) { if (content != null) Content = content; }
        public T GetSettings<T>() where T : new() => new T();
        public int Saves;
        public int Draws;
        public virtual string SettingsCategory() => "Settings";
        public virtual void DoSettingsWindowContents(UnityEngine.Rect rect) { Draws++; }
        public virtual void WriteSettings() { Saves++; }
    }
    public static class LoadedModManager { public static List<Mod> ModHandles = new List<Mod>(); }
    public static class Log
    {
        public static List<string> Errors = new List<string>();
        public static void Error(string text) { Errors.Add(text); }
        public static void Warning(string text) { Errors.Add(text); }
    }
    public static class LanguageDatabase { public static object activeLanguage; }
    public static class Find
    {
        public static WindowStack WindowStack = new WindowStack();
        public static UnityEngine.Camera Camera;
        public static UnityEngine.Camera WorldCamera;
    }
    public class WindowStack
    {
        public List<object> Windows = new List<object>();
        public bool IsOpen<T>() => Windows.Exists(window => window is T);
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        public void Add(Window window) { Windows.Add(window); }
        public T WindowOfType<T>() where T : class => Windows.Find(window => window is T) as T;
        public Window Focused;
        public void Notify_ManuallySetFocus(Window window) { Focused = window; }
    }
    public class Listing_Standard
    {
        public bool maxOneColumn;
        public float CurHeight;
        public float ColumnWidth;
        public UnityEngine.Rect? BoundingRectCached { get; set; }
        public void Begin(UnityEngine.Rect rect) { Widgets.Groups++; ColumnWidth = rect.width; }
        public void End() { Widgets.Groups--; }
        public UnityEngine.Rect GetRect(float height, float gap = 1f)
        {
            UnityEngine.Rect rect = new UnityEngine.Rect(0f, CurHeight, ColumnWidth, height);
            CurHeight += height * gap;
            return rect;
        }
        public void Gap(float gap) { CurHeight += gap; }
        public void GapLine(float gap) { Gap(gap); }
        public void Label(string label) { Widgets.Label(GetRect(Text.CalcHeight(label, ColumnWidth)), label); Gap(4f); }
        public void CheckboxLabeled(string label, ref bool value, string tooltip = null)
        {
            GetRect(30f);
            Gap(4f);
        }
    }
    public static class Widgets
    {
        public static bool ButtonText(UnityEngine.Rect rect, string text) => false;
        public static UnityEngine.Color WindowBGFillColor;
        public static string LastLabel;
        public static void Label(UnityEngine.Rect rect, string text) { LastLabel = text; }
        public static float HorizontalSlider(UnityEngine.Rect rect, float value, float min, float max) => value;
        public static void TextFieldNumeric(UnityEngine.Rect rect, ref int value, ref string buffer, int min, int max) { }
        public static void TextFieldNumeric(UnityEngine.Rect rect, ref float value, ref string buffer, float min, float max) { }
        public static void DrawBox(UnityEngine.Rect rect) { }
        public static int Highlights;
        public static void DrawHighlight(UnityEngine.Rect rect) { Highlights++; }
        public static int Groups;
        public static UnityEngine.Rect LastContent;
        public static UnityEngine.Vector2 LastPosition;
        public static void BeginScrollView(UnityEngine.Rect viewport, ref UnityEngine.Vector2 position, UnityEngine.Rect content)
        { Groups++; LastContent = content; LastPosition = position; }
        public static void EndScrollView() { Groups--; }
    }
    public static class Text
    {
        public static float CalcHeight(string text, float width) => text == null ? 0f : text.Length;
    }
    public static class TooltipHandler
    {
        public static void TipRegion(UnityEngine.Rect rect, string text) { }
    }
    public sealed class FloatMenuOption
    {
        public FloatMenuOption(string label, Action action) { }
    }
    public sealed class FloatMenu : Window
    {
        public FloatMenu(List<FloatMenuOption> options) { }
    }
    public static class StringExtensions
    {
        public static string Truncate(this string value, float maxLength) => value;
    }
}

namespace IrisMenus
{
    internal sealed class GuiState { internal void Restore() { } }
    public class MenuWindow : Verse.Window
    {
        public MenuWindow() { }
        internal MenuWindow(MenuPage page) { Page = page; }
        internal MenuPage Page;
        internal bool AcceptPage = true;
        internal bool OpenPage(MenuPage page) { if (!AcceptPage) return false; Page = page; return true; }
    }
}
