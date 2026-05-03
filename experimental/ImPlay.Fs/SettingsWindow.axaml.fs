namespace ImPlay.Fs

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Markup.Xaml
open Domain

type SettingsWindow() as this =
    inherit Window()

    let mutable config: PlayerConfig = defaultConfig
    let mutable result: PlayerConfig option = None

    do
        this.InitializeComponent()

    member private this.InitializeComponent() =
        AvaloniaXamlLoader.Load(this)
        
        this.Title <- Lang.i18n "views.settings.title"

        let chkSingleWindow = this.FindControl<CheckBox>("ChkSingleWindow")
        let chkUseMpvConfig = this.FindControl<CheckBox>("ChkUseMpvConfig")
        let chkWatchLater = this.FindControl<CheckBox>("ChkWatchLater")
        let chkSaveWindow = this.FindControl<CheckBox>("ChkSaveWindow")
        let numRecentLimit = this.FindControl<NumericUpDown>("NumRecentLimit")
        let comboLogLevel = this.FindControl<ComboBox>("ComboLogLevel")
        let comboTheme = this.FindControl<ComboBox>("ComboTheme")
        let comboLang = this.FindControl<ComboBox>("ComboLang")
        let numFps = this.FindControl<NumericUpDown>("NumFps")
        let txtFontPath = this.FindControl<TextBox>("TxtFontPath")
        let sliderFontSize = this.FindControl<Slider>("SliderFontSize")
        
        let btnOk = this.FindControl<Button>("BtnOk")
        let btnCancel = this.FindControl<Button>("BtnCancel")
        let btnApply = this.FindControl<Button>("BtnApply")

        // Localize
        chkSingleWindow.Content <- Lang.i18n "views.settings.general.window.single"
        chkUseMpvConfig.Content <- Lang.i18n "views.settings.general.mpv.config"
        chkWatchLater.Content <- Lang.i18n "views.settings.general.mpv.watch_later"
        chkSaveWindow.Content <- Lang.i18n "views.settings.general.window.save"
        btnOk.Content <- Lang.i18n "views.settings.ok"
        btnCancel.Content <- Lang.i18n "views.settings.cancel"
        btnApply.Content <- Lang.i18n "views.settings.apply"

    member this.LoadConfig(c: PlayerConfig) =
        config <- c
        let chkSingleWindow = this.FindControl<CheckBox>("ChkSingleWindow")
        let chkUseMpvConfig = this.FindControl<CheckBox>("ChkUseMpvConfig")
        let chkWatchLater = this.FindControl<CheckBox>("ChkWatchLater")
        let chkSaveWindow = this.FindControl<CheckBox>("ChkSaveWindow")
        let numRecentLimit = this.FindControl<NumericUpDown>("NumRecentLimit")
        let comboLogLevel = this.FindControl<ComboBox>("ComboLogLevel")
        let comboTheme = this.FindControl<ComboBox>("ComboTheme")
        let comboLang = this.FindControl<ComboBox>("ComboLang")
        let numFps = this.FindControl<NumericUpDown>("NumFps")
        let txtFontPath = this.FindControl<TextBox>("TxtFontPath")
        let sliderFontSize = this.FindControl<Slider>("SliderFontSize")

        chkSingleWindow.IsChecked <- Nullable c.Window.Single
        chkUseMpvConfig.IsChecked <- Nullable c.Mpv.UseConfig
        chkWatchLater.IsChecked <- Nullable c.Mpv.WatchLater
        chkSaveWindow.IsChecked <- Nullable c.Window.Save
        numRecentLimit.Value <- Nullable (decimal c.Recent.Limit)
        numFps.Value <- Nullable (decimal c.Interface.Fps)
        txtFontPath.Text <- c.Font.Path
        sliderFontSize.Value <- float c.Font.Size

        let logLevels = [| "fatal"; "error"; "warn"; "info"; "status"; "v"; "debug"; "trace"; "no" |]
        comboLogLevel.SelectedIndex <- 
            logLevels 
            |> Array.tryFindIndex (fun x -> x = c.Debug.LogLevel) 
            |> Option.defaultValue 3
        
        let themes = [| "dark"; "light"; "system" |]
        comboTheme.SelectedIndex <- 
            themes 
            |> Array.tryFindIndex (fun x -> x = c.Interface.Theme.ToLower()) 
            |> Option.defaultValue 0

    member private this.SaveToConfig() : PlayerConfig =
        let chkSingleWindow = this.FindControl<CheckBox>("ChkSingleWindow")
        let chkUseMpvConfig = this.FindControl<CheckBox>("ChkUseMpvConfig")
        let chkWatchLater = this.FindControl<CheckBox>("ChkWatchLater")
        let chkSaveWindow = this.FindControl<CheckBox>("ChkSaveWindow")
        let numRecentLimit = this.FindControl<NumericUpDown>("NumRecentLimit")
        let comboLogLevel = this.FindControl<ComboBox>("ComboLogLevel")
        let comboTheme = this.FindControl<ComboBox>("ComboTheme")
        let numFps = this.FindControl<NumericUpDown>("NumFps")
        let txtFontPath = this.FindControl<TextBox>("TxtFontPath")
        let sliderFontSize = this.FindControl<Slider>("SliderFontSize")

        let logLevels = [| "fatal"; "error"; "warn"; "info"; "status"; "v"; "debug"; "trace"; "no" |]
        let themes = [| "dark"; "light"; "system" |]

        { config with
            Window = { config.Window with Single = chkSingleWindow.IsChecked.Value; Save = chkSaveWindow.IsChecked.Value }
            Mpv = { config.Mpv with UseConfig = chkUseMpvConfig.IsChecked.Value; WatchLater = chkWatchLater.IsChecked.Value }
            Recent = { config.Recent with Limit = int numRecentLimit.Value.Value }
            Interface = { config.Interface with Fps = int numFps.Value.Value; Theme = themes.[max 0 comboTheme.SelectedIndex] }
            Debug = { config.Debug with LogLevel = logLevels.[max 0 comboLogLevel.SelectedIndex] }
            Font = { config.Font with Path = txtFontPath.Text; Size = int sliderFontSize.Value }
        }

    static member ShowDialog(owner: Window, initialConfig: PlayerConfig) =
        let win = SettingsWindow()
        win.LoadConfig(initialConfig)
        win.FindControl<Button>("BtnOk").Click.Add(fun _ -> 
            let newConfig = win.SaveToConfig()
            win.Close(Some newConfig)
        )
        win.FindControl<Button>("BtnCancel").Click.Add(fun _ -> win.Close(None))
        owner.ShowDialog<PlayerConfig option>(win)
