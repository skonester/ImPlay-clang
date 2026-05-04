namespace ImPlay.App.Controls

open System
open System.Runtime.InteropServices
open Avalonia
open Avalonia.Controls
open Avalonia.Platform
open ImPlay.Core.Services

module private Win32 =
    [<Literal>]
    let WS_CHILD = 0x40000000
    [<Literal>]
    let WS_VISIBLE = 0x10000000
    [<Literal>]
    let WS_CLIPSIBLINGS = 0x04000000
    [<Literal>]
    let WS_CLIPCHILDREN = 0x02000000
    [<Literal>]
    let SW_HIDE = 0
    [<Literal>]
    let SW_SHOW = 5

    [<DllImport("user32.dll", EntryPoint = "CreateWindowExW", CharSet = CharSet.Unicode)>]
    extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName, int dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam)

    [<DllImport("user32.dll")>]
    [<return: MarshalAs(UnmanagedType.Bool)>]
    extern bool DestroyWindow(IntPtr hWnd)

    [<DllImport("user32.dll")>]
    [<return: MarshalAs(UnmanagedType.Bool)>]
    extern bool ShowWindow(IntPtr hWnd, int nCmdShow)

    [<DllImport("kernel32.dll", EntryPoint = "GetModuleHandleW", CharSet = CharSet.Unicode)>]
    extern IntPtr GetModuleHandle(string lpModuleName)

type NativeMpvVideoHost() as self =
    inherit NativeControlHost()

    let mutable hwnd = IntPtr.Zero

    static let PlaybackProperty =
        AvaloniaProperty.Register<NativeMpvVideoHost, PlaybackService>("Playback")

    static let IsActiveProperty =
        AvaloniaProperty.Register<NativeMpvVideoHost, bool>("IsActive")

    member _.Playback
        with get() = self.GetValue(PlaybackProperty)
        and set(v) = self.SetValue(PlaybackProperty, v) |> ignore

    member _.IsActive
        with get() = self.GetValue(IsActiveProperty)
        and set(v) = self.SetValue(IsActiveProperty, v) |> ignore

    member _.ReattachNativeVideoWindow() =
        if hwnd <> IntPtr.Zero && self.IsActive && not (isNull self.Playback) then
            Win32.ShowWindow(hwnd, Win32.SW_SHOW) |> ignore
            self.Playback.UseNativeVideoWindow(hwnd, true) |> ignore
            StartupLogger.Log($"Native mpv video host reattached: hwnd=0x{hwnd.ToInt64():X}, gpu-api=vulkan.")

    override _.CreateNativeControlCore(parent: IPlatformHandle) =
        if not (OperatingSystem.IsWindows()) || parent.Handle = IntPtr.Zero then
            base.CreateNativeControlCore(parent)
        else
            let mutable style = Win32.WS_CHILD ||| Win32.WS_CLIPCHILDREN ||| Win32.WS_CLIPSIBLINGS
            if self.IsActive then
                style <- style ||| Win32.WS_VISIBLE

            let handle =
                Win32.CreateWindowEx(
                    0,
                    "STATIC",
                    "",
                    style,
                    0,
                    0,
                    1,
                    1,
                    parent.Handle,
                    IntPtr.Zero,
                    Win32.GetModuleHandle(null),
                    IntPtr.Zero)

            if handle = IntPtr.Zero then
                base.CreateNativeControlCore(parent)
            else
                hwnd <- handle
                StartupLogger.Log($"Native mpv video host created: hwnd=0x{handle.ToInt64():X}.")
                Win32.ShowWindow(handle, if self.IsActive then Win32.SW_SHOW else Win32.SW_HIDE) |> ignore
                if self.IsActive && not (isNull self.Playback) then
                    self.Playback.UseNativeVideoWindow(handle, true) |> ignore
                PlatformHandle(handle, "HWND") :> IPlatformHandle

    override _.DestroyNativeControlCore(control: IPlatformHandle) =
        if hwnd <> IntPtr.Zero then
            if not (isNull self.Playback) then
                self.Playback.DetachNativeVideoWindow(hwnd)
            Win32.DestroyWindow(hwnd) |> ignore
            StartupLogger.Log($"Native mpv video host destroyed: hwnd=0x{hwnd.ToInt64():X}.")
            hwnd <- IntPtr.Zero
        else
            base.DestroyNativeControlCore(control)

    override _.OnPropertyChanged(change: AvaloniaPropertyChangedEventArgs) =
        base.OnPropertyChanged(change)

        if (change.Property = PlaybackProperty || change.Property = IsActiveProperty) && hwnd <> IntPtr.Zero then
            if self.IsActive then
                Win32.ShowWindow(hwnd, Win32.SW_SHOW) |> ignore
                if not (isNull self.Playback) then
                    self.Playback.UseNativeVideoWindow(hwnd, true) |> ignore
            else
                if not (isNull self.Playback) then
                    self.Playback.DetachNativeVideoWindow(hwnd)
                Win32.ShowWindow(hwnd, Win32.SW_HIDE) |> ignore
