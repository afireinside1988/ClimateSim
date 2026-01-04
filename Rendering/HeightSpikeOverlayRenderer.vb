

Imports System.Runtime.InteropServices

Public Class HeightSpikeOverlayRenderer

    Private Sub New()

    End Sub

    Private Const A As Byte = 120
    Private Shared ReadOnly SpikeColor As Color = Color.FromArgb(A, 255, 0, 255)       'Magenta

    Public Shared Function RenderSpikeMask(mask As Boolean(), width As Integer, height As Integer) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(mask)
        If width <= 0 OrElse height <= 0 Then Throw New ArgumentOutOfRangeException("Ungültige Rastergröße.")
        If mask.Length <> width * height Then Throw New InvalidOperationException("Größe der Spike-Mask ist ungültig.")

        Dim wb As New WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, Nothing)

        Dim stride As Integer = width * 4
        Dim bytes As Byte() = New Byte(stride * height - 1) {}

        For i As Integer = 0 To mask.Length - 1
            If Not mask(i) Then Continue For

            Dim x As Integer = i Mod width
            Dim y As Integer = i \ width
            Dim ofs As Integer = y * stride + x * 4
            bytes(ofs + 0) = SpikeColor.B
            bytes(ofs + 1) = SpikeColor.G
            bytes(ofs + 2) = SpikeColor.R
            bytes(ofs + 3) = SpikeColor.A
        Next

        wb.Lock()
        Try
            Marshal.Copy(bytes, 0, wb.BackBuffer, bytes.Length)
            wb.AddDirtyRect(New Int32Rect(0, 0, width, height))
        Finally
            wb.Unlock()
        End Try

        wb.Freeze()
        Return wb
    End Function

End Class
