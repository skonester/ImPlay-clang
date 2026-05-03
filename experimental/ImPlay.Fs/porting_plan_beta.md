# ImPlay.Fs Porting Plan Beta - Current Findings

## Current Status
The experimental F# / Avalonia build is protected by a hard launch gate. A build is not considered working unless `ImPlay.Fs.exe` compiles, starts, and shows a visible Avalonia `MainWindow` within the smoke timeout.

The media control bar has been rewritten as a native Avalonia transport surface inspired by the original mpv OSC layout. It is not an exact ImGui port because the old persistent control bar is not primarily drawn in C++ ImGui. The old app relies on mpv's bundled OSC Lua/ASS UI for the transport bar behavior and layout, while C++ ImGui is used more for overlays and utility views such as quickview, context UI, and palettes.

## Important Discovery: C++ vs mpv OSC
The permanent playback controls map mostly to `resources/romfs/mpv/osc.lua`, not to hand-authored C++ ImGui widgets.

The OSC layout includes:
- Title/status row with playlist previous/next.
- Main transport row with play/pause, seek back, seek forward, frame/chapter-adjacent behavior.
- Audio/subtitle/fullscreen/volume controls.
- Bottom seekbar with current time and remaining/duration display.

The Avalonia implementation should aim for functional parity with the OSC behavior, not a literal ImGui clone.

## Implemented: Hardiron Build Gate
Implemented startup hardening so the app cannot silently vanish during normal double-click/debug use.

Completed items:
- Single-instance IPC is disabled by default for experimental builds.
- IPC only activates through explicit opt-in, such as `--single-instance=yes`.
- Startup diagnostics write to durable `startup.log` paths.
- Startup logs include app/config/resource paths, CLI args, IPC mode, Avalonia startup, `MainWindow` construction, `Opened`, mpv observer setup, and fatal startup errors.
- Fatal startup errors attempt to show a visible Windows message box instead of exiting invisibly.
- `App.axaml.fs` logs `MainWindow` assignment.
- `MainWindow.axaml.fs` logs constructor, XAML load, `Opened`, mpv observer init, and file load.
- `libmpv-2.dll` is copied into the output root so launch smoke works from built exe output.
- Added `experimental/ImPlay.Fs/scripts/smoke-launch.ps1`.
- Added FAKE `Smoke` target after build.

Validated gates:
- `dotnet build experimental\ImPlay.Fs\ImPlay.Fs.fsproj --no-restore`
- `powershell -NoProfile -ExecutionPolicy Bypass -File experimental\ImPlay.Fs\scripts\smoke-launch.ps1 -Configuration Debug`
- `dotnet run --project experimental\ImPlay.Fs\build\build.fsproj -- --target Smoke`

## Implemented: Avalonia Media Control Bar Rewrite
The old bottom control strip was replaced with an OSC-inspired native Avalonia transport box.

Current controls:
- Open file.
- Open folder.
- Previous/next media using weak playlist navigation.
- Status/title text.
- Quickview/sidebar toggle.
- Previous/next chapter.
- A-B loop buttons and clear loop.
- Seek back and seek forward.
- Frame back and frame step.
- Play/pause.
- Stop.
- Mute.
- Volume slider.
- Fullscreen.
- Seek slider.
- Current time and remaining time labels.

## Implemented: Tighter mpv Integration
The C++ app wraps mpv behind `Mpv::command`, `commandv`, typed `property`, `option`, observed events, and cached mpv state. The F# window previously called mpv directly from many UI paths.

Completed F# integration work:
- Added guarded `TryMpvCommand`, `TryMpvSetProperty`, and `TryMpvSetBoolProperty` helpers in `MainWindow.axaml.fs`.
- Routed `MainWindow` UI-triggered mpv commands/properties through those helpers.
- `startup.log` now acts as an mpv command ledger with labels, args, and failure messages.
- Command/property failures are caught and shown in status text instead of throwing through UI handlers.
- Direct `RunCommand`/`SetProperty` calls in `MainWindow` are now limited to the wrapper itself, apart from higher-level LibMpv methods such as `LoadFile`, `LoadPlaylist`, and `PlaylistClear`.

Important C++ parity still missing:
- C++ routes app actions through `script-message-to implay ...` and handles them in `Player::execute`.
- F# still needs either client-message handling, if LibMpv exposes it, or it should stop loading app-private bindings/scripts that send unhandled `script-message-to implay ...` messages.

## Implemented: Seekbar / Invalid Parameter Fixes
We saw invalid-parameter symptoms around seek behavior and a seekbar that could appear to start at the end. The final root issue was specific: the seekbar UI was moving, and mpv was receiving drag values, but LibMpv rejected `seek ... absolute-percent` command forms as `invalid parameter`.

Completed fixes:
- `SliderSeek` is percent-based: range `0..100`.
- File load resets seekbar percentage and enables the slider for media interaction.
- Stop resets the seekbar to the beginning.
- Holding left click captures the pointer and updates the slider continuously while dragging.
- Dragging throttles meaningful seek updates to avoid flooding mpv.
- The visual slider thumb moves during drag.
- Rejected `seek <percent> absolute-percent` and `seek <percent> absolute-percent+exact` command forms were removed from the slider path.
- Slider seeking now sets `time-pos` directly when duration is known.
- Slider seeking falls back to setting `percent-pos` only when duration is unknown.
- Time labels derive from `duration * percent / 100`.
- Duration/time updates are clamped before updating slider and taskbar progress.

Current seek semantics:
- Slider drag/click with known duration: `set time-pos <seconds>`.
- Slider drag/click without known duration: `set percent-pos <percent>` fallback.
- Relative seek controls: `seek <seconds> relative keyframes` through the guarded command wrapper.
- The seekbar starts at the beginning and now moves video during drag.

Confirmed diagnosis:
- `startup.log` showed `mpv command failed: seek absolute percent [...] invalid parameter` for `seek ... absolute-percent`.
- After switching the slider to `time-pos`, video seeking started working.

## Known Remaining Warnings
These are not new regressions from the media bar pass, but should be cleaned up later:
- NU1510 package pruning warnings.
- Fody `PrivateAssets='All'` warning.
- Avalonia deprecated drag/drop API warnings.
- Avalonia clipboard deprecation warning.
- FAKE/build project vulnerability warnings from older transitive packages.

## Current Risk Areas
The launch gate is stable, but functional parity still needs broader manual media testing.

Highest priority risks:
- Verify seekbar behavior across streams and unknown-duration media. Local known-duration file drag is confirmed working via `time-pos`.
- Confirm no mpv invalid-parameter spam remains during menu seek, keyboard seek, chapter navigation, playlist navigation, config/script loading, and equalizer changes.
- Clean up known noisy mpv failures: `load-config`, `load-script`, and equalizer filter calls have shown invalid-parameter logs.
- Verify A-B loop UI state reflects mpv loop points clearly.
- Verify chapter buttons are disabled or harmless when no chapters exist.
- Verify playlist weak prev/next matches original OSC behavior.
- Verify volume/mute state stays synchronized if changed externally by mpv or shortcuts.

## Next Implementation Targets
Keep feature work small and gate every slice.

Recommended next slices:
1. Add UI enablement rules for transport controls based on current mpv state: no file, idle, valid duration, playlist count, chapter count.
2. Add pure helper tests for time formatting, seek clamp behavior, and playlist/chapter enablement rules.
3. Implement C++ parity for `script-message-to implay ...`: either handle mpv client messages if LibMpv exposes them, or stop loading app-private bindings that F# cannot receive yet.
4. Continue porting quickview features in small chunks: playlist operations, chapters, tracks, audio/subtitle/video controls.
5. Clean up noisy startup mpv failures, especially invalid `load-config`, `load-script`, and equalizer filter calls.
6. Add a debug view or log-tail shortcut for recent mpv command failures.

## Development Rules Going Forward
Every meaningful feature slice must pass:
- `dotnet build experimental\ImPlay.Fs\ImPlay.Fs.fsproj --no-restore`
- Any available unit tests.
- `powershell -NoProfile -ExecutionPolicy Bypass -File experimental\ImPlay.Fs\scripts\smoke-launch.ps1 -Configuration Debug`
- Preferably `dotnet run --project experimental\ImPlay.Fs\build\build.fsproj -- --target Smoke`

Do not merge broad state-machine rewrites until the visible-window launch gate and media-control behavior are consistently stable.

## Acceptance Standard
A build is only working if:
- It compiles.
- It opens a visible Avalonia `MainWindow` from the built exe.
- Startup failures are logged and visible.
- The transport bar does not send invalid mpv command parameters during normal use.
- The seekbar starts at the beginning and seeks correctly once media is loaded. Known-duration media seeks by setting `time-pos`; unknown-duration media falls back to `percent-pos`.
