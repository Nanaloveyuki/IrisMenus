using System;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class FrostBackground : IWindowDrawing, IDisposable
    {
        private readonly DefaultWindowDrawing vanilla = new DefaultWindowDrawing();
        internal Window Owner;
        private FrostPipeline pipeline;
        private bool failed;
        private int capturedFrame = -10;
        private bool listening;
        private MenuSettings Settings => IrisMenusMod.Instance.Settings;

        public GUIStyle EmptyStyle => vanilla.EmptyStyle;

        internal void Start()
        {
            if (listening) return;
            Camera.onPreRender += BeforeRender;
            Camera.onPostRender += AfterRender;
            listening = true;
        }

        private void BeforeRender(Camera camera)
        {
            if (Owner == null || !Find.WindowStack.Windows.Contains(Owner))
            {
                Dispose();
                return;
            }
            if (!Settings.FrostEnabled || Settings.Transparency <= 0f)
            {
                pipeline?.ReleaseTargets();
                capturedFrame = -10;
                return;
            }
            if (Current.ProgramState != ProgramState.Playing)
            {
                pipeline?.ReleaseTargets();
                capturedFrame = -10;
                return;
            }
            // World view has a separate camera; portrait and preview cameras are never eligible.
            Camera source = WorldRendererUtility.WorldSelected ? Find.WorldCamera : Find.Camera;
            if (pipeline != null && pipeline.Camera != source) { pipeline.ReleaseTargets(); capturedFrame = -10; }
            if (failed || camera != source || camera.targetTexture != null) return;
            try
            {
                if (pipeline == null)
                {
                    // RimWorld owns and preloads bundles in the mod's AssetBundles directory.
                    Shader shader = null;
                    foreach (AssetBundle loaded in IrisMenusMod.Instance.Content.assetBundles.loadedAssetBundles)
                    {
                        if (loaded != null && loaded.Contains("assets/frost.shader"))
                        {
                            shader = loaded.LoadAsset<Shader>("assets/frost.shader");
                            break;
                        }
                    }
                    if (shader == null) throw new InvalidOperationException("Frost shader not found in IrisMenus' game-loaded AssetBundles.");
                    pipeline = new FrostPipeline(shader);
                }
                pipeline.Bind(camera, camera.pixelWidth, camera.pixelHeight, Settings.Blur);
            }
            catch (Exception exception)
            {
                failed = true;
                Release();
                Log.Error("[IrisMenus] Frost background unavailable: " + exception);
            }
        }

        private void AfterRender(Camera camera)
        {
            if (pipeline != null && pipeline.Camera == camera) capturedFrame = Time.frameCount;
        }

        public void DoWindowBackground(Rect rect)
        {
            if (Settings.Transparency <= 0f)
            {
                vanilla.DoWindowBackground(rect);
                return;
            }
            if (Event.current.type != EventType.Repaint) return;
            Color previous = GUI.color;
            try
            {
                Rect window = Owner.windowRect;
                Rect uv = MenuLayout.BackgroundUv(window, UI.screenWidth, UI.screenHeight);
                GUI.color = Color.white;
                if (Settings.FrostEnabled && pipeline?.Texture != null && capturedFrame >= Time.frameCount - 1)
                    GUI.DrawTextureWithTexCoords(rect, pipeline.Texture, uv);
                Color fill = Widgets.WindowBGFillColor;
                fill.a = 1f - Settings.Transparency;
                GUI.color = fill;
                GUI.DrawTexture(rect, BaseContent.WhiteTex);
                GUI.color = new Color(1f, 0.85f, 0.55f, 0.35f);
                Widgets.DrawBox(rect);
            }
            finally { GUI.color = previous; }
        }

        internal void Retry()
        {
            Release();
            failed = false;
        }

        private void Release()
        {
            capturedFrame = -10;
            pipeline?.Dispose();
            pipeline = null;
        }

        public void Dispose()
        {
            if (listening)
            {
                Camera.onPreRender -= BeforeRender;
                Camera.onPostRender -= AfterRender;
                listening = false;
            }
            Release();
        }

        public bool DoCloseButton(Rect rect, string text) => vanilla.DoCloseButton(rect, text);
        public bool DoClostButtonSmall(Rect rect) => vanilla.DoClostButtonSmall(rect);
        public void BeginGroup(Rect rect) => vanilla.BeginGroup(rect);
        public void EndGroup() => vanilla.EndGroup();
        public void DoGrayOut(Rect rect) => vanilla.DoGrayOut(rect);
    }
}
