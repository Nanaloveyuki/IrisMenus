using System;
using UnityEngine;

namespace UnityEngine
{
    public class Camera
    {
        public static event Action<Camera> onPreRender;
        public static event Action<Camera> onPostRender;
        public static int Subscribers => (onPreRender?.GetInvocationList().Length ?? 0) + (onPostRender?.GetInvocationList().Length ?? 0);
        public object targetTexture;
        public int pixelWidth = 1920, pixelHeight = 1080;
        public void Render() { onPreRender?.Invoke(this); onPostRender?.Invoke(this); }
    }
    public sealed class Shader { }
    public sealed class AssetBundle
    {
        public static bool FailLoad;
        public static int Loads, Unloads;
        public bool Contains(string path) => !FailLoad;
        public static AssetBundle LoadFromFile(string path) { Loads++; return FailLoad ? null : new AssetBundle(); }
        public T LoadAsset<T>(string name) where T : new() => new T();
        public void Unload(bool all) { Unloads++; }
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b) : this(r, g, b, 1f) { }
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
    }
    public class GUIStyle { }
    public enum EventType { Repaint }
    public class Event { public static Event current = new Event(); public EventType type; }
    public static class Time { public static int frameCount; public static float realtimeSinceStartup; }
    public static class GUI
    {
        public static Color color;
        public static int TextureDraws;
        public static float OverlayAlpha;
        public static void DrawTextureWithTexCoords(Rect rect, object texture, Rect uv) { TextureDraws++; }
        public static void DrawTexture(Rect rect, object texture) { OverlayAlpha = color.a; }
        public static void BeginGroup(Rect rect) { Verse.Widgets.Groups++; }
        public static void EndGroup() { Verse.Widgets.Groups--; }
    }
}

namespace Verse
{
    public class ModAssetBundlesHandler { public System.Collections.Generic.List<AssetBundle> loadedAssetBundles = new System.Collections.Generic.List<AssetBundle>(); }
    public class Window { public Rect windowRect; }
    public static class UI { public static int screenWidth = 1920, screenHeight = 1080; }
    public enum ProgramState { Entry, Playing }
    public static class Current { public static ProgramState ProgramState; }
    public static class BaseContent { public static object WhiteTex = new object(); }
    public interface IWindowDrawing
    {
        GUIStyle EmptyStyle { get; }
        void DoWindowBackground(Rect rect);
        bool DoCloseButton(Rect rect, string text);
        bool DoClostButtonSmall(Rect rect);
        void BeginGroup(Rect rect);
        void EndGroup();
        void DoGrayOut(Rect rect);
    }
    public sealed class DefaultWindowDrawing : IWindowDrawing
    {
        public static int Backgrounds;
        public GUIStyle EmptyStyle => new GUIStyle();
        public void DoWindowBackground(Rect rect) { Backgrounds++; }
        public bool DoCloseButton(Rect rect, string text) => false;
        public bool DoClostButtonSmall(Rect rect) => false;
        public void BeginGroup(Rect rect) { }
        public void EndGroup() { }
        public void DoGrayOut(Rect rect) { }
    }
}

namespace RimWorld.Planet
{
    public static class WorldRendererUtility { public static bool WorldSelected; }
}

namespace IrisMenus
{
    // GPU internals are tested separately in Unity; this double observes host lifetime.
    internal sealed class FrostPipeline : IDisposable
    {
        internal static int Created, Disposed, Binds;
        internal Camera Camera;
        internal object Texture;
        internal FrostPipeline(Shader shader) { Created++; }
        internal void Bind(Camera camera, int width, int height, float strength)
        { Camera = camera; Texture = new object(); Binds++; }
        internal void ReleaseTargets() { Camera = null; Texture = null; }
        public void Dispose() { Disposed++; ReleaseTargets(); }
    }
}
