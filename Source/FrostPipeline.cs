using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace IrisMenus
{
    // Unity-only GPU path; also compiled into the editor validation project.
    internal sealed class FrostPipeline : IDisposable
    {
        private readonly Material material;
        private Camera camera;
        private CommandBuffer commands;
        private RenderTexture scratch;
        internal RenderTexture Texture { get; private set; }
        internal Camera Camera => camera;

        internal FrostPipeline(Shader shader)
        {
            if (shader == null || !shader.isSupported || shader.passCount < 2)
                throw new InvalidOperationException("Frost shader is missing or unsupported.");
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        internal void Bind(Camera source, int width, int height, float strength)
        {
            int w = Mathf.Max(1, width / 2);
            int h = Mathf.Max(1, height / 2);
            material.SetFloat("_Radius", Mathf.Lerp(0f, 6f, Mathf.Clamp01(strength)));
            if (source == camera && Texture != null && Texture.width == w && Texture.height == h) return;
            ReleaseTargets();
            if (source == null) return;
            try
            {
                Texture = CreateTarget(w, h, "IrisMenus Frost");
                scratch = CreateTarget(w, h, "IrisMenus Frost Scratch");
                commands = new CommandBuffer { name = "IrisMenus Frost" };
                Record(commands, BuiltinRenderTextureType.CameraTarget, Texture, scratch, material);
                commands.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                camera = source;
                camera.AddCommandBuffer(CameraEvent.AfterEverything, commands);
            }
            catch { ReleaseTargets(); throw; }
        }

        internal static void Record(CommandBuffer buffer, RenderTargetIdentifier source,
            RenderTexture output, RenderTexture scratch, Material blur)
        {
            buffer.Blit(source, output);
            buffer.Blit(output, scratch, blur, 0);
            buffer.Blit(scratch, output, blur, 1);
        }

        private static RenderTexture CreateTarget(int width, int height, string name)
        {
            var texture = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                name = name,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            if (!texture.Create())
            {
                UnityEngine.Object.Destroy(texture);
                throw new InvalidOperationException("Cannot allocate frost render texture.");
            }
            return texture;
        }

        internal void ReleaseTargets()
        {
            if (camera != null && commands != null) camera.RemoveCommandBuffer(CameraEvent.AfterEverything, commands);
            camera = null;
            commands?.Release();
            commands = null;
            DestroyTarget(Texture);
            DestroyTarget(scratch);
            Texture = null;
            scratch = null;
        }

        private static void DestroyTarget(RenderTexture texture)
        {
            if (texture == null) return;
            texture.Release();
            UnityEngine.Object.Destroy(texture);
        }

        public void Dispose()
        {
            ReleaseTargets();
            UnityEngine.Object.Destroy(material);
        }
    }
}
