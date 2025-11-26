Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class HistoryWindow

    Private ReadOnly _history As List(Of SimulationRecord)

    Public Sub New(history As List(Of SimulationRecord))
        InitializeComponent()

        'Kopie der Liste, damit Änderungen im MainWindow nicht direkt während des Zeichnens reinfunken
        _history = New List(Of SimulationRecord)(history)

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

    Private Sub RedrawChart()
        CanvasChart.Children.Clear()
        If _history Is Nothing OrElse _history.Count < 2 Then Return

        Dim width As Double = CanvasChart.ActualWidth
        Dim height As Double = CanvasChart.ActualHeight

        If width <= 0 OrElse height <= 0 Then Return

        'Bereich bestimmen
        Dim minYear As Double = _history.Min(Function(rec) rec.Year)
        Dim maxYear As Double = _history.Max(Function(rec) rec.Year)

        Dim minTemp As Double = _history.Min(Function(rec) rec.GlobalMeanTempC)
        Dim maxTemp As Double = _history.Max(Function(rec) rec.GlobalMeanTempC)

        Dim minCO2 As Double = _history.Min(Function(rec) rec.CO2ppm)
        Dim maxCO2 As Double = _history.Max(Function(rec) rec.CO2ppm)

        'kleiner Rand
        Dim marginLeft As Double = 50
        Dim marginRight As Double = 20
        Dim marginTop As Double = 20
        Dim marginBottom As Double = 40

        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight As Double = Math.Max(10, height - marginTop - marginBottom)

        'Achsen zeichnen
        Dim axisPen As New SolidColorBrush(Colors.Gray)

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

        'Label für Y-Achse Temp (links)
        Dim tempLabel As New TextBlock With {
            .Text = "T global [°C]",
            .Foreground = Brushes.White
            }
        Canvas.SetLeft(tempLabel, marginLeft)
        Canvas.SetTop(tempLabel, 0)
        CanvasChart.Children.Add(tempLabel)

        'Label für Y-Achse CO2 (rechts)
        Dim co2Label As New TextBlock With {
            .Text = "CO2 [ppm]",
            .Foreground = Brushes.White
            }
        Canvas.SetLeft(co2Label, marginLeft + plotWidth - 40)
        Canvas.SetTop(co2Label, 0)
        CanvasChart.Children.Add(co2Label)

        '---
        ' X-Achse beschriften (Jahr)
        Dim xLabel As New TextBlock() With {
    .Text = "Jahr",
    .Foreground = Brushes.White
}
        ' mittig unter der X-Achse
        Canvas.SetLeft(xLabel, marginLeft + plotWidth / 2 - 15)
        Canvas.SetTop(xLabel, marginTop + plotHeight + 5)
        CanvasChart.Children.Add(xLabel)

        ' Min-/Max-Jahr an den Enden der X-Achse
        Dim minYearLabel As New TextBlock() With {
    .Text = $"{minYear:F0}",
    .Foreground = Brushes.White
}
        Canvas.SetLeft(minYearLabel, marginLeft - 5)
        Canvas.SetTop(minYearLabel, marginTop + plotHeight + 20)
        CanvasChart.Children.Add(minYearLabel)

        Dim maxYearLabel As New TextBlock() With {
    .Text = $"{maxYear:F0}",
    .Foreground = Brushes.White
}
        Canvas.SetLeft(maxYearLabel, marginLeft + plotWidth - 20)
        Canvas.SetTop(maxYearLabel, marginTop + plotHeight + 20)
        CanvasChart.Children.Add(maxYearLabel)

        ' Y-Achse links: Temp-Skala
        Dim tempMinLabel As New TextBlock() With {
    .Text = $"{minTemp:F1} °C",
    .Foreground = Brushes.White
}
        Canvas.SetLeft(tempMinLabel, 0)
        Canvas.SetTop(tempMinLabel, marginTop + plotHeight - 10)
        CanvasChart.Children.Add(tempMinLabel)

        Dim tempMaxLabel As New TextBlock() With {
    .Text = $"{maxTemp:F1} °C",
    .Foreground = Brushes.White
}
        Canvas.SetLeft(tempMaxLabel, 0)
        Canvas.SetTop(tempMaxLabel, marginTop - 10)
        CanvasChart.Children.Add(tempMaxLabel)

        ' Y-Achse rechts: CO₂-Skala
        Dim co2MinLabel As New TextBlock() With {
    .Text = $"{minCO2:F0} ppm",
    .Foreground = Brushes.LightGray
}
        Canvas.SetLeft(co2MinLabel, marginLeft + plotWidth + 5)
        Canvas.SetTop(co2MinLabel, marginTop + plotHeight - 10)
        CanvasChart.Children.Add(co2MinLabel)

        Dim co2MaxLabel As New TextBlock() With {
    .Text = $"{maxCO2:F0} ppm",
    .Foreground = Brushes.LightGray
}
        Canvas.SetLeft(co2MaxLabel, marginLeft + plotWidth + 5)
        Canvas.SetTop(co2MaxLabel, marginTop - 10)
        CanvasChart.Children.Add(co2MaxLabel)
        '---

        'Hilfsfunktionen zum Umrechnen
        Dim yearRange As Double = If(maxYear > minYear, maxYear - minYear, 1)
        Dim tempRange As Double = If(maxTemp > minTemp, maxTemp - minTemp, 1)
        Dim co2Range As Double = If(maxCO2 > minCO2, maxCO2 - minCO2, 1)

        Dim tempPolyline As New Polyline With {
            .Stroke = Brushes.Orange,
            .StrokeThickness = 2
        }

        Dim co2Polyline As New Polyline With {
            .Stroke = Brushes.Cyan,
            .StrokeThickness = 1.5
        }

        For Each r As SimulationRecord In _history
            Dim tNorm = (r.Year - minYear) / yearRange
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
        TxtSelectedYear.Text = $"{r.Year:F1}"
        TxtSelectedTemp.Text = $"{r.GlobalMeanTempC:F2} °C"
        TxtSelectedCO2.Text = $"{r.CO2ppm:F0} ppm"
    End Sub

    Private Sub DrawSelectionMarker(index As Integer)
        CanvasChart.Children.OfType(Of Line).Where(Function(l) l.Tag IsNot Nothing AndAlso l.Tag.Equals("Selection")).ToList().ForEach(Sub(l) CanvasChart.Children.Remove(l))

        If _history.Count < 2 Then Return

        Dim width As Double = CanvasChart.ActualWidth
        Dim height As Double = CanvasChart.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return

        Dim marginLeft As Double = 50
        Dim marginRight As Double = 20
        Dim marginTop As Double = 20
        Dim marginBottom As Double = 40

        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight As Double = Math.Max(10, height - marginTop - marginBottom)

        Dim minYear = _history.Min(Function(rec) rec.Year)
        Dim maxYear = _history.Max(Function(rec) rec.Year)
        Dim yearRange As Double = If(maxYear > minYear, maxYear - minYear, 1)

        Dim r = _history(index)
        Dim tNorm = (r.Year - minYear) / yearRange
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

End Class
