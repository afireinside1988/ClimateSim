Public Class BitmapDiffResult

    Public Property DiffBitmap As WriteableBitmap
    Public Property DifferentPixelCount As Integer
    Public Property TotalPixelCount As Integer

    Public ReadOnly Property DifferentRatio As Double
        Get
            If TotalPixelCount <= 0 Then Return 0.0
            Return DifferentPixelCount / CDbl(TotalPixelCount)
        End Get
    End Property

End Class

''' <summary>
''' Erstellt ein Diff-Overlay zweier gleich großer WriteableBitmaps (Bgra32).
''' Diff-Pixel werden rot (voll deckend) markiert, sonst transparent.
''' </summary>
Public Class BitmapDiffRenderer

    Public Shared Function RenderDiffOverlay(a As WriteableBitmap, b As WriteableBitmap) As BitmapDiffResult

        ArgumentNullException.ThrowIfNull(a)
        ArgumentNullException.ThrowIfNull(b)

        If a.PixelWidth <> b.PixelWidth OrElse a.PixelHeight <> b.PixelHeight Then
            Throw New ArgumentException("Bitmaps müssen die gleiche Größe haben.")
        End If

        If a.Format <> PixelFormats.Bgra32 OrElse b.Format <> PixelFormats.Bgra32 Then
            Throw New ArgumentException("Bitmaps müssen PixelFormats.Bgra32 haben")
        End If

        Dim width = a.PixelWidth
        Dim height = a.PixelHeight
        Dim stride = width * 4

        Dim bytesA(stride * height - 1) As Byte
        Dim bytesB(stride * height - 1) As Byte
        a.CopyPixels(bytesA, stride, 0)
        b.CopyPixels(bytesB, stride, 0)

        Dim diff As New WriteableBitmap(width, height, a.DpiX, a.DpiY, PixelFormats.Bgra32, Nothing)
        Dim diffBytes(stride * height - 1) As Byte

        Dim differentCount As Integer = 0
        Dim total As Integer = width * height

        'rot in BGRA
        Const Blue As Byte = 0
        Const Green As Byte = 0
        Const Red As Byte = 255
        Const Aalpha As Byte = 255

        For i As Integer = 0 To bytesA.Length - 1 Step 4
            'Vergleich BGRA (4 Bytes)
            If bytesA(i) <> bytesB(i) OrElse
                    bytesA(i + 1) <> bytesB(i + 1) OrElse
                    bytesA(i + 2) <> bytesB(i + 2) OrElse
                    bytesA(i + 3) <> bytesB(i + 3) Then

                differentCount += 1
                diffBytes(i) = Blue
                diffBytes(i + 1) = Green
                diffBytes(i + 2) = Red
                diffBytes(i + 3) = Aalpha
            Else
                'transparent
                diffBytes(i) = 0
                diffBytes(i + 1) = 0
                diffBytes(i + 2) = 0
                diffBytes(i + 3) = 0

            End If
        Next

        diff.WritePixels(New Int32Rect(0, 0, width, height), diffBytes, stride, 0)

        Return New BitmapDiffResult With {
            .DiffBitmap = diff,
            .DifferentPixelCount = differentCount,
            .TotalPixelCount = total
            }
    End Function
End Class
