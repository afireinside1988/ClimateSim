Public Class ClimateInitializer
    ''' <summary>
    ''' Setzt ein einfaches Temperaturfeld:
    ''' - warm am Äquator
    ''' - kalt an den Polen
    ''' Temperatur in Kelvin
    ''' </summary>
    ''' 

    Private Shared ReadOnly _rnd As New Random()

    Public Shared Sub InitializeSimpleLatitudeProfile(grid As ClimateGrid)

        'Wir nehmen als groben globalen Mittelwert ca. 288K (15°C) an
        Dim T_equator As Double = 300.0 '27°C
        Dim T_pole As Double = 240.0 ' -33°C

        Dim height As Integer = grid.Height
        Dim width As Integer = grid.Width

        For latIndex As Integer = 0 To height - 1
            Dim y As Double = latIndex / CDbl(height - 1) '0..1 von Nordpol zu Südpol
            Dim distFromEquator As Double = Math.Abs(y - 0.5) * 2.0 '0 am Äquator, 1 an den Polen
            Dim T_lat As Double = T_equator - distFromEquator * (T_equator - T_pole)

            For lonIndex As Integer = 0 To width - 1
                Dim cell = grid.GetCell(latIndex, lonIndex)
                'kleines Rauschen hinzufügen (+/- 1K)
                Dim noise As Double = (_rnd.NextDouble() - 0.5) * 2.0
                cell.TemperatureK = T_lat + noise
            Next
        Next
    End Sub

End Class
