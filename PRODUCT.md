# Product
<!-- impeccable:product-schema 1 -->

## Platform
Windows 11 native desktop, explicitly requested by the user. The skill's web/iOS/Android platform catalog does not describe this target.

## Stack
C# and WinUI 3, chosen by the user. Direct code implementation, chosen by the user.

## Users
The owner of an ATK ZERO EX who wants independent color control on Windows 11.

## Product Purpose
Select and apply one of seven physically verified colors, or arrange those colors into a looping gradient sequence. Restore the settings captured before the session. Preserve DPI values and polling rate.

## Tabs
Two fixed WinUI tabs: 灯光控制 is the first and default tab, containing the existing controls. 自定义 is the second, with drag-to-add palette colors, reordering, per-step hold/fade times, looping playback including last-to-first interpolation, offline preview, pause, stop/restore, and local draft persistence. Their headers each occupy half of the available tab strip, including after resizing; do not leave a blank region to their right. Switching tabs retains the current controls and session; it sends no device commands. Tabs cannot be added, closed, dragged or reordered.

## Mandatory Sleep Behavior
The user explicitly requires the LED to be off while the mouse sleeps. This is a release acceptance requirement. Preserve the existing mouse sleep parameters. During explicitly started custom playback, send colors only while this mouse has recent movement/button activity, at most once every 450 ms. After about 2 seconds without that activity, freeze sequence time and stop all automatic HID commands. Resume on physical mouse activity. Use passive Windows Raw Input, not HID polling, to detect activity. No background battery polling, keepalive, or automatic reconnect. Read battery only during an explicit connection. Firmware-managed sleep extinction must be physically verified; accepting RGB configuration alone is not evidence that this requirement works.

## Capabilities and Constraints
Official mouse firmware 3.01; VID 373B, wired PID 11CC and NANO receiver PID 11FE. Vendor HID report 8, 17 bytes. Wireless display verified for cyan, red, green, blue, magenta, yellow, white and interpolated intermediate colors. Custom hardware playback requires the NANO wireless connection; charging indication can override wired display. Idle wireless communication can time out. Save a backup before writes, keep a recovery record, and verify restoration. No firmware flashing or automatic color changes on launch. Preview and editing do not access hardware. Playback currently uses the verified configuration-write channel; a dedicated volatile/RAM animation channel and long-duration write endurance have not been established.

## Evidence on Hand
The owner confirmed all seven colors on the actual mouse. Protocol traces, checksum validation and exact 96-byte configuration restoration are recorded in ../../outputs/ATK-ZERO-EX-七色验证结果.json.

## Product Principles
- Make application and restoration explicit.
- Show actual connection and operation outcomes.
- Keep a recoverable baseline across restarts.
- Use native Windows controls and keyboard interaction.

## Exit Policy
The user now explicitly requires the applied light to stay on after the program exits, while retaining the original mandatory sleep-off behavior. From version0.2.2, default normal exit stops host-driven playback and retains the last applied static color; it does not restore or turn off the light. The restore-on-exit checkbox defaults unchecked; explicit manual restore and opt-in restore-on-exit remain available. Firmware still turns the LED off at the existing idle timeout. A local RetainedOnExit marker preserves original recovery data and distinguishes intentional retention from an interrupted operation on the next connection. A custom loop cannot continue without the host application.

## Application Icon
The user requested an original software icon and its integration. Use an independently drawn mouse silhouette with a white wheel and cyan–blue–magenta outline suggesting the light cycle. This identity asset is confined to the application icon; the main interface keeps its native Windows theme. Supply a transparent multi-resolution ICO for the executable, native window and taskbar, with the editable SVG source.
