Imports System.ComponentModel
Imports System.Transactions
Imports System.Windows.Media.TextFormatting

Public NotInheritable Class ReliefRenderer

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Rendert ein halbtransparentes Graustufen-Overlay basierend auf Höhe:
    ''' - 0m ist neutral (mid-gray)
    ''' - Land (höher) wird heller
    ''' - Ozean (tiefer) wird dunkler
    ''' 
    ''' - Skalen werden per Histogramm aus Height-Vorzeichen bestimmt
    ''' - Pro Pixel wird die Richtung (Land/Ozean) bevorzugt über LandMask entschieden, falls vorhanden:
    '''     LandMask=1 -> Land-Branch (auch wenn h kleiner 0)
    '''     LandMask=0 -> Ocean-Branch
    '''     LandMask=2/unknown -> transparent
    '''     
    ''' -Kurve: Power (gamma) + optional Deadzone, um kleine Hügel ruhig zu halten
    ''' </summary>
    Public Shared Function RenderRelief(cache As EarthSurfaceCache,
                                        Optional dpi As Double = 96.0) As WriteableBitmap

        If cache Is Nothing OrElse cache.Meta Is Nothing Then
            Throw New ArgumentNullException(NameOf(cache), "Cache oder Cache.Meta fehlt.")
        End If

        Dim width As Integer = cache.Meta.LonCount
        Dim height As Integer = cache.Meta.LatCount
        If width <= 0 OrElse height <= 0 Then
            Throw New ArgumentOutOfRangeException(NameOf(cache), "Cache-Raster ungültig.")
        End If

        If dpi <= 0 Then dpi = 96.0

        Dim hArr As Single() = cache.HeightM
        If hArr Is Nothing OrElse hArr.Length <> width * height Then
            Throw New InvalidOperationException("Cache.HeightM fehlt oder hat falsche Länge")
        End If

        Dim lmArr As Byte() = cache.LandMask
        Dim hasLm As Boolean = (lmArr IsNot Nothing AndAlso lmArr.Length = width * height)

        '--------------------------
        ' A) Skalen aus Historgramm
        '--------------------------

        Dim stats = HeightHistogramStats.ComputeScalesFromHeight(hArr)
        Dim landScale As Double = stats.LandScaleM
        Dim oceanScale As Double = stats.OceanScaleM

        Dim landMax As Double = stats.LandMaxM
        Dim oceanMax As Double = stats.OceanMaxDepthM

        'Sicherheits-Fallback:
        If Double.IsNaN(landScale) OrElse landScale <= 1 Then landScale = 1000.0
        If Double.IsNaN(oceanScale) OrElse oceanScale <= 1 Then oceanScale = 5000.0
        If Double.IsNaN(landMax) OrElse landMax <= 1 Then landMax = landScale
        If Double.IsNaN(oceanMax) OrElse oceanMax <= 1 Then oceanMax = oceanScale

        'Denominator für Log-Normierung vorbereiten
        Dim landDen As Double = Math.Log(1.0 + (landMax / landScale))
        If landDen <= 0.0 Then landDen = 1.0
        Dim oceanDen As Double = Math.Log(1.0 + (landMax / landScale))
        If oceanDen <= 0.0 Then oceanDen = 1.0

        '------------------------
        ' B) Bitmap + Pixelbuffer
        '------------------------

        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(width * height - 1) As Integer

        Dim alpha As Integer = CInt(ReliefRenderSettings.ReliefAlpha)
        Dim neutral As Integer = CInt(ReliefRenderSettings.ReliefNeutral)
        Dim baseGrayLand As Integer = CInt(ReliefRenderSettings.ReliefBaseGrayLand)
        Dim baseGrayOcean As Integer = CInt(ReliefRenderSettings.ReliefBaseGryOcean)

        Dim gammaLand As Double = ReliefRenderSettings.ReliefGammaLand
        Dim gammaOcean As Double = ReliefRenderSettings.ReliefGammaOcean

        Dim deadLand As Double = ReliefRenderSettings.ReliefDeadzoneLandT
        Dim deadOcean As Double = ReliefRenderSettings.ReliefDeadzoneOceanT

        Dim dLandMax As Double = ReliefRenderSettings.ReliefDeltaLandMax
        Dim dOceanMax As Double = ReliefRenderSettings.ReliefDeltaOceanMax

        For i As Integer = 0 To hArr.Length - 1

            Dim h As Double = CDbl(hArr(i))

            If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then
                pixels(i) = 0        'transparent
                Continue For
            End If

            'Branch-Entscheidung:
            ' - bevorzugt LandMask
            ' - sonst Height-Vorzeichen
            Dim isLand As Boolean
            Dim isOcean As Boolean

            If hasLm Then
                Select Case lmArr(i)
                    Case 1
                        isLand = True : isOcean = False
                    Case 0
                        isLand = False : isOcean = True
                    Case Else
                        'unknown (2) oder anderes -> transparent
                        pixels(i) = 0
                        Continue For
                End Select
            Else
                isLand = (h > 0)
                isOcean = (h < 0)
                If Not isLand AndAlso Not isOcean Then
                    'h=0 -> neutral
                    Dim g0 As Integer = neutral
                    pixels(i) = PackBgra(alpha, g0, g0, g0)
                    Continue For
                End If
            End If

            Dim gray As Integer = neutral

            If isLand Then
                'Land: auch wenn h < 0 (Depressionen) nicht abdunkeln, sondern neutral bleiben
                Dim hh As Double = Math.Max(0.0, h)

                'Deadzone relativ zur Scale interpretieren
                Dim t0 As Double = hh / landScale
                If t0 <= deadLand Then t0 = 0.0

                Dim w As Double = Clamp(ReliefRenderSettings.ReliefTailWeight, 0.0, 0.95)

                Dim t As Double

                If t0 <= 1.0 Then
                    'Basisbereich: Gamma auf t0 anwenden
                    t = (1.0 - w) * Math.Pow(Clamp(t0, 0.0, 1.0), gammaLand)
                Else
                    'Tail (Hochgebirge): weiche Annäherung an 1, ohne hart zu clippen
                    'u = 0..1 für [landScale..landMax]
                    Dim denom As Double = Math.Max(1.0, landMax - landScale)
                    Dim u As Double = (hh - landScale) / denom
                    u = Clamp(u, 0.0, 1.0)

                    'Tail-Krümmung
                    Dim tail As Double = 1.0 - Math.Pow(1.0 - u, ReliefRenderSettings.ReliefTailPower)

                    'tBase ist exakt 1.0 am Knee (weil t0=1), wir geben dem Tail nur noch einen zusatzanteil
                    t = 1.0 + ReliefRenderSettings.ReliefTailWeight * tail

                    'Wichtig: wieder 0..1 zurück mappen
                    t = (1.0 - w) + w * tail
                End If


                Dim delta As Double = dLandMax * t
                gray = CInt(Math.Round(baseGrayLand + delta))
            ElseIf isOcean Then
                'Ozean: Tiefe als positiv
                Dim dd As Double = Math.Max(0.0, -h)

                'Deadzone relativ zur Scale interpretieren
                Dim t0 As Double = dd / oceanScale
                If t0 <= deadOcean Then t0 = 0.0

                Dim t As Double

                If t0 <= 1.0 Then
                    'Basisbereich: Gamma auf t0 anwenden
                    t = Math.Pow(Clamp(t0, 0.0, 1.0), gammaOcean)
                Else
                    'Tail (Tiefsee): weiche Annäherung an 1, ohne hart zu clippen
                    'u = 0..1 für [oceanScale..oceanMax]
                    Dim denom As Double = Math.Max(1.0, oceanMax - oceanScale)
                    Dim u As Double = (dd - oceanScale) / denom
                    u = Clamp(u, 0.0, 1.0)

                    'Tail-Krümmung
                    Dim tail As Double = 1.0 - Math.Pow(1.0 - u, ReliefRenderSettings.ReliefTailPower)

                    'tBase ist exakt 1.0 am Knee (weil t0=1), wir geben dem Tail nur noch einen zusatzanteil
                    t = 1.0 + ReliefRenderSettings.ReliefTailWeight * tail

                    'Wichtig: wieder 0..1 zurück mappen
                    t = t / (1.0 + ReliefRenderSettings.ReliefTailWeight)
                End If

                Dim delta As Double = dOceanMax * t
                gray = CInt(Math.Round(baseGrayOcean - delta))
            End If

            gray = Clamp(gray, 0, 255)

            pixels(i) = PackBgra(alpha, gray, gray, gray)
        Next

        Dim stride As Integer = width * 4
        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, stride, 0)
        Return bmp

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
