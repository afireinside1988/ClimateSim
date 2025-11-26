Public Class ClimateModel2D

    Private ReadOnly _grid As ClimateGrid

    'Zeitkonstante, mit der die Temperatur zur Gleichgewichtstemperatur zurückläuft (in Jahren)
    Public Property RelaxationTimescaleYears As Double = 5.0

    'Stärke der horizontalen Diffusion (Spielparamter, dimensionslos)
    Public Property DiffusionCoefficient As Double = 0.1

    Public Sub New(grid As ClimateGrid)
        _grid = grid
    End Sub

    ''' <summary>
    ''' Führt einen Zeitschritt in Jahren aus (z.B. dtYears = 0.5).
    ''' </summary>
    Public Sub StepSimulation(dtYears As Double)
        Dim width = _grid.Width
        Dim height = _grid.Height

        Dim newTemps(height - 1, width - 1) As Double

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                Dim cell = _grid.GetCell(lat, lon)
                Dim T As Double = cell.TemperatureK

                ' 1) Gleichgewichtstemperatur abhängig von Breitengrad
                Dim Teq As Double = EquilibriumTemperature(cell.LatitudeDeg)

                ' Relaxation in Richtung Gleichgewichtstemperatur
                Dim relaxTerm As Double = (Teq - T) / RelaxationTimescaleYears

                ' 2) Diffusion mit 4 Nachbarzellen (N,S,E,W)
                Dim latN As Integer = If(lat = 0, 0, lat - 1) 'oberer Rand: auf sich selbst zeigen
                Dim latS As Integer = If(lat = height - 1, height - 1, lat + 1) 'unterer Rand: auf sich selbst zeigen
                Dim lonW As Integer = (lon - 1 + width) Mod width 'zyklisch in Längsrichtung
                Dim lonE As Integer = (lon + 1) Mod width

                Dim Tn As Double = _grid.GetCell(latN, lon).TemperatureK
                Dim Ts As Double = _grid.GetCell(latS, lon).TemperatureK
                Dim Tw As Double = _grid.GetCell(lat, lonW).TemperatureK
                Dim Te As Double = _grid.GetCell(lat, lonE).TemperatureK

                Dim laplacian As Double = Tn + Ts + Tw + Te - 4.0 * T
                Dim diffTerm As Double = DiffusionCoefficient * laplacian

                ' 3) Gesamtänderung
                Dim dTdt As Double = relaxTerm + diffTerm

                newTemps(lat, lon) = T + dTdt * dtYears
            Next
        Next

        'Neue Temperaturen zurückschreiben
        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                _grid.GetCell(lat, lon).TemperatureK = newTemps(lat, lon)
            Next
        Next
    End Sub

    ''' <summary>
    ''' "Soll"-Temperatur als Funktion der Breite (wie in unserem Initialisierer).
    ''' </summary>
    Private Function EquilibriumTemperature(latitudeDeg As Double) As Double
        Dim T_equator As Double = 300.0 '27°C
        Dim T_pole As Double = 240.0 ' -33°C

        Dim latRad As Double = latitudeDeg * Math.PI / 180.0
        Dim weight As Double = Math.Pow(Math.Cos(latRad), 2) 'Gewichtungsfaktor basierend auf cos²(lat))

        If weight < 0 Then weight = 0

        Return T_pole + (T_equator - T_pole) * weight
    End Function

End Class
