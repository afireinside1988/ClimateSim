Public NotInheritable Class RgiRegionMask

    Private Const CellSizeDeg As Double = 0.5
    Private ReadOnly Property LonCount As Integer
    Private ReadOnly Property LatCount As Integer

    Private Const LonMin As Double = -180.0
    Private Const LatMin As Double = -90.0


    Public ReadOnly Property Regions As Byte()

    Public Sub New(regions As Byte())

        Me.Regions = regions
        Me.LonCount = CInt(360.0 / CellSizeDeg)
        Me.LatCount = CInt(180.0 / CellSizeDeg)

    End Sub

    Public Function GetRegion(lat As Double, lon As Double) As RGIRegion

        lon = Wrap180(lon)

        If lat < -90.0 OrElse lat > 90.0 Then Return RGIRegion.Global_Region
        If lon < -180.0 OrElse lon >= 180.0 Then Return RGIRegion.Global_Region

        Dim col As Integer = CInt(Math.Floor((lon - LonMin) / CellSizeDeg))
        Dim row As Integer = CInt(Math.Floor((lat - LatMin) / CellSizeDeg))

        If col < 0 OrElse col >= LonCount OrElse row < 0 OrElse row >= LatCount Then
            Return RGIRegion.Global_Region
        End If

        Dim idx As Integer = row * LonCount + col
        Return CType(Regions(idx), RGIRegion)

    End Function

End Class
