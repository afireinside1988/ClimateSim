Public Class EarthInitializer

    'Hinweis: Die exakte Form der Kontinente/Biome kommt von IEarthSurfaceProvider.
    'Der Initialisierer selbst kennt keine "Erde", nur das Grid.
    Public Shared Sub InitializeSurface(grid As ClimateGrid, provider As IEarthSurfaceProvider)
        Dim height As Integer = grid.Height
        Dim width As Integer = grid.Width

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                Dim cell As ClimateCell = grid.GetCell(lat, lon)

                Dim info As SurfaceInfo = provider.GetSurfaceInfo(cell.LatitudeDeg, cell.LongitudeDeg)

                cell.Surface = info.Surface
                cell.HeightM = info.HeightM

                'Falls Provider spezifische Werte liefert, übernehmen; sonst Default-Werte je nach SurfaceType
                cell.Albedo = If(info.Albedo, DefaultAlbedoFor(cell.Surface))
                cell.HeatCapacityFactor = If(info.HeatCapacityFactor, DefaultHeatCapacityFor(cell.Surface, cell.HeightM))

            Next
        Next
    End Sub
End Class
