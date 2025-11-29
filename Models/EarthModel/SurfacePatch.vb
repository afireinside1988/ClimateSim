Public Class SurfacePatch
    Public Property LatMin As Double
    Public Property LatMax As Double

    Public Property LonMin As Double
    Public Property LonMax As Double

    Public Property Surface As SurfaceType
    Public Property HeightM As Double

    Public Sub New(latMin As Double, latMax As Double,
                   lonMin As Double, lonMax As Double,
                   surface As SurfaceType,
                   heightM As Double)
        Me.LatMin = latMin
        Me.LatMax = latMax
        Me.LonMin = lonMin
        Me.LonMax = lonMax

        Me.Surface = surface
        Me.HeightM = heightM
    End Sub

    Public Function Contains(lat As Double, lon As Double) As Boolean
        Return lat >= LatMin AndAlso lat <= LatMax AndAlso
               lon >= LonMin AndAlso lon <= LonMax
    End Function

End Class
