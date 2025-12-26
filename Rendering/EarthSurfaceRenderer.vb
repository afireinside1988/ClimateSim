
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

Public Structure CameraState
    Public Property CenterLat As Double         'Clamp -90..+90
    Public Property CenterLon As Double         'unbounded, wrap erst bei Sampling
    Public SpanLat As Double                    'z.b. 180 = ganze Welt in Höhe
    Public SpanLon As Double                    'z.b. 360 = ganze Welt in Breite

    Public Shared ReadOnly Property World As CameraState
        Get
            Return New CameraState With {.CenterLat = 0, .CenterLon = 0, .SpanLat = 180, .SpanLon = 360}
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

        Dim cam As New CameraState With {
            .CenterLat = 0.0,
            .CenterLon = 0.0,
            .SpanLat = 180.0,
            .SpanLon = 360.0
        }

        Return RenderSurfaceTypeCamera(provider, width, height, cam, dpi)

    End Function

    Public Shared Function RenderSurfaceTypeCamera(provider As IEarthSurfaceProvider,
                                                   width As Integer,
                                                   height As Integer,
                                                   camera As CameraState,
                                                   Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(provider)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height)

        If dpi <= 0 Then dpi = 96.0
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(camera.SpanLat)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(camera.SpanLon)

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        'Pixelzentren -> norm [0..1]
        'xNorm = (x+0.5)/width, yNorm = (y+0.5)/height
        'lon = CenterLon + (xNorm - 0.5) * SpanLon
        'lat = CenterLat + (0.5 - yNorm) * SpanLat
        '
        'danach: lon wrap, lat clamp
        Dim idx As Integer = 0

        For y As Integer = 0 To height - 1

            Dim yNorm As Double = (y + 0.5) / height
            Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat
            lat = Clamp(lat, -90.0, 90.0)

            For x As Integer = 0 To width - 1

                Dim xNorm As Double = (x + 0.5) / width
                Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
                lon = WrapLon180(lon)

                Dim info As SurfaceInfo = provider.GetSurfaceInfo(lat, lon)
                Dim c As Color = ColorForSurface(info.Surface)

                'BGRA32 in Int32: AARRGGBB wird von WritePixels als BGRA interpretiert,
                'aber in der Praxis ist dieses Packing (A<<24 | R<<16 | G<<8 | B) das übliche für Bgra32-Int32-Puffer.
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

    Private Shared Function ColorForSurface(surface As SurfaceType) As Color
        Select Case surface
            Case SurfaceType.Ocean
                Return Colors.MidnightBlue

            Case SurfaceType.SeaIce
                Return Color.FromRgb(210, 235, 240)
            Case SurfaceType.LandPlain
                Return Color.FromRgb(70, 140, 65)
            Case SurfaceType.LandForest
                Return Color.FromRgb(30, 95, 45)
            Case SurfaceType.LandDesert
                Return Color.FromRgb(200, 165, 110)
            Case SurfaceType.LandMountain
                Return Color.FromRgb(120, 85, 55)
            Case SurfaceType.LandIce
                Return Color.FromRgb(245, 245, 245)

            Case SurfaceType.Unknown
                Return Colors.Magenta
            Case Else
                Return Colors.Gray
        End Select
    End Function

End Class
