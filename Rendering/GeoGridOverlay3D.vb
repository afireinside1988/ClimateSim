
Public Class GeoGridOverlay3D

    Public Shared Function BuildGridOverlayImage(pxW As Integer, pxH As Integer, Optional highlightEquator As Boolean = True) As ImageSource

        Dim dv As New DrawingVisual()

        Using dc As DrawingContext = dv.RenderOpen()

            'Transparenter Hintergrund
            dc.DrawRectangle(Brushes.Transparent, Nothing, New Rect(0, 0, pxW, pxH))

            'Linien-Stil
            Dim lineBrush As New SolidColorBrush(Color.FromArgb(110, 255, 255, 255))
            If lineBrush.CanFreeze Then lineBrush.Freeze()

            Dim pen As New Pen(lineBrush, 0.5)
            pen.Freeze()

            'Optional Äquator und Nullmeridian hervorheben
            Dim majorPen As Pen = pen
            If highlightEquator Then
                Dim majorBrush As New SolidColorBrush(Color.FromArgb(160, 255, 255, 255))
                If majorBrush.CanFreeze Then majorBrush.Freeze()
                majorPen = New Pen(majorBrush, 0.8)
                majorPen.Freeze()
            End If

            '=== Meridiane (nur bis 350, damit sich Ost-West-Naht nicht doppelt) ===
            For lon As Integer = 0 To 350 Step 10
                Dim x As Double = (lon / 360.0) * (pxW - 1)
                dc.DrawLine(pen, New Point(x, 0), New Point(x, pxH - 1))
            Next

            '=== Äquatorialparallelen ===
            'lat: +90 oben -> -90 unten
            For lat As Integer = -80 To 80 Step 10
                Dim y As Double = ((90.0 - lat) / 180.0) * (pxH - 1)
                If highlightEquator Then
                    Dim usepen As Pen = If(lat = 0, majorPen, pen)
                    dc.DrawLine(usepen, New Point(0, y), New Point(pxW - 1, y))
                Else
                    dc.DrawLine(pen, New Point(0, y), New Point(pxW - 1, y))
                End If
            Next

        End Using

        Dim rtb As New RenderTargetBitmap(pxW, pxH, 96, 96, PixelFormats.Pbgra32)
        rtb.Render(dv)
        If rtb.CanFreeze Then rtb.Freeze()
        Return rtb
    End Function

End Class
