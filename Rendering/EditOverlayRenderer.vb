Imports System.Runtime.InteropServices

Public NotInheritable Class EditOverlayRenderer

    Private Sub New()

    End Sub

    'Alpha und Farben der EditOverlay-Pixel
    Private Const A As Byte = 120
    Private Shared ReadOnly ColorLand As Color = Color.FromArgb(A, 0, 255, 8)
    Private Shared ReadOnly ColorWater As Color = Color.FromArgb(A, 0, 140, 255)

    ''' <summary>
    ''' Erzeug ein leeres Overlay-Bitmap
    ''' </summary>
    Public Shared Function CreateEmpty(width As Integer, height As Integer) As WriteableBitmap
        If width <= 0 OrElse height <= 0 Then Return Nothing

        Dim wb As New WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, Nothing)
        ClearAll(wb)
        Return wb
    End Function

    ''' <summary>
    ''' Setzt das gesamte Bitmap auf transparent
    ''' </summary>
    ''' <param name="wb"></param>
    Public Shared Sub ClearAll(wb As WriteableBitmap)
        If wb Is Nothing Then Return

        Dim w As Integer = wb.PixelWidth
        Dim h As Integer = wb.PixelHeight
        Dim stride = w * 4
        Dim bytes(stride - h - 1) As Byte

        wb.Lock()
        Try
            Marshal.Copy(bytes, 0, wb.BackBuffer, bytes.Length)
            wb.AddDirtyRect(New Int32Rect(0, 0, w, h))
        Finally
            wb.Unlock()
        End Try
    End Sub

    ''' <summary>
    ''' Komplett-Rebuild: Nutzt die Overrides aus session.Delta und zeichnet alle editierten Zellen.
    ''' Für "Session neu" / "Cache neu" / "EditMode an"
    ''' </summary>
    Public Shared Sub RebuildAll(wb As WriteableBitmap, session As EarthSurfaceEditSession, lonCount As Integer, latCount As Integer)

        If wb Is Nothing OrElse session Is Nothing Then Return
        If lonCount <= 0 OrElse latCount <= 0 Then Return

        Dim w As Integer = wb.PixelWidth
        Dim h As Integer = wb.PixelHeight
        If w <> lonCount OrElse h <> latCount Then
            Throw New InvalidOperationException("Overlay-Bitmap-Größe passt nicht zum Cache-Raster.")
        End If

        Dim stride As Integer = w * 4
        Dim bytes(stride * h - 1) As Byte      'transparent vorinitialisiert

        'LandMask-Overrides malen
        For Each kvp In session.Delta.LandMaskOverrides
            Dim idx As Integer = kvp.Key
            Dim v As Byte = kvp.Value

            If idx < 0 OrElse idx >= w * h Then Continue For
            Dim x As Integer = idx Mod w
            Dim y As Integer = idx \ w

            WritePixelToArray(bytes, stride, x, y, If(v = 1, ColorLand, ColorWater))
        Next

        wb.Lock()
        Try
            Marshal.Copy(bytes, 0, wb.BackBuffer, bytes.Length)
            wb.AddDirtyRect(New Int32Rect(0, 0, w, h))
        Finally
            wb.Unlock()
        End Try
    End Sub

    ''' <summary>
    ''' Inkrementell: aktualisiert genau einen Index (idx).
    ''' - wenn Override vorhanden: Halbtransparentfarbe entsprechend Wert (0/1)
    ''' - wenn Override nicht vorhanden: Pixel transparent (zurückgesetzt)
    ''' </summary>
    Public Shared Sub UpdateOne(wb As WriteableBitmap, session As EarthSurfaceEditSession, idx As Integer, lonCount As Integer, latCount As Integer)
        If wb Is Nothing OrElse session Is Nothing Then Return
        If lonCount <= 0 OrElse latCount <= 0 Then Return
        If idx < 0 OrElse idx >= lonCount * latCount Then Return

        Dim x As Integer = idx Mod lonCount
        Dim y As Integer = idx \ lonCount

        Dim buf(3) As Byte  'BGRA

        Dim v As Byte
        If session.Delta.TryGetLandMask(idx, v) Then
            Dim c As Color = If(v = 1, ColorLand, ColorWater)
            buf(0) = c.B
            buf(1) = c.G
            buf(2) = c.R
            buf(3) = c.A
        Else
            'transparent
            buf(0) = 0
            buf(1) = 0
            buf(2) = 0
            buf(3) = 0
        End If

        wb.WritePixels(New Int32Rect(x, y, 1, 1), buf, 4, 0)
    End Sub


    Private Shared Sub WritePixelToArray(bytes() As Byte, stride As Integer, x As Integer, y As Integer, c As Color)
        Dim offset As Integer = y * stride + x * 4
        bytes(offset + 0) = c.B
        bytes(offset + 1) = c.G
        bytes(offset + 2) = c.R
        bytes(offset + 3) = c.A
    End Sub
End Class
