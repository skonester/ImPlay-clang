namespace ImPlay.Fs

open System
open System.Runtime.InteropServices
open System.IO
open System.Diagnostics
open Avalonia
open Avalonia.Controls
open Avalonia.Platform

[<ComImport; Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type ITaskbarList3 =
    abstract member HrInit: unit -> unit
    abstract member AddTab: hwnd: nativeint -> unit
    abstract member DeleteTab: hwnd: nativeint -> unit
    abstract member ActivateTab: hwnd: nativeint -> unit
    abstract member SetActiveAlt: hwnd: nativeint -> unit
    abstract member MarkFullscreenWindow: hwnd: nativeint * fFullscreen: bool -> unit
    abstract member SetProgressValue: hwnd: nativeint * ullCompleted: uint64 * ullTotal: uint64 -> unit
    abstract member SetProgressState: hwnd: nativeint * tbpFlags: int -> unit
    abstract member RegisterTab: hwndTab: nativeint * hwndMDI: nativeint -> unit
    abstract member UnregisterTab: hwndTab: nativeint -> unit
    abstract member SetTabOrder: hwndTab: nativeint * hwndInsertBefore: nativeint -> unit
    abstract member SetTabActive: hwndTab: nativeint * hwndMDI: nativeint * tbpFlags: int -> unit
    abstract member ThumbBarAddButtons: hwnd: nativeint * cButtons: uint32 * [<MarshalAs(UnmanagedType.LPArray)>] pButton: nativeint -> unit
    abstract member ThumbBarUpdateButtons: hwnd: nativeint * cButtons: uint32 * [<MarshalAs(UnmanagedType.LPArray)>] pButton: nativeint -> unit
    abstract member ThumbBarSetImageList: hwnd: nativeint * himl: nativeint -> unit
    abstract member SetOverlayIcon: hwnd: nativeint * hIcon: nativeint * pszDescription: string -> unit
    abstract member SetThumbnailTooltip: hwnd: nativeint * pszTip: string -> unit
    abstract member SetThumbnailClip: hwnd: nativeint * prcClip: nativeint -> unit

[<ComImport; Guid("56fdf344-fd6d-11d0-958a-006097c9a090"); ClassInterface(ClassInterfaceType.None)>]
type TaskbarList = class end

module Platform =

    type TbpFlag =
        | NoProgress = 0
        | Indeterminate = 1
        | Normal = 2
        | Error = 4
        | Paused = 8

    let private taskbarList =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            try
                let t: Type = Type.GetTypeFromCLSID(Guid("56fdf344-fd6d-11d0-958a-006097c9a090"))
                Activator.CreateInstance(t) :?> ITaskbarList3 |> Some
            with _ -> None
        else None

    let setTaskbarProgress (window: Window) (value: float) (state: TbpFlag) =
        match taskbarList with
        | Some tb ->
            let hwnd = window.TryGetPlatformHandle().Handle
            if hwnd <> nativeint 0 then
                tb.SetProgressState(hwnd, int state)
                if state = TbpFlag.Normal || state = TbpFlag.Paused || state = TbpFlag.Error then
                    tb.SetProgressValue(hwnd, uint64 (value * 100.0), 100UL)
        | None -> ()

    let getResourcesPath () =
        let mutable dir = AppDomain.CurrentDomain.BaseDirectory
        while not (String.IsNullOrEmpty(dir)) && not (Directory.Exists(Path.Combine(dir, "resources"))) do
            dir <- Path.GetDirectoryName(dir)
        if String.IsNullOrEmpty(dir) then AppDomain.CurrentDomain.BaseDirectory else dir

    let getBundledConfigPath () =
        Path.Combine(getResourcesPath(), "resources", "romfs", "mpv")

    let registerFileAssociations (extensions: string list) (appName: string) (exePath: string) =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            try
                use classesKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Classes", true)
                
                // Register app progid
                let progId = sprintf "%s.AssocFile" appName
                use progIdKey = classesKey.CreateSubKey(progId)
                progIdKey.SetValue("", sprintf "%s Media File" appName)
                
                use shellKey = progIdKey.CreateSubKey("shell")
                use openKey = shellKey.CreateSubKey("open")
                use commandKey = openKey.CreateSubKey("command")
                commandKey.SetValue("", sprintf "\"%s\" \"%%1\"" exePath)
                
                // Associate extensions
                for ext in extensions do
                    let extDot = if ext.StartsWith(".") then ext else "." + ext
                    use extKey = classesKey.CreateSubKey(extDot)
                    extKey.SetValue("", progId)
            with _ -> ()
        else ()

    let setupFileAssociations () =
        if RuntimeInformation.IsOSPlatform(OSPlatform.Windows) then
            let exePath = Process.GetCurrentProcess().MainModule.FileName
            let extensions = Media.videoTypes @ Media.audioTypes @ Media.isoTypes
            registerFileAssociations extensions "ImPlay.Fs" exePath
        else ()
