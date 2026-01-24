Imports System.Windows.Controls
Imports System.Windows.Media
Imports System.Windows.Shapes

Public Class HistoryChartRenderer

    Public Structure ChartContext
        Public MinYear As Double
        Public MaxYear As Double
        Public YearRange As Double

        Public MarginLeft As Double
        Public MarginRight As Double
        Public MarginTop As Double
        Public MarginBottom As Double
    End Structure

    ''' <summary>
    ''' Zeichnet Achsen, Gridlines und die beiden Kurven (Tglobal, CO₂) in das Canvas.
    ''' Gibt einen ChartContext zurück, der u. a. MinYear/YearRange enthält.
    ''' </summary>
    Public Shared Function RenderHistoryChart(canvas As Canvas,
                                              records As IList(Of SimulationRecord),
                                              Optional marginLeft As Double = 70,
                                              Optional marginRight As Double = 70,
                                              Optional marginTop As Double = 30,
                                              Optional marginBottom As Double = 50) As ChartContext

        Dim ctx As New ChartContext With {
            .MinYear = 0,
            .MaxYear = 0,
            .YearRange = 1,
            .MarginLeft = marginLeft,
            .MarginRight = marginRight,
            .MarginTop = marginTop,
            .MarginBottom = marginBottom
        }

        canvas.Children.Clear()

        If records Is Nothing OrElse records.Count < 2 Then
            Return ctx
        End If

        Dim width As Double = canvas.ActualWidth
        Dim height As Double = canvas.ActualHeight
        If width <= 0 OrElse height <= 0 Then
            Return ctx
        End If

        ' --- Bereich bestimmen ---
        ctx.MinYear = records.Min(Function(rec) rec.Year)
        ctx.MaxYear = records.Max(Function(rec) rec.Year)

        Dim minTemp As Double = records.Min(Function(rec) rec.GlobalMeanTempC)
        Dim maxTemp As Double = records.Max(Function(rec) rec.GlobalMeanTempC)

        Dim minCO2 As Double = records.Min(Function(rec) rec.CO2ppm)
        Dim maxCO2 As Double = records.Max(Function(rec) rec.CO2ppm)

        ' Padding auf Skalen
        Dim tempPadding As Double = (maxTemp - minTemp) * 0.05
        If tempPadding <= 0 Then tempPadding = 1
        minTemp -= tempPadding
        maxTemp += tempPadding

        Dim co2Padding As Double = (maxCO2 - minCO2) * 0.05
        If co2Padding <= 0 Then co2Padding = 10
        minCO2 -= co2Padding
        maxCO2 += co2Padding

        ctx.YearRange = If(ctx.MaxYear > ctx.MinYear, ctx.MaxYear - ctx.MinYear, 1)

        ' Plotgröße
        Dim plotWidth As Double = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight As Double = Math.Max(10, height - marginTop - marginBottom)

        ' Achsen zeichnen
        Dim axisPen As New SolidColorBrush(Colors.White)

        Dim xAxis As New Line With {
            .X1 = marginLeft,
            .Y1 = marginTop + plotHeight,
            .X2 = marginLeft + plotWidth,
            .Y2 = marginTop + plotHeight,
            .Stroke = axisPen,
            .StrokeThickness = 1
        }
        canvas.Children.Add(xAxis)

        Dim yAxis As New Line With {
            .X1 = marginLeft,
            .Y1 = marginTop,
            .X2 = marginLeft,
            .Y2 = marginTop + plotHeight,
            .Stroke = axisPen,
            .StrokeThickness = 1
        }
        canvas.Children.Add(yAxis)

        ' Achsentitel
        Dim xTitle As New TextBlock() With {
            .Text = "Jahr",
            .Foreground = Brushes.White
        }
        Canvas.SetLeft(xTitle, marginLeft + plotWidth / 2 - 15)
        Canvas.SetTop(xTitle, marginTop + plotHeight + 25)
        canvas.Children.Add(xTitle)

        Dim tempTitle As New TextBlock() With {
            .Text = "T global [°C]",
            .Foreground = Brushes.Orange
        }
        Canvas.SetLeft(tempTitle, 5)
        Canvas.SetTop(tempTitle, marginTop - 25)
        canvas.Children.Add(tempTitle)

        Dim co2Title As New TextBlock() With {
            .Text = "CO₂ [ppm]",
            .Foreground = Brushes.Cyan
        }
        Canvas.SetLeft(co2Title, marginLeft + plotWidth - 25)
        Canvas.SetTop(co2Title, marginTop - 25)
        canvas.Children.Add(co2Title)

        ' Skalenbereiche
        Dim yearRange As Double = ctx.YearRange
        Dim tempRange As Double = If(maxTemp > minTemp, maxTemp - minTemp, 1)
        Dim co2Range As Double = If(maxCO2 > minCO2, maxCO2 - minCO2, 1)

        Dim yearTickStep As Integer = ComputeYearTickStep(ctx.MinYear, ctx.MaxYear, plotWidth)
        Dim tempTickCount As Integer = 5
        Dim tempTickStep As Double = tempRange / tempTickCount
        Dim co2TickCount As Integer = 5
        Dim co2TickStep As Double = co2Range / co2TickCount

        Dim gridBrush As Brush = Brushes.DimGray
        Dim yAxisY As Double = marginTop + plotHeight

        ' X-Ticks + Gridlines
        Dim firstYearTick As Integer = CInt(Math.Ceiling(ctx.MinYear / yearTickStep)) * yearTickStep
        Dim lastYearTick As Integer = CInt(Math.Floor(ctx.MaxYear / yearTickStep)) * yearTickStep

        For yearTick As Integer = firstYearTick To lastYearTick Step yearTickStep
            Dim tNorm As Double = (yearTick - ctx.MinYear) / yearRange
            Dim x As Double = marginLeft + tNorm * plotWidth

            Dim vLine As New Line() With {
                .X1 = x,
                .Y1 = marginTop,
                .X2 = x,
                .Y2 = marginTop + plotHeight,
                .Stroke = gridBrush,
                .StrokeThickness = 0.5,
                .StrokeDashArray = New DoubleCollection({2, 2})
            }
            canvas.Children.Add(vLine)

            Dim tick As New Line() With {
                .X1 = x,
                .Y1 = yAxisY,
                .X2 = x,
                .Y2 = yAxisY + 5,
                .Stroke = Brushes.White,
                .StrokeThickness = 1
            }
            canvas.Children.Add(tick)

            Dim yearLabel As New TextBlock() With {
                .Text = $"{yearTick:F0}",
                .Foreground = Brushes.White,
                .FontSize = 10
            }
            Canvas.SetLeft(yearLabel, x - 15)
            Canvas.SetTop(yearLabel, yAxisY + 5)
            canvas.Children.Add(yearLabel)
        Next

        ' Y-Ticks links (Temperatur)
        For i As Integer = 0 To tempTickCount
            Dim tempVal As Double = minTemp + i * tempTickStep
            Dim tempNorm As Double = (tempVal - minTemp) / tempRange
            Dim y As Double = marginTop + plotHeight * (1 - tempNorm)

            Dim hLine As New Line() With {
                .X1 = marginLeft,
                .Y1 = y,
                .X2 = marginLeft + plotWidth,
                .Y2 = y,
                .Stroke = gridBrush,
                .StrokeThickness = 0.5,
                .StrokeDashArray = New DoubleCollection({2, 2})
            }
            canvas.Children.Add(hLine)

            Dim tick As New Line() With {
                .X1 = marginLeft - 5,
                .Y1 = y,
                .X2 = marginLeft,
                .Y2 = y,
                .Stroke = Brushes.Orange,
                .StrokeThickness = 1
            }
            canvas.Children.Add(tick)

            Dim tempLabel As New TextBlock() With {
                .Text = $"{tempVal:F1}",
                .Foreground = Brushes.Orange
            }
            Canvas.SetRight(tempLabel, width - (marginLeft - 8))
            Canvas.SetTop(tempLabel, y - 8)
            canvas.Children.Add(tempLabel)
        Next

        ' Y-Ticks rechts (CO₂)
        For i As Integer = 0 To co2TickCount
            Dim co2Val = minCO2 + i * co2TickStep
            Dim co2Norm = (co2Val - minCO2) / co2Range
            Dim y = marginTop + plotHeight * (1 - co2Norm)

            Dim xRight = marginLeft + plotWidth
            Dim tick As New Line() With {
                .X1 = xRight,
                .Y1 = y,
                .X2 = xRight + 5,
                .Y2 = y,
                .Stroke = Brushes.Cyan,
                .StrokeThickness = 1
            }
            canvas.Children.Add(tick)

            Dim co2Label As New TextBlock() With {
                .Text = $"{co2Val:F0}",
                .Foreground = Brushes.Cyan
            }
            Canvas.SetLeft(co2Label, xRight + 8)
            Canvas.SetTop(co2Label, y - 8)
            canvas.Children.Add(co2Label)
        Next

        ' Kurven
        Dim tempPolyline As New Polyline With {
            .Stroke = Brushes.Orange,
            .StrokeThickness = 2
        }

        Dim co2Polyline As New Polyline With {
            .Stroke = Brushes.Cyan,
            .StrokeThickness = 1.5
        }

        For Each r As SimulationRecord In records
            Dim tNorm = (r.Year - ctx.MinYear) / yearRange
            Dim x = marginLeft + tNorm * plotWidth

            Dim tempNorm = (r.GlobalMeanTempC - minTemp) / tempRange
            Dim yTemp = marginTop + plotHeight * (1 - tempNorm)
            tempPolyline.Points.Add(New Point(x, yTemp))

            Dim co2Norm = (r.CO2ppm - minCO2) / co2Range
            Dim yCO2 = marginTop + plotHeight * (1 - co2Norm)
            co2Polyline.Points.Add(New Point(x, yCO2))
        Next

        canvas.Children.Add(tempPolyline)
        canvas.Children.Add(co2Polyline)

        Return ctx
    End Function

    Public Shared Sub DrawSelectionMarker(canvas As Canvas,
                                          ctx As ChartContext,
                                          records As IList(Of SimulationRecord),
                                          index As Integer)

        If records Is Nothing OrElse records.Count < 2 Then Return
        If index < 0 OrElse index >= records.Count Then Return
        If ctx.YearRange <= 0 Then Return

        Dim width = canvas.ActualWidth
        Dim height = canvas.ActualHeight
        If width <= 0 OrElse height <= 0 Then Return

        ' alte Marker entfernen
        Dim toRemove = canvas.Children.OfType(Of Line)().
            Where(Function(l) l.Tag IsNot Nothing AndAlso l.Tag.Equals("Selection")).ToList()

        For Each l In toRemove
            canvas.Children.Remove(l)
        Next

        Dim marginLeft = ctx.MarginLeft
        Dim marginRight = ctx.MarginRight
        Dim marginTop = ctx.MarginTop
        Dim marginBottom = ctx.MarginBottom

        Dim plotWidth = Math.Max(10, width - marginLeft - marginRight)
        Dim plotHeight = Math.Max(10, height - marginTop - marginBottom)

        Dim rec = records(index)
        Dim tNorm = (rec.Year - ctx.MinYear) / ctx.YearRange
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

        canvas.Children.Add(marker)
    End Sub

    Private Shared Function ComputeYearTickStep(minYear As Double, maxYear As Double, plotWidth As Double) As Integer
        Dim yearRange As Double = maxYear - minYear
        If yearRange <= 0 OrElse plotWidth <= 0 Then
            Return 10
        End If

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
