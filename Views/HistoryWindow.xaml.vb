Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Converters
Imports System.Windows.Shapes
Imports System.Windows.Input
Imports System.Windows.Media.Media3D
Imports System.Security.Cryptography.Xml
Imports System.ComponentModel
Imports System.Collections.ObjectModel

Public Class HistoryWindow

    Private ReadOnly _engine As SimulationEngine
    Private ReadOnly _history As ObservableCollection(Of SimulationRecord)


    Private _timeStepMode As TimeStepMode = TimeStepMode.Year

    Private _isMouseDown As Boolean = False

    Private _chartContext As HistoryChartRenderer.ChartContext

    'Gemeinsame Achsen-Infos
    Private _minYear As Double
    Private _maxYear As Double
    Private _yearRange As Double

    'ViewModel
    Private _vm As HistoryViewModel
    Public Sub New(engine As SimulationEngine, timeStepMode As TimeStepMode)
        InitializeComponent()

        _engine = engine

        _vm = New HistoryViewModel(engine, timeStepMode)
        Me.DataContext = _vm

        ' Slider an die Länge der History anpassen
        If _vm.Records IsNot Nothing AndAlso _vm.Records.Count > 0 Then
            SldTime.Minimum = 0
            SldTime.Maximum = _vm.Records.Count - 1
            SldTime.Value = _vm.SelectedIndex
        Else
            SldTime.Minimum = 0
            SldTime.Maximum = 0
            SldTime.Value = 0
        End If

        AddHandler Loaded, AddressOf HistoryWindow_Loaded
        AddHandler _vm.PropertyChanged, AddressOf ViewModel_PropertyChanged

        'Canvas-Events bleiben nötig
        AddHandler CanvasChart.SizeChanged, AddressOf CanvasChart_SizeChanged
        AddHandler CanvasChart.MouseDown, AddressOf CanvasChart_MouseDown
        AddHandler CanvasChart.MouseMove, AddressOf CanvasChart_MouseMove
        AddHandler CanvasChart.MouseUp, AddressOf CanvasChart_MouseUp
        AddHandler CanvasChart.MouseLeave, AddressOf CanvasChart_MouseLeave
    End Sub

    Private Sub HistoryWindow_Loaded(sender As Object, e As RoutedEventArgs)
        RedrawChart()
        UpdateSelectionDisplay(CInt(Math.Round(SldTime.Value)))
    End Sub

    Private Sub CanvasChart_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        RedrawChart()

        Dim history = _vm.Records
        If history IsNot Nothing AndAlso history.Count > 0 Then
            Dim idx As Integer = _vm.SelectedIndex
            idx = Math.Max(0, Math.Min(history.Count - 1, idx))
            HistoryChartRenderer.DrawSelectionMarker(CanvasChart, _chartContext, history, idx)
        End If
    End Sub

    Private Sub CanvasChart_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If _vm.Records Is Nothing OrElse _vm.Records.Count < 2 Then Return

        _isMouseDown = True
        CanvasChart.CaptureMouse()

        Dim pos As Point = e.GetPosition(CanvasChart)
        UpdateSelectionFromMouseX(pos.X)
    End Sub

    Private Sub CanvasChart_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _isMouseDown Then Return
        If _vm.Records Is Nothing OrElse _vm.Records.Count < 2 Then Return

        Dim pos As Point = e.GetPosition(CanvasChart)
        UpdateSelectionFromMouseX(pos.X)
    End Sub

    Private Sub CanvasChart_MouseUp(sender As Object, e As MouseButtonEventArgs)
        If Not _isMouseDown Then Return
        _isMouseDown = False
        CanvasChart.ReleaseMouseCapture()
    End Sub

    Private Sub CanvasChart_MouseLeave(sender As Object, e As MouseEventArgs)
        If Not _isMouseDown Then Return
        CanvasChart.ReleaseMouseCapture()
    End Sub

    Private Sub RedrawChart()

        Dim history = _vm.Records
        _chartContext = HistoryChartRenderer.RenderHistoryChart(CanvasChart, history)

    End Sub

    Private Sub UpdateSelectionDisplay(index As Integer)
        If index < 0 OrElse index >= _vm.Records.Count Then Return

        Dim r = _vm.Records(index)
        _vm.SelectedYearText = TimeFormatting.FormatYearWithStepMode(r.Year, _timeStepMode)
        _vm.SelectedTempText = $"{r.GlobalMeanTempC:F2} °C"
        _vm.SelectedCO2Text = $"{r.CO2ppm:F0} ppm"
    End Sub

    Private Sub UpdateSelectionFromMouseX(mouseX As Double)
        Dim history = _vm.Records
        If history Is Nothing OrElse history.Count < 2 Then Return

        Dim width = CanvasChart.ActualWidth
        Dim height = CanvasChart.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return

        Dim marginLeft As Double = _chartContext.MarginLeft
        Dim marginRight As Double = _chartContext.MarginRight

        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotLeft As Double = marginLeft
        Dim plotRight As Double = marginLeft + plotWidth

        Dim xClamped As Double = Clamp(mouseX, plotLeft, plotRight)
        Dim tnorm As Double = (xClamped - plotLeft) / plotWidth
        tnorm = Clamp(tnorm, 0.0, 1.0)

        Dim minIndex As Double = SldTime.Minimum
        Dim maxIndex As Double = SldTime.Maximum

        Dim idxDouble As Double = minIndex + tNorm * (maxIndex - minIndex)
        Dim idx As Integer = CInt(Math.Round(idxDouble))
        idx = Math.Max(0, Math.Min(history.Count - 1, idx))

        _vm.SelectedIndex = idx
    End Sub

    Private Sub ViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(HistoryViewModel.SelectedIndex) Then
            Dim history = _vm.Records
            If history IsNot Nothing AndAlso history.Count > 0 Then
                HistoryChartRenderer.DrawSelectionMarker(CanvasChart, _chartContext, history, _vm.SelectedIndex)
            End If

            If TypeOf Me.Owner Is MainWindow Then
                Dim main = DirectCast(Me.Owner, MainWindow)
                main.RefreshFromEngine()
            End If
        End If
    End Sub

End Class
