Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Converters
Imports System.Windows.Shapes
Imports System.Windows.Input
Imports System.Windows.Media.Media3D
Imports System.Security.Cryptography.Xml

Public Class HistoryWindow

    Private ReadOnly _engine As SimulationEngine
    Private ReadOnly _history As List(Of SimulationRecord)

    Private _timeStepMode As TimeStepMode = TimeStepMode.Year

    Private _isMouseDown As Boolean = False

    'Gemeinsame Layout-Parameter
    Private _marginLeft As Double = 70 '50
    Private _marginRight As Double = 70 '70
    Private _marginTop As Double = 30 '30
    Private _marginBottom As Double = 50 '50

    'Gemeinsame Achsen-Infos
    Private _minYear As Double
    Private _maxYear As Double
    Private _yearRange As Double

    Public Sub New(engine As SimulationEngine, timeStepMode As TimeStepMode)
        InitializeComponent()

        'Kopie der Liste, damit Änderungen im MainWindow nicht direkt während des Zeichnens reinfunken
        _engine = engine
        _history = engine.History

        _timeStepMode = timeStepMode

        If _history.Count > 1 Then
            SldTime.Minimum = 0
            SldTime.Maximum = _history.Count - 1
            SldTime.Value = _history.Count - 1
        Else
            SldTime.Minimum = 0
            SldTime.Maximum = 0
            SldTime.Value = 0
        End If

        AddHandler Loaded, AddressOf HistoryWindow_Loaded
        AddHandler SldTime.ValueChanged, AddressOf SldTime_ValueChanged
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

    Private Sub SldTime_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        If _history Is Nothing OrElse _history.Count = 0 Then Return

        Dim idx As Integer = CInt(Math.Round(SldTime.Value))
        idx = Math.Max(0, Math.Min(_history.Count - 1, idx))
        UpdateSelectionDisplay(idx)
        DrawSelectionMarker(idx)

        '>>> Klima-Modell auf diesen Stand setzen <<<
        If _engine IsNot Nothing Then
            _engine.JumpToIndex(idx)

            'Hauptfenster aktualisieren
            If TypeOf Me.Owner Is MainWindow Then
                Dim main = DirectCast(Me.Owner, MainWindow)
                main.RefreshFromEngine()
            End If
        End If

    End Sub

    Private Sub CanvasChart_SizeChanged(sender As Object, e As SizeChangedEventArgs)
        'Bei Größenänderung Diagramm neu zeichnen + Marker aktualisieren

        RedrawChart()

        If _history IsNot Nothing AndAlso _history.Count > 0 Then
            Dim idx As Integer = CInt(Math.Round(SldTime.Value))
            idx = Math.Max(0, Math.Min(_history.Count - 1, idx))
            DrawSelectionMarker(idx)
        End If
    End Sub

    Private Sub CanvasChart_MouseDown(sender As Object, e As MouseButtonEventArgs)
        If _history Is Nothing OrElse _history.Count < 2 Then Return

        _isMouseDown = True
        CanvasChart.CaptureMouse()

        Dim pos As Point = e.GetPosition(CanvasChart)
        UpdateSelectionFromMouseX(pos.X)
    End Sub

    Private Sub CanvasChart_MouseMove(sender As Object, e As MouseEventArgs)
        If Not _isMouseDown Then Return
        If _history Is Nothing OrElse _history.Count < 2 Then Return

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
        CanvasChart.Children.Clear()
        If _history Is Nothing OrElse _history.Count < 2 Then Return

        Dim width As Double = CanvasChart.ActualWidth
        Dim height As Double = CanvasChart.ActualHeight

        If width <= 0 OrElse height <= 0 Then Return

        '--- Bereich bestimmen ---
        _minYear = _history.Min(Function(rec) rec.Year)
        _maxYear = _history.Max(Function(rec) rec.Year)

        Dim minTemp As Double = _history.Min(Function(rec) rec.GlobalMeanTempC)
        Dim maxTemp As Double = _history.Max(Function(rec) rec.GlobalMeanTempC)

        Dim minCO2 As Double = _history.Min(Function(rec) rec.CO2ppm)
        Dim maxCO2 As Double = _history.Max(Function(rec) rec.CO2ppm)

        '---- Ein bisschen Padding auf den Skalen, damit Kurven nicht genau am Rand kleben ---
        Dim tempPadding As Double = (maxTemp - minTemp) * 0.05
        If tempPadding <= 0 Then tempPadding = 1
        minTemp -= tempPadding
        maxTemp += tempPadding

        Dim co2Padding As Double = (maxCO2 - minCO2) * 0.05
        If co2Padding <= 0 Then co2Padding = 10
        minCO2 -= co2Padding
        maxCO2 += co2Padding

        _yearRange = If(_maxYear > _minYear, _maxYear - _minYear, 1)

        '--- Ränder (etwas größer, damit Platz für Labels ist) ---
        Dim marginLeft As Double = _marginLeft
        Dim marginRight As Double = _marginRight
        Dim marginTop As Double = _marginTop
        Dim marginBottom As Double = _marginBottom

        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight As Double = Math.Max(10, height - marginTop - marginBottom)

        'Achsen zeichnen
        Dim axisPen As New SolidColorBrush(Colors.White)

        'X-Achse
        Dim xAxis As New Line With {
            .X1 = marginLeft,
            .Y1 = marginTop + plotHeight,
            .X2 = marginLeft + plotWidth,
            .Y2 = marginTop + plotHeight,
            .Stroke = axisPen,
            .StrokeThickness = 1
        }
        CanvasChart.Children.Add(xAxis)

        'Y-Achse
        Dim yAxis As New Line With {
            .X1 = marginLeft,
            .Y1 = marginTop,
            .X2 = marginLeft,
            .Y2 = marginTop + plotHeight,
            .Stroke = axisPen,
            .StrokeThickness = 1
        }
        CanvasChart.Children.Add(yAxis)

        '--- Achsentitel (farblich passend zu den Kurven) ---

        'X-Achse
        Dim xTitle As New TextBlock() With {
            .Text = "Jahr",
            .Foreground = Brushes.White
            }
        Canvas.SetLeft(xTitle, marginLeft + plotWidth / 2 - 15)
        Canvas.SetTop(xTitle, marginTop + plotHeight + 25)
        CanvasChart.Children.Add(xTitle)

        'Y-Achse links: Temperatur
        Dim tempTitle As New TextBlock() With {
            .Text = "T global [°C]",
            .Foreground = Brushes.Orange
            }
        Canvas.SetLeft(tempTitle, 5)
        Canvas.SetTop(tempTitle, marginTop - 25)
        CanvasChart.Children.Add(tempTitle)

        'Y-Achse rechts: CO2
        Dim co2Title As New TextBlock() With {
            .Text = "CO₂ [ppm]",
            .Foreground = Brushes.Cyan
            }
        Canvas.SetLeft(co2Title, marginLeft + plotWidth - 25)
        Canvas.SetTop(co2Title, marginTop - 25)
        CanvasChart.Children.Add(co2Title)

        '--- Skalenbereiche & Hilfsfunktionen ---
        Dim yearRange As Double = _yearRange
        Dim tempRange As Double = If(maxTemp > minTemp, maxTemp - minTemp, 1)
        Dim co2Range As Double = If(maxCO2 > minCO2, maxCO2 - minCO2, 1)

        'Hilfsfunktionen für "schöne" Schrittweiten
        Dim yearTickStep As Integer = ComputeYearTickStep(_minYear, _maxYear, plotWidth)

        Dim tempTickCount As Integer = 5 'Anzahl der Temperatur-Ticks
        Dim tempTickStep As Double = tempRange / tempTickCount

        Dim co2TickCount As Integer = 5 'Anzahl der CO2-Ticks
        Dim co2TickStep As Double = co2Range / co2TickCount

        '--- Gridlines & Haupt-Ticks X-Achse ---
        Dim gridBrush As Brush = Brushes.DimGray
        Dim yAxisY As Double = marginTop + plotHeight

        'Ersten Tick auf ein Vielfaches von yearTickStep runden
        Dim firstYearTick As Integer = CInt(Math.Ceiling(_minYear / yearTickStep)) * yearTickStep
        Dim lastYearTick As Integer = CInt(Math.Floor(_maxYear / yearTickStep)) * yearTickStep

        For yearTick As Integer = firstYearTick To lastYearTick Step yearTickStep
            Dim tNorm As Double = (yearTick - _minYear) / yearRange
            Dim x As Double = marginLeft + tNorm * plotWidth

            'vertikale Gridline
            Dim vLine As New Line() With {
                .X1 = x,
                .Y1 = marginTop,
                .X2 = x,
                .Y2 = marginTop + plotHeight,
                .Stroke = gridBrush,
                .StrokeThickness = 0.5,
                .StrokeDashArray = New DoubleCollection({2, 2})
            }
            CanvasChart.Children.Add(vLine)

            'Tick auf X-Achse
            Dim tick As New Line() With {
                .X1 = x,
                .Y1 = yAxisY,
                .X2 = x,
                .Y2 = yAxisY + 5,
                .Stroke = Brushes.White,
                .StrokeThickness = 1
            }
            CanvasChart.Children.Add(tick)

            'Jahr-Label
            Dim yearLabel As New TextBlock() With {
                .Text = $"{yearTick:F0}",
                .Foreground = Brushes.White,
                .FontSize = 10
                }
            'grob zentrieren; ActualWidth ist hier oft noch 0, daher fixe Verschiebung
            Canvas.SetLeft(yearLabel, x - 15)
            Canvas.SetTop(yearLabel, yAxisY + 5)
            CanvasChart.Children.Add(yearLabel)
        Next

        '--- Gridlines & Ticks Y-Achse links (Temperatur) ---
        For i As Integer = 0 To tempTickCount
            Dim tempVal As Double = minTemp + i * tempTickStep
            Dim tempNorm As Double = (tempVal - minTemp) / tempRange
            Dim y As Double = marginTop + plotHeight * (1 - tempNorm)

            'horizontale Gridline
            Dim hLine As New Line() With {
                .X1 = marginLeft,
                .Y1 = y,
                .X2 = marginLeft + plotWidth,
                .Y2 = y,
                .Stroke = gridBrush,
                .StrokeThickness = 0.5,
                .StrokeDashArray = New DoubleCollection({2, 2})
                }
            CanvasChart.Children.Add(hLine)

            'Tick an Y-Achse links
            Dim tick As New Line() With {
                .X1 = marginLeft - 5,
                .Y1 = y,
                .X2 = marginLeft,
                .Y2 = y,
                .Stroke = Brushes.Orange,
                .StrokeThickness = 1
                }
            CanvasChart.Children.Add(tick)

            'Label links (Temperatur)
            Dim tempLabel As New TextBlock() With {
                .Text = $"{tempVal:F1}",
                .Foreground = Brushes.Orange
                }
            Canvas.SetRight(tempLabel, width - (marginLeft - 8)) 'ungefähr linksbündig neben Achse
            Canvas.SetTop(tempLabel, y - 8)
            CanvasChart.Children.Add(tempLabel)
        Next

        ' --- Ticks Y-Achse rechts (CO2), ohne zweite Gridlines (wir nutzen dieselben) ---
        For i = 0 To co2TickCount
            Dim co2Val = minCO2 + i * co2TickStep
            Dim co2Norm = (co2Val - minCO2) / co2Range
            Dim y = marginTop + plotHeight * (1 - co2Norm)

            ' Tick rechts
            Dim xRight = marginLeft + plotWidth
            Dim tick As New Line() With {
            .X1 = xRight,
            .Y1 = y,
            .X2 = xRight + 5,
            .Y2 = y,
            .Stroke = Brushes.Cyan,
            .StrokeThickness = 1
        }
            CanvasChart.Children.Add(tick)

            ' Label rechts (CO2)
            Dim co2Label As New TextBlock() With {
            .Text = $"{co2Val:F0}",
            .Foreground = Brushes.Cyan
        }
            Canvas.SetLeft(co2Label, xRight + 8)
            Canvas.SetTop(co2Label, y - 8)
            CanvasChart.Children.Add(co2Label)
        Next

        '--- Kurven zeichnen ---
        Dim tempPolyline As New Polyline With {
            .Stroke = Brushes.Orange,
            .StrokeThickness = 2
        }

        Dim co2Polyline As New Polyline With {
            .Stroke = Brushes.Cyan,
            .StrokeThickness = 1.5
        }

        For Each r As SimulationRecord In _history
            Dim tNorm = (r.Year - _minYear) / yearRange
            Dim x = marginLeft + tNorm * plotWidth

            'Temperatur -> linke Y-Achse
            Dim tempNorm = (r.GlobalMeanTempC - minTemp) / tempRange
            Dim yTemp = marginTop + plotHeight * (1 - tempNorm)

            tempPolyline.Points.Add(New Point(x, yTemp))

            'CO2 -> rechte Y-Achse (gleicher Plot, andere Skalierung)
            Dim co2Norm = (r.CO2ppm - minCO2) / co2Range
            Dim yCO2 = marginTop + plotHeight * (1 - co2Norm)

            co2Polyline.Points.Add(New Point(x, yCO2))
        Next

        CanvasChart.Children.Add(tempPolyline)
        CanvasChart.Children.Add(co2Polyline)

    End Sub

    Private Sub UpdateSelectionDisplay(index As Integer)
        If index < 0 OrElse index >= _history.Count Then Return

        Dim r = _history(index)
        TxtSelectedYear.Text = TimeFormatting.FormatYearWithStepMode(r.Year, _timeStepMode)
        TxtSelectedTemp.Text = $"{r.GlobalMeanTempC:F2} °C"
        TxtSelectedCO2.Text = $"{r.CO2ppm:F0} ppm"
    End Sub

    Private Sub UpdateSelectionFromMouseX(mouseX As Double)
        If _history Is Nothing OrElse _history.Count < 2 Then Return

        Dim width = CanvasChart.ActualWidth
        Dim height = CanvasChart.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return

        'dieselben Margins wie in RedrawChart/DrawSelectionMarker
        Dim marginLeft As Double = _marginLeft
        Dim marginRight As Double = _marginRight

        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotLeft As Double = marginLeft
        Dim plotRight As Double = marginLeft + plotWidth

        'Maus-X auf Plotbereich clampen
        Dim xClamped = Math.Max(plotLeft, Math.Min(plotRight, mouseX))

        '0..1 innerhalb des Plots
        Dim tNorm = (xClamped - plotLeft) / plotWidth
        If tNorm < 0 Then tNorm = 0
        If tNorm > 1 Then tNorm = 1

        'tNorm -> Index im Verlauf (über den Sliderbereich, der ja 0..Count-1 ist)
        Dim minIndex As Double = SldTime.Minimum
        Dim maxIndex As Double = SldTime.Maximum

        Dim idxDouble As Double = minIndex + tNorm * (maxIndex - minIndex)
        Dim idx As Integer = CInt(Math.Round(idxDouble))

        idx = Math.Max(0, Math.Min(_history.Count - 1, idx))

        'Slider setzen - der löst dann automatisch SldTime_ValueChanged aus,
        'das wiederum UpdateSelectionDisplay + DrawSelectionMarker aufruft
        SldTime.Value = idx
    End Sub

    Private Sub DrawSelectionMarker(index As Integer)
        'Vorherige Selection-Linien entfernen
        Dim toRemove = CanvasChart.Children.OfType(Of Line)().Where(Function(l) l.Tag IsNot Nothing AndAlso l.Tag.Equals("Selection")).ToList()

        For Each l In toRemove
            CanvasChart.Children.Remove(l)
        Next

        If _history Is Nothing OrElse _history.Count < 2 Then Return

        Dim width = CanvasChart.ActualWidth
        Dim height = CanvasChart.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return

        ' dieselben Margins wie der Plot (aber aus Feldern)
        Dim marginLeft As Double = _marginLeft
        Dim marginRight As Double = _marginRight
        Dim marginTop As Double = _marginTop
        Dim marginBottom As Double = _marginBottom

        Dim plotWidth = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight = Math.Max(10, height - marginTop - marginBottom)

        'Achseninfos aus Feldern (von RedrawChart gesetzt)
        If _yearRange <= 0 Then Return

        If index < 0 OrElse index >= _history.Count Then Return
        Dim rec As SimulationRecord = _history(index)

        Dim tNorm = (rec.Year - _minYear) / _yearRange
        Dim x As Double = marginLeft + tNorm * plotWidth

        Dim marker As New Line() With {
            .X1 = x,
            .Y1 = marginTop,
            .X2 = x,
            .Y2 = marginTop + plotHeight,
            .Stroke = Brushes.White,
            .StrokeThickness = 1,
            .StrokeDashArray = New DoubleCollection({2, 2}),
            .Tag = "Selection"
            }

        CanvasChart.Children.Add(marker)
    End Sub

    Private Function ComputeYearTickStep(minYear As Double, maxYear As Double, plotWidth As Double) As Integer
        Dim yearRange As Double = maxYear - minYear
        If yearRange <= 0 OrElse plotWidth <= 0 Then
            Return 10
        End If

        'grob 60px Platz pro Label
        Dim approxLabelWidthPx As Double = 60.0
        Dim maxLabels As Double = Math.Max(2.0, plotWidth / approxLabelWidthPx)

        Dim roughStep As Double = yearRange / maxLabels

        Dim candidates() As Integer = {1, 2, 5, 10, 20, 50, 100, 200, 500, 1000}

        For Each c As Integer In candidates
            If c >= roughStep Then
                Return c
            End If
        Next

        Return candidates(candidates.Length - 1)
    End Function

End Class
