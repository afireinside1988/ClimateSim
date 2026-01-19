Public NotInheritable Class TopoRenderer

    Private Const A255 As Integer = &HFF000000
    Private Const Magenta As Integer = &HFFFF00FF

    Private Shared ReadOnly _landLut As Integer() = BuildLandLut()
    Private Shared ReadOnly _oceanLut As Integer() = BuildOceanLut()

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

        Dim meta = cache.Meta
        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        '------------------------
        'Fast Path (direct Cache)
        '------------------------

        If width = meta.LonCount AndAlso height = meta.LatCount AndAlso IsWorldCamera(camera) Then

            Dim hArr As Single() = cache.HeightM
            If hArr Is Nothing OrElse hArr.Length <> width * height Then
                Throw New InvalidOperationException("Cache.HeightM fehlt oder hat falsche Länge.")
            End If

            Dim lmArr As Byte() = cache.LandMask
            Dim hasLm As Boolean = (lmArr IsNot Nothing AndAlso lmArr.Length = hArr.Length)

            Parallel.For(
                0, pixels.Length,
                Sub(i)



                    Dim hs As Single = hArr(i)
                    If Single.IsNaN(hs) OrElse Single.IsInfinity(hs) Then
                        pixels(i) = Magenta    'Magenta als Void
                        Return
                    End If

                    Dim isLand As Boolean
                    If hasLm Then
                        'LandMask: 1=Land, 0=Ocean, sonst unknown -> Magenta
                        Dim lm As Byte = lmArr(i)
                        If lm = 1 Then
                            isLand = True
                        ElseIf lm = 0 Then
                            isLand = False
                        Else
                            pixels(i) = Magenta         'Magenta als Void
                            Return
                        End If
                    Else
                        isLand = (hs > 0)
                    End If

                    If isLand Then
                        Dim h As Integer = CInt(hs)
                        h = Clamp(h, 0, 7000)
                        pixels(i) = _landLut(h)
                    Else
                        Dim d As Integer = CInt(-hs)
                        d = Clamp(d, 0, 8000)
                        pixels(i) = _oceanLut(d)
                    End If
                End Sub)

            bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, width * 4, 0)
            Return bmp
        End If


        '-------------------------
        'Slow Path (Camera-Sample)
        '-------------------------


        Parallel.For(
            0, height,
            Sub(y)

                Dim yNorm As Double = (y + 0.5) / height
                Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat
                lat = Clamp(lat, -90.0, 90.0)

                Dim row As Integer = y * width

                For x As Integer = 0 To width - 1

                    Dim xNorm As Double = (x + 0.5) / width
                    Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
                    lon = Wrap180(lon)

                    Dim c As Color = SampleTopoColor(cache, lat, lon)

                    pixels(row + x) = (&HFF << 24) Or (CInt(c.R) << 16) Or (CInt(c.G) << 8) Or CInt(c.B)

                Next
            End Sub)


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

    Private Shared Function BuildLandLut() As Integer()
        'Skalen für Farbwechsel
        Const hMid As Integer = 1700
        Const hHigh As Integer = 3000
        Const hMax As Integer = 7000

        Dim lut(hMax) As Integer

        'Skalenfarben
        Dim g0 = (110, 130, 90)       'grün
        Dim y0 = (200, 185, 120)      'ocker
        Dim br = (140, 100, 65)       'braun
        Dim wh = (245, 245, 245)      'fast weiß

        Const p As Double = 2.2       'Power für Peak-Stufe, damit Tibet nicht so hell wird

        For h As Integer = 0 To hMax

            Dim r As Integer, g As Integer, b As Integer

            If h < hMid Then
                Dim t As Double = h / CDbl(hMid)
                LerpRGB(g0, y0, t, r, g, b)

            ElseIf h < hHigh Then
                Dim t As Double = (h - hMid) / CDbl(hHigh - hMid)
                LerpRGB(y0, br, t, r, g, b)

            Else
                Dim tLin As Double = (h - hHigh) / CDbl(hMax - hHigh)
                Dim t As Double = Math.Pow(Clamp(tLin, 0.0, 1.0), p)
                LerpRGB(br, wh, t, r, g, b)
            End If

            lut(h) = PackArgb(r, g, b)
        Next

        Return lut

    End Function

    Private Shared Function BuildOceanLut() As Integer()
        Const dMax As Integer = 8000
        Dim lut(dMax) As Integer

        Dim shallow = (50, 70, 95)        'hellblau - Küste
        Dim deep = (12, 18, 35)           'Dunkelblau - Tiefen

        For d As Integer = 0 To dMax

            Dim t As Double = d / CDbl(dMax)
            t = Math.Pow(Clamp(t, 0.0, 1.0), 1.15)

            Dim r As Integer, g As Integer, b As Integer
            LerpRGB(shallow, deep, t, r, g, b)
            lut(d) = PackArgb(r, g, b)

        Next

        Return lut
    End Function

    Private Shared Function ColorForOceanDepth(d As Double) As Color

        d = Math.Max(0.0, d)

        Const dMax As Double = 8000.0

        'Dim t As Double = Clamp(d / dMax, 0.0, 1.0)
        Dim t As Double = Clamp(d / dMax, 0.0, 1.0)
        t = Math.Pow(t, 1.15) ' >1: mehr Fokus auf Tiefsee, <1: mehr Fokus auf Schelf

        Return LerpColor(
            Color.FromRgb(50, 70, 95),          'flach
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

    Private Shared Sub LerpRGB(a As (Integer, Integer, Integer),
                                    b As (Integer, Integer, Integer),
                                    t As Double,
                                    ByRef r As Integer,
                                    ByRef g As Integer,
                                    ByRef bb As Integer)

        t = Clamp(t, 0.0, 1.0)

        r = CInt(a.Item1 + (b.Item1 - a.Item1) * t)
        g = CInt(a.Item2 + (b.Item2 - a.Item2) * t)
        bb = CInt(a.Item3 + (b.Item3 - a.Item3) * t)

        r = Clamp(r, 0, 255)
        g = Clamp(g, 0, 255)
        bb = Clamp(bb, 0, 255)
    End Sub

    Private Shared Function PackArgb(r As Integer, g As Integer, b As Integer) As Integer
        Return A255 Or ((r And 255) << 16) Or ((g And 255) << 8) Or (b And 255)
    End Function
    '========
    ' Helper
    '========

    Private Shared Function IsWorldCamera(cam As CameraState) As Boolean
        Const eps As Double = 0.00000001
        Return Math.Abs(cam.CenterLat - 0.0) < eps AndAlso
               Math.Abs(cam.CenterLon - 0.0) < eps AndAlso
               Math.Abs(cam.SpanLat - 180.0) < eps AndAlso
               Math.Abs(cam.SpanLon - 360.0) < eps
    End Function
End Class
