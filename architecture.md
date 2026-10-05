# ImPlay architecture

This document is an implementation map for maintainers and human or AI coding
agents. It describes the current source tree, runtime boundaries, build
variants, and the invariants that should be preserved when changing the
application.

## System overview

```text
CLI arguments
    |
    v
source/main.cpp ----> Config --------------------> implay.conf + recent files
    |
    +-- headless path ----------------------------> Zig core --> libmpv
    |
    +-- Window
          |
          +-- Slint AppWindow <--> ui/*.slint
          |
          +-- OpenGL/FBO render integration
          |
          +-- ImPlay::Mpv (C++ adapter)
                    |
                    v
              include/mpv_core.h
                    |
                    v
              source/zig/mpv_core.zig
                    |
                    v
                  libmpv
```

The default build is the Slint path. The ImGui path is selected at CMake
configure time and is not a runtime plugin: it compiles a different set of
frontend source files into the same `ImPlay` executable name.

## Runtime flow

### Startup

1. `source/main.cpp` parses command-line options with `OptionParser`.
2. `--help` prints the built-in usage text and exits.
3. Output-only or `--no-video` invocations use the headless mpv path.
4. GUI invocations load `Config`, including language, window, mpv, font, debug,
   and recent-file settings.
5. If single-instance mode is enabled, paths are sent to the existing process
   through `Config::ipcSocket()` and the new process exits.
6. `Window::init()` configures mpv, installs wakeup/render callbacks, and
   connects the renderer to the selected UI backend.
7. `Window::run()` enters the Slint or ImGui event loop and saves state on exit.

### Playback and rendering

The Slint window uses Slint's FemtoVG/OpenGL rendering notifier. During renderer
setup it initializes libmpv's render context; before each frame it asks mpv to
render into an OpenGL framebuffer/texture that is exposed to the Slint UI.
mpv event wakeups are posted back to the UI thread, and render updates request
another UI redraw.

The ImGui frontend uses its own GLFW/OpenGL window and player/rendering
integration under `imgui/source/`. Do not move Slint-specific assumptions into
the ImGui implementation or assume that the two frontend `Window` classes have
the same lifecycle.

## Module responsibilities

### Application and shared C++

- `source/main.cpp`: process entry point, CLI dispatch, single-instance
  forwarding, and top-level error reporting.
- `source/window.cpp` / `include/window.h`: Slint window lifecycle, native
  window handling, OpenGL/libmpv rendering, UI callbacks, and media event
  coordination.
- `source/mpv.cpp` / `include/mpv.h`: type-safe C++ adapter over the Zig C ABI;
  caches observable mpv properties and copies callback-owned list data.
- `source/config.cpp` / `include/config.h`: INI configuration and recent-file
  persistence.
- `source/helpers/`: language loading, file dialogs, command-line/path
  utilities, platform data paths, and shared ImGui/Slint support where
  applicable.

### Slint UI

- `ui/app-window.slint`: root component and the main UI contract consumed by
  `Window`.
- `ui/state.slint`: UI state/data structures and callback-facing properties.
- `ui/theme.slint`, `ui/widgets.slint`, `ui/icons.slint`: reusable visual
  primitives.
- `ui/settings-panel.slint`, `ui/context-menu.slint`,
  `ui/subtitle-panel.slint`: feature-specific panels.

Changes to callback names, properties, structs, or enums in Slint files must be
updated consistently in `source/window.cpp` and rebuilt through CMake so the
generated C++ bindings stay synchronized.

### Zig mpv core

`source/zig/mpv_core.zig` owns every direct libmpv call in the Slint build:

- creates the main and client mpv handles;
- owns the event thread and render context;
- initializes libmpv and forwards options, commands, properties, and logs;
- pumps events on the caller/UI side;
- converts list properties into callback payloads;
- exposes the stable C ABI declared in `include/mpv_core.h`.

`source/zig/mpv_parse.zig` contains allocation-aware, libmpv-independent
parsers for playlist, chapters, tracks, audio devices, bindings, profiles, and
simple commands. Its returned strings/items are callback-lifetime data; the C++
adapter must copy them before the next mpv event pump.

`source/zig/mpv_c.h` is the Zig translate-C input. Keep the layouts of the
`extern` structs in `mpv_parse.zig`, `mpv_core.zig`, and `include/mpv_core.h`
identical. ABI changes require rebuilding and testing both sides.

## Threading and ownership invariants

- mpv wakeup callbacks can run on an mpv thread.
- render-update callbacks can run on mpv's render thread.
- event, property, log, and parsed-list callbacks are dispatched from
  `implay_mpv_pump()` on the caller's thread.
- UI mutations must be marshalled to the UI thread through the existing
  `postToUi`/redraw scheduling path.
- Parsed Zig list data is temporary. `ImPlay::Mpv` copies it into C++ containers.
- `Window` must stop scheduling work before its mpv object and UI handle are
  destroyed; `shuttingDown` and the pending-wakeup flags protect this
  transition.
- `Mpv` destruction terminates the event thread and releases the render context;
  callers must not retain callbacks or borrowed mpv data after destruction.

## Build graph

The root `CMakeLists.txt` reads the version from `package.json`, selects
`IMPLAY_UI`, fetches/configures dependencies, and creates the `ImPlay` target.
For Slint it also:

1. invokes `zig build` through the `implay_mpv_core` custom target;
2. builds the shared C++/Slint executable;
3. links `implay_mpv`, libmpv, Slint, glad, fmt, JSON, nativefiledialog,
   natsort, inipp, and libromfs.

For ImGui, `imgui/ImPlayImGui.cmake` returns early from the root configuration
and builds the ImGui-specific source set with GLFW, Dear ImGui, FreeType, and
the direct libmpv integration.

The checked-in presets currently target Windows x64 Clang/MSVC release builds.
Do not assume a build directory is clean: generated files under `out/build/`
are configuration artifacts, not source inputs.

## Change guide for agents

Before editing:

1. Identify the selected frontend and whether the behavior belongs to shared
   code or a frontend-specific implementation.
2. Trace the data from CLI/configuration through `Window`, `Mpv`, the C ABI, and
   the UI callback/property before changing a signature.
3. Search both `source/` and `imgui/` for behavior that is intended to remain
   consistent.
4. Preserve thread boundaries and copy borrowed callback data.

After editing:

- Build the smallest affected preset with CMake.
- Run `zig build test` when changing `source/zig/` or
  `include/mpv_core.h`.
- Verify Slint property/callback changes through a full CMake rebuild.
- Update `README.md`, this document, or `CREDITS.md` when build behavior,
  architecture, dependencies, or licensing changes.
- Do not commit generated `out/build/` artifacts unless a task explicitly
  requires them.

## Common extension points

- Add a playback command/property: update the C++ `Mpv` adapter, observe/cache
  it where needed, then connect it to the relevant UI callback.
- Add a Slint control: update the appropriate `ui/` component and wire its
  callback/property in `Window::initCallbacks()` or the related window code.
- Add a persistent setting: extend `ConfigData`, then update both `Config::load`
  and `Config::save`, plus the corresponding UI.
- Add a translation: update the locale JSON files under
  `resources/romfs/lang/`; keep the English fallback complete.
- Add a libmpv list type: define matching C ABI/extern layouts, parse it in
  `mpv_parse.zig`, publish it from `mpv_core.zig`, and copy it in
  `source/mpv.cpp`.

## Licensing boundary

The application is GPL-2.0-only except for dependencies and the Slint UI
component whose licenses are documented in [CREDITS.md](CREDITS.md). Preserve
the applicable copyright and SPDX headers when moving or deriving source.
