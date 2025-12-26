Imports System.IO

Public NotInheritable Class HeightHistogramStats

    Public Structure Result
        Public LandScaleM As Double         'z.B. p98 der Landhöhen (Meter)
        Public OceanScaleM As Double        'z.B. p98 der Ozeantiefen (als positive Meter)
        Public LandCount As Integer
        Public OceanCount As Integer
        Public LandMaxM As Double
        Public OceanMaxDepthM As Double
    End Structure

    Private Sub New()

    End Sub

    Public Shared Function ComputeScalesFromHeight(heightM As Single()) As Result

        Dim res As New Result With {
            .LandScaleM = Double.NaN,
            .OceanScaleM = Double.NaN,
            .LandCount = 0,
            .OceanCount = 0,
            .LandMaxM = 0.0,
            .OceanMaxDepthM = 0.0
        }

        If heightM Is Nothing OrElse heightM.Length = 0 Then Return res

        '------------------------
        ' Pass 1: Maxima + Counts
        '------------------------

        For i As Integer = 0 To heightM.Length - 1
            Dim h As Double = CDbl(heightM(i))

            If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then Continue For

            If h > 0 Then
                res.LandCount += 1
                If h > res.LandMaxM Then res.LandMaxM = h
            ElseIf h < 0 Then
                res.OceanCount += 1
                Dim d As Double = -h        'Tiefe positivieren
                If d > res.OceanMaxDepthM Then res.OceanMaxDepthM = d
            End If
        Next

        'Fallbacks, wenn praktisch keine Daten -> dann lieber "sinnvolle Defaults", statt Div/0 oder 0-Skalen
        If res.LandCount < 10 Then
            res.LandScaleM = 1000.0
        End If
        If res.OceanCount < 10 Then
            res.OceanScaleM = 5000.0
        End If

        'Wenn Max=0 macht Histogramm keinen Sinn
        Dim doLand As Boolean = (res.LandCount >= 10 AndAlso res.LandMaxM > 0.0)
        Dim doOcean As Boolean = (res.OceanCount >= 10 AndAlso res.OceanMaxDepthM > 0.0)

        '--------------------------
        ' Pass 2: Histogramm füllen
        '--------------------------

        Dim bins As Integer = ReliefRenderSettings.HistogramBins
        If bins < 256 Then bins = 256

        Dim landHist As Integer() = If(doLand, New Integer(bins - 1) {}, Nothing)
        Dim oceanHist As Integer() = If(doOcean, New Integer(bins - 1) {}, Nothing)

        If doLand OrElse doOcean Then

            For i As Integer = 0 To heightM.Length - 1

                Dim h As Double = CDbl(heightM(i))
                If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then Continue For

                If doLand AndAlso h > 0 Then
                    Dim t As Double = h / res.LandMaxM      '0..1
                    Dim b As Integer = BinIndex(t, bins)
                    landHist(b) += 1
                ElseIf doOcean AndAlso h < 0 Then
                    Dim d As Double = -h
                    Dim t As Double = d / res.OceanMaxDepthM   '0..1
                    Dim b As Integer = BinIndex(t, bins)
                    oceanHist(b) += 1
                End If
            Next
        End If

        '---------------------------
        ' Pass 3: Perzentile aus CDF
        '---------------------------

        If doLand Then
            Dim tScale As Double = PercentileFromHistogram(landHist, res.LandCount, ReliefRenderSettings.LandPhi)
            'tScale ist normiert 0..1 relativ zu LandMax
            res.LandScaleM = Math.Max(1.0, tScale * res.LandMaxM)
        End If

        If doOcean Then
            Dim tScale As Double = PercentileFromHistogram(oceanHist, res.OceanCount, ReliefRenderSettings.OceanPhi)
            res.OceanScaleM = Math.Max(1.0, tScale * res.OceanMaxDepthM)
        End If

        Return res
    End Function


    Private Shared Function BinIndex(t As Double, bins As Integer) As Integer

        If t <= 0 Then Return 0
        If t >= 1 Then Return bins - 1

        Dim x As Integer = CInt(Math.Floor(t * bins))

        x = Clamp(x, 0, bins - 1)
        Return x

    End Function

    ''' <summary>
    ''' Liefert normiertes t in [0..1], das dem gegebenen Perzentil entspricht.
    ''' pct muss [0..1] liegen (z.B. 0.98)
    ''' </summary>
    Private Shared Function PercentileFromHistogram(hist As Integer(), totalCount As Integer, pct As Double) As Double

        If hist Is Nothing OrElse hist.Length = 0 OrElse totalCount <= 0 Then Return Double.NaN

        pct = Clamp(pct, 0.0, 1.0)

        Dim target As Double = pct * totalCount
        Dim cum As Double = 0.0

        For b As Integer = 0 To hist.Length - 1
            cum += hist(b)

            If cum >= target Then
                'Bin-Mitte als Repräsentant
                Dim tMid As Double = (b + 0.5) / hist.Length
                Return Clamp(tMid, 0.0, 1.0)
            End If
        Next

        Return 1.0
    End Function

End Class
