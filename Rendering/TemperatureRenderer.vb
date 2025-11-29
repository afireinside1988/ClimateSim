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
    Private Shared Function MapTemperatureToColor(tempC As Double,
                                              tMin As Double,
                                              tMax As Double) As Color
        If tMax <= tMin Then
            tMax = tMin + 1.0 'Vermeide Division durch Null
        End If

        Dim x As Double = (tempC - tMin) / (tMax - tMin)
        If x < 0 Then x = 0
        If x > 1 Then x = 1

        'Einfaches Farbschema:
        '0 = blau, 0.5 = grünlich/gelblich, 1 = rot
        'Wir basteln eine kleine Farbverlauf-Funktion:

        Dim r As Byte
        Dim g As Byte
        Dim b As Byte

        If x < 0.5 Then
            'Blau -> Cyan -> Grün
            Dim k As Double = x / 0.5 ' 0..1
            r = 0
            g = CByte(255 * k)
            b = 255
        Else
            'Grün -> Gelb -> Rot
            Dim k As Double = (x - 0.5) / 0.5 ' 0..1
            r = 255
            g = CByte(255 * (1 - k))
            b = 0
        End If

        Return Color.FromArgb(255, r, g, b)
    End Function

End Class
