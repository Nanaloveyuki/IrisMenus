using System;
using System.IO;
using IrisMenus;
using UnityEngine;
using Verse;

internal static class MarkdownChecks
{
    public static void Run(Action<bool, string> check)
    {
        var parser = new MarkdownParser();
        check(parser.Parse("**bold**") == "<b>bold</b>", "Bold Markdown becomes Unity rich text");
        check(parser.Parse("text **bold**") == "text <b>bold</b>", "Bold after whitespace is parsed");
        check(parser.Parse("text,**bold**") == "text,**bold**", "Punctuation before bold keeps original Markdown");
        check(parser.Parse("text, **bold**") == "text, <b>bold</b>", "Whitespace separates punctuation from bold");
        check(parser.Parse("<color=#ff0000ff>**red**</color>") ==
            "<color=#ff0000ff><b>red</b></color>", "RimWorld color tags remain intact around Markdown");
        check(parser.Parse("**<color=#ff0000ff>red</color>**") ==
            "<b><color=#ff0000ff>red</color></b>", "RimWorld color tags remain intact inside Markdown");
        check(parser.Parse("<color=#ff0000ff># Heading</color>") ==
            "<b><color=#ff0000ff>Heading</color></b>", "Color-wrapped headings keep valid rich-text nesting");
        check(parser.Parse("<color=#ff0000ff>- **item**</color>") ==
            "<color=#ff0000ff>- <b>item</b></color>", "Color-wrapped lists keep block parsing");
        check(parser.Parse("<size=200>large</size>") == "large", "Unsupported rich-text tags are removed consistently");
        check(parser.Parse("<size=200>- item</size>") == "- item", "Unsupported tags are removed from block prefixes");
        check(parser.Parse("<i>italic</i>") == "<i>italic</i>", "Supported RimWorld style tags remain intact");
        check(parser.Parse("# Heading\n- **item**\n1. *next*") ==
            "<b>Heading</b>\n- <b>item</b>\n1. <i>next</i>", "Common settings-page blocks are parsed");
        check(parser.Parse(@"\**bold**") == "**bold**", "Escaped Markdown delimiter stays literal");
        check(parser.Parse("line\r\nnext") == "line\nnext", "Windows line endings are normalized");

        string source = "**one**";
        var dynamicParser = new CountingParser();
        var dynamic = new DynamicMarkdown(() => source, dynamicParser);
        check(dynamic.RenderedText == "<b>one</b>", "Dynamic Markdown renders its initial source");
        dynamic.Measure(100f);
        dynamic.Draw(new Rect());
        check(dynamicParser.Calls == 1 && Widgets.LastLabel == "<b>one</b>", "Dynamic Markdown caches and draws unchanged input");
        source = "**two**";
        check(dynamic.RenderedText == "<b>two</b>" && dynamicParser.Calls == 2, "Dynamic Markdown reparses changed input");

        string sectionSource = "one";
        var sectionMarkdown = new DynamicMarkdown(() => sectionSource);
        var sectionView = new MenuSectionView(new[]
        {
            new MenuSection("markdown", sectionMarkdown.Measure, sectionMarkdown.Draw,
                () => sectionMarkdown.MarkdownText)
        });
        sectionView.Draw(new Rect(0f, 0f, 120f, 10f));
        float firstContentHeight = Widgets.LastContent.height;
        sectionSource = "one\nline whose height changes";
        sectionView.Draw(new Rect(0f, 0f, 120f, 10f));
        check(Widgets.LastContent.height > firstContentHeight,
            "Dynamic Markdown layout key remeasures changed content");

        Widgets.LastLabel = null;
        var listing = new Listing_Standard
        {
            ColumnWidth = 120f,
            BoundingRectCached = new Rect(0f, 200f, 120f, 20f)
        };
        MenuControls.Markdown(listing, sectionMarkdown);
        check(Widgets.LastLabel == null, "Markdown control skips drawing outside listing bounds");
        listing = new Listing_Standard
        {
            ColumnWidth = 120f,
            BoundingRectCached = new Rect(0f, 0f, 120f, 40f)
        };
        MenuControls.Markdown(listing, sectionMarkdown);
        check(Widgets.LastLabel == sectionMarkdown.RenderedText, "Markdown control draws rows inside listing bounds");

        string path = Path.Combine(Path.GetTempPath(), "irismenues-markdown-" + Guid.NewGuid().ToString("N") + ".md");
        try
        {
            File.WriteAllText(path, "**file**");
            var staticParser = new CountingParser();
            var document = new StaticMarkdown(path, staticParser);
            File.WriteAllText(path, "**changed**");
            check(document.RenderedText == "<b>file</b>" && staticParser.Calls == 1, "Static Markdown keeps its initial file snapshot");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private sealed class CountingParser : IMarkdownParser
    {
        private readonly MarkdownParser inner = new MarkdownParser();
        public int Calls { get; private set; }
        public string Parse(string markdown) { Calls++; return inner.Parse(markdown); }
    }
}
