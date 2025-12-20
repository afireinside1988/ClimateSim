''' <summary>
''' Rendert SurfaceType direkt aus einem IEarthSurfaceProvider, aber auf exakt dem Raster eines ClimateGrid.
''' Damit kann man "Provider vs. Grid" deterministisch vergleichen
''' </summary>
Public Class ProviderSurfaceTypeRenderer

    Public Shared Function RenderSurfaceType(provider As IEarthSurfaceProvider, grid As ClimateGrid) As WriteableBitmap

        ArgumentNullException.ThrowIfNull(provider)
        ArgumentNullException.ThrowIfNull(grid)

        Dim width As Integer = grid.Width
        Dim height As Integer = grid.Height

        Dim dpi As Double = 200.0
        Dim bmp As New WriteableBitmap(width, height, dpi, dpi, PixelFormats.Bgra32, Nothing)

        Dim pixels(width * height - 1) As Integer
        Dim idx As Integer = 0

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1

                Dim cell As ClimateCell = grid.GetCell(lat, lon)

                'WICHTIG: exakt dieselben Koordinaten nutzen, die auch IEarthInitializer nutzt.
                Dim info As SurfaceInfo = provider.GetSurfaceInfo(cell.LatitudeDeg, cell.LongitudeDeg)

                Dim c As Color = ColorForSurface(info.Surface)

                'BGRA32 zusammensetzen (wie im SurfaceTypeRenderer)
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

    'Farben identisch zur Referenz (SurfaceTypeRenderer)
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
