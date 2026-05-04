# ImPlay-Clang Architecture

ImPlay-Clang is architected as a production-grade C++ core supplemented by an extensive research laboratory for next-generation features in .NET and F#.

## 1. Core C++ Architecture (source/ & include/)

The C++ core follows a modular design with a clear separation between windowing, media playback, and user interface. The `source/` and `include/` directories maintain a mirrored structure.

### 1.1 Application Lifecycle
- **main.cpp**: Manages startup, CLI parsing, and IPC server initialization for single-instance behavior.
- **window.cpp / window.h**: Wraps GLFW to provide cross-platform windowing and graphics context management (OpenGL/Vulkan). It delegates input events to the `Player` class.

### 1.2 Media Engine (mpv.cpp & player.cpp)
- **Mpv Class**: A low-level C++ wrapper for `libmpv`, handling asynchronous event observation and property management.
- **Player Class**: The central coordinator. It manages playback state, synchronizes UI updates with mpv events, and handles complex logic like playlist sorting and resume positions.

### 1.3 User Interface (source/views/)
The UI is built using **Dear ImGui** and is decoupled into specialized view classes:
- **Command Palette**: Searchable command dispatcher.
- **Context Menu**: Comprehensive playback and setting control via a native-style menu.
- **Debug & Metrics**: Real-time property inspection and colored mpv log filtering.
- **Quick Settings & Main Settings**: Modular configuration interfaces.

### 1.4 Embedded Resource System (resources/romfs/)
ImPlay-Clang uses **libromfs** to embed critical assets directly into the binary:
- **mpv Configuration**: Default `mpv.conf` and `input.conf` are bundled as static resources.
- **On-Screen Controller**: `osc.lua` is embedded and loaded at runtime.
- **Localization**: I18n JSON files (`lang/*.json`) are bundled, allowing for zero-dependency deployment of localized strings.

---

## 2. Research & Development Lab (experimental/)

The `experimental` directory is a high-fidelity laboratory for prototyping architectural shifts.

### 2.1 C# Modular Core (experimental/ImPlay/)
This project prototypes a service-oriented architecture in C#:
- **Advanced Services**: 
    - `DlnaCastService`: Native support for DLNA and Chromecast.
    - `SubtitleSearchService`: Online subtitle discovery and automated downloading.
- **Hardened Logic**: Services like `PlaybackService` and `SettingsService` are designed for maximum modularity.

### 2.2 F# Avalonia Port (experimental/implayfsharpavalonia/)
A research project exploring functional-first stability and modern UI frameworks:
- **MVVM Architecture**: `MainViewModel.fs` acts as the central state machine, coordinating between core services and the Avalonia UI.
- **Custom UI Controls**: F# implementations of media-specific controls:
    - `AudioBars.fs`: Dynamic audio visualization.
    - `SeekBar.fs` / `VolumeSlider.fs`: Specialized interactive controls.
- **Hybrid Rendering Pipeline**:
    - `MpvVideoSurface.fs`: OpenGL-based rendering hosted within the Avalonia visual tree.
    - `NativeMpvVideoHost.fs`: Research into native Vulkan/Win32 hosting for maximum performance.

### 2.3 F# Core Logic (experimental/ImPlay.Fs/)
Provides the foundational functional logic and bindings used across all F# experimental efforts, ensuring a unified domain model.

---

## 3. Build & Integration Infrastructure

### 3.1 Toolchain & Environment
- **Windows**: Built with **LLVM/Clang-CL**, **Ninja**, and **Visual Studio 2022**.
- **Iron-Clad Build Script**: `build-windows-clang.ps1` performs exhaustive system audits (disk, network, permissions) and ensures environment consistency.

### 3.2 Dependency Strategy
- **Vendor Copies**: Critical dependencies like `imgui`, `glfw`, and `glad` are vendored in `third_party/` for custom patching and stable integration.
- **Automated Injection**: `FetchContent` is used in `CMakeLists.txt` for libraries like `fmt`, `nlohmann_json`, and `freetype` to ensure zero-effort dependency management.
