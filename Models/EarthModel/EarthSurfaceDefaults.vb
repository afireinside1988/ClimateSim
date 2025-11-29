Public Module EarthSurfaceDefaults
    Public Function DefaultAlbedoFor(surface As SurfaceType) As Double
        Select Case surface
            Case SurfaceType.Ocean
                Return 0.07
            Case SurfaceType.SeaIce
                Return 0.6
            Case SurfaceType.LandPlain
                Return 0.28
            Case SurfaceType.LandForest
                Return 0.18
            Case SurfaceType.LandDesert
                Return 0.38
            Case SurfaceType.LandMountain
                Return 0.3
            Case SurfaceType.LandIce
                Return 0.7
            Case Else
                Return 0.3
        End Select
    End Function

    Public Function DefaultHeatCapacityFor(surface As SurfaceType, heightM As Double) As Double
        Select Case surface
            Case SurfaceType.Ocean
                'Tiefsee deutlich träger
                If heightM < -2000 Then
                    Return 6.0
                Else
                    Return 4.0
                End If
            Case SurfaceType.SeaIce
                Return 3.0
            Case SurfaceType.LandIce
                Return 0.5
            Case SurfaceType.LandForest
                Return 1.3
            Case SurfaceType.LandDesert
                Return 0.8
            Case SurfaceType.LandMountain
                Return 1.0
            Case SurfaceType.LandPlain
                Return 1.0
            Case Else
                Return 1.0
        End Select
    End Function

End Module
