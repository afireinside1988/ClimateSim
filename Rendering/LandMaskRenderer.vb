Public NotInheritable Class LandMaskRenderer

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Rendert eine Landmask aus dem EarthSurfaceCache (schwarz=Ocean; weiß=Land)
    ''' Optional: alpha steuern
    ''' </summary>
    Public Shared Function RenderLandMask(cache As EarthSurfaceCache,
                                          Optional alpha As Byte = 255,
                                          Optional dpi As Double = 96.0) As WriteableBitmap

        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim w As Integer = cache.Meta.LonCount
        Dim h As Integer = cache.Meta.LatCount
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(w)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(h)
        Dim mask As Byte() = cache.LandMask
        If mask Is Nothing OrElse mask.Length <> w * h Then Throw New InvalidOperationException("LandMask fehlt oder hat die falsche Größe.")

        If dpi <= 0 Then dpi = 96.0

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer

        Dim aShift As Integer = CInt(alpha) << 24
        Dim greyArgb As Integer = aShift Or (&HE6 << 16) Or (&HE6 << 8) Or &HE6
        Dim blackArgb As Integer = aShift        'RGB=0

        For i As Integer = 0 To pixels.Length - 1
            Dim island As Boolean = (mask(i) <> 0)
            pixels(i) = If(island, greyArgb, blackArgb)
        Next

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp
    End Function

    ''' <summary>
    ''' Rendert Küstenlinien aus dem EarthSurfaceCache (default Cyan und leicht transparent)
    ''' Optional: alpha steuern, andere Farbe festlegen, auch diagonale Pixel für die Kantenbestimmung heranziehen
    ''' </summary>
    ''' <param name="cache"></param>
    ''' <param name="color"></param>
    ''' <param name="alpha"></param>
    ''' <param name="dpi"></param>
    ''' <param name="includeDiagonal"></param>
    ''' <returns></returns>
    Public Shared Function RenderShoreLines(cache As EarthSurfaceCache,
                                            Optional color As Color? = Nothing,
                                            Optional alpha As Byte = 220,
                                            Optional dpi As Double = 96.0,
                                            Optional includeDiagonal As Boolean = False) As WriteableBitmap

        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim w As Integer = cache.Meta.LonCount
        Dim h As Integer = cache.Meta.LatCount
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(w)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(h)
        Dim mask As Byte() = cache.LandMask

        If w < 3 OrElse h < 3 Then
            Dim empty As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
            empty.Freeze()
            Return empty
        End If

        If mask Is Nothing OrElse mask.Length <> w * h Then Throw New InvalidOperationException("LandMask fehlt oder hat die falsche Größe.")

        If dpi <= 0 Then dpi = 96.0

        Dim c As Color = If(color, Colors.Cyan)
        Dim aShift As Integer = CInt(alpha) << 24
        Dim edgeArgb As Integer = aShift Or (CInt(c.R) << 16) Or (CInt(c.G) << 8) Or CInt(c.B)

        Dim bmp As New WriteableBitmap(w, h, dpi, dpi, PixelFormats.Bgra32, Nothing)
        Dim pixels(w * h - 1) As Integer    'default = 0 -> transparent

        'Rand lassen wir transparent (1..h-2 / 1..w-2), damit kein Out-of-Range
        For y As Integer = 1 To h - 2

            Dim row As Integer = y * w

            For x As Integer = 1 To w - 2

                Dim i As Integer = row + x

                Dim isLand As Boolean = (mask(i) <> 0)

                'die 4 Nachbarn wenn keine Diagonalen
                Dim north As Boolean = (mask(i - w) <> 0)
                Dim south As Boolean = (mask(i + w) <> 0)
                Dim west As Boolean = (mask(i - 1) <> 0)
                Dim east As Boolean = (mask(i + 1) <> 0)

                Dim isEdge As Boolean = False

                If isLand Then
                    isEdge = (north = False) OrElse
                             (south = False) OrElse
                             (west = False) OrElse
                             (east = False)
                End If


                If isLand AndAlso Not isEdge AndAlso includeDiagonal Then
                    'Optional die Diagonalen
                    Dim northwest As Boolean = (mask(i - w - 1) <> 0)
                    Dim northeast As Boolean = (mask(i - w + 1) <> 0)
                    Dim southwest As Boolean = (mask(i + w - 1) <> 0)
                    Dim southeast As Boolean = (mask(i + w + 1) <> 0)

                    isEdge = (northwest = False) OrElse
                             (northeast = False) OrElse
                             (southwest = False) OrElse
                             (southeast = False)
                End If

                If isEdge Then pixels(i) = edgeArgb
            Next
        Next

        bmp.WritePixels(New Int32Rect(0, 0, w, h), pixels, w * 4, 0)
        bmp.Freeze()

        Return bmp
    End Function

End Class
