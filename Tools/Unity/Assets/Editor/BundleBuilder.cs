using System;
using System.IO;
using System.Linq;
using IrisMenus;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class BundleBuilder
{
    private static int assertions;
    private static string Root => Path.GetFullPath(Path.Combine(Application.dataPath, "../../.."));

    public static void Build()
    {
        try
        {
            string output = Path.Combine(Root, "1.6/AssetBundles");
            Directory.CreateDirectory(output);
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Frost.shader");
            Require(shader != null && !ShaderUtil.ShaderHasError(shader), "Shader compilation");
            var manifest = BuildPipeline.BuildAssetBundles(output, new[]
            {
                new AssetBundleBuild { assetBundleName = "irismenus_frost", assetNames = new[] { "Assets/Frost.shader" } }
            }, BuildAssetBundleOptions.ForceRebuildAssetBundle | BuildAssetBundleOptions.ChunkBasedCompression, BuildTarget.StandaloneWindows64);
            Require(manifest != null, "Bundle build");
            var bundle = AssetBundle.LoadFromFile(Path.Combine(output, "irismenus_frost"));
            try
            {
                Require(bundle != null, "Bundle reload");
                shader = bundle.LoadAsset<Shader>("assets/frost.shader");
                Require(shader != null && shader.isSupported && shader.passCount == 2, "Bundle shader support and passes");
                ValidateGpu(shader);
                ValidateCamera(shader);
                ValidateLayout();
            }
            finally { if (bundle != null) bundle.Unload(true); }
            string result = "PASS: " + assertions + " Unity/GPU assertions; device=" + SystemInfo.graphicsDeviceName + "; API=" + SystemInfo.graphicsDeviceType;
            Directory.CreateDirectory(Path.Combine(Root, "tmp"));
            File.WriteAllText(Path.Combine(Root, "tmp/unity-validation.txt"), result);
            Debug.Log(result);
            EditorApplication.Exit(0);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            EditorApplication.Exit(1);
        }
    }

    private static void ValidateGpu(Shader shader)
    {
        var material = new Material(shader);
        try
        {
            foreach (Vector2Int size in new[] { new Vector2Int(1920, 1080), new Vector2Int(960, 540), new Vector2Int(1279, 719) })
            {
                var source = new Texture2D(size.x, size.y, TextureFormat.RGBA32, false);
                var pixels = new Color[size.x * size.y];
                for (int y = 0; y < size.y; y++)
                    for (int x = 0; x < size.x; x++)
                    {
                        float stripe = ((x / 8 + y / 8) % 2) == 0 ? 0.2f : 0.8f;
                        pixels[y * size.x + x] = new Color((float)y / size.y, stripe, 1f - (float)y / size.y, 1f);
                    }
                source.SetPixels(pixels);
                source.Apply();
                var output = new RenderTexture(size.x / 2, size.y / 2, 0, RenderTextureFormat.ARGB32);
                var scratch = new RenderTexture(output.width, output.height, 0, RenderTextureFormat.ARGB32);
                output.Create(); scratch.Create();
                try
                {
                    material.SetFloat("_Radius", 0f);
                    Execute(source, output, scratch, material);
                    Color[] sharp = Read(output, null);
                    material.SetFloat("_Radius", 6f);
                    Execute(source, output, scratch, material);
                    Color[] blurred = Read(output, Path.Combine(Root, "tmp/frost-" + size.x + "x" + size.y + ".png"));
                    double sharpVariance = sharp.Average(c => Math.Pow(c.g - 0.5, 2));
                    double blurVariance = blurred.Average(c => Math.Pow(c.g - 0.5, 2));
                    Require(blurVariance < sharpVariance * 0.04, "Blur lowers checker variance " + size);
                    Require(blurred.Average(c => c.r) > 0.35 && blurred.Average(c => c.b) > 0.35, "Nonblank color output " + size);
                    int bottom = output.width * 8 + output.width / 2;
                    int top = output.width * (output.height - 9) + output.width / 2;
                    Require(blurred[bottom].b > blurred[bottom].r && blurred[top].r > blurred[top].b, "Vertical orientation " + size);
                    Require(Math.Abs(sharp.Average(c => c.r) - blurred.Average(c => c.r)) < 0.025, "Color mean preserved " + size);
                }
                finally
                {
                    output.Release(); scratch.Release();
                    UnityEngine.Object.DestroyImmediate(output);
                    UnityEngine.Object.DestroyImmediate(scratch);
                    UnityEngine.Object.DestroyImmediate(source);
                }
            }
        }
        finally { UnityEngine.Object.DestroyImmediate(material); }
    }

    private static void Execute(Texture source, RenderTexture output, RenderTexture scratch, Material material)
    {
        RenderTexture previous = RenderTexture.active;
        using (var commands = new CommandBuffer())
        {
            FrostPipeline.Record(commands, source, output, scratch, material);
            Graphics.ExecuteCommandBuffer(commands);
        }
        RenderTexture.active = previous;
    }

    private static Color[] Read(RenderTexture output, string png)
    {
        RenderTexture previous = RenderTexture.active;
        var texture = new Texture2D(output.width, output.height, TextureFormat.RGBA32, false);
        try
        {
            RenderTexture.active = output;
            texture.ReadPixels(new Rect(0, 0, output.width, output.height), 0, 0);
            texture.Apply();
            if (png != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(png));
                File.WriteAllBytes(png, texture.EncodeToPNG());
            }
            return texture.GetPixels();
        }
        finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void ValidateCamera(Shader shader)
    {
        var gameObject = new GameObject("IrisMenus camera fixture");
        var camera = gameObject.AddComponent<Camera>();
        camera.enabled = false;
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = Color.red;
        camera.cullingMask = 0;
        var target = new RenderTexture(640, 480, 24);
        target.Create();
        camera.targetTexture = target;
        using (var pipeline = new FrostPipeline(shader))
        {
            try
            {
                pipeline.Bind(camera, 640, 480, 0.5f);
                RenderTexture first = pipeline.Texture;
                pipeline.Bind(camera, 640, 480, 0.8f);
                Require(ReferenceEquals(first, pipeline.Texture), "Targets reused on strength change");
                Require(camera.GetCommandBuffers(CameraEvent.AfterEverything).Length == 1, "One capture command buffer");
                camera.Render();
                Color[] pixels = Read(pipeline.Texture, null);
                Require(pixels.Average(c => c.r) > 0.9 && pixels.Average(c => c.g) < 0.05, "Actual camera target capture");
                camera.backgroundColor = Color.blue;
                camera.Render();
                Require(Read(pipeline.Texture, null).Average(c => c.b) > 0.9, "Camera changes refresh the background");
                camera.backgroundColor = Color.red;
                pipeline.Bind(camera, 320, 240, 0.5f);
                Require(pipeline.Texture.width == 160 && pipeline.Texture.height == 120, "Resolution change reallocates targets");
                Require(camera.GetCommandBuffers(CameraEvent.AfterEverything).Length == 1, "No duplicate buffers after resize");
                pipeline.ReleaseTargets();
                Require(camera.GetCommandBuffers(CameraEvent.AfterEverything).Length == 0 && pipeline.Texture == null, "Close detaches and releases");
                pipeline.Bind(camera, 640, 480, 0.5f);
                camera.Render();
                Require(Read(pipeline.Texture, null).Average(c => c.r) > 0.9, "Reopen captures again");
            }
            finally { camera.targetTexture = null; }
        }
        Require(camera.GetCommandBuffers(CameraEvent.AfterEverything).Length == 0, "Dispose detaches capture");
        target.Release();
        UnityEngine.Object.DestroyImmediate(target);
        UnityEngine.Object.DestroyImmediate(gameObject);
    }

    private static void ValidateLayout()
    {
        foreach (float width in new[] { 360f, 540f, 620f, 960f, 1920f, 3840f })
        foreach (float height in new[] { 440f, 540f, 720f, 1080f, 2160f })
        {
            Rect rect = MenuLayout.Clamp(new Rect(-500f, 9000f, width * 2f, height * 2f), width, height);
            Require(rect.x >= 0f && rect.y >= 0f && rect.xMax <= width && rect.yMax <= height, "Window containment");
            MenuLayout layout = MenuLayout.Calculate(rect.width - 36f, rect.height - 36f);
            Require(layout.Content.width > 0f && layout.Content.height > 0f, "Positive content size");
            Require(layout.Content.yMax <= layout.Footer.y, "Content/footer separation");
            if (!layout.Compact) Require(layout.Navigation.xMax < layout.Content.x, "Column separation");
        }
        Rect invalid = MenuLayout.Clamp(new Rect(float.NaN, float.PositiveInfinity, float.NaN, float.NegativeInfinity), 1920, 1080);
        Require(!float.IsNaN(invalid.x) && !float.IsNaN(invalid.width), "Invalid saved geometry recovery");
        Rect full = MenuLayout.BackgroundUv(new Rect(0, 0, 1920, 1080), 1920, 1080);
        Require(full == new Rect(0, 0, 1, 1), "Full-screen UV");
        Rect scaled = MenuLayout.BackgroundUv(new Rect(100, 50, 400, 300), 960, 540);
        Rect physical = MenuLayout.BackgroundUv(new Rect(200, 100, 800, 600), 1920, 1080);
        Require(scaled == physical, "UV invariant under UI scaling");
        Require(Math.Abs(scaled.yMax - (1f - 50f / 540f)) < 0.0001f, "GUI top-down to texture bottom-up coordinates");
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("Validation failed: " + message);
        assertions++;
    }
}
