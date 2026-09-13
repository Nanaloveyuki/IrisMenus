using System;
using System.IO;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    /// <summary>Read-only rendered Markdown content that can be measured and drawn by a settings page.</summary>
    public interface IMarkdown
    {
        string MarkdownText { get; }
        string RenderedText { get; }
        float Measure(float width);
        void Draw(Rect rect);
    }

    /// <summary>Markdown whose source is read again when its content is requested.</summary>
    public sealed class DynamicMarkdown : IMarkdown
    {
        private readonly Func<string> getMarkdown;
        private readonly IMarkdownParser parser;
        private bool hasCache;
        private string markdownText = string.Empty;
        private string renderedText = string.Empty;

        public DynamicMarkdown(Func<string> getMarkdown) : this(getMarkdown, null) { }

        public DynamicMarkdown(Func<string> getMarkdown, IMarkdownParser parser)
        {
            this.getMarkdown = getMarkdown ?? throw new ArgumentNullException(nameof(getMarkdown));
            this.parser = parser ?? new MarkdownParser();
        }

        public string MarkdownText
        {
            get
            {
                Refresh();
                return markdownText;
            }
        }

        public string RenderedText
        {
            get
            {
                Refresh();
                return renderedText;
            }
        }

        public float Measure(float width)
        {
            Refresh();
            return MarkdownRendering.Measure(renderedText, width);
        }

        public void Draw(Rect rect)
        {
            Refresh();
            MarkdownRendering.Draw(rect, renderedText);
        }

        private void Refresh()
        {
            string current = getMarkdown() ?? string.Empty;
            if (hasCache && string.Equals(current, markdownText, StringComparison.Ordinal)) return;
            markdownText = current;
            renderedText = parser.Parse(current) ?? string.Empty;
            hasCache = true;
        }
    }

    /// <summary>Markdown loaded once from an external UTF-8 file; the file is never written or watched.</summary>
    public sealed class StaticMarkdown : IMarkdown
    {
        private readonly IMarkdownParser parser;

        public StaticMarkdown(string filePath) : this(filePath, null) { }

        public StaticMarkdown(string filePath, IMarkdownParser parser)
        {
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("A Markdown file path is required.", nameof(filePath));
            FilePath = Path.GetFullPath(filePath);
            MarkdownText = File.ReadAllText(FilePath);
            this.parser = parser ?? new MarkdownParser();
            RenderedText = this.parser.Parse(MarkdownText) ?? string.Empty;
        }

        public string FilePath { get; }
        public string MarkdownText { get; }
        public string RenderedText { get; }

        public float Measure(float width) => MarkdownRendering.Measure(RenderedText, width);

        public void Draw(Rect rect) => MarkdownRendering.Draw(rect, RenderedText);
    }

    /// <summary>Factories and the pure conversion entry point for the built-in Markdown implementation.</summary>
    public static class Markdown
    {
        public static string Parse(string markdown) => new MarkdownParser().Parse(markdown);

        public static IMarkdown Dynamic(Func<string> getMarkdown, IMarkdownParser parser = null)
        {
            return new DynamicMarkdown(getMarkdown, parser);
        }

        public static IMarkdown StaticFile(string filePath, IMarkdownParser parser = null)
        {
            return new StaticMarkdown(filePath, parser);
        }
    }

    internal static class MarkdownRendering
    {
        public static float Measure(string renderedText, float width)
        {
            return Text.CalcHeight(renderedText ?? string.Empty, Mathf.Max(1f, width));
        }

        public static void Draw(Rect rect, string renderedText)
        {
            Widgets.Label(rect, renderedText ?? string.Empty);
        }
    }
}
