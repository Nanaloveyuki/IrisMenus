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

A narrow Harmony prefix on `WindowStack.Add` redirects an exact
`Dialog_ModSettings` instance only when its owner registered a page or the
user selected that owner for hosting. Existing IrisMenus windows are reused.
Unselecting the owner restores its original entry immediately; custom dialog
subclasses are not redirected. No mod's drawing method is patched.

The adapter calls the original `DoSettingsWindowContents(Rect)` directly,
without an extra outer scroll view, and uses the original `WriteSettings()`.
It does not emulate `Dialog_ModSettings`, its lifecycle, or mod-specific patches
to that dialog. Drawing failures are logged and the page exposes a retry action.
Unbalanced third-party IMGUI groups cannot be reliably recovered by the host.

## Build

```powershell
./scripts/build-and-deploy.ps1
./scripts/validate.ps1
./scripts/build-and-deploy.ps1 -SkipBuild -GameModPath 'D:\Appdata\Steam\steamapps\common\RimWorld\Mods\IrisMenus'
```

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
two half-resolution targets and a separable Gaussian shader generate the
background. Strength changes reuse textures; resizing or changing cameras
rebinds them. The window's footer provides live on/off, blur and transparency
controls. Closing detaches the command buffer, unsubscribes camera callbacks,
and releases textures and material. The shader is borrowed from this mod's
`Content.assetBundles.loadedAssetBundles`; RimWorld owns its loading/unloading.
There is no duplicate bundle load and no CPU pixel readback in
the runtime path.

Outside a rendered game scene (including the main menu), with no eligible
camera, or after a shader-load failure, the window uses an unblurred translucent
background. Transparency works independently of the frost switch; zero
transparency restores the opaque native background.
Turning the background off and on retries loading without restarting the game.
Window geometry and the last selected page persist; the IrisMenus entry in
the original mod settings dialog can reset window geometry.

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
