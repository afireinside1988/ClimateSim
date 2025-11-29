Imports System.Net.NetworkInformation

Public Class ToyEarthSurfaceProvider
    Implements IEarthSurfaceProvider

    Public Function GetSurfaceInfo(latitudeDeg As Double, longitudeDeg As Double) As SurfaceInfo Implements IEarthSurfaceProvider.GetSurfaceInfo

        'Start: Ozean-Tiefsee als Default
        Dim info As New SurfaceInfo With {
            .Surface = SurfaceType.Ocean,
            .HeightM = -3000.0,
            .HeatCapacityFactor = 6.0,
            .Albedo = 0.07
            }

        Dim lat As Double = latitudeDeg
        Dim lon As Double = longitudeDeg

        '--- Kontinente grob definieren ---

        'Nordamerika
        If lat > 8 AndAlso lat < 72 AndAlso
                lon > -170 AndAlso lon < -50 Then
            info.Surface = SurfaceType.LandPlain
            info.HeightM = 300.0

            'Südamerika
        ElseIf lat > -60 AndAlso lat < 12 AndAlso
            lon > -85 AndAlso lon < -30 Then
            info.Surface = SurfaceType.LandPlain
            info.HeightM = 300.0

            'Afrika
        ElseIf lat > -35 AndAlso lat < 35 AndAlso
                lon > -20 AndAlso lon < 50 Then
            info.Surface = SurfaceType.LandPlain
            info.HeightM = 300.0

            'Eurasien
        ElseIf lat > 10 AndAlso lat < 75 AndAlso
                lon > -10 AndAlso lon < 150 Then
            info.Surface = SurfaceType.LandPlain
            info.HeightM = 400.0

            'Australien
        ElseIf lat > -45 AndAlso lat < 0 AndAlso
                lon > 110 AndAlso lon < 155 Then
            info.Surface = SurfaceType.LandPlain
            info.HeightM = 200.0
        End If

        '--- Polareisschilde ---

        'Grönland
        If lat > 60 AndAlso lat < 85 AndAlso
                lon > -60 AndAlso lon < -20 Then
            info.Surface = SurfaceType.LandIce
            info.HeightM = 2500.0
        End If

        'Antarktis
        If lat < -50 Then
            info.Surface = SurfaceType.LandIce
            info.HeightM = 2500.0
        End If

        Return info

    End Function
End Class
