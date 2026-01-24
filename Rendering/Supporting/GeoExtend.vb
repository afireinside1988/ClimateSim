Public Structure GeoExtent
    Public Property LatMin As Double
    Public Property LatMax As Double
    Public Property LonMin As Double
    Public Property LonMax As Double

    Public Sub New(latMin As Double, latMax As Double, lonMin As Double, lonMax As Double)
        Me.LatMin = latMin
        Me.LatMax = latMax
        Me.LonMin = lonMin
        Me.LonMax = lonMax
    End Sub

End Structure