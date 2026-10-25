---
name: "ZERO EX · 灯光控制"
description: "Native Windows 11 control of seven verified ATK ZERO EX colors and user-arranged looping gradients."
colors:
  device-cyan: "#00FFFF"
  device-red: "#FF0000"
  device-green: "#00FF00"
  device-blue: "#0000FF"
  device-magenta: "#FF00FF"
  device-yellow: "#FFFF00"
  device-white: "#FFFFFF"
typography:
  connection-status:
    fontWeight: 600
  guidance:
    fontSize: "12px"
rounded:
  swatch: "5px"
  compact-swatch: "4px"
  sequence: "6px"
spacing:
  tight: "4px"
  header: "6px"
  related: "8px"
  group-internal: "12px"
  section: "24px"
  page: "28px"
components:
  content-surface:
    padding: "24px 28px 28px"
  palette-item:
    width: "116px"
    padding: "8px 10px"
  palette-swatch:
    rounded: "{rounded.swatch}"
    height: "36px"
  compact-palette-swatch:
    rounded: "{rounded.compact-swatch}"
    height: "22px"
  sequence-container:
    rounded: "{rounded.sequence}"
  guidance:
    typography: "{typography.guidance}"
  connection-status:
    typography: "{typography.connection-status}"
  backup-link:
    padding: "6px 0"
---

# Design System: ZERO EX · 灯光控制

## Overview

**Creative North Star: "Native Windows 11 Fluent"**

The user chose C# and WinUI 3 for a focused operating utility. Windows supplies its visual authority: ordinary solid surfaces, native controls, the system accent, native keyboard feedback, and the operating system's light or dark theme. This captures the reviewed implementation and merges the provisional design notes; it does not establish a separate brand identity.

Use a clear, moderately spaced control surface that helps the owner connect, apply a color or arrange a looping sequence, and restore the saved settings. Strong saturated color belongs inside device-color and playback previews. State text and native status components explain connection, verification, and recovery without simulated telemetry. Functional gradients belong to explicitly started light playback and its offline preview. Sleep behavior and recovery acceptance remain defined by PRODUCT.md.

**Key Characteristics:**

- Native WinUI controls and theme resources.
- Seven named, exact device-output swatches.
- Explicit Apply, playback, and Restore actions with visible operation status.
- Equal full-strip tabs, scrolling editors, and a pinned custom-playback footer.
- Readable Chinese labels and standard keyboard and accessibility semantics.

The frontmatter owns portable token primitives. Its pixel values are the numeric equivalent of WinUI device-independent pixels (DIP), not physical capture pixels. Native theme resources, inherited typography, minimum dimensions, and state semantics are normative in `.impeccable/design.json`; the CSS-oriented frontmatter cannot represent them faithfully.

## Colors

The application inherits Windows' neutral surfaces and accent, while the palette shows the exact device-output choices from `ColorChoice`.

### Primary

The application action accent comes from `AccentButtonStyle`. Selection, focus, checkbox, and hyperlink feedback remain native WinUI styles. Resolve them through `XamlControlsResources`; do not replace the user's accent with one of the LED colors.

### Neutral

- `ApplicationPageBackgroundThemeBrush` supplies the content background.
- `TextFillColorSecondaryBrush` supplies device metadata, selection details, battery-at-connection text, and guidance.
- `ControlStrokeColorDefaultBrush` supplies swatch, sequence-container, and footer-separator outlines, including a visible edge around the white option.
- `ControlFillColorDefaultBrush` supplies the native sequence surface and the playback preview's initial fill.
- Primary text and other control surfaces use inherited WinUI resources.

Light and dark values are intentionally not frozen here. The native resource key is the source of truth, including any OS theme or accessibility adaptation.

### Device-output palette

Use the seven `colors.device-*` tokens for physical color choices, sequence steps, and corresponding output values. Display labels remain 青色, 红色, 绿色, 蓝色, 紫色, 黄色, and 白色, in that order. The 紫色 option uses the magenta token. The selected static color's name and hex value appear in text as well as in its visual swatch. Custom playback interpolates between these chosen endpoints, including the last-to-first transition; its preview swatch may therefore display intermediate RGB values.

**The Native Theme Rule.** Preserve resource-based surfaces, text, accent, selection, and focus instead of copying sampled screenshot colors.

**The Device Color Rule.** Preserve the seven exact selectable endpoint colors. Intermediate colors are functional sequence interpolation and preview, not an application-brand tonal ramp or decorative UI gradient. The independently authored application icon is a scoped identity-asset exception described in Components; its contour colors do not become interface tokens.

## Typography

**Display and Body Font:** inherit the WinUI system font and language fallback through `XamlControlsResources`. The provisional Segoe UI Variable direction is implemented through native typography, without a custom font-family override.

The hierarchy uses native `TitleTextBlockStyle` for 鼠标灯光 and 自定义, `SubtitleTextBlockStyle` for 选择颜色, 颜色库, and 播放序列, and `BodyTextBlockStyle` for ATK ZERO EX. Other labels and control text inherit their native styles. Do not translate these styles into fixed custom font sizes. Connection and playback status are explicitly semibold; operational guidance, timing summaries, draft-save text, and playback detail reuse the smaller guidance role in frontmatter.

Metadata and guidance use secondary text color. Device detail and guidance paragraphs wrap. Keep native language fallback and system text scaling; the Chinese text is content, never raster artwork.

## Layout

The theme-backed root Grid contains two fixed native tabs: 灯光控制 first and selected by default, 自定义 second. Both headers fill the available tab strip equally, with no blank region to the right. `TabWidthMode="Equal"` is paired with the App.xaml resource override `TabViewItemMaxWidth` so the native default width cap cannot prevent the user-requested half-width headers. Add, close, drag, and reorder affordances remain disabled. The TabView has a group-internal top/left/right margin and no bottom margin.

The lighting tab retains a vertical `ScrollViewer` with automatic vertical scrolling and disabled horizontal scrolling. Its content stack uses `content-surface` padding and `spacing.section` between groups. Internal groups use `spacing.group-internal`, the header uses `spacing.header`, the connection title/detail pair uses `spacing.tight`, and swatch content and the final guidance group use `spacing.related`.

The custom tab has a flexible scrolling editor above an automatic-height pinned playback footer. Connection, color library, sequence, and timing controls scroll; status, playback preview, Start/Preview, Pause/Stop, and the concise loop/sleep reminder remain in the footer. Keep the footer outside the editor ScrollViewer. Its two-column action grid keeps Start and Preview in the first row and Pause and Stop/Restore in the second. Native wrapping and vertical scrolling handle narrow or short windows.

The initial outer window is sized to 760 × 830 DIP, capped to the current working area with a 60-physical-pixel allowance. The presenter enforces a DPI-aware minimum outer size of 460 × 460 DIP and refreshes that constraint when the XAML root changes. This is a window constraint, not a viewport breakpoint or a fixed content height.

Connection metadata and the transport row each use a two-column grid: a flexible left column, an automatic right column, and group-internal column spacing. The transport field stretches while Connect aligns to the bottom. The battery label is a connection-time observation.

The color `GridView` uses horizontal wrapping with at most four items per row. Its item cells are 148 × 100 DIP. A cell contains the frontmatter `palette-item` stack, a swatch, and a centered text label. At the reviewed narrow width the items wrap to two columns. Keep the native item container's selection and keyboard behavior.

Apply and Restore occupy one horizontal row with group-internal spacing. Each has minimum dimensions of 166 × 44 DIP; Connect has a minimum height of 40 DIP. The minimum window width protects both action labels. Short windows scroll vertically to recovery status and the backup link.

**The Equal Tabs Rule.** Each of the two fixed headers occupies half of the available strip at every supported window width; equal sizing must not stop at the native per-tab width cap.

## Elevation & Depth

The content uses a flat native page surface. No custom card shadows, blur, overlays, or elevation tokens are defined. Borders and standard native control states establish structure. The native window title bar, content dialog, and OS control templates own their platform depth treatment.

## Shapes

The lighting and playback LED swatches are softly rounded rectangles using `rounded.swatch`, with a one-DIP native control-stroke outline. The compact source-palette and sequence swatches use `rounded.compact-swatch`; the sequence container uses `rounded.sequence`. Other radii, borders, checkmarks, selection frames, and focus treatments belong to native WinUI templates. Do not generalize a swatch radius into a replacement radius for all controls.

## Components

### Application icon

Version 0.2.1 adds an original upright mouse silhouette with a dark body, white wheel, split-button lines, and a continuous cyan–blue–magenta perimeter on a transparent exterior. This identity asset identifies the executable and native window/title bar/taskbar. The colored contour is confined to that asset; the application interface retains its Windows theme, typography, controls, and seven-color choices. Do not extend the icon palette or its gradient to general UI styling.

The editable source is `Assets/ZeroExLight.svg`; `tools/create_icon.py` renders the transparent `Assets/ZeroExLight.png` master and nine-frame `Assets/ZeroExLight.ico`. The ICO contains 16, 20, 24, 32, 40, 48, 64, 128, and 256-pixel frames; the PNG master is 512 pixels square. `Assets/ZeroExLight.provenance.json` records independently authored geometry and no external artwork. Asset-local colors and integration details live in `extensions.applicationIcon` in the sidecar, outside the general UI token palette.

The project embeds the ICO through `ApplicationIcon`, copies it to output/publish, and sets the native window icon through `AppWindow.SetIcon`. `.impeccable/review/icon-finish-review.md` records ship with no material fixes. Its evidence includes literal native-size samples on light/dark grounds, the actual window title bar, and executable/window icon checks; a separate taskbar capture was not supplied.

### Buttons

Apply and custom Start use `AccentButtonStyle`; Restore, Connect, Preview, Pause, and Stop use the ordinary native button style. Preserve minimum sizes rather than fixing their height. Hover, pressed, disabled, and keyboard focus states come from WinUI. The backup action is a left-aligned `HyperlinkButton` using frontmatter padding.

Apply becomes available after a successful connection with a selected color, while no operation or playback is running. Restore becomes available when settings have changed and no operation is busy; it stops playback before restoration. Busy operations disable competing actions. Playback locks sequence editing, connection, static Apply, and transport changes. Transport is also disabled while restoration is pending. Selection alone changes the preview text; Apply performs the static device write.

### Transport field

A native `ComboBox` is headed and accessibly named 连接方式. It contains 2.4G 无线接收器 and USB 有线, with wireless selected initially. Keep the native dropdown, focus, keyboard selection, and disabled behavior.

### Color selector

A single-selection native `GridView`, accessibly named 七种已验证的颜色, contains seven text-labeled `ColorChoice` records. `ColorChoice.ToString()` returns the color name; each visible label stays alongside its swatch. The selected value is restated as 已选择 {name} · {hex}. The first item is selected initially, while Apply remains disabled until connected. Do not communicate choice by hue alone.

### Custom sequence editor and playback

Reuse the native seven-color `GridView` as a compact, drag-enabled library. Dragging copies colors into the ordered native `ListView`; repeated colors remain independent steps. An Add Selected Color button and Up/Down/Remove/Clear buttons provide explicit alternatives to pointer dragging. Reordering uses the native list interaction. Each row has an order number, swatch, name, and timing summary. Native `NumberBox` controls edit selected-step hold and fade-to-next durations within 0–30 seconds; a sequence contains at most 32 steps and a valid loop lasts at least 0.45 seconds. The last step fades back to the first. Draft edits auto-save locally and load on the next ordinary launch; editing and offline preview do not access hardware.

Start requires a valid sequence and the shared wireless connection. Offline Preview requires no connection. Pause/Continue preserves timeline position; Stop ends preview, or stops hardware playback and restores original settings. The playback swatch and current/next step/hex detail show the sequence sample, not live optical telemetry. Hardware frame read-back verifies configuration, not measured light output. Device writes are spaced at least 450 ms apart; the more frequent UI timer is not a hardware frame rate and must not be described as 60 fps.

**The Activity Gate Rule.** Explicitly started hardware playback uses passive Raw Input for this mouse's actual movement/button activity. About two seconds without activity freezes sequence time and stops automatic HID commands; physical activity resumes it. Preserve firmware sleep settings, avoid HID polling and keepalive, and retain explicit Stop/Restore.

### Status and recovery

The lighting tab's native `InfoBar` remains permanently open and nonclosable. The custom tab mirrors operation status through a closable `CustomStatus` InfoBar in the pinned footer, initially closed. Both carry a title, readable message, and semantic severity. Use Informational for instructions and work in progress, Success for operation results, and Warning for errors or pending recovery. Playback-start status indicates the controller started, not measured device telemetry. Native glyphs and text support the color semantics. Do not replace a verification result with a merely optimistic success state.

A native `CheckBox` controls restoration on exit and starts unchecked. Normal exit stops and awaits host playback, then retains the last applied color; the loop cannot continue after the application closes. Adjacent wrapping secondary guidance explains default retention and changes when restoration is explicitly selected. The custom footer mirrors that choice in `CustomExitBehaviorHint`, while continuing to state that the mouse follows its original sleep-off settings. Manual Restore and the explicit Stop/Restore playback action remain available; they are distinct from closing the application.

Intentional retention saves a local `RetainedOnExit` marker without issuing a hardware restore or off command. It preserves the original backup and recovery data so the next connection can distinguish retained settings from an interrupted operation. A new write or manual restoration clears that marker before hardware mutation. Opting into restore-on-exit restores the original configuration, including an originally disabled light. A failed opt-in restoration keeps the native `ContentDialog` path: 返回恢复 is the default close action and 保留当前设置退出 is the explicit alternative. If the local retention record cannot be saved, the corresponding dialog defaults to 返回 and also offers 保留当前设置退出. Preserve the visible choice, readable guidance, and recoverable baseline.

Operational guidance explains that idle communication does not query or keep the mouse awake, sleep settings remain inherited, battery is read during connection, and the mouse may need movement before Apply or Restore. The custom tab also explains the activity-gated loop. Keep operational guidance wrapped and reachable, with playback controls pinned. The UI must not infer sleep-off success from an accepted RGB configuration; acceptance evidence remains in the product/test records.

## Do's and Don'ts

### Do:

- **Do** inherit Windows themes, system accent, native focus states, and language fallback.
- **Do** preserve all seven named swatches and the selected name/hex text.
- **Do** keep Apply and Restore explicit, readable, and reachable at the minimum window size.
- **Do** show actual connection, backup, verification, and recovery outcomes in text.
- **Do** retain vertical scrolling and wrapping guidance when space is constrained.
- **Do** keep both tab headers half-width and the custom playback/recovery controls pinned while its editor scrolls.
- **Do** distinguish user-arranged light gradients and offline preview from decorative interface motion and measured device telemetry.

### Don't:

- **Don't** turn device-output colors into general interface branding or replace native theme resources with sampled hex values; the original application icon is the sole scoped identity-asset exception.
- **Don't** add decorative images or decorative gradients inside the interface, ornamental dashboard cards, or motion competing with the functional sequence preview; retain the original icon in its native application-identity role.
- **Don't** present simulated battery, device details, or sleep verification as observed hardware state.
- **Don't** add HID polling, keepalive, automatic playback on launch, or playback writes after the mouse activity gate closes.
- **Don't** remove native labels, selection feedback, keyboard operation, or the visible recovery path.
