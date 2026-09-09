# IrisMenus 接入指南

IrisMenus 是 RimWorld 1.6 的共享 Mod 设置窗口，需要 Harmony，不依赖 RimIris。
设置值仍由你的 Mod 持有、修改和保存；IrisMenus 负责页面、导航和搜索。

## 1. 引用与加载

编译时引用 `1.6/Assemblies/IrisMenus.dll`，设置 `Private=false`，不要把它再打包进自己的 Mod。
在你的 `About/About.xml` 的 `modDependencies` 中加入：

```xml
<li>
  <packageId>Nanaloveyuki.IrisMenus</packageId>
  <displayName>IrisMenus</displayName>
</li>
```

让 Harmony、IrisMenus 排在消费方之前。通常在 Mod 构造函数中、取得自己的设置对象后注册页面。
注册和 UI 操作都在游戏主线程执行，不要在每次绘制时重复注册。

## 2. 普通页面与分类

```csharp
MenuRegistry.RegisterSubItemListing(this, "general", () => "常规", DrawGeneral);
MenuRegistry.RegisterSubItemListing(this, "advanced", () => "高级", DrawAdvanced);
```

两个绘制方法的参数都是 `Listing_Standard`。分类 ID 在同一个 Mod 内必须唯一且稳定，不要使用翻译后的标题作 ID。
标题回调可以返回 `"YourTranslationKey".Translate().ToString()`。

旧的 `RegisterListing` 仍可使用：只有一页时直接打开，多页时自动按 Mod 分组。
`RegisterSubItemListing` 明确声明分类，即使只有一个分类也显示父级。仅选中的 Mod 展开，并记住它上次打开的页面。

```csharp
private void DrawGeneral(Listing_Standard list)
{
    MenuControls.Section(list, "基础设置");
    MenuControls.Anchor(list, "enabled");
    MenuControls.Checkbox(list, "启用功能", ref settings.Enabled);
}
```

`settings` 是你自己的设置对象。可用控件还有 `Slider`、整数/浮点 `Number` 和下拉 `Select`。
`Number` 的编辑字符串必须保存在实例字段里，不能每帧重新创建。

## 3. 搜索并跳转到控件

在注册页面后声明搜索元数据：

```csharp
MenuRegistry.RegisterSearchProvider(this, "general", () => new[]
{
    new MenuSearchEntry("enabled", () => "启用功能",
        keywords: () => "开关 activate toggle",
        context: () => "控制本 Mod 的主要功能")
});
```

搜索条目 ID 与绘制时的 `MenuControls.Anchor` ID 相同。锚点放在目标控件前，不占布局高度；默认高亮高度是 30，可用第三个参数调整。
点击结果后会打开所属页，按本次布局测量的位置滚动并短暂高亮。Listing 的滚动在下一次绘制生效。
条件隐藏的控件无法定位，应提供可见的分节锚点，或自定义 focus 回调先展开条件。

左侧“全局选项”是分页目录，不是所有控件拼成的长表。全局搜索覆盖已接入的页面；选中 Mod 后搜索该 Mod 的全部分类。
结果只显示名称、Mod、分类和上下文，不直接修改设置。返回箭头保留当前窗口中的关键词、结果页和结果滚动位置。
无关键词时全局目录也包含显式声明的设置条目，每页最多 20 条。关键词以空白分词，所有词都匹配才返回，不区分大小写。

元数据应轻量，不调用绘制函数。大量条目建议用 `yield return` 分批提供；语言切换自动重建索引，其他元数据变化后调用 `MenuRegistry.InvalidateSearchIndex()`。

## 4. 自定义页面与超长页面

`Register` / `RegisterSubItem` 接受 `Action<Rect>`，页面负责自己的滚动。
普通 `RegisterListing` 会执行整页回调，滚动裁剪不等于虚拟列表。
超长页面可以用 `MenuSectionView`：

```csharp
var view = new MenuSectionView(new[]
{
    new MenuSection("details", width => 100f,
        rect => Widgets.Label(rect, "详细设置"))
});
MenuRegistry.RegisterSubItem(this, "details", () => "详细", view.Draw);
MenuRegistry.RegisterSearchProvider(this, "details", () => new[]
{
    new MenuSearchEntry("details", () => "详细设置")
}, view.Focus);
```

实际使用时，高度回调应按可用宽度准确测量内容。分节仅在可见时绘制；宽度或语言变化时自动重新测量。
展开状态、内容或字体变化导致高度改变时，调用 `view.InvalidateLayout()`。
同一个 view 应保持为长期实例，不能每帧创建。

## 5. 原版设置页与保存

用户可以在添加设置页面中手动选择原版 Mod；适配页直接调用它的 `DoSettingsWindowContents(Rect)`，不再套一层滚动。
没有额外元数据时只能按 Mod/页面名称搜索并打开整页，无法自动识别控件。
`RegisterAdapterSearch(owner, provider, focus)` 可以补充元数据和自定义定位，但不会自动启用托管，也不会自动拆分分类。
需要独立分类时，应显式注册多个页面。

各注册接口最后一个可选参数是保存回调，默认 `owner.WriteSettings`。
切换页面或关闭窗口时保存，失败会保留当前页/窗口供重试。设置是即时修改的，关闭不是取消。
同一页面重复点击不触发离页保存；搜索结果返回时会保存。搜索关键词和结果页不跨游戏重启保存。

完整、参与编译验证的示例位于仓库 `tests/ApiConsumer/ExampleMod.cs`。
引擎替身测试和编译不能替代游戏实测：接入后需检查切页、定位、长翻译、窗口缩放、保存与重启。
