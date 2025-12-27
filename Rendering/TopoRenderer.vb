Public NotInheritable Class TopoRenderer

    Public Sub New()

    End Sub

    '=================================
    ' Public: einfache Cache-Overloads
    '=================================

    ''' <summary>
    ''' Rendert das TopoLayer exakt im Cache-Raster mit World-Camera.
    ''' </summary>
    Public Shared Function RenderTopoLayer(cache As EarthSurfaceCache,
                                           Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta fehlt.")

        Return RenderTopoLayerCamera(
            cache,
            cache.Meta.LonCount,
            cache.Meta.LatCount,
            CameraState.World,
            dpi)
    End Function

    ''' <summary>
    ''' Rendert das TopoLayer im Cache-Raster mit freier Camera.
    ''' </summary>
    Public Shared Function RenderTopoLayer(cache As EarthSurfaceCache,
                                           camera As CameraState,
                                           Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta fehlt.")

        Return RenderTopoLayerCamera(
            cache,
            cache.Meta.LonCount,
            cache.Meta.LatCount,
            camera,
            dpi)
    End Function

    '========================
    ' Public: Camera-Variante
    '========================

    Public Shared Function RenderTopoLayerCamera(cache As EarthSurfaceCache,
                                                 width As Integer,
                                                 height As Integer,
                                                 camera As CameraState,
                                                 Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height)

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        Dim meta = cache.Meta
        Dim cell As Double = meta.CellSizeDeg

        Dim idx As Integer = 0

        For y As Integer = 0 To height - 1

            Dim yNorm As Double = (y + 0.5) / height
            Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat
            lat = Clamp(lat, -90.0, 90.0)

            For x As Integer = 0 To width - 1

                Dim xNorm As Double = (x + 0.5) / width
                Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
                lon = WrapLon180(lon)

                Dim c As Color = SampleTopoColor(cache, lat, lon)

                pixels(idx) = (255 << 24) Or (CInt(c.R) << 16) Or (CInt(c.G) << 8) Or CInt(c.B)

                idx += 1
            Next
        Next

        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, width * 4, 0)
        Return bmp
    End Function

    '==============================
    ' Intern: Sampling + Farbkurven
    '==============================

    Private Shared Function SampleTopoColor(cache As EarthSurfaceCache, lat As Double, lon As Double) As Color

        Dim meta = cache.Meta
        Dim cellSizeDeg As Double = meta.CellSizeDeg

        Dim latIdx As Integer = CInt(Math.Floor((90.0 - lat) / cellSizeDeg))
        latIdx = Clamp(latIdx, 0, meta.LatCount - 1)

        Dim lonIdx As Integer = CInt(Math.Floor((lon + 180.0) / cellSizeDeg))
        lonIdx = Clamp(lonIdx, 0, meta.LonCount - 1)

        Dim idx As Integer = latIdx * meta.LonCount + lonIdx

        If cache.HeightM Is Nothing OrElse idx < 0 OrElse idx >= cache.HeightM.Length Then
            Return Colors.Magenta
        End If

        Dim h As Double = cache.HeightM(idx)
        If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then
            Return Colors.Magenta
        End If

        'Land/Ocean-Entscheidung
        Dim island As Boolean

        If cache.LandMask IsNot Nothing AndAlso idx < cache.LandMask.Length Then
            island = (cache.LandMask(idx) = 1)
        Else
            island = (h > 0)
        End If

        If island Then
            Return ColorForLandHeight(h)
        Else
            Return ColorForOceanDepth(-h)
        End If
    End Function

    '============
    ' Farbkurven
    '============

    Private Shared Function ColorForLandHeight(h As Double) As Color

        'Clamp
        h = Math.Max(0.0, h)

        'Typeische Skalen
        Const hMid As Double = 1700.0
        Const hHigh As Double = 3000.0
        Const hMax As Double = 7000.0

        If h < hMid Then
            'Grün->Gelb
            Dim t As Double = h / hMid
            Return LerpColor(
                Color.FromRgb(110, 130, 90),
                Color.FromRgb(200, 185, 120), t)
        ElseIf h < hHigh Then
            'Gelb->Braun
            Dim t As Double = (h - hMid) / (hHigh - hMid)
            Return LerpColor(
                Color.FromRgb(200, 185, 120),
                Color.FromRgb(140, 100, 65), t)
        Else
            'Braun->fast weiß
            Dim tLin As Double = Clamp((Math.Min(h, hMax) - hHigh) / (hMax - hHigh), 0.0, 1.0)

            'Ease-In: erst spät wird es richtig weiß
            Const p As Double = 2.2
            Dim t As Double = Math.Pow(tLin, p)

            Return LerpColor(
                Color.FromRgb(140, 100, 65),
                Color.FromRgb(245, 245, 245), t)
        End If
    End Function

    Private Shared Function ColorForOceanDepth(d As Double) As Color

        d = Math.Max(0.0, d)

        Const dMax As Double = 8000.0

        'Dim t As Double = Clamp(d / dMax, 0.0, 1.0)
        Dim t As Double = Clamp(d / dMax, 0.0, 1.0)
        t = Math.Pow(t, 1.15) ' >1: mehr Fokus auf Tiefsee, <1: mehr Fokus auf Schelf

        Return LerpColor(
            Color.FromRgb(50, 70, 95),        'flach
            Color.FromRgb(12, 18, 35), t)       'tief

    End Function

    Private Shared Function LerpColor(a As Color, b As Color, t As Double) As Color
        t = Clamp(t, 0.0, 1.0)

        Dim r As Integer = CInt(Math.Round(CDbl(a.R) + (CDbl(b.R) - CDbl(a.R)) * t))
        Dim g As Integer = CInt(Math.Round(CDbl(a.G) + (CDbl(b.G) - CDbl(a.G)) * t))
        Dim bb As Integer = CInt(Math.Round(CDbl(a.B) + (CDbl(b.B) - CDbl(a.B)) * t))

        r = Clamp(r, 0, 255)
        g = Clamp(g, 0, 255)
        bb = Clamp(bb, 0, 255)

        Return Color.FromRgb(CByte(r), CByte(g), CByte(bb))
    End Function

End Class
