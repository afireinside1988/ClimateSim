Imports System.IO

Public NotInheritable Class TidRenderer

    Public Shared Function RenderTidLayer(cache As EarthSurfaceCache,
                                          Optional alpha As Byte = 255,
                                          Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(cache)
        If cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache), "cache.Meta darf nicht Nothing sein.")

        Dim meta As EarthSurfaceCacheMeta = cache.Meta
        Dim w As Integer = meta.LonCount
        Dim h As Integer = meta.LatCount

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(w)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(h)

        Dim tidArr As Byte() = cache.Tid
        If tidArr Is Nothing OrElse tidArr.Length <> w * h Then Throw New InvalidDataException("TID fehlt oder hat die falsche Größe.")

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer

        '---TID-Color LUT---
        Dim aShift As Integer = CInt(alpha) << 24
        Dim lut(255) As Integer

        For t As Integer = 0 To 255
            Dim code As Integer = TidHelpers.TidByteToCode(CByte(t))
            Dim c As Color = TidLegend.TidColor(code)
            lut(t) = aShift Or (CInt(c.R) << 16) Or (CInt(c.G) << 8) Or CInt(c.B)
        Next

        Parallel.For(
            0, pixels.Length,
            Sub(i)

                pixels(i) = lut(tidArr(i))
            End Sub)

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp

    End Function

End Class
