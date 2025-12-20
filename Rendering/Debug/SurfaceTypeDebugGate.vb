Public Class SurfaceTypeDebugGateResult
    Public Property GridBitmap As WriteableBitmap
    Public Property ProviderBitmap As WriteableBitmap
    Public Property DiffOverlay As WriteableBitmap
    Public Property DifferentPixelCount As Integer
    Public Property TotalPixelCount As Integer

    Public ReadOnly Property DifferentPercent As Double
        Get
            If TotalPixelCount <= 0 Then Return 0.0
            Return 100.0 * DifferentPixelCount / CDbl(TotalPixelCount)
        End Get
    End Property
End Class


Public Class SurfaceTypeDebugGate

    Public Shared Function Run(provider As IEarthSurfaceProvider, grid As ClimateGrid) As SurfaceTypeDebugGateResult

        ArgumentNullException.ThrowIfNull(provider)
        ArgumentNullException.ThrowIfNull(grid)

        Dim gridBmp = SurfaceTypeRenderer.RenderSurfaceType(grid)
        Dim provBmp = ProviderSurfaceTypeRenderer.RenderSurfaceType(provider, grid)
        Dim diffRes = BitmapDiffRenderer.RenderDiffOverlay(gridBmp, provBmp)

        Return New SurfaceTypeDebugGateResult With {
            .GridBitmap = gridBmp,
            .ProviderBitmap = provBmp,
            .DiffOverlay = diffRes.DiffBitmap,
            .DifferentPixelCount = diffRes.DifferentPixelCount,
            .TotalPixelCount = diffRes.TotalPixelCount
        }

    End Function

End Class
