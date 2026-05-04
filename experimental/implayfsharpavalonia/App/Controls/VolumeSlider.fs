namespace ImPlay.App.Controls

open System
open Avalonia
open Avalonia.Controls
open Avalonia.Input
open Avalonia.Interactivity
open Avalonia.Media

type VolumeSlider() as self =
    inherit Control()

    let trackBrush = SolidColorBrush.Parse("#4A4A4A")
    let fillBrush = SolidColorBrush.Parse("#FFFFFF")

    let mutable _isDragging = false

    static let MinimumProperty =
        AvaloniaProperty.Register<VolumeSlider, double>("Minimum", 0.0)

    static let MaximumProperty =
        AvaloniaProperty.Register<VolumeSlider, double>("Maximum", 100.0)

    static let ValueProperty =
        AvaloniaProperty.Register<VolumeSlider, double>("Value", 50.0, defaultBindingMode = Data.BindingMode.TwoWay)

    static do
        Control.FocusableProperty.OverrideDefaultValue<VolumeSlider>(false)

    member _.Minimum
        with get() : double = self.GetValue(MinimumProperty)
        and set(v: double) = self.SetValue(MinimumProperty, v) |> ignore

    member _.Maximum
        with get() : double = self.GetValue(MaximumProperty)
        and set(v: double) = self.SetValue(MaximumProperty, v) |> ignore

    member _.Value
        with get() : double = self.GetValue(ValueProperty)
        and set(v: double) = self.SetValue(ValueProperty, v) |> ignore

    override _.OnPropertyChanged(change) =
        base.OnPropertyChanged(change)
        if change.Property = ValueProperty || change.Property = MinimumProperty || change.Property = MaximumProperty then
            self.InvalidateVisual()

    override _.MeasureOverride(availableSize) =
        Size(Math.Max(availableSize.Width, 60.0), 18.0)

    override _.Render(context) =
        let width = self.Bounds.Width
        let height = self.Bounds.Height
        if width > 0.0 && height > 0.0 then
            let trackHeight = 4.0
            let trackY = (height - trackHeight) / 2.0
            let track = Rect(0.0, trackY, width, trackHeight)
            let radius = 2.0
            
            let range = self.Maximum - self.Minimum
            let ratio = if range <= 0.0 then 0.0 else Math.Clamp((self.Value - self.Minimum) / range, 0.0, 1.0)
            let filled = Rect(0.0, trackY, width * ratio, trackHeight)
            
            context.DrawRectangle(trackBrush, null, track, radius, radius)
            context.DrawRectangle(fillBrush, null, filled, radius, radius)
            
            context.DrawEllipse(fillBrush, null, Point(width * ratio, height / 2.0), 5.0, 5.0)

    member private _.SetValueFromPointer(e: PointerEventArgs) =
        if self.Bounds.Width > 0.0 then
            let point = e.GetPosition(self)
            let ratio = Math.Clamp(point.X / self.Bounds.Width, 0.0, 1.0)
            self.Value <- self.Minimum + ratio * (self.Maximum - self.Minimum)

    override _.OnPointerPressed(e) =
        base.OnPointerPressed(e)
        if e.GetCurrentPoint(self).Properties.IsLeftButtonPressed then
            _isDragging <- true
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
            _isDragging <- false
            e.Pointer.Capture(null) |> ignore
            e.Handled <- true
