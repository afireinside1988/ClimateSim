Public Module ImageComposeHelper

    Public Function ComposeOver(baseImg As ImageSource, overlayImg As ImageSource, Optional overlayOpacity As Double = 1.0) As ImageSource

        If baseImg Is Nothing Then Return Nothing
        If overlayImg Is Nothing OrElse overlayOpacity <= 0 Then Return baseImg

        Dim baseBmp As BitmapSource = TryCast(baseImg, BitmapSource)
        Dim ovBmp As BitmapSource = TryCast(overlayImg, BitmapSource)

        If baseBmp Is Nothing OrElse ovBmp Is Nothing Then
            'Fallback: wenn keine BitmapSource -> base zurückgeben
            Return baseImg
        End If

        Dim w As Integer = baseBmp.PixelWidth
        Dim h As Integer = baseBmp.PixelHeight
        Dim dpiX As Double = baseBmp.DpiX
        Dim dpiY As Double = baseBmp.DpiY

        Dim dv As New DrawingVisual()
        Using dc = dv.RenderOpen()

            'Base 1:1
            dc.DrawImage(baseBmp, New Rect(0, 0, w, h))

            'Overlay ggf. skalieren + Opacity anwenden
            Dim brush As New ImageBrush(ovBmp) With {
                .Stretch = Stretch.Fill,
                .Opacity = overlayOpacity
            }

            dc.DrawRectangle(brush, Nothing, New Rect(0, 0, w, h))
        End Using

        Dim rtb As New RenderTargetBitmap(w, h, dpiX, dpiY, PixelFormats.Pbgra32)
        rtb.Render(dv)

        If rtb.CanFreeze Then rtb.Freeze()
        Return rtb
    End Function

End Module
