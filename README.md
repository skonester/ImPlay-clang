# ImPlay

ImPlay is a cross-platform desktop media player built around
[libmpv](https://mpv.io/). This repository contains the current Slint frontend
(`ImPlay-Slint`) and a maintained ImGui frontend for comparison and
compatibility.

The application provides a native desktop window, playlist and playback
controls, subtitle and audio selection, configurable themes and languages,
recent files, drag-and-drop/file dialogs, mpv option passthrough, and an
optional single-instance mode. It can also run mpv headlessly when video is
disabled or an output-only option is supplied.

## Frontends

The top-level CMake project selects the frontend with `IMPLAY_UI`:

- **Slint (default)**: `source/`, `include/`, and `ui/`. Slint owns the
  declarative interface, while C++ owns the window integration, playback
  orchestration, configuration, and native helpers.
- **ImGui**: `imgui/`. This is a separate frontend with its own window,
  player, views, theme, and helper implementation. It is useful when changing
  shared behavior or comparing the two UI implementations.

Both frontends produce an executable named `ImPlay`.

## Requirements

The normal native build requires:

- CMake 3.24 or newer
- Ninja
- A C++20 compiler (the checked-in presets target Windows x64 Clang or MSVC)
- Zig 0.16 or newer for the mpv core
- Rust and a working C++ toolchain when configuring Slint from source
- A libmpv development package

On Windows, the repository includes the pinned `mpv-dev-*.7z` archive used by
the default presets. CMake verifies its SHA-256 hash and extracts the headers
and import library during configuration. On other platforms, provide libmpv
through the system and make its headers/libraries discoverable to CMake.

Configuration also downloads several dependencies with CMake `FetchContent`
(including Slint, fmt, and nlohmann/json), so the first configure needs network
access unless those dependencies are already available in the build cache.

## Configure and build

The repository includes presets for Windows x64:

```powershell
cmake --preset x64-clang-release
cmake --build --preset x64-clang-release
```

For MSVC:

```powershell
cmake --preset x64-msvc-release
cmake --build --preset x64-msvc-release
```

The ImGui variants are:

```powershell
cmake --preset x64-clang-imgui-release
cmake --build --preset x64-clang-imgui-release
```

Replace `clang` with `msvc` for the MSVC ImGui preset. Build output is placed
under `out/build/<preset>/`.

The Slint build invokes `zig build` automatically to create the static
`implay_mpv` core library. The Zig parser/core tests can be run independently:

```powershell
zig build test -Dmpv-include=<directory-containing-mpv-client.h>
```

## Running

The executable accepts paths or URLs and forwards mpv-style options. Examples:

```text
ImPlay movie.mkv
ImPlay https://example.invalid/video
ImPlay --fs movie.mkv
ImPlay --sub-file=subtitles.srt movie.mkv
ImPlay --playlist=playlist.m3u
ImPlay --no-video --o=audio-only-option song.mp3
```

Run `ImPlay --help` for the short built-in option list. The complete set of
playback options is documented by the installed mpv manual.

When single-instance mode is enabled, a second invocation sends its paths to
the first process through a named pipe on Windows or a Unix domain socket on
Unix-like systems.

## Repository layout

| Path | Purpose |
| --- | --- |
| `source/` | Slint application entry point, window integration, configuration, helpers, and C++ mpv wrapper |
| `include/` | Public headers for the Slint-side application and the Zig C ABI |
| `ui/` | Slint components, state, theme, widgets, and generated application UI inputs |
| `source/zig/` | Zig libmpv owner, event/render lifecycle, C ABI implementation, and parsers/tests |
| `imgui/` | Alternate ImGui frontend and its frontend-specific third-party sources |
| `resources/` | Embedded translations, mpv scripts/configuration, icons, and packaging assets |
| `third_party/` | Vendored or locally built dependencies shared by the frontends |
| `cmake/` | mpv development package integration and CPack helpers |
| `CMakePresets.json` | Supported local configure/build presets |
| `CREDITS.md` | Project, frontend, dependency, and license attribution |

For implementation details and guidance for maintainers or AI coding agents,
see [architecture.md](architecture.md).

## Licensing

The project is distributed under the GNU GPL v2 only unless a component is
covered by its own license. See [LICENSE.txt](LICENSE.txt),
[CREDITS.md](CREDITS.md), and the license files in `third_party/` and `ui/`.
