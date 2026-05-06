namespace ImPlay.App.Controls

open System
open System.Collections.Generic
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Media

type SeekBar() as self =
    inherit Control()

    let trackBrush = SolidColorBrush.Parse("#554A4A4A")
    let fillBrush = SolidColorBrush.Parse("#FFFFFF")
    let chapterBrush = SolidColorBrush.Parse("#80F7F5F3")

    let mutable _isDragging = false
    let mutable _displayValue = 0.0

    static let MinimumProperty =
        AvaloniaProperty.Register<SeekBar, double>("Minimum", 0.0)

    static let MaximumProperty =
        AvaloniaProperty.Register<SeekBar, double>("Maximum", 1000.0)

    static let ValueProperty =
        AvaloniaProperty.Register<SeekBar, double>("Value", 0.0, defaultBindingMode = Data.BindingMode.TwoWay)

    static let ChapterPositionsProperty =
        AvaloniaProperty.Register<SeekBar, IReadOnlyList<double>>("ChapterPositions", [||])

    static let IsSeekingProperty =
        AvaloniaProperty.Register<SeekBar, bool>("IsSeeking", false, defaultBindingMode = Data.BindingMode.TwoWay)

    static let SeekCommittedEvent =
        RoutedEvent.Register<SeekBar, RoutedEventArgs>("SeekCommitted", RoutingStrategies.Bubble)

    static do
        Control.FocusableProperty.OverrideDefaultValue<SeekBar>(false)

    member _.Minimum
        with get() : double = self.GetValue(MinimumProperty)
        and set(v: double) = self.SetValue(MinimumProperty, v) |> ignore

    member _.Maximum
        with get() : double = self.GetValue(MaximumProperty)
        and set(v: double) = self.SetValue(MaximumProperty, v) |> ignore

    member _.Value
        with get() : double = self.GetValue(ValueProperty)
        and set(v: double) = self.SetValue(ValueProperty, v) |> ignore

    member _.ChapterPositions
        with get() : IReadOnlyList<double> = self.GetValue(ChapterPositionsProperty)
        and set(v: IReadOnlyList<double>) = self.SetValue(ChapterPositionsProperty, v) |> ignore

    member _.IsSeeking
        with get() : bool = self.GetValue(IsSeekingProperty)
        and set(v: bool) = self.SetValue(IsSeekingProperty, v) |> ignore

    member _.AddSeekCommittedHandler(handler: EventHandler<RoutedEventArgs>) =
        self.AddHandler(SeekCommittedEvent, handler) |> ignore

    member _.RemoveSeekCommittedHandler(handler: EventHandler<RoutedEventArgs>) =
        self.RemoveHandler(SeekCommittedEvent, handler) |> ignore

    override _.OnPropertyChanged(change) =
        base.OnPropertyChanged(change)
        if change.Property = ValueProperty || change.Property = MinimumProperty || change.Property = MaximumProperty || change.Property = ChapterPositionsProperty then
            if change.Property = ValueProperty && not _isDragging then
                _displayValue <- self.Value
            self.InvalidateVisual()

    override _.MeasureOverride(availableSize) =
        let width = if Double.IsInfinity(availableSize.Width) then 240.0 else availableSize.Width
        Size(width, 18.0)

    override _.OnAttachedToVisualTree(e) =
        base.OnAttachedToVisualTree(e)
        _displayValue <- self.Value

    member private _.GetRatio() =
        let range = self.Maximum - self.Minimum
        if range <= 0.0 then 0.0
        else
            let v = if _isDragging then _displayValue else self.Value
            Math.Clamp((v - self.Minimum) / range, 0.0, 1.0)

    override _.Render(context) =
        let width = self.Bounds.Width
        let height = self.Bounds.Height
        if width > 0.0 && height > 0.0 then
            let trackHeight = 5.0
            let trackY = (height - trackHeight) / 2.0
            let track = Rect(0.0, trackY, width, trackHeight)
            let radius = trackHeight / 2.0
            let ratio = self.GetRatio()
            let filled = Rect(0.0, trackY, width * ratio, trackHeight)
            
            context.DrawRectangle(trackBrush, null, track, radius, radius)
            context.DrawRectangle(fillBrush, null, filled, radius, radius)
            
            let chapters = self.ChapterPositions
            if not (isNull chapters) && chapters.Count > 0 then
                let range = self.Maximum - self.Minimum
                let tickH = 8.0
                let tickY = (height - tickH) / 2.0
                if range > 0.0 then
                    for pos in chapters do
                        if pos > self.Minimum && pos < self.Maximum then
                            let tickX = Math.Round(width * (pos - self.Minimum) / range)
                            context.DrawRectangle(chapterBrush, null, Rect(tickX - 0.5, tickY, 1.0, tickH))

    member private _.SetValueFromPointer(e: PointerEventArgs) =
        if self.Bounds.Width > 0.0 then
            let point = e.GetPosition(self)
            let ratio = Math.Clamp(point.X / self.Bounds.Width, 0.0, 1.0)
            _displayValue <- self.Minimum + ratio * (self.Maximum - self.Minimum)
            self.InvalidateVisual()

    member private _.Commit(pointer: IPointer) =
        _isDragging <- false
        self.IsSeeking <- false
        pointer.Capture(null) |> ignore
        self.Value <- _displayValue
        self.RaiseEvent(RoutedEventArgs(SeekCommittedEvent))

    override _.OnPointerPressed(e) =
        base.OnPointerPressed(e)
        if e.GetCurrentPoint(self).Properties.IsLeftButtonPressed then
            _isDragging <- true
            self.IsSeeking <- true
            e.Pointer.Capture(self) |> ignore
            self.SetValueFromPointer(e)
            e.Handled <- true

    override _.OnPointerMoved(e) =
        base.OnPointerMoved(e)
        if _isDragging then
            self.SetValueFromPointer(e)
            e.Handled <- true

    override _.OnPointerReleased(e) =
        base.OnPointerReleased(e)
        if _isDragging then
            self.SetValueFromPointer(e)
            self.Commit(e.Pointer)
            e.Handled <- true

    override _.OnPointerCaptureLost(e) =
        base.OnPointerCaptureLost(e)
        if _isDragging then
            _isDragging <- false
            self.IsSeeking <- false
            self.RaiseEvent(RoutedEventArgs(SeekCommittedEvent))
