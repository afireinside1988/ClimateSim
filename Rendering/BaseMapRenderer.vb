
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
Public Class BaseMapRenderer

    '=========================================================
    ' Public: einfachste Overloads (Cache → Cache-Raster)
    '=========================================================

    ''' <summary>
    ''' Rendert den BaseLayer exakt im Cache-Raster (LonCount x LatCount) mit World-Camera.
    ''' </summary>
    Public Shared Function RenderBaseMapLayer(cache As EarthSurfaceCache,
                                           Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta darf nicht Nothing sein.")

        Dim w As Integer = cache.Meta.LonCount
        Dim h As Integer = cache.Meta.LatCount

        Return RenderBaseLayerMapCamera(cache, w, h, CameraState.World, dpi)
    End Function

    ''' <summary>
    ''' Rendert den BaseLayer exakt im Cache-Raster, aber mit frei wählbarer Camera.
    ''' (Praktisch, falls du im Cache-Editor später mal wirklich crop/preview willst.)
    ''' </summary>
    Public Shared Function RenderBaseMapLayer(cache As EarthSurfaceCache,
                                           camera As CameraState,
                                           Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta darf nicht Nothing sein.")

        Dim w As Integer = cache.Meta.LonCount
        Dim h As Integer = cache.Meta.LatCount

        Return RenderBaseLayerMapCamera(cache, w, h, camera, dpi)
    End Function

    '=========================================================
    ' Public: Camera-Variante (bleibt drin)
    '=========================================================

    Public Shared Function RenderBaseLayerMapCamera(cache As EarthSurfaceCache,
                                                 width As Integer,
                                                 height As Integer,
                                                 camera As CameraState,
                                                 Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta darf nicht Nothing sein.")
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height)

        If dpi <= 0 Then dpi = 96.0
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(camera.SpanLat)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(camera.SpanLon)

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        Dim idx As Integer = 0

        For y As Integer = 0 To height - 1

            Dim yNorm As Double = (y + 0.5) / height
            Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat
            lat = Clamp(lat, -90.0, 90.0)

            For x As Integer = 0 To width - 1

                Dim xNorm As Double = (x + 0.5) / width
                Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
                lon = WrapLon180(lon)

                'Cache-basiertes Sampling (Zellindex) – keine Provider-Abhängigkeit
                Dim c As Color = SampleBaseColorFromCache(cache, lat, lon)

                pixels(idx) =
                    (CInt(c.A) << 24) Or
                    (CInt(c.R) << 16) Or
                    (CInt(c.G) << 8) Or
                    CInt(c.B)

                idx += 1
            Next
        Next

        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, width * 4, 0)
        Return bmp
    End Function

    '=========================================================
    ' Intern: Cache-Sampling + Farben
    '=========================================================

    Private Shared Function SampleBaseColorFromCache(cache As EarthSurfaceCache, lat As Double, lon As Double) As Color
        Dim m = cache.Meta
        Dim cell As Double = m.CellSizeDeg

        Dim latIdx As Integer = CInt(Math.Floor((90.0 - lat) / cell))
        latIdx = Clamp(latIdx, 0, m.LatCount - 1)

        Dim lonIdx As Integer = CInt(Math.Floor((lon + 180.0) / cell))
        lonIdx = Clamp(lonIdx, 0, m.LonCount - 1)

        Dim idx As Integer = latIdx * m.LonCount + lonIdx

        'Land/Ocean/Unknown aus Cache bestimmen
        Dim isLand As Boolean
        Dim isOcean As Boolean = False
        Dim isUnknown As Boolean = False

        If cache.LandMask IsNot Nothing AndAlso idx >= 0 AndAlso idx < cache.LandMask.Length Then
            Select Case cache.LandMask(idx)
                Case 1 : isLand = True
                Case 0 : isOcean = True
                Case Else : isUnknown = True
            End Select
        ElseIf cache.HeightM IsNot Nothing AndAlso idx >= 0 AndAlso idx < cache.HeightM.Length Then
            Dim h As Double = cache.HeightM(idx)
            If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then
                isUnknown = True
            ElseIf h < 0 Then
                isOcean = True
            ElseIf h > 0 Then
                isLand = True
            Else
                isUnknown = True
            End If
        Else
            isUnknown = True
        End If

        If isUnknown Then Return Colors.Magenta
        If isOcean Then Return Colors.MidnightBlue

        'Land (BaseLayer bewusst “nur Land”)
        Return Color.FromRgb(85, 125, 55)
    End Function
End Class
