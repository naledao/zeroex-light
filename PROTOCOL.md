# Verified protocol scope

Device: ATK ZERO EX, firmware 3.01, CID/MID 1/4. VID 373B, wired PID 11CC, NANO PID 11FE. Windows HID usage page FF02 / usage 2, 17-byte input/output reports, report ID 08. The mouse is addressed through the NANO receiver in wireless mode.

The 16-byte payload starts after the report ID. Payload[0] is command; [1] is status; [2:4] EEPROM address, big-endian; [4] data length; [5:13] up to eight bytes; [15] checksum. The sum of all 17 frame bytes modulo 256 is 55 hex.

Allowed commands: 04 battery, 07 configuration write, 08 configuration read, 0E profile, 10 CID/MID, 12 version. Writes are bounded to eight bytes at decimal addresses 44, 52, 60, 68, 76. The application never emits firmware-upgrade commands.

Color slots occupy 44..75, four bytes each: R,G,B,(55-R-G-B)&FF. The active slot index is configuration[4]. Write its 8-byte pair while preserving the adjacent slot. Effects 76..83: mode 01/54 = steady, enabled 01/54 at offset6. Original brightness/speed bytes are retained. Preserve bytes0..43 and never write sleep configuration.

Backups capture configuration0..95. Recovery records are persisted before writes because a transport timeout does not prove the device rejected a write. Each write is followed by a configuration read-back. A firmware/model/profile check gates writes.

Normal exit from0.2.2 defaults to retaining the device configuration. The controller first cancels/awaits playback, then only marks the local recovery record RetainedOnExit=true and closes the HID handle; this path sends no restore, disable or other device command. Restore-on-exit is explicit opt-in. The original snapshot and modified-address list remain available across restarts. The next write or manual restoration clears the retained marker before issuing writes. The marker distinguishes intentional retention from an unfinished operation; it is not firmware data. Normal firmware-managed idle sleep still extinguishes the LED.

Static protocol references were derived from the installed official ATK HUB. Dynamic evidence: seven visible colors, exact restoration, native C# no-op write round-trip, and idle-off/wake behavior on the owner's device. No vendor binaries are included with this source package.

The existing idle LED close time is at173 with a10-second unit; sensor enable/time at181/183. The read-only sleep observation found60 seconds and verified168..191 unchanged after restoration. The application does not write those fields. A successful read-back proves configuration, not physical light output; the recorded human observation supplies the latter.

Custom playback: interpolate RGB components linearly, including the last-to-first segment. The window previews independently. For wireless hardware, coalesce identical RGB samples and allow one update at most every450ms. Each new active-color pair preserves its neighbor and checksum, and each write is read back. Check profile and active DPI slot; invalidate the cached pair on resume, failure, stop or other operations. Save each modified address to the recovery record before its first write.

Activity gating: passive Windows Raw Input accepts movement/buttons from the selected VID373B/PID11FE mouse only. After about2seconds without mouse input, freeze the timeline and send no further automatic HID reports; each outgoing exchange checks activity and playback generation again immediately before transmission. Already-submitted I/O may finish. Stop awaits the outstanding task before restore/dispose. No polling, battery refresh or reconnect runs while idle. Physical motion resumes playback. This preserves the firmware's existing sleep-off policy; it does not immediately turn the LED off after2seconds.

This is a configuration-write implementation. A separate volatile animation command and sustained configuration-write endurance have not been verified. Short gradient tests showed intermediate colors, and the owner confirmed the native C# loop visibly works. Do not infer full24-bit optical precision or continuous high-frame-rate support from read-back alone.
