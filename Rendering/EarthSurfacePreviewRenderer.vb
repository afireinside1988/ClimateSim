Imports System.IO
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public NotInheritable Class EarthSurfacePreviewRenderer

    Private Sub New()
    End Sub

    'Erzeugt eine WriteableBitmap: Land (>=0) = weiß, Ocean (<0) = schwarz, NaN = magenta
    Public Shared Function BuildLandOceanBitmapFromHeight(cache As EarthSurfaceCache) As WriteableBitmap

        Dim w As Integer = cache.Meta.LonCount
        Dim h As Integer = cache.Meta.LatCount

        Dim bmp As New WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, Nothing)
        Dim stride As Integer = w * 4
        Dim pixels As Byte() = New Byte(stride * h - 1) {}

        Dim data = cache.HeightM

        For y As Integer = 0 To h - 1
            For x As Integer = 0 To w - 1
                Dim idx As Integer = y * w + x
                Dim v As Single = data(idx)

                Dim b As Byte, g As Byte, r As Byte, a As Byte
                a = 255

                If Single.IsNaN(v) Then
                    'NaN sichtbar machen
                    r = 255 : g = 0 : b = 255
                ElseIf v >= 0.0F Then
                    r = 255 : g = 255 : b = 255
                Else
                    r = 0 : g = 0 : b = 0
                End If

                Dim p As Integer = (y * stride) + (x * 4)
                pixels(p + 0) = b
                pixels(p + 1) = g
                pixels(p + 2) = r
                pixels(p + 3) = a
            Next
        Next

        bmp.WritePixels(New System.Windows.Int32Rect(0, 0, w, h), pixels, stride, 0)
        Return bmp
    End Function

    'Optional: PNG speichern
    Public Shared Sub SavePng(bitmap As BitmapSource, filePath As String)
        Directory.CreateDirectory(Path.GetDirectoryName(filePath))
        Using fs As New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
            Dim enc As New PngBitmapEncoder()
            enc.Frames.Add(BitmapFrame.Create(bitmap))
            enc.Save(fs)
        End Using
    End Sub

End Class