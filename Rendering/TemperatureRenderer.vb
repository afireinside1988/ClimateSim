Imports System.Configuration
Imports System.Data.Common
Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public Class TemperatureRenderer

    ''' <summary>
    ''' Rendert das Temperaturfeld als WritableBitmap.
    ''' tempRangeCelsiusMin/Max bestimmen die Farbkodierung.
    ''' </summary>

    Public Shared Function RenderTemperatureField(grid As ClimateGrid,
                                                  tempRangeCelsiusMin As Double,
                                                  tempRangeCelsiusMax As Double) As WriteableBitmap

        Dim width As Integer = grid.Width
        Dim height As Integer = grid.Height

        Dim dpi As Double = 96 'Auflösung des Renderings
        Dim wb As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim stride As Integer = width * 4 '4 Bytes pro Pixel (BGRA)
        Dim pixels(width * height * 4 - 1) As Byte

        For latIndex As Integer = 0 To height - 1
            For lonIndex As Integer = 0 To width - 1

                Dim cell = grid.GetCell(latIndex, lonIndex)
                Dim tempC As Double = cell.TemperatureK - 273.15 'Kelvin -> Celsius

                Dim color As Color = MapTemperatureToColor(tempC, tempRangeCelsiusMin, tempRangeCelsiusMax)

                Dim pixelIndex As Integer = (latIndex * width + lonIndex) * 4
                pixels(pixelIndex + 0) = color.B 'Blue
                pixels(pixelIndex + 1) = color.G 'Green
                pixels(pixelIndex + 2) = color.R 'Red
                pixels(pixelIndex + 3) = color.A 'Alpha

            Next
        Next

        Dim rect As New Int32Rect(0, 0, width, height)
        wb.WritePixels(rect, pixels, stride, 0)
        Return wb

    End Function

    ''' <summary>
    ''' Mappt eine Temperatur in °C auf eine Farbe.
    ''' Einfaches lineares Mapping von Blau (kalt) über Grün zu Rot (heiß).
    ''' </summary>
    Private Shared Function MapTemperatureToColor(tempC As Double, tMin As Double, tMax As Double) As Color

        If tMax <= tMin Then
            tMax = tMin + 1.0 'Vermeide Division durch Null
        End If

        'Noramierung auf 0..1
        Dim x As Double = (tempC - tMin) / (tMax - tMin)
        If x < 0 Then x = 0
        If x > 1 Then x = 1

        'Farb-Stützpunkte (x, R, G, B)
        Dim stops As (Pos As Double, R As Integer, G As Integer, B As Integer)() =
            {
                (0.0, 60, 0, 90),   'sehr kalt: dunkellila
                (0.1, 180, 0, 120),   'lila/pink
                (0.25, 0, 0, 130),   'dunkelblau
                (0.35, 120, 180, 255),   'hellblau
                (0.5, 170, 255, 170),   'hellgrün (um 0°C-Bereich)
                (0.6, 0, 170, 0),   'sattes grün
                (0.7, 255, 240, 0),   'gelb
                (0.8, 255, 170, 0),   'orange
                (0.9, 230, 40, 0),   'rot
                (1.0, 50, 0, 0)    'sehr dunkles Rot/fast schwarz
            }

        'passenden Intervall finden
        Dim i As Integer = 0
        While i < stops.Length - 1 AndAlso x > stops(i + 1).Pos
            i += 1
        End While

        Dim s0 = stops(i)
        Dim s1 = stops(Math.Min(i + 1, stops.Length - 1))

        Dim span As Double = s1.Pos - s0.Pos
        Dim k As Double

        If span <= 0 Then
            k = 0
        Else
            k = (x - s0.Pos) / span
        End If

        Dim r As Integer = CInt(Math.Round(s0.R + (s1.R - s0.R) * k))
        Dim g As Integer = CInt(Math.Round(s0.G + (s1.G - s0.G) * k))
        Dim b As Integer = CInt(Math.Round(s0.B + (s1.B - s0.B) * k))

        Return Color.FromArgb(255, CByte(r), CByte(g), CByte(b))
    End Function

End Class
