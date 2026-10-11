# IrisMenus

Opt-in RimWorld 1.6 settings UI library. Requires Harmony; no dependency on RimIris.
Original settings entries for opted-in owners open IrisMenus. Unselected mods
and unrelated game settings remain untouched.

## Integration

See [guide.md](guide.md) for the Chinese integration walkthrough and
[guide_agents.md](guide_agents.md) for exact API contracts and troubleshooting.
Markdown help and documentation content is covered in
[docs/markdown.md](docs/markdown.md).
Pages are grouped by Mod; only the selected group expands. Existing single-page
registrations still open directly. Search supports global or owner-wide metadata
results, pagination, and explicit anchors without drawing unselected pages.

Reference `IrisMenus.dll` with `Private=false`, declare a mod dependency on
`Nanaloveyuki.IrisMenus`, and register on the game thread, usually in your Mod
constructor:

```csharp
MenuRegistry.RegisterListing(this, "general", () => "My Mod", DrawSettings);
```

`RegisterListing` receives `Action<Listing_Standard>` and manages scrolling
using measured content height. Use `MenuControls.Section`, `Checkbox`, `Slider`,
`Number` (int/float), and `Select` (dropdown) to build a page. Keep numeric edit
buffers in your Mod or page instance, not local variables created each frame.

For existing custom rendering, `MenuRegistry.Register` receives `Action<Rect>`.
That page owns its controls and scrolling; `MenuScrollView` is also available
for composing a custom page. Both APIs use the same optional save callback.

The page ID is stable and unique within the owning Mod class. Titles are
evaluated when drawn and may use `Translate()`. Drawing uses a clipped local
coordinate space. Balance all GUI groups and restore any GUI state you change.
Do not ship another copy of IrisMenus.dll inside your consumer mod.

The optional fifth argument is a save callback; the default is the owner's
`WriteSettings`. Saving runs when leaving a page and when closing the window,
even if drawing failed. Changes are live; closing does not cancel them. The host
does not implement a separate settings store for consumers. Call
`MenuRegistry.Open()` to open the shared window. Save failures keep the current
page/window open for retry through the game's `OnCloseRequest` contract.
The window pauses
the simulation, like the original mod settings dialog; Enter does not close it.

See `tests/ApiConsumer/ExampleMod.cs` for a compile-checked consumer.

## Manual Hosting

Users can select settings pages in the add-mod-settings view. Only loaded Mod
instances with a nonempty `SettingsCategory()` are listed. Selection persists
by package ID and full Mod type name; unloaded selections remain stored.
Explicitly registered owners take precedence over adapters.

The list includes aspect-fit ModIcons, with RimWorld's default icon for packages
without custom icons, and wraps long names without overlapping the checkbox.

### Language-specific Mod names

From 1.1.1, mods and translation packs can register names for themselves or
another package without changing `About.xml` or patching `Content.Name`:

```csharp
MenuRegistry.RegisterModNameAlias(this, "English", "My Mod");
MenuRegistry.RegisterModNameAlias("author.targetmod", "English", "Hunger and Havoc");
```

Use RimWorld language folder names (`English`, `ChineseSimplified`, etc.).
`ModContentPack` is also accepted. Same-package/language registrations are
last-wins and case-insensitive. Unregistered languages retain the original
name; explicit category titles and persistent IDs do not change. Navigation,
manual-hosting titles and search paths use the current alias; searches also
match the original name. `MenuRegistry.GetModDisplayName(Mod|ModContentPack)`
exposes the same resolution for consumers. See both guides for full contracts.

A narrow Harmony prefix on `WindowStack.Add` redirects an exact
`Dialog_ModSettings` instance only when its owner registered a page or the
user selected that owner for hosting. Existing IrisMenus windows are reused.
Unselecting the owner restores its original entry immediately; custom dialog
subclasses are not redirected. No mod's drawing method is patched.

Ordinary adapters call the original `DoSettingsWindowContents(Rect)` directly,
without an extra outer scroll view, and use the original `WriteSettings()`.
It does not emulate `Dialog_ModSettings`, its lifecycle, or mod-specific patches
to that dialog. Drawing failures are logged and the page exposes a retry action.
Unbalanced third-party IMGUI groups cannot be reliably recovered by the host.

Character Editor (`CharacterEditor.CharacterEditorMod`, Workshop 1874644848)
uses its drawing callback to open a separate configuration window and close
the vanilla settings dialog. Its hosted page therefore provides an **Open
original settings window** button instead of invoking that callback every
IMGUI event. Clicking opens the original window, or focuses it if already open;
closing it never automatically reopens it. Save changes with Character Editor's
own Save button. Existing manual selections and settings entry routing still
work; explicit page registrations retain precedence. No Character Editor
methods or private settings are patched or reflected.

## Build

```powershell
./scripts/build-and-deploy.ps1
./scripts/validate.ps1
./scripts/build-and-deploy.ps1 -SkipBuild -GameModPath 'D:\Appdata\Steam\steamapps\common\RimWorld\Mods\IrisMenus'
```

On WSL, build against the installed game's managed assemblies and deploy to
`E:\Apps\Steam\steamapps\common\RimWorld\Mods\IrisMenus`:

```bash
./scripts/deploy.sh
./scripts/deploy.sh --build-only
RIMWORLD_DIR='/mnt/d/Steam/steamapps/common/RimWorld' ./scripts/deploy.sh
```

`CONFIGURATION` defaults to `Release` (`Debug` is also accepted). `RIMWORLD_DIR`
overrides the default game directory; `RimWorldDir` is accepted as a fallback.
The script reuses the checked-in shader bundle without running Unity, refuses
deployment while RimWorld is running, validates the complete file manifest and
target package identity before copying, rejects destination symlink redirects,
and verifies SHA-256 hashes. It deploys only IrisMenus runtime files, README,
both guides, and `docs/`; Harmony and game assemblies are never copied.

Deployment is opt-in. It creates a new target or verifies an existing target's
package ID, copies runtime package files, both integration guides, and `docs/`,
then verifies each file hash.
It never edits ModsConfig or removes other files.

Shader builds need Unity 2022.3.35 on Windows with D3D11. The script discovers
that editor in Unity Hub's local registry, or accepts `-UnityPath`. It builds
the included shader source and runs GPU fixtures against the reloaded bundle.
No RimIris source or AssetBundle is distributed. Build products include
`1.6/Assemblies/IrisMenus.dll` and `1.6/AssetBundles/irismenus_frost`.

For DLL-only iteration use `dotnet build Source/IrisMenus.csproj -c Release`.
Pass `-p:RimWorldManagedDir=...` to compile against an installed game's actual
Managed directory instead of NuGet reference assemblies. `validate.ps1
-SkipGpu` runs the API/engine-double/package checks using the existing bundle.

## Background

The private `IWindowDrawing` implementation affects only IrisMenus. A command
buffer on the active map/world camera captures its output after rendering;
top-origin backbuffer captures are normalized before blurring. Two half-resolution
targets and a separable Gaussian shader generate the background. The blur scratch
target is reused for the window crop; the background, tint and border share the
upstream's 12-unit rounded corners, including when frost is disabled. Strength
changes reuse textures; resizing or changing cameras rebinds them. The footer
provides live on/off, blur, transparency and remember-state controls.
Closing detaches the command buffer, unsubscribes camera callbacks,
and releases textures and material. The shader is borrowed from this mod's
`Content.assetBundles.loadedAssetBundles`; RimWorld owns its loading/unloading.
There is no duplicate bundle load and no CPU pixel readback in
the runtime path.

Outside a rendered game scene (including the main menu), with no eligible
camera, or after a shader-load failure, the window uses an unblurred translucent
background. Transparency works independently of the frost switch; zero
transparency uses an opaque rounded background.
Turning the background off and on retries loading without restarting the game.
**Remember last page** is enabled by default in the window footer and the original
IrisMenus settings dialog. It restores the page, global/owner search, result page,
management filter, host scroll positions and registered Listing-page scrolling
across reopen/restart. Explicitly opening a Mod's settings takes precedence over
the saved view. Missing pages fall back to an available category of the same Mod,
or the global catalog. Disabling memory clears host navigation state, not Mod
settings, adapter selections or window geometry. Custom pages/adapters remain
responsible for their own internal UI state. The original IrisMenus settings
dialog can reset window geometry.

The original IrisMenus settings dialog includes **Show IrisMenus button in the
bottom bar**, enabled by default. Toggling it immediately hides/restores the
bottom button and persists across restarts. When hidden, use **Options → Mod
settings → IrisMenus → Open IrisMenus**; registered/hosted Mod entry routing is
unaffected.

## Release notes

### Unreleased

- Add a default-on, persistent bottom-bar IrisMenus button visibility setting
  shared by the original Mod settings dialog and the IrisMenus window footer;
  changes apply and save immediately, and the settings-page launcher remains available.
- Place the bottom-bar button visibility toggle to the right of the appearance
  controls, aligned with the frosted-background toggle; keep a separate lower
  row when the window or translated label cannot fit.
- Reduce the footer from 120 to 72 UI units when its controls fit on two rows,
  returning 48 units to settings and navigation; narrow windows and long
  translations retain the space required for wrapped controls.
- Fix the startup `MenuActionOwner` constructor error by storing action-only
  launcher content directly instead of creating an automatically discovered Mod subclass.
- Add black-on-white line-art Preview and ModIcon PNGs. Regenerate with
  `uv run --no-project --with pillow scripts/generate-about-images.py`.

### 1.1.1

- Add language-specific Mod name aliases, including translation-pack registration
  for other package IDs, and a shared display-name lookup.
- Show cached ModIcons in the add-mod-settings list; reserve icon/checkbox space
  when measuring wrapped names. Search matches localized and original names.
- Publish atomic dynamic SubItem listing replacement and per-content Mod actions,
  including action-only launcher pages.


## Current Verification Boundary

The implementation provides the two-column window (single-pane navigation on
narrow windows), registered pages, manual adapters, resizing, geometry/page
persistence, frosted background, and English/Chinese labels.

`tests/RuntimeChecks` links unchanged registry/session/scroll/settings/background
production files against engine doubles, including persistence contracts and
camera callback lifetime and ownership of game-preloaded bundles. The entry
router is exercised with real Harmony against a doubled window stack.
Unity fixtures compile the actual GPU/layout files,
reload the built bundle, check pixel variance/orientation, render a camera,
verify texture reuse/resize/release, and check layout containment. PNG outputs
and the GPU result are written under ignored `tmp/`.

Compile checks are not in-game acceptance. Before release, verify native and
adapted pages, original settings entry points, save/reopen/restart, draw/save
failure paths, long translated labels, scrolling, and UI scale/resolution
changes in RimWorld. Check the manual-hosting path with both ordinary and
self-scrolling mod settings pages.
