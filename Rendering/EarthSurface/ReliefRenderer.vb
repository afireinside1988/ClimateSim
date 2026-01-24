Imports System.ComponentModel
Imports System.Transactions
Imports System.Windows.Media.TextFormatting
Imports System.Buffers
Imports System.Threading.Tasks
Imports System.Threading
Imports System.Collections.Concurrent

Public NotInheritable Class ReliefRenderer

    Private Shared ReadOnly _renderLock As New Object()

    '=====
    'LUTs
    '=====

    Private Const LUTN As Integer = 4096
    Private Const NORMAL_LUT_N As Integer = 4096

    Private Shared ReadOnly _energyLandLut As Double() = BuildPowLut(ReliefRenderSettings.EnergyLandPower)
    Private Shared ReadOnly _energyOceanLut As Double() = BuildPowLut(ReliefRenderSettings.EnergyOceanPower)
    Private Shared ReadOnly _hillAlphaLandLut As Double() = BuildPowLut(ReliefRenderSettings.HillShadeAlphaLandPower)

    Private Shared ReadOnly _invLenLut As Double() = BuildInvLenLut()

    '=============
    'Reused Arrays
    '=============
    Private Shared _dx As Single()
    Private Shared _dy As Single()
    Private Shared _e As Single()
    Private Shared _pixels As Integer()
    Private Shared _landHist As Integer()
    Private Shared _oceanHist As Integer()
    Private Shared _threadLand As Integer()()
    Private Shared _threadOcean As Integer()()
    Private Shared _threadCount As Integer

    Private Shared Sub EnsureBuffers(width As Integer, height As Integer)

        Dim n As Integer = width * height
        If _dx Is Nothing OrElse _dx.Length <> n Then _dx = New Single(n - 1) {}
        If _dy Is Nothing OrElse _dy.Length <> n Then _dy = New Single(n - 1) {}
        If _e Is Nothing OrElse _e.Length <> n Then _e = New Single(n - 1) {}
        If _pixels Is Nothing OrElse _pixels.Length <> n Then _pixels = New Integer(n - 1) {}

    End Sub
    Private Shared Sub EnsureHists(bins As Integer)
        If _landHist Is Nothing OrElse _landHist.Length <> bins Then _landHist = New Integer(bins - 1) {}
        If _oceanHist Is Nothing OrElse _oceanHist.Length <> bins Then _oceanHist = New Integer(bins - 1) {}
    End Sub

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Kombiniertes Overlay: HillShade + ReliefEnergy als Alpha-Maske.
    ''' Ergebnis ist ein einzelnes BGRA-Overlay, das über Topo gelegt wird.
    ''' </summary>
    Public Shared Function RenderRelief(cache As EarthSurfaceCache,
                                        Optional includeHillShade As Boolean = True,
                                        Optional dpi As Double = 96.0) As WriteableBitmap

        SyncLock _renderLock


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
            ' Wir berechnen Energie pro Pixel
            ' und sammeln Min/Max via Histogramm getrennt für Land/Ozean.

            Dim n As Integer = width * height

            EnsureBuffers(width, height)

            Dim dxArr As Single() = _dx
            Dim dyArr As Single() = _dy
            Dim eArr As Single() = _e

            Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
            Dim pixels As Integer() = _pixels

            'Pass 1: Energie grob, dabei maxE für Bin-Mapping bestimmen
            Dim maxE As Double = 1.0
            Dim maxLock As New Object()

            Parallel.For(0, height,
                     Sub(y)



                         Dim localMax As Double = 1.0

                         Dim ym As Integer = If(y > 0, y - 1, y)
                         Dim yp As Integer = If(y < height - 1, y + 1, y)

                         Dim row As Integer = y * width
                         Dim rowYm As Integer = ym * width
                         Dim rowYp As Integer = yp * width

                         For x As Integer = 0 To width - 1

                             Dim xm As Integer = If(x > 0, x - 1, x)
                             Dim xp As Integer = If(x < width - 1, x + 1, x)

                             Dim i As Integer = y * width + x
                             Dim h0 As Double = CDbl(hArr(i))
                             If Double.IsNaN(h0) OrElse Double.IsInfinity(h0) Then
                                 eArr(i) = Single.NaN
                                 dxArr(i) = Single.NaN
                                 dyArr(i) = Single.NaN
                                 Continue For
                             End If

                             'Nachbarn
                             Dim hA As Double = CDbl(hArr(rowYm + xm))
                             Dim hB As Double = CDbl(hArr(rowYm + x))
                             Dim hC As Double = CDbl(hArr(rowYm + xp))
                             Dim hD As Double = CDbl(hArr(row + xm))
                             Dim hE As Double = CDbl(hArr(row + xp))
                             Dim hF As Double = CDbl(hArr(rowYp + xm))
                             Dim hG As Double = CDbl(hArr(rowYp + x))
                             Dim hH As Double = CDbl(hArr(rowYp + xp))

                             If Double.IsNaN(hA) OrElse Double.IsInfinity(hA) OrElse
                                Double.IsNaN(hB) OrElse Double.IsInfinity(hB) OrElse
                                Double.IsNaN(hC) OrElse Double.IsInfinity(hC) OrElse
                                Double.IsNaN(hD) OrElse Double.IsInfinity(hD) OrElse
                                Double.IsNaN(hE) OrElse Double.IsInfinity(hE) OrElse
                                Double.IsNaN(hF) OrElse Double.IsInfinity(hF) OrElse
                                Double.IsNaN(hG) OrElse Double.IsInfinity(hG) OrElse
                                Double.IsNaN(hH) OrElse Double.IsInfinity(hH) Then

                                 eArr(i) = 0.0F
                                 dxArr(i) = 0.0F
                                 dyArr(i) = 0.0F
                                 Continue For
                             End If

                             Dim dx As Double = (hC + 2 * hE + hH) - (hA + 2 * hD + hF)
                             Dim dy As Double = (hF + 2 * hG + hH) - (hA + 2 * hB + hC)

                             dxArr(i) = CSng(dx)
                             dyArr(i) = CSng(dy)

                             Dim e As Double = Math.Abs(dx) + Math.Abs(dy)
                             eArr(i) = CSng(e)

                             If e > localMax Then localMax = e
                         Next

                         SyncLock maxLock
                             If localMax > maxE Then maxE = localMax
                         End SyncLock
                     End Sub)


            'Pass 2: Histogramm füllen (land/ocean getrennt) - slotfrei via Partitioner
            Dim bins As Integer = ReliefRenderSettings.EnergyBins

            EnsureHists(bins)
            Dim landHist = _landHist
            Dim oceanHist = _oceanHist
            Array.Clear(landHist, 0, bins)
            Array.Clear(oceanHist, 0, bins)

            'Chunk Size setzen lohnt sich oft (sonst zu viele kleine Ranges)
            Dim parts = Partitioner.Create(0, n, 65536)

            Dim po As New ParallelOptions With {.MaxDegreeOfParallelism = Environment.ProcessorCount}

            Dim localsLand As New ConcurrentBag(Of Integer())()
            Dim localsOcean As New ConcurrentBag(Of Integer())()

            Parallel.ForEach(Of Tuple(Of Integer, Integer), (Land As Integer(), Ocean As Integer()))(
    parts,
    po,
    Function() (Land:=New Integer(bins - 1) {}, Ocean:=New Integer(bins - 1) {}),
    Function(range As Tuple(Of Integer, Integer),
             state As ParallelLoopState,
             local As (Land As Integer(), Ocean As Integer())) As (Land As Integer(), Ocean As Integer())

        Dim lh = local.Land
        Dim oh = local.Ocean

        For i As Integer = range.Item1 To range.Item2 - 1

            Dim e As Double = CDbl(eArr(i))
            If Double.IsNaN(e) OrElse e <= 0 Then Continue For

            Dim isOcean As Boolean
            If hasLm Then
                isOcean = (lmArr(i) = 0)
            Else
                isOcean = (CDbl(hArr(i)) < 0)
            End If

            Dim bin As Integer = CInt((e / maxE) * (bins - 1))
            If bin < 0 Then
                bin = 0
            ElseIf bin >= bins Then
                bin = bins - 1
            End If

            If isOcean Then
                oh(bin) += 1
            Else
                lh(bin) += 1
            End If
        Next

        Return local
    End Function,
    Sub(local As (Land As Integer(), Ocean As Integer()))
        localsLand.Add(local.Land)
        localsOcean.Add(local.Ocean)
    End Sub
)

            'Merge (seriell, sehr günstig)
            For Each lh In localsLand
                For b As Integer = 0 To bins - 1
                    landHist(b) += lh(b)
                Next
            Next

            For Each oh In localsOcean
                For b As Integer = 0 To bins - 1
                    oceanHist(b) += oh(b)
                Next
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

            Dim az As Double = ReliefRenderSettings.SunAzimuthDeg * Math.PI / 180.0
            Dim el As Double = ReliefRenderSettings.SunElevationDeg * Math.PI / 180.0

            Dim lx As Double = Math.Cos(el) * Math.Sin(az)
            Dim ly As Double = Math.Cos(el) * Math.Cos(az)
            Dim lz As Double = Math.Sin(el)

            Dim neutral As Integer = ReliefRenderSettings.HillShadeNeutral
            Dim amp As Double = ReliefRenderSettings.HillShadeAmplitude
            Dim oceanFactor As Double = ReliefRenderSettings.HillShadeOceanFactor

            Parallel.For(0, height,
                    Sub(y)

                        Dim row As Integer = y * width

                        For x As Integer = 0 To width - 1

                            Dim i As Integer = row + x
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
                            Dim e As Double = eArr(i)
                            Dim eNorm As Double

                            If Double.IsNaN(e) OrElse e <= 0 Then
                                eNorm = 0.0
                            ElseIf isocean Then
                                eNorm = (e - oceanElo) / (oceanEhi - oceanElo)
                                eNorm = Clamp(eNorm, 0.0, 1.0)
                                If eNorm <= ReliefRenderSettings.EnergyOceanDeadzoneT Then eNorm = 0.0
                                eNorm = _energyOceanLut(LutIndex01(eNorm))
                            Else
                                eNorm = (e - landElo) / (landEhi - landElo)
                                eNorm = Clamp(eNorm, 0.0, 1.0)
                                If eNorm <= ReliefRenderSettings.EnergyLandDeadzoneT Then eNorm = 0.0
                                eNorm = _energyLandLut(LutIndex01(eNorm))
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

                            Dim dx As Double = dxArr(i)
                            Dim dy As Double = dyArr(i)

                            Dim nx As Double = -dx
                            Dim ny As Double = -dy
                            Dim nz As Double = 1.0

                            Dim invLen As Double = InvLenFast(nx, ny)
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

                                Dim di As Integer = LutIndex01(d)
                                Dim aHill As Double = ReliefRenderSettings.HillShadeAlphaLandMax * _hillAlphaLandLut(di)

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

                    End Sub)

            bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, width * 4, 0)
            Return bmp

        End SyncLock
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

    Private Shared Function BuildPowLut(p As Double) As Double()

        Dim lut(LUTN - 1) As Double
        For i As Integer = 0 To LUTN - 1
            Dim t As Double = i / CDbl(LUTN - 1)
            lut(i) = Math.Pow(t, p)
        Next

        Return lut

    End Function

    Private Shared Function BuildInvLenLut() As Double()

        Dim lut(NORMAL_LUT_N - 1) As Double

        'nx² + ny² wird auf [0 .. MAX] gemappt
        Const maxR2 As Double = 16.0        ' ausreichend für Sobel

        For i As Integer = 0 To NORMAL_LUT_N - 1
            Dim r2 As Double = (i / CDbl(NORMAL_LUT_N - 1)) * maxR2
            lut(i) = 1.0 / Math.Sqrt(r2 + 1.0)
        Next

        Return lut
    End Function

    Private Shared Function InvLenFast(nx As Double, ny As Double) As Double

        Dim r2 As Double = nx * nx + ny * ny
        Dim t As Double = r2 / 16.0
        t = Clamp(t, 0.0, 1.0)
        Dim idx As Integer = CInt(t * (NORMAL_LUT_N - 1))

        Return _invLenLut(idx)

    End Function

    Private Shared Function LutIndex01(t As Double) As Integer
        If t <= 0.0 Then Return 0
        If t >= 1.0 Then Return LUTN - 1
        Return CInt(t * (LUTN - 1))
    End Function
End Class
