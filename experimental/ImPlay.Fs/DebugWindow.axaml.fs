namespace ImPlay.Fs

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Avalonia.Collections
open Avalonia.Threading
open Domain
open HanumanInstitute.LibMpv

type DebugWindow() as this =
    inherit Window()

    let logsSource = AvaloniaList<LogEntry>()
    let propertiesSource = AvaloniaList<PropertyEntry>()
    let commandsSource = AvaloniaList<CommandEntry>()
    let mutable mpv: MpvContext = null

    do
        this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)

        let listLogs = this.FindControl<ListBox>("ListLogs")
        let txtConsoleInput = this.FindControl<TextBox>("TxtConsoleInput")
        let btnSendCommand = this.FindControl<Button>("BtnSendCommand")
        let listProperties = this.FindControl<ListBox>("ListProperties")
        let listCommands = this.FindControl<ListBox>("ListCommands")

        listLogs.ItemsSource <- logsSource
        listProperties.ItemsSource <- propertiesSource
        listCommands.ItemsSource <- commandsSource

        let executeCommand (cmd: string) =
            if mpv <> null && not (String.IsNullOrWhiteSpace(cmd)) then
                try
                    mpv.RunCommand(MpvCommandOptions(), cmd.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                    txtConsoleInput.Text <- ""
                with ex ->
                    this.AddLog("error", sprintf "Command error: %s" ex.Message)

        btnSendCommand.Click.Add(fun _ -> executeCommand txtConsoleInput.Text)
        txtConsoleInput.KeyDown.Add(fun e -> if e.Key = Avalonia.Input.Key.Enter then executeCommand txtConsoleInput.Text)

    member this.AddLog(level: string, message: string) =
        let color = 
            match level with
            | "fatal" | "error" -> "#FF5252"
            | "warn" -> "#FFD740"
            | "v" | "debug" | "trace" -> "#9E9E9E"
            | _ -> "#FFFFFF"
        
        Dispatcher.UIThread.Invoke(fun () ->
            logsSource.Add({ Message = message; Color = color })
            if logsSource.Count > 1000 then logsSource.RemoveAt(0)
        )

    member this.Init(ctx: MpvContext) =
        mpv <- ctx
        // LibMpv doesn't expose LogMessage directly in a friendly way in some versions, 
        // but it has events we can hook.
        // Actually, let's use the low-level API if needed, but we'll start with property polling for debug.
        
        this.RefreshData()

    member private this.RefreshData() =
        if mpv <> null then
            // Populate properties
            try
                // In a real implementation, we'd query 'property-list' and iterate.
                // For the prototype, we'll add some key ones.
                propertiesSource.Clear()
                let props = [ "path"; "time-pos"; "duration"; "volume"; "mute"; "pause"; "speed"; "fps"; "video-format"; "audio-format" ]
                for p in props do
                    try
                        let v = mpv.GetPropertyString(p)
                        propertiesSource.Add({ Name = p; Value = v })
                    with _ -> ()
            with _ -> ()

    static member Show(owner: Window, ctx: MpvContext) =
        let win = DebugWindow()
        win.Init(ctx)
        win.Show(owner)
        win
