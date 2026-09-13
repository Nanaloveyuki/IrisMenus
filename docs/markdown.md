# Markdown 渲染

IrisMenus 提供两种只读 Markdown 内容对象。它们把 Markdown 转换为 RimWorld/Unity IMGUI 使用的富文本，然后通过 `IMarkdown` 提供测量和绘制。解析器不依赖 HTML，也不会创建 HTML 文档。

## 快速接入

引用 `IrisMenus.dll` 后使用以下公开类型：

```csharp
using IrisMenus;
using System.IO;
using Verse;

public sealed class ExampleMod : Mod
{
    private readonly IMarkdown liveText;
    private readonly IMarkdown readmeText;

    public ExampleMod(ModContentPack content) : base(content)
    {
        liveText = Markdown.Dynamic(() =>
            "**即时状态**\n\n当前值: " + GetCurrentValue());

        readmeText = Markdown.StaticFile(
            Path.Combine(content.RootDir, "docs", "readme.md"));

        MenuRegistry.RegisterSubItemListing(
            this, "help", () => "帮助", DrawHelp);
    }

    private void DrawHelp(Listing_Standard listing)
    {
        MenuControls.Markdown(listing, liveText);
        MenuControls.Markdown(listing, readmeText);
    }

    private string GetCurrentValue()
    {
        return "由 Mod 自己的状态计算";
    }
}
```

`MenuControls.Markdown` 会先调用 `Measure` 再取得列表区域并调用 `Draw`，适合普通 `Listing_Standard` 页面。自定义页面可以直接注册 `IMarkdown.Draw`：

```csharp
var document = Markdown.Dynamic(() => settings.Description);
MenuRegistry.RegisterSubItem(this, "description", () => "说明", document.Draw);
```

如果页面使用 `MenuSectionView`，把同一个对象的测量和绘制委托交给 section：

```csharp
var document = Markdown.Dynamic(() => settings.Description);
var view = new MenuSectionView(new[]
{
    new MenuSection(
        "description",
        width => document.Measure(width),
        document.Draw,
        () => document.MarkdownText)
});
MenuRegistry.RegisterSubItem(this, "description", () => "说明", view.Draw);
```

## 动态内容

`Markdown.Dynamic(Func<string>)` 和 `new DynamicMarkdown(Func<string>)` 接收内容回调。每次访问 `MarkdownText`、`RenderedText`、`Measure` 或 `Draw` 时都会取得当前输入；只有输入字符串发生变化时才重新解析。把 `() => document.MarkdownText` 作为 `MenuSection` 的第四个 `layoutKey` 参数，可以让 `MenuSectionView` 在动态内容变化时自动重新测量；没有 layout key 的其他动态 section 仍需在高度变化后调用 `view.InvalidateLayout()`。

回调应当只读取 Mod 的当前状态，并在游戏主线程上执行。不要在回调里执行文件 I/O、注册页面或修改游戏状态。若输入是 `null`，按空字符串处理。

可以传入自己的 `IMarkdownParser`：

```csharp
IMarkdownParser parser = new MarkdownParser();
IMarkdown document = new DynamicMarkdown(
    () => "**动态内容**", parser);
```

## 静态文件

`Markdown.StaticFile(path)` 和 `new StaticMarkdown(path)` 会在构造时以 UTF-8 读取外部文件并立即解析。对象只保存这次读取的内容，不监听文件、不热重载，也不会写回文件；文件缺失或读取失败会直接抛出异常。

通常把文件路径绑定到自己的 `ModContentPack.RootDir`：

```csharp
var document = Markdown.StaticFile(
    Path.Combine(Content.RootDir, "docs", "readme.md"));
```

文件变化后需要由消费方重新创建 `StaticMarkdown` 对象。不要在每一帧重新创建对象，否则会重复进行文件 I/O。

## 支持的语法

内置解析器支持设置页面和帮助文本常用的子集：

- `# 标题` 到 `###### 标题`：转换为加粗文本。
- `- 项目`、`* 项目`、`+ 项目`：转换为无序列表行。
- `1. 项目`：保留编号并解析行内内容。
- `> 引用`：保留引用前缀并解析行内内容。
- `**加粗**`：转换为 Unity 的 `<b>...</b>`。
- `*斜体*`、`_斜体_`：转换为 Unity 的 `<i>...</i>`。
- 反斜杠可以转义 Markdown 分隔符，例如 `\**文本**` 会显示为 `**文本**`。
- 输入换行会保留；CRLF 会统一为 LF。

链接、表格、图片和 HTML 不做额外转换；代码围栏不识别，围栏中的行仍按普通文本行解析。原文中的 HTML 不会被解析成 Markdown 或 HTML DOM，且不在受支持富文本标签白名单内的类 XML 标签会被移除，使 `Measure` 与 `Draw` 保持一致。

## `<color>` 兼容

RimWorld 原版生成的 `<color=...>...</color>` 会被识别为已有的富文本标签并原样透传，颜色不会因为 Markdown 解析丢失：

```text
<color=#ff0000ff>**危险**</color>
```

结果相当于：

```text
<color=#ff0000ff><b>危险</b></color>
```

因此也可以把已有的 `ColoredText.Colorize(...)` 结果放入动态内容。解析器只保留 RimWorld 的 `<color>`、`<b>` 和 `<i>` 标签，不提供 HTML 标签适配；其他完整的类 XML 标签会被移除。

## `**` 分隔符边界

加粗分隔符遵循原版 Markdown 在本项目中的边界约定：开始的 `**` 前面必须是文本/字母数字、空白或内容开头。若前面紧邻逗号等其他符号，则不把它当作加粗标记：

```text
**加粗**        -> <b>加粗</b>
文字 **加粗**   -> 文字 <b>加粗</b>
文字,**加粗**   -> 文字,**加粗**
```

不要为了强制解析标记而删除前面的标点；在需要加粗时，用空格将标点和 `**` 分开。

## 低层接口

`IMarkdown` 的成员如下：

```csharp
public interface IMarkdown
{
    string MarkdownText { get; }
    string RenderedText { get; }
    float Measure(float width);
    void Draw(Rect rect);
}
```

`RenderedText` 是面向 RimWorld `Widgets.Label` 的富文本字符串，不是 HTML。若只需要转换结果，可以调用 `Markdown.Parse(source)` 或 `new MarkdownParser().Parse(source)`；若需要绘制和自动测量，应长期持有 `IMarkdown` 对象。
