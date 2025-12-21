Imports System.Printing

Public Structure GeoExtent
    Public Property LatMin As Double
    Public Property LatMax As Double
    Public Property LonMin As Double
    Public Property LonMax As Double

    Public Sub New(latMin As Double, latMax As Double, lonMin As Double, lonMax As Double)
        Me.LatMin = latMin
        Me.LatMax = latMax
        Me.LonMin = lonMin
        Me.LonMax = lonMax
    End Sub

    Public Shared ReadOnly Property World As GeoExtent
        Get
            Return New GeoExtent(-90.0, 90.0, -180.0, 180.0)
        End Get
    End Property
End Structure

''' <summary>
''' Minimaler Provider-basierter Renderer (C1)
''' *Best-of-Both*: Public minimal, intern schon mit Extent erweiterbar.
''' </summary>
Public Class EarthSurfaceRenderer

    Public Shared Function RenderSurfaceTypeWorld(provider As IEarthSurfaceProvider,
                                                  width As Integer,
                                                  height As Integer,
                                                  Optional dpi As Double = 96.0) As WriteableBitmap

        Return RenderSurfaceType(provider, width, height, GeoExtent.World, dpi)

    End Function

    Friend Shared Function RenderSurfaceType(provider As IEarthSurfaceProvider,
                                             width As Integer,
                                             height As Integer,
                                             extent As GeoExtent,
                                             dpi As Double) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(provider)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height)
        If dpi <= 0 Then dpi = 96.0     'Wenn DPI ungültig auf Default setzen

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        Dim lonSpan As Double = extent.LonMax - extent.LonMin
        Dim latSpan As Double = extent.LatMax - extent.LatMin

        'Pixeltzentren -> Geo (half-cell-centered Logik)
        Dim dLonPerPx As Double = lonSpan / width
        Dim dLatPerPx As Double = latSpan / height

        Dim idx As Integer = 0

        For y As Integer = 0 To height - 1

            'y=0 ist oben -> lat nahe LatMax
            Dim lat As Double = extent.LatMax - (y + 0.5) * dLatPerPx

            For x As Integer = 0 To width - 1

                'DEBUG:
                'Dim lon As Double = extent.LonMax + (x + 0.5) * dLonPerPx
                Dim lon As Double = extent.LonMin + (x + 0.5) * dLonPerPx

                'Provider-Contract: liefert SurfaceInfo für Geo
                Dim info As SurfaceInfo = provider.GetSurfaceInfo(lat, lon)

                Dim c As Color = ColorForSurface(info.Surface)

                'ARGB in INt32 (WriteableBitmap Bgra32 akzeptiert Int3-Puffer via WritePixels)
                Dim argb As Integer =
                    (CInt(c.A) << 24) Or
                    (CInt(c.R) << 16) Or
                    (CInt(c.G) << 8) Or
                    CInt(c.B)

                pixels(idx) = argb
                idx += 1
            Next
        Next

        Dim stride As Integer = width * 4
        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, stride, 0)

        Return bmp
    End Function

    'Farben exakt wie eure Referenz (SurfaceTypeRenderer), damit Vergleiche leicht bleiben.
    Private Shared Function ColorForSurface(surface As SurfaceType) As Color
        Select Case surface
            Case SurfaceType.Ocean
                Return Colors.MidnightBlue
            Case SurfaceType.SeaIce
                Return Colors.LightCyan
            Case SurfaceType.LandPlain
                Return Colors.OliveDrab
            Case SurfaceType.LandForest
                Return Colors.ForestGreen
            Case SurfaceType.LandDesert
                Return Colors.SandyBrown
            Case SurfaceType.LandMountain
                Return Colors.SaddleBrown
            Case SurfaceType.LandIce
                Return Colors.White
            Case Else
                Return Colors.Gray
        End Select
    End Function
End Class
