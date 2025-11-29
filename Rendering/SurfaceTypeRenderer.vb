Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging

Public Class SurfaceTypeRenderer

    Public Shared Function RenderSurfaceType(grid As ClimateGrid) As WriteableBitmap
        Dim width As Integer = grid.Width
        Dim height As Integer = grid.Height

        Dim dpi = 96.0 'Auflösung des Renderings
        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)

        Dim pixels(width * height - 1) As Integer
        Dim idx As Integer = 0

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                Dim cell As ClimateCell = grid.GetCell(lat, lon)
                Dim c As Color = ColorForSurface(cell.Surface)

                'BGRA32 zusammensetzen
                Dim argb As Integer =
                    (CInt(c.A) << 24) Or
                    (CInt(c.R) << 16) Or
                    (CInt(c.G) << 8) Or
                    CInt(c.B)

                pixels(idx) = argb
                idx += 1
            Next
        Next

        Dim stride As Integer = width * 4
        bmp.WritePixels(New Int32Rect(0, 0, width, height), pixels, stride, 0)

        Return bmp
    End Function

    Private Shared Function ColorForSurface(surface As SurfaceType) As Color
        Select Case surface
            Case SurfaceType.Ocean
                Return Colors.MidnightBlue
            Case SurfaceType.SeaIce
                Return Colors.LightCyan
            Case SurfaceType.LandPlain
                Return Colors.OliveDrab
            Case SurfaceType.LandForest
                Return Colors.ForestGreen
            Case SurfaceType.LandDesert
                Return Colors.SandyBrown
            Case SurfaceType.LandMountain
                Return Colors.SaddleBrown
            Case SurfaceType.LandIce
                Return Colors.White
            Case Else
                Return Colors.Gray
        End Select
    End Function
End Class
