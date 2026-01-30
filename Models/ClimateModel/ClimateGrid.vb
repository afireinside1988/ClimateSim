
Public Class ClimateGrid
    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer

    '2D-Array der Zellen: (latIndex, lonIndex)
    Private ReadOnly _cells(,) As ClimateCell

    Public Sub New(width As Integer, height As Integer)
        If width <= 0 OrElse height <= 0 Then
            Throw New ArgumentException("Width und Height müssen > 0 sein.")
        End If

        Me.Width = width
        Me.Height = height
        ReDim _cells(height - 1, width - 1)

        InitializeCells()

    End Sub

    <Obsolete("Alter Zell-Initialisierer mit Zellzentren auf Extrema")>
    Private Sub InitializeCellsOld()

        'Wir gehen davon aus, dass:
        'latIndex = 0 -> Nordpol (+90°)
        'latIndex = Height-1 -> Südpol (-90°)

        For latIndex As Integer = 0 To Height - 1
            Dim latDeg As Double = 90.0 - 180.0 * (latIndex / CDbl(Height - 1))

            For lonIndex As Integer = 0 To Width - 1
                Dim lonDeg As Double = -180.0 + 360.0 * (lonIndex / CDbl(Width - 1))

                _cells(latIndex, lonIndex) = New ClimateCell() With {
                    .LatitudeDeg = latDeg,
                    .LongitudeDeg = lonDeg,
                    .TemperatureK = 0.0, 'Defaultwerte für neue Zelle setzen: Tiefsee-Ozean
                    .Surface = SurfaceType.Ocean,
                    .HeightM = -3000.0,
                    .HeatCapacityFactor = 6.0,
                    .Albedo = 0.07,
                    .AreaWeight = 0.0
                }
            Next
        Next
    End Sub

    ''' <summary>
    ''' Neuer Zell-Initialisierer erzeugt half-cell-centered Zellen
    ''' </summary>
    Private Sub InitializeCells()

        'Half-cell-centered Grid:
        'latIndex 0         ->  +90 - 0.5*dLat
        'latIndex h-1       ->  -90 + 0.5*dLat
        '
        'lonIndex 0         ->  -180 + 0.5*dLon
        'lonIndex w-1       ->  +180 - 0.5*dLon

        Dim dLat As Double = 180.0 / CDbl(Height)
        Dim dLon As Double = 360.0 / CDbl(Width)

        For latIndex As Integer = 0 To Height - 1

            Dim latDeg As Double = 90.0 - (latIndex + 0.5) * dLat
            Dim latRad As Double = Mathematics.DegToRad(latDeg)

            'AreaWeight ~ cos(phi); nie negativ, nie exakt 0
            Dim areaW As Double = Math.Cos(latRad)
            If areaW < 0 Then areaW = 0

            For lonIndex As Integer = 0 To Width - 1

                Dim lonDeg As Double = -180.0 + (lonIndex + 0.5) * dLon

                _cells(latIndex, lonIndex) = New ClimateCell() With {
                    .LatitudeDeg = latDeg,
                    .LongitudeDeg = lonDeg,
                    .TemperatureK = 0.0,
                    .Surface = SurfaceType.Ocean,
                    .HeightM = -3000.0,
                    .HeatCapacityFactor = 6.0,
                    .Albedo = 0.07,
                    .AreaWeight = areaW
                    }
            Next

        Next

    End Sub

    Public Function GetCell(latIndex As Integer, lonIndex As Integer) As ClimateCell
        If latIndex < 0 OrElse latIndex >= Height Then
            Throw New ArgumentOutOfRangeException(NameOf(latIndex), "latIndex außerhalb des gültigen Bereichs.")
        End If
        If lonIndex < 0 OrElse lonIndex >= Width Then
            Throw New ArgumentOutOfRangeException(NameOf(lonIndex), "lonIndex außerhalb des gültigen Bereichs.")
        End If
        Return _cells(latIndex, lonIndex)
    End Function

    Public Function GetCells() As ClimateCell(,)
        Return _cells
    End Function

    Public Function ComputeGlobalMeanTemperatureC() As Double
        Dim weightedSum As Double = 0.0 'Summe aller gewichteten Temperaturen
        Dim weightSum As Double = 0.0 'Summe aller Flächengewichtungen

        For lat As Integer = 0 To Height - 1
            For lon As Integer = 0 To Width - 1
                Dim cell As ClimateCell = _cells(lat, lon)

                'Flächengewicht (falls noch 0, minimalen Wert verwenden
                Dim w As Double = cell.AreaWeight
                w = Math.Max(w, MinAreaWeight)

                'Umrechnung von Kelvin in Celsius
                Dim tempC As Double = cell.TemperatureK - 273.15

                weightedSum += tempC * w
                weightSum += w
            Next
        Next

        If weightSum <= 0 Then Return 0.0

        Return weightedSum / weightSum

    End Function

End Class
