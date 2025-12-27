Imports System.ComponentModel
Imports System.Transactions
Imports System.Windows.Media.TextFormatting

Public NotInheritable Class ReliefRenderer

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Kombiniertes Overlay: HillShade + ReliefEnergy als Alpha-Maske.
    ''' Ergebnis ist ein einzelnes BGRA-Overlay, das über Topo gelegt wird.
    ''' </summary>
    Public Shared Function RenderRelief(cache As EarthSurfaceCache,
                                        Optional includeHillShade As Boolean = True,
                                        Optional dpi As Double = 96.0) As WriteableBitmap

        If cache Is Nothing OrElse cache.Meta Is Nothing Then
            Throw New ArgumentNullException(NameOf(cache))
        End If

        Dim width As Integer = cache.Meta.LonCount
        Dim height As Integer = cache.Meta.LatCount
        If width <= 0 OrElse height <= 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(cache), "Ungültiges Raster.")
        End If

        Dim hArr As Single() = cache.HeightM
        If hArr Is Nothing OrElse hArr.Length <> width * height Then
            Throw New InvalidOperationException("Cache.HeightM fehlt oder hat falsche Länge.")
        End If

        Dim lmArr As Byte() = cache.LandMask
        Dim hasLm As Boolean = (lmArr IsNot Nothing AndAlso lmArr.Length = hArr.Length)

        If dpi <= 0 Then dpi = 96.0

        '----------------------------------------
        ' A) ReliefEnergy berechnen (Sobel magnitude)
        '----------------------------------------
        ' Wir berechnen Energie pro Pixel: sqrt(dx^2 + dy^2)
        ' und sammeln Min/Max via Histogramm getrennt für Land/Ozean.

        Dim energy(width * height - 1) As Double

        Dim landHist(ReliefRenderSettings.EnergyBins - 1) As Integer
        Dim oceanHist(ReliefRenderSettings.EnergyBins - 1) As Integer

        'Pass 1: Energie grob, dabei maxE für Bin-Mapping bestimmen
        Dim maxE As Double = 1.0

        For y As Integer = 0 To height - 1

            Dim ym As Integer = If(y > 0, y - 1, y)
            Dim yp As Integer = If(y < height - 1, y + 1, y)

            For x As Integer = 0 To width - 1

                Dim xm As Integer = If(x > 0, x - 1, x)
                Dim xp As Integer = If(x < width - 1, x + 1, x)

                Dim i As Integer = y * width + x
                Dim h0 As Double = CDbl(hArr(i))
                If Double.IsNaN(h0) OrElse Double.IsInfinity(h0) Then
                    energy(i) = Double.NaN
                    Continue For
                End If

                'Nachbarn
                Dim hA As Double = CDbl(hArr(ym * width + xm))
                Dim hB As Double = CDbl(hArr(ym * width + x))
                Dim hC As Double = CDbl(hArr(ym * width + xp))
                Dim hD As Double = CDbl(hArr(y * width + xm))
                Dim hE As Double = CDbl(hArr(y * width + xp))
                Dim hF As Double = CDbl(hArr(yp * width + xm))
                Dim hG As Double = CDbl(hArr(yp * width + x))
                Dim hH As Double = CDbl(hArr(yp * width + xp))

                If Double.IsNaN(hA) OrElse Double.IsNaN(hB) OrElse Double.IsNaN(hC) OrElse
                   Double.IsNaN(hD) OrElse Double.IsNaN(hE) OrElse
                   Double.IsNaN(hF) OrElse Double.IsNaN(hG) OrElse Double.IsNaN(hH) Then
                    energy(i) = 0.0
                    Continue For
                End If

                Dim dx As Double = (hC + 2 * hE + hH) - (hA + 2 * hD + hF)
                Dim dy As Double = (hF + 2 * hG + hH) - (hA + 2 * hB + hC)

                Dim e As Double = Math.Sqrt(dx * dx + dy * dy)
                energy(i) = e
                If e > maxE Then maxE = e
            Next
        Next

        'Pass 2: Histogramm füllen (land/ocean getrennt)
        For i As Integer = 0 To energy.Length - 1

            Dim e As Double = energy(i)
            If Double.IsNaN(e) OrElse e <= 0 Then Continue For

            Dim isOcean As Boolean
            If hasLm Then
                isOcean = (lmArr(i) = 0)
            Else
                isOcean = (CDbl(hArr(i)) < 0)
            End If

            Dim bin As Integer = CInt(Math.Floor((e / maxE) * (ReliefRenderSettings.EnergyBins - 1)))
            bin = Clamp(bin, 0, ReliefRenderSettings.EnergyBins - 1)

            If isOcean Then
                oceanHist(bin) += 1
            Else
                landHist(bin) += 1
            End If
        Next

        'Perzentile -> Energieschwellen (in "E" Einheiten)
        Dim landElo As Double, landEhi As Double
        Dim oceanElo As Double, oceanEhi As Double

        landElo = PercentileFromHist(landHist, ReliefRenderSettings.EnergyLandPlo) * maxE
        landEhi = PercentileFromHist(landHist, ReliefRenderSettings.EnergyLandPhi) * maxE
        oceanElo = PercentileFromHist(oceanHist, ReliefRenderSettings.EnergyOceanPlo) * maxE
        oceanEhi = PercentileFromHist(oceanHist, ReliefRenderSettings.EnergyOceanPhi) * maxE

        'Fallbacks:
        If landEhi <= landElo Then landEhi = landElo + 1.0
        If oceanEhi <= oceanElo Then oceanEhi = oceanElo + 1.0

        '----------------------------------------
        ' B) HillShade rendern, Alpha mit Energy maskieren
        '----------------------------------------

        Dim az As Double = ReliefRenderSettings.SunAzimutDeg * Math.PI / 180.0
        Dim el As Double = ReliefRenderSettings.SunElevationDeg * Math.PI / 180.0

        Dim lx As Double = Math.Cos(el) * Math.Sin(az)
        Dim ly As Double = Math.Cos(el) * Math.Cos(az)
        Dim lz As Double = Math.Sin(el)

        Dim neutral As Integer = ReliefRenderSettings.HillShadeNeutral
        Dim amp As Double = ReliefRenderSettings.HillShadeAmplitude
        Dim oceanFactor As Double = ReliefRenderSettings.HillShadeOceanFactor

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        For y As Integer = 0 To height - 1

            Dim ym As Integer = If(y > 0, y - 1, y)
            Dim yp As Integer = If(y < height - 1, y + 1, y)

            For x As Integer = 0 To width - 1

                Dim xm As Integer = If(x > 0, x - 1, x)
                Dim xp As Integer = If(x < width - 1, x + 1, x)

                Dim i As Integer = y * width + x
                Dim h0 As Double = CDbl(hArr(i))

                If Double.IsNaN(h0) OrElse Double.IsInfinity(h0) Then
                    pixels(i) = 0
                    Continue For
                End If

                Dim isocean As Boolean
                If hasLm Then
                    If lmArr(i) = 2 Then
                        pixels(i) = 0
                        Continue For
                    End If
                    isocean = (lmArr(i) = 0)
                Else
                    isocean = (h0 < 0)
                End If

                'Energy -> eNorm (0..1) getrennt für Land/Ocean
                Dim e As Double = energy(i)
                Dim eNorm As Double

                If Double.IsNaN(e) OrElse e <= 0 Then
                    eNorm = 0.0
                ElseIf isocean Then
                    eNorm = (e - oceanElo) / (oceanEhi - oceanElo)
                    eNorm = Clamp(eNorm, 0.0, 1.0)
                    If eNorm <= ReliefRenderSettings.EnergyOceanDeadzoneT Then eNorm = 0.0
                    eNorm = Math.Pow(eNorm, ReliefRenderSettings.EnergyOceanPower)
                Else
                    eNorm = (e - landElo) / (landEhi - landElo)
                    eNorm = Clamp(eNorm, 0.0, 1.0)
                    If eNorm <= ReliefRenderSettings.EnergyLandDeadzoneT Then eNorm = 0.0
                    eNorm = Math.Pow(eNorm, ReliefRenderSettings.EnergyLandPower)
                End If

                If Not includeHillShade Then
                    'Nur Energy als "Kantenmaske": wir zeichnen neutral-grau mit sehr kleinem Alpha
                    Dim aOnly As Integer = CInt(Math.Round(Clamp(18.0 * eNorm, 0.0, 18.0)))
                    If aOnly <= 1 Then
                        pixels(i) = 0
                    Else
                        pixels(i) = PackBgra(aOnly, 128, 128, 128)
                    End If
                    Continue For
                End If

                'HillShade Nachbarn
                Dim hA As Double = CDbl(hArr(ym * width + xm))
                Dim hB As Double = CDbl(hArr(ym * width + x))
                Dim hC As Double = CDbl(hArr(ym * width + xp))
                Dim hD As Double = CDbl(hArr(y * width + xm))
                Dim hE As Double = CDbl(hArr(y * width + xp))
                Dim hF As Double = CDbl(hArr(yp * width + xm))
                Dim hG As Double = CDbl(hArr(yp * width + x))
                Dim hH As Double = CDbl(hArr(yp * width + xp))

                If Double.IsNaN(hA) OrElse Double.IsNaN(hB) OrElse Double.IsNaN(hC) OrElse
                   Double.IsNaN(hD) OrElse Double.IsNaN(hE) OrElse
                   Double.IsNaN(hF) OrElse Double.IsNaN(hG) OrElse Double.IsNaN(hH) Then
                    pixels(i) = 0
                    Continue For
                End If

                Dim dx As Double = (hC + 2 * hE + hH) - (hA + 2 * hD + hF)
                Dim dy As Double = (hF + 2 * hG + hH) - (hA + 2 * hB + hC)

                Dim nx As Double = -dx
                Dim ny As Double = -dy
                Dim nz As Double = 1.0

                Dim invLen As Double = 1.0 / Math.Sqrt(nx * nx + ny * ny + nz * nz)
                nx *= invLen
                ny *= invLen
                nz *= invLen

                Dim shade As Double = nx * lx + ny * ly + nz * lz
                shade = Clamp(shade, -1.0, 1.0)

                Dim localAmp As Double = amp
                If isocean Then localAmp *= oceanFactor

                Dim gray As Integer = CInt(Math.Round(neutral + shade * localAmp))
                gray = Clamp(gray, 0, 255)

                Dim alpha As Integer

                If isocean Then
                    'Ozean: euer statisches Alpha, aber leicht über Energy maskiert
                    Dim a0 As Double = ReliefRenderSettings.HillShadeAlphaOcean
                    Dim maskStrength As Double = ReliefRenderSettings.EnergyMaskStrengthOcean
                    Dim a As Double = a0 * ((1.0 - maskStrength) + maskStrength * eNorm)
                    alpha = Clamp(CInt(Math.Round(a)), 0, 255)

                    If alpha <= 1 Then
                        pixels(i) = 0
                    Else
                        pixels(i) = PackBgra(alpha, gray, gray, gray)
                    End If

                Else
                    'Land: euer dynamisches Alpha + Energy-Maske
                    Dim d As Double = Math.Abs(gray - neutral) / Math.Max(0.000001, localAmp)
                    d = Clamp(d, 0.0, 1.0)

                    Dim aHill As Double = ReliefRenderSettings.HillShadeAlphaLandMax *
                                          Math.Pow(d, ReliefRenderSettings.HillShadeAlphaLandPower)

                    Dim maskStrength As Double = ReliefRenderSettings.EnergyMaskStrengthLand
                    Dim aFinal As Double = aHill * ((1.0 - maskStrength) + maskStrength * eNorm)

                    alpha = Clamp(CInt(Math.Round(aFinal)), 0, 255)

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
    '-----------------------
    ' Histogram percentile
    '-----------------------
    Private Shared Function PercentileFromHist(hist() As Integer, p As Double) As Double
        p = Clamp(p, 0.0, 1.0)

        Dim total As Long = 0
        For i As Integer = 0 To hist.Length - 1
            total += hist(i)
        Next
        If total <= 0 Then Return 0.0

        Dim target As Long = CLng(Math.Round(total * p))
        Dim acc As Long = 0

        For i As Integer = 0 To hist.Length - 1
            acc += hist(i)
            If acc >= target Then
                Return i / CDbl(hist.Length - 1) '0..1
            End If
        Next

        Return 1.0
    End Function

    ''' <summary>
    ''' Packt einen Grauwert + Alpha in BGRA32 (AARRGGBB in Int32-Puffer)
    ''' </summary>
    ''' <param name="a">Alpha</param>
    ''' <param name="r">Rot</param>
    ''' <param name="g">Grün</param>
    ''' <param name="b">Blau</param>
    ''' <returns></returns>
    Private Shared Function PackBgra(a As Integer, r As Integer, g As Integer, b As Integer) As Integer
        Return (a << 24) Or (r << 16) Or (g << 8) Or b
    End Function

End Class
