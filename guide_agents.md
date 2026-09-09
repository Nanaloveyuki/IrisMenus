# IrisMenus Agent Integration Contract

## Scope and ownership

- Target RimWorld 1.6, .NET Framework 4.8, namespace `IrisMenus`. Require Harmony and package `Nanaloveyuki.IrisMenus`; no RimIris dependency.
- Reference IrisMenus.dll with `Private=false`. Never ship a duplicate DLL in a consumer. Declare the mod dependency and load the provider before the consumer.
- Run registration, invalidation, focus and rendering on the game thread. Register once, normally after `GetSettings<T>()` in the Mod constructor. No unregister/replace-page API exists.
- Consumer owns settings and persistence. The host neither serializes consumer values nor supports rollback. Preserve existing settings keys and save semantics.
- Do not patch other mods, scan private fields, or invoke drawing to discover settings. Manual adapter opt-in remains a user decision.

## Public API

```csharp
MenuRegistry.Register(Mod owner, string pageId, Func<string> title,
    Action<Rect> draw, Action save = null);
MenuRegistry.RegisterListing(Mod owner, string pageId, Func<string> title,
    Action<Listing_Standard> draw, Action save = null);
MenuRegistry.RegisterSubItem(Mod owner, string pageId, Func<string> title,
    Action<Rect> draw, Action save = null);
MenuRegistry.RegisterSubItemListing(Mod owner, string pageId, Func<string> title,
    Action<Listing_Standard> draw, Action save = null);
MenuRegistry.RegisterSearchProvider(Mod owner, string pageId,
    Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null);
MenuRegistry.RegisterAdapterSearch(Mod owner,
    Func<IEnumerable<MenuSearchEntry>> provider, Action<string> focus = null);
MenuRegistry.InvalidateSearchIndex();
MenuRegistry.Open();

new MenuSearchEntry(string id, Func<string> title,
    Func<string> keywords = null, Func<string> context = null);
MenuControls.Anchor(Listing_Standard list, string id, float height = 30f);

new MenuScrollView(); // Draw(Rect, Action<Listing_Standard>), Focus(string), Reset()
new MenuSection(string id, Func<float, float> measure, Action<Rect> draw);
new MenuSectionView(IEnumerable<MenuSection> sections); // Draw(Rect), Focus(string), InvalidateLayout()
```

These are invocation/signature summaries, not a standalone compilable file. See `tests/ApiConsumer/ExampleMod.cs` for a compile-checked integration.

## IDs, navigation and persistence

- Owner identity is `Content.PackageIdPlayerFacing + ":" + GetType().FullName`; page identity appends `":" + pageId`. Keep IDs and owner type stable across versions. Renaming loses remembered navigation unless separately migrated.
- Registration rejects null owner/title/draw and blank page ID; duplicate full page ID throws. Search declarations require an already registered page. Re-declaring a provider replaces its metadata provider and increments the registry revision.
- All pages of an owner group together when there are multiple pages. A single legacy page opens directly; a single explicit SubItem retains a parent heading. Mixed legacy/SubItem registrations share one group.
- Only the selected owner expands. Selecting the owner heading restores its last page, falling back to the first available page when removed. `LastSubItems` and `LastPage` persist through host settings.
- The global catalog is a directory of pages plus declared settings, not a combined settings editor. Search scope is all hosted pages or all categories of the selected owner.
- Opening a search hit retains the original search scope, query, result-page index and scroll. Back returns to those results. Explicit navigation clears the query and resets result pagination. Search state is window-local.

## Metadata and incremental work

- `MenuSearchEntry.Id` is both the unique per-page metadata ID and the anchor passed to focus. There is no separate anchor property. Title is required; keywords/context are optional callbacks. Null returned title falls back to the page title.
- Matching covers title, context, keywords, owner name and page title. Whitespace-separated terms use AND with `CurrentCultureIgnoreCase`. No fuzzy matching, ranking or private-control introspection.
- Index construction never invokes page draws. Each frame advances at most 32 provider/page steps and 32 record matches, once across IMGUI events. Pagination draws at most 20 result rows. Partial results are visible while indexing.
- This is a work-count budget, NOT a wall-clock guarantee. Provider invocation, each `MoveNext`, and text callbacks must be cheap. Prefer lazy `yield return`; avoid prebuilding large arrays, I/O or game-state mutation in providers.
- All records/results remain in memory; pagination bounds drawn rows, not total index memory. Matching a changed query scans stored records incrementally. Unchanged queries do not rescan.
- Language object changes, registry revisions and adapter selection changes rebuild the catalog. Call `InvalidateSearchIndex()` after other metadata changes. Do not invalidate every draw. Providers must be re-enumerable.
- Null/duplicate entries or callback/enumerator exceptions log a page-specific failure, stop that provider and preserve already collected records and other pages. Enumerators are disposed on completion, failure, rebuild and close. A valid page-level fallback is added before enumeration.

## Listing anchors

- Registered Listing pages already own a persistent `MenuScrollView`, and search focus defaults to its `Focus`. With custom `Register` plus a scroll view, explicitly pass `scroll.Focus` to `RegisterSearchProvider`.
- Call `MenuControls.Anchor` immediately before a control, using the active listing's content coordinates. It does not reserve height. Supply the actual highlight height when different from 30.
- Anchor IDs must be nonblank and unique during each draw. Calling Anchor outside a host scroll view throws. Do not use it in nested independent listing coordinate spaces; use a custom focus implementation instead.
- Focus stores an ID, never a cached pixel offset. The next draw resolves current layout and updates scrolling for the following draw. Highlight lasts four real-time seconds from Focus.
- A missing conditional anchor logs a warning and consumes the pending focus. To reveal conditional content, install a callback that expands it then calls a separately owned scroll view's Focus, or index an always-visible section.
- Passing a non-null focus to `RegisterSearchProvider` replaces the default listing focus. Passing null retains the existing focus. There is no public getter for a registered listing's private scroll view.
- `Reset()` only resets scroll position. Listing clipping still executes the complete callback; do not claim virtualization.

## Lazy sections

- Construct a persistent `MenuSectionView` once. Constructor materializes the section sequence and rejects null sections and duplicate IDs. The section set is fixed; rebuild the view explicitly if membership must change.
- Measure receives content width (viewport minus scrollbar allowance). Return finite, nonnegative height. Measurement must not draw or have side effects. Eight units of spacing follow each section.
- All section heights are measured initially and on width/language changes; only intersecting positive-height sections draw. Measurement is cached, but visibility traversal still visits every section, so this is not an O(visible) index.
- Draw receives a clipped local rect starting at (0,0). Keep content within measured height; balance all GUI groups. The host restores GUI state around each section.
- InvalidateLayout when conditional content, text, fonts or other non-width inputs change height. Focus requires an existing section ID, scrolls using recomputed geometry and highlights for four real-time seconds. Search IDs must match sections, or a custom callback must map them explicitly.
- Section virtualization can stop drawing offscreen text fields; own edit buffers and commit semantics in consumer state, not transient draws.

## Adapters, errors and lifecycle

- Manual adapters require a loaded Mod with nonempty `SettingsCategory()` and user opt-in. Explicit registrations take precedence. RegisterAdapterSearch alone does not enable hosting.
- Without metadata, fallback records search owner/page name and open the whole page. With metadata but no focus, setting results also open only the page. Custom adapters own scrolling, expansion and highlighting; none can be inferred from `DoSettingsWindowContents(Rect)`.
- Adapter metadata supports one original page, not automatic category splitting. Use explicit page registrations for real categories. The original dialog's lifecycle and mod-specific patches to it are not emulated.
- The router redirects only exact `Dialog_ModSettings` entries for opted-in owners. Custom subclasses and unrelated settings remain untouched.
- Save defaults to `owner.WriteSettings`. Leaving a different page, returning from a search hit, and normal close save; save errors are logged, surfaced and block navigation/close for retry. Same-page selection is a no-op. Forced window removal can only attempt saving, not veto removal.
- Drawing exceptions log and enter a retry view; `ExitGUIException` propagates as required by IMGUI. Save still runs after draw failure. Consumer-unbalanced IMGUI groups cannot be reliably repaired by the host.
- Focus errors are reported after destination selection; the destination remains selected. Fix the ID/callback rather than suppressing diagnostics.
- Title callback errors log once for that page object and fall back to owner name. Do not depend on callback evaluation order or frequency.

## Diagnosis and validation

| Symptom | Check / correction |
| --- | --- |
| Duplicate page exception | Move registration out of Draw; ensure unique stable page IDs. |
| Search provider cannot find page | Register page first; use same owner identity and exact page ID. |
| No setting-level results | Supply metadata; wait for incremental completion; confirm owner scope and adapter opt-in. |
| Stale translated/dynamic text | Use text callbacks; invalidate after non-language changes, never each frame. |
| Result opens without scrolling | Match metadata and anchor IDs; check focus installation and conditional visibility. |
| Anchor outside scroll error | Use RegisterListing or persistent MenuScrollView; section views use their own Focus. |
| High indexing frame time | Remove eager provider work and expensive callbacks; use lazy enumeration. |
| Long page still expensive | Listing draws everything; split into independent measured MenuSections. |
| Section clipped after expanding | Recompute height correctly and call InvalidateLayout. |
| Text entry resets | Keep numeric edit buffers and view objects in persistent instance fields. |
| Cannot leave/close | Inspect save exception and repair consumer WriteSettings/custom save. |
| Adapter behaves unlike original dialog | Check dependency on original dialog lifecycle/patches; opt out or explicitly integrate. |

Repository validation: `./scripts/validate.ps1 -SkipGpu` builds the API consumer, runs linked-production engine-double assertions, validates localization/XML and smoke-deploys with hashes. Full `./scripts/validate.ps1` additionally builds the shader with Unity 2022.3.35 and runs GPU/layout fixtures. Actual installed-game compile: `dotnet build Source/IrisMenus.csproj -c Release -p:RimWorldManagedDir=<game>/RimWorldWin64_Data/Managed` (DLL directory, not decompiled source directory).

Deployment: `./scripts/build-and-deploy.ps1 -SkipBuild -GameModPath <game>/Mods/IrisMenus` after successful build. It checks target package identity, copies runtime files plus both guides and verifies hashes. It does not edit ModsConfig, remove unrelated target files or hot-reload a running game.

Acceptance still requires real RimWorld: grouped/legacy/mixed pages, global and scoped searches, page/back restoration, missing anchors, resized/translated targets, long lazy pages, save/draw failures, manual/self-scrolling adapters, UI scales, and reopen/restart persistence. Engine doubles do not execute the real MenuWindow; GPU fixtures cover background/layout, not game UI interactions or FPS.
