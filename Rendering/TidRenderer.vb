Imports System.IO

Public NotInheritable Class TidRenderer

    Private Sub New()

    End Sub

    Public Shared Function RenderTidLayer(cache As EarthSurfaceCache,
                                          Optional alpha As Byte = 200,
                                          Optional dpi As Double = 96.0) As WriteableBitmap

        ArgumentNullException.ThrowIfNullOrEmpty(NameOf(cache))
        ArgumentNullException.ThrowIfNullOrEmpty(NameOf(cache.Meta))

        Dim meta As EarthSurfaceCacheMeta = cache.Meta
        Dim w As Integer = meta.LonCount
        Dim h As Integer = meta.LatCount

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(w)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(h)

        If cache.Tid Is Nothing OrElse cache.Tid.Length <> w * h Then Throw New InvalidDataException("TID fehlt oder hat die falsche Größe.")

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer

        Dim a As Integer = alpha

        For i As Integer = 0 To pixels.Length - 1

            Dim t As Single = cache.Tid(i)

            Dim code As Integer = TidHelpers.TidValueToCode(t)

            Dim c As Color = TidLegend.TidColor(code)
            pixels(i) = (a << 24) Or
                        (CInt(c.R) << 16) Or
                        (CInt(c.G) << 8) Or
                        CInt(c.B)
        Next

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp

    End Function

End Class
