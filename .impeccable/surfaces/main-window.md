# Main window

Authority: the user's native Windows 11 WinUI 3 choice, implemented directly in C#. Version 0.2.0 extends the incumbent Fluent utility with an ordered light loop and explicit restoration. It introduces no new visual identity.

## Composition

The theme-backed Root Grid contains a fixed native TabView named Pages, accessibly named 功能页签. 灯光控制 is first and selected by default; 自定义 is second. Each header fills half of the available strip, including after resizing. TabWidthMode=Equal is supported by App.xaml's TabViewItemMaxWidth=10000 override, removing the native width cap that previously left a trailing blank region. The TabView retains its 12-DIP top/left/right margin. Tabs cannot be added, closed, dragged, or reordered.

The 灯光控制 tab retains the existing LightingContent: 鼠标灯光 / ATK ZERO EX, shared connection and transport, seven color choices, selected-name/hex text, Apply and Restore, the nonclosable Status InfoBar, restoration preference, guidance, and backup-folder link. It remains vertically scrollable.

The 自定义 tab now presents 自定义 and 拖入颜色，按你的顺序循环渐变。, the shared connection row, 颜色库, 播放序列, timing controls, and a pinned playback area. Its story is palette → sequence → time → preview/start → pause/restore. The initial viewport presents half-width tabs, purpose and connection, palette and sequence entry, and the protected footer. Editing content scrolls above that footer.

## Custom editor

- A compact native GridView supplies the same seven named endpoint colors. Its cells are 82 × 70 DIP, with a 62-DIP content stack, 22-DIP swatch height, and the compact swatch radius. It wraps naturally with at most seven columns.
- Dragging a library color copies it into the sequence at the drop position. 添加所选颜色 appends the selection. Repeated colors have independent step identities.
- The native single-selection ListView permits reordering and provides Up/Down/Remove/Clear alternatives. Rows show order, a 32-DIP swatch, name, and a smaller timing summary. The bordered sequence area uses a native control fill; the list is bounded to 112–220 DIP in height.
- The empty state reads 将颜色拖到这里. The summary gives count out of 32, total loop duration, and last-to-first continuity; the last step actually fades back to the first.
- Two native NumberBoxes edit selected-step hold and fade-to-next seconds, each bounded to 0–30 with 0.5-second step buttons. The loop must total at least 0.45 seconds. A new step starts with 1-second hold and 3-second fade.
- Ordinary launch loads the locally saved draft from Presets/custom-sequence.json beneath the application directory or starts empty. Order, repeated steps, and timings auto-save. Editing and offline preview perform no device access. Capture-only sample data is not normal-launch content.

## Pinned playback area

A two-row Grid places CustomScroll in its flexible top row and the footer in its automatic-height bottom row. The footer has a one-DIP top separator, native page background, and 28/12/28/20-DIP left/top/right/bottom padding. It remains visible while editing content scrolls. No custom shadow or floating overlay is introduced.

The footer contains a closable CustomStatus InfoBar, a 44-DIP preview swatch and state/detail text, a two-column action grid, and the loop/sleep reminder. Start and Preview occupy the first action row with minimum height 44 DIP; Pause and Stop/Restore occupy the second with minimum height 40 DIP. The action grid uses 12-DIP column and 8-DIP row spacing. CustomStatus starts closed and mirrors operation results; the lighting page's Status remains open and nonclosable.

## States

| State | Visible behavior |
| --- | --- |
| Initial lighting | Wireless transport and cyan selected, disconnected, Apply/Restore disabled, restore-on-exit unchecked. |
| Empty custom | Drag/add guidance, disabled invalid-sequence actions, waiting-for-sequence preview. |
| Edited custom | Ordered steps, selected timing values, duration/count, local save status; valid offline preview available. |
| Connecting | Actual shared connection/backup operation; competing actions disabled. |
| Offline preview | Interface-only interpolated sample and step/hex text; no connection required and no device writes. |
| Hardware playback | Requires the shared 2.4G connection and valid loop; explicit Start only. Editor, connection, transport, and static Apply are locked. |
| Manual pause | 暂停 becomes 继续; timeline position is preserved without playback writes. |
| Mouse idle | After about two seconds without this mouse's actual movement/button input, 静置暂停 · 等待鼠标活动; timeline freezes and automatic HID commands stop. Physical activity resumes it. |
| Stop | Preview stops without changing hardware; hardware playback stops and restores original settings. The sequence draft stays editable. |
| Default exit | Stops and awaits host playback, retains the last applied color, and saves intentional retention locally. No restore/off command is added by the retention path. The loop ends with the application. |
| Opt-in exit restoration | Checking the native exit preference changes the adjacent explanation and custom footer hint; closing restores the original settings, which may have originally disabled the light. |
| Tab switching | Existing controls, session, and running/paused sequence are retained; switching itself starts no hardware operation. |
| Error/recovery | Warning and recoverable baseline are retained. The explicit default 返回恢复 and alternative 保留当前设置退出 remain available after failed exit restoration. |

Version 0.2.2 makes retention the normal exit choice. ExitBehaviorDescription explains that the last color remains and the host-driven loop stops; selecting restoration replaces that copy with the original-configuration consequence. The pinned CustomExitBehaviorHint similarly switches between 退出保留最后颜色。 and 退出将恢复原灯光。, followed by 静置后仍按原设置休眠熄灯。. This uses existing secondary text styling and wrapping, with no new layout or palette. Intentional retention preserves the original snapshot and modified-address list through RetainedOnExit; the next connection treats it differently from a failed operation. A new write or restoration clears the marker before mutation. A failed local record save has a native dialog with 返回 as the default and 保留当前设置退出 as the alternative. Manual Restore and the playback Stop/Restore command retain their existing behavior.

The playback swatch and 第 current → next 项/hex detail represent sequence samples, not measured light telemetry. Read-back verifies written configuration. The UI timer is 50 ms while playing and 200 ms while paused; hardware updates are rate-limited to at least 450 ms apart and stop behind the passive Raw Input activity gate. This is not a 60-fps device channel. Battery is still read only on explicit connection. Sleep settings remain firmware-owned, with no HID polling or keepalive; hardware sleep-off remains a separate physical acceptance requirement.

## Width and height behavior

The initial outer size remains 760 × 830 DIP subject to the working-area cap. The DPI-aware minimum is now 460 × 460 DIP, refreshed on XamlRoot changes. Each tab header continues to fill half of the supported width. The lighting palette wraps to two columns at the minimum outer width; the more compact custom palette wraps independently. Both pages disable horizontal scrolling. The custom editor and bounded sequence list scroll as needed, while its playback/recovery footer remains pinned. The bottom-state capture intentionally shows the editor scrolled to reorder/timing controls; it is not an initial-viewport capture.

## Reviewed evidence

- Version 0.2.2 exit-policy captures: `.impeccable/review/exit-default-wide.png` and `exit-default-narrow.png` show scrolled settings viewports with the checkbox unchecked and the explanation visible. They are not full-page or document-bottom captures. `exit-restore-opt-in.png` shows the checked state and changed explanation; `exit-custom.png` shows the custom tab's pinned exit/sleep hint. `exit-policy-review.md` records ship with no material fixes for this bounded refinement.
- Exit behavior evidence: `retained-exit-device.json` records success, defaultRestoreOnExit=false, retainedRecord=true, and 60 commands both before and after the real native closing handler. `retained-exit-probe.json` confirms the 96-byte configuration after process exit exactly matches the expected retained configuration. The user physically confirmed that cyan remained after exit and normal mouse movement. `exit-custom-ui.json` reports all 11 existing custom checks passing with no hardware connection or modification. These findings retain the existing mandatory sleep-off requirement and do not claim a host-driven loop continues after exit.
- Version 0.2.1 adds the original `Assets/ZeroExLight.ico` as the executable/native window identity, with no page-layout or control-style change. `Assets/ZeroExLight.svg` is the editable mouse/colored-perimeter source and `Assets/ZeroExLight.png` is the transparent 512-pixel master. The asset-only exception is documented in root DESIGN.md; its colors are not general UI tokens.
- Icon evidence: `.impeccable/review/icon-sizes.png` shows literal 16/20/24/32/40/48/64-pixel samples on light and dark grounds; `icon-native-window.png` is the actual 1520 × 1660-pixel application-window capture with the icon visible in the title bar. `icon-native-check.json` reports the executable icon, both small/large window icons at 64 pixels for the current DPI, and hardwareAccess=false. `icon-finish-review.md` records ship with no material fixes. A separate taskbar capture was not supplied; taskbar identity is the intended integration role, not a separately captured verification.
- Source checked: MainWindow.xaml, MainWindow.xaml.cs, MainWindow.Custom.cs, App.xaml, PlaybackController.cs, and the ColorChoice definition. PRODUCT.md already records the current feature and sleep constraints.
- Current native viewport captures: .impeccable/review/custom-empty.png, custom-editor.png, custom-editor-narrow.png, custom-editor-bottom.png, and tabs-lighting.png. Normal views are 1494 × 1589 physical pixels; the narrow custom view is 894 × 1589. The populated capture uses diagnostic sample data.
- Final review: .impeccable/review/custom-final-review.md records ship with no material fixes and confirms the full-strip half-width tabs across all five captures. The earlier custom-finish-review.md predates the tab-width correction and is not the final verdict.
- Saved verification reports: custom-self-test.json passes 25 logic/protocol checks; custom-ui-test.json passes 11 native-control/DataPackage checks with hardwareConnected=false and hardwareModified=false.
- custom-device-idle-test.json records success, 60 verified frames, at least three seconds of idle command silence, exact original 96-byte restoration, and pendingRecovery=false. The user also confirmed a visible native-device loop. These reports do not independently measure LED output or prove sleep extinction on every firmware/device state.

The captures establish visible native layout, empty/selected/editor/bottom states, and pinned footer composition. Source and saved reports support playback/error paths; they are not new screenshots of those states. Dark/high-contrast themes, keyboard traversal, screen-reader output, and native drag pointer gestures remain unexercised evidence limits. The DataPackage insertion test is not a claim that a live pointer drag was exercised. The documentation pass used no hardware or UI automation and made no source changes. Earlier sleep-off acceptance remains limited to the physically tested wireless firmware 3.01 case; a visual review and configuration read-back do not extend that evidence.
