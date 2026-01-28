Imports System.IO

Public NotInheritable Class LandCoverRenderer

    Public Shared Function RenderLandCoverLayer(cache As LandCoverCache, Optional alpha As Byte = 255, Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache.Meta))

        Dim meta As LandCoverCacheMeta = cache.Meta
        Dim w As Integer = meta.LonCount
        Dim h As Integer = meta.LatCount

        Dim clsArr As Byte() = cache.LandCoverClass
        If clsArr Is Nothing OrElse clsArr.Length <> w * h Then Throw New InvalidDataException("LandCoverClass fehlt oder hat falsche Dimension.")

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer

        '--- LUT für 0..255 (wir nutzen effektiv nur 0..11) ---
        Dim aShift As Integer = CInt(alpha) << 24
        Dim lut(255) As Integer

        For i As Integer = 0 To 255
            Dim cls As LandCoverClass =
                If(i <= 11, CType(i, LandCoverClass), LandCoverClass.NoData)

            Dim c As Color = LandCoverSchema.GetColor(cls)
            lut(i) = aShift Or (CInt(c.R) << 16) Or (CInt(c.G) << 8) Or c.B
        Next

        Parallel.For(
            0, pixels.Length,
                Sub(i)
                    pixels(i) = lut(clsArr(i))
                End Sub)

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp

    End Function

    Public Shared Function RenderConfidenceOverlay(cache As LandCoverCache, Optional alpha As Byte = 160, Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache.Meta))

        Dim meta As LandCoverCacheMeta = cache.Meta
        Dim w As Integer = meta.LonCount
        Dim h As Integer = meta.LatCount

        Dim conf As Byte() = cache.Confidence
        If conf Is Nothing OrElse conf.Length <> w * h Then Throw New InvalidDataException("LandCoverConfidence fehlt oder hat falsche Dimension.")

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer

        Dim aShift As Integer = CInt(alpha) << 24

        'LUT: value -> Grau RGB
        Dim lut(255) As Integer
        For v As Integer = 0 To 255
            Dim g As Integer = v
            lut(v) = aShift Or (g << 16) Or (g << 8) Or g
        Next

        Parallel.For(
            0, pixels.Length,
                Sub(i)
                    pixels(i) = lut(conf(i))
                End Sub)

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp
    End Function

End Class
