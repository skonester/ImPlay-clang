namespace ImPlay.App.Controls

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Media
open Avalonia.Threading

type AudioBars() as self =
    inherit Control()

    let mutable _timer : DispatcherTimer option = None
    let _random = Random()
    let _heights = Array.init 5 (fun _ -> 0.4 + _random.NextDouble() * 0.6)
    let _barBrush = SolidColorBrush.Parse("#D0CBC7")

    static let IsActiveProperty =
        AvaloniaProperty.Register<AudioBars, bool>("IsActive", false)

    member _.IsActive
        with get() = self.GetValue(IsActiveProperty)
        and set(v) = self.SetValue(IsActiveProperty, v) |> ignore

    override _.OnPropertyChanged(change) =
        base.OnPropertyChanged(change)
        if change.Property = IsActiveProperty then
            self.UpdateTimer()
            self.InvalidateVisual()

    member private _.UpdateTimer() =
        if self.IsActive then
            if _timer.IsNone then
                let t = DispatcherTimer(TimeSpan.FromMilliseconds(100.0), DispatcherPriority.Render, (fun _ _ -> 
                    for i in 0 .. _heights.Length - 1 do
                        _heights.[i] <- 0.3 + _random.NextDouble() * 0.7
                    self.InvalidateVisual()
                ))
                _timer <- Some t
                t.Start()
        else
            _timer |> Option.iter (fun t -> t.Stop())
            _timer <- None
            self.InvalidateVisual()

    override _.Render(context) =
        let width = self.Bounds.Width
        let height = self.Bounds.Height
        if width > 0.0 && height > 0.0 then
            let barCount = 5
            let spacing = 4.0
            let barWidth = (width - float(barCount - 1) * spacing) / float barCount
            
            for i in 0 .. barCount - 1 do
                let hRatio = if self.IsActive then _heights.[i] else 0.2
                let barHeight = height * hRatio
                let x = float i * (barWidth + spacing)
                let y = height - barHeight
                context.DrawRectangle(_barBrush, null, Rect(x, y, barWidth, barHeight), 1.0, 1.0)
