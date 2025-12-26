

Imports System.Runtime.InteropServices.Marshalling
Imports System.Transactions

Public NotInheritable Class HillShadeRenderer


    Private Sub New()

    End Sub

    ''' <summary>
    ''' Rendert ein HillShade-Overlay (Sobel-Gradient + Lambert-Shading)
    ''' Neutral ist Mid-Gray, Alpha seperat
    ''' Ozean wird über Faktor gedämpft, damit Bathymetrie subtil bleibt
    ''' </summary>
    Public Shared Function RenderHillShade(cache As EarthSurfaceCache, Optional dpi As Double = 96.0) As WriteableBitmap

        If cache Is Nothing OrElse cache.Meta Is Nothing Then
            Throw New ArgumentNullException(NameOf(cache))
        End If

        Dim width As Integer = cache.Meta.LonCount
        Dim height As Integer = cache.Meta.LatCount
        If width <= 0 OrElse height <= 0 Then
            Throw New ArgumentOutOfRangeException("Ungültiges Raster.")
        End If

        Dim hArr As Single() = cache.HeightM
        If hArr Is Nothing OrElse hArr.Length <> width * height Then
            Throw New InvalidOperationException("Cache.heightM fehlt oder hat falsche Länge.")
        End If

        Dim lmArr As Byte() = cache.LandMask
        Dim hasLm As Boolean = (lmArr IsNot Nothing AndAlso lmArr.Length = hArr.Length)

        If dpi <= 0 Then dpi = 96.0

        '---------------------------
        ' Sonnenrichtung vorbereiten
        '---------------------------

        Dim az As Double = ReliefRenderSettings.SunAzimutDeg * Math.PI / 180.0
        Dim el As Double = ReliefRenderSettings.SunElevationDeg * Math.PI / 180.0

        'Konvention:
        ' x = Osten, y = Süden, z = oben
        Dim lx As Double = Math.Cos(el) * Math.Sin(az)
        Dim ly As Double = Math.Cos(el) * Math.Cos(az)
        Dim lz As Double = Math.Sin(el)

        '----------------
        ' Bitmap + Buffer
        '----------------

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        Dim neutral As Integer = ReliefRenderSettings.HillShadeNeutral
        Dim amp As Double = ReliefRenderSettings.HillShadeAmplitude
        Dim oceanFactor As Double = ReliefRenderSettings.HillShadeOceanFactor
        Dim alpha As Integer = ReliefRenderSettings.HillShadeAlphaOcean

        '------------------------
        ' Sobel-Kernel (implizit)
        '------------------------

        For y As Integer = 0 To height - 1

            For x As Integer = 0 To width - 1

                Dim i As Integer = y * width + x
                Dim h0 As Double = CDbl(hArr(i))

                If Double.IsNaN(h0) OrElse Double.IsInfinity(h0) Then
                    pixels(i) = 0           'transparent
                    Continue For
                End If

                'Randbehandlung: clamp
                Dim xm As Integer = If(x > 0, x - 1, x)
                Dim xp As Integer = If(x < width - 1, x + 1, x)
                Dim ym As Integer = If(y > 0, y - 1, y)
                Dim yp As Integer = If(y < height - 1, y + 1, y)

                'Nachbarschaft
                Dim hA As Double = CDbl(hArr(ym * width + xm))
                Dim hB As Double = CDbl(hArr(ym * width + x))
                Dim hC As Double = CDbl(hArr(ym * width + xp))
                Dim hD As Double = CDbl(hArr(y * width + xm))
                Dim hE As Double = CDbl(hArr(y * width + xp))
                Dim hF As Double = CDbl(hArr(yp * width + xm))
                Dim hG As Double = CDbl(hArr(yp * width + x))
                Dim hH As Double = CDbl(hArr(yp * width + xp))

                'Falls irgendein Nachbar NaN ist -> neutral
                If Double.IsNaN(hA) OrElse Double.IsNaN(hB) OrElse Double.IsNaN(hC) OrElse
                   Double.IsNaN(hD) OrElse Double.IsNaN(hE) OrElse
                   Double.IsNaN(hF) OrElse Double.IsNaN(hG) OrElse Double.IsNaN(hH) Then

                    pixels(i) = PackBgra(alpha, neutral, neutral, neutral)
                    Continue For
                End If

                'Sobel-Gradient
                Dim dx As Double = (hC + 2 * hE + hH) - (hA + 2 * hD + hF)
                Dim dy As Double = (hF + 2 * hG + hH) - (hA + 2 * hB + hC)

                'Normale (z hoch, damit flaches Gelände nicht kippt)
                Dim nx As Double = -dx
                Dim ny As Double = -dy
                Dim nz As Double = 1.0

                Dim invLen As Double = 1.0 / Math.Sqrt(nx * nx + ny * ny + nz * nz)
                nx *= invLen
                ny *= invLen
                nz *= invLen

                'Lambert
                Dim shade As Double = nx * lx + ny * ly + nz * lz
                shade = Clamp(shade, -1.0, 1.0)

                'Ozean-Dämpfung
                Dim localAmp As Double = amp
                If hasLm AndAlso lmArr(i) = 0 Then
                    localAmp *= oceanFactor
                End If

                Dim gray As Integer = CInt(Math.Round(neutral + shade * localAmp))
                gray = Clamp(gray, 0, 255)

                Dim isOcean As Boolean = False
                If hasLm Then
                    isOcean = (lmArr(i) = 0)
                Else
                    'Fallback ohne LandMask: Height-Vorzeichen
                    isOcean = (h0 < 0)
                End If

                If isOcean Then
                    '-----------------------
                    'Ozean: statisches Alpha
                    '-----------------------

                    alpha = CInt(ReliefRenderSettings.HillShadeAlphaOcean)
                    pixels(i) = PackBgra(alpha, gray, gray, gray)
                Else
                    '------------------------------------------------
                    ' Land: Dynamisches Alpha - nur Struktur sichtbar
                    '------------------------------------------------
                    Dim d As Double = Math.Abs(gray - neutral) / localAmp
                    d = Clamp(d, 0.0, 1.0)

                    'Alpha-Kurve (macht schwache Schattierung fast transparent
                    Dim a As Double = ReliefRenderSettings.HillSahdeAlphaLandMax * Math.Pow(d, ReliefRenderSettings.HillShadeAlphaLandPower)

                    alpha = Clamp(CInt(Math.Round(a)), 0, 255)

                    'Wenn praktisch neutral -> komplett transparent
                    If alpha <= 1 Then
                        pixels(i) = 0
                    Else
                        pixels(i) = PackBgra(alpha, gray, gray, gray)
                    End If

                End If
            Next
        Next

        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, width * 4, 0)
        Return bmp

    End Function


    Private Shared Function PackBgra(a As Integer, r As Integer, g As Integer, b As Integer) As Integer
        Return (a << 24) Or (r << 16) Or (g << 8) Or b
    End Function
End Class
