Public Class ClimateInitializer
    ''' <summary>
    ''' Setzt ein einfaches Temperaturfeld:
    ''' - warm am Äquator
    ''' - kalt an den Polen
    ''' Temperatur in Kelvin
    ''' </summary>
    ''' 

    Private Shared ReadOnly _rnd As New Random()

    ''' <summary>
    ''' Fallback-Initialisierung wenn kein Temperatur-Provider vorliegt
    ''' </summary>
    ''' <param name="grid"></param>
    Public Shared Sub InitializeSimpleLatitudeProfile(grid As ClimateGrid)

        'Wir nehmen als groben globalen Mittelwert ca. 288K (15°C) an
        Dim T_equator As Double = 298.0 '27°C
        Dim T_pole As Double = 258.0 ' -33°C

        Dim height As Integer = grid.Height
        Dim width As Integer = grid.Width

        For latIndex As Integer = 0 To height - 1

            'Breite aus erster Zelle holen und Flächenfaktor berechnen
            Dim cell0 = grid.GetCell(latIndex, 0)
            Dim latDeg As Double = cell0.LatitudeDeg
            Dim latRad As Double = latDeg * Math.PI / 180.0

            'Flächengewicht (Zellfläche ∝ cos(lat))
            Dim areaWeight As Double = Math.Cos(latRad)
            areaWeight = Math.Max(areaWeight, MinAreaWeight)

            'Breitenprofil entsprechend EquilibriumTemperature: cos²(lat)
            Dim weight As Double = Math.Pow(Math.Cos(latRad), 2)

            Dim T_lat As Double = T_pole + (T_equator - T_pole) * weight

            For lonIndex As Integer = 0 To width - 1
                Dim cell = grid.GetCell(latIndex, lonIndex)

                'Flächengewicht in die Zelle schreiben
                cell.AreaWeight = areaWeight

                'kleines Rauschen hinzufügen (+/- 1K)
                Dim noise As Double = (_rnd.NextDouble() - 0.5) * 2.0
                cell.TemperatureK = T_lat + noise
            Next
        Next
    End Sub

    '''<summary>
    '''Initialisiert das Temperaturfled aus einem Temperature-Provider und setzt gleichzeitig AreaWeight pro Zelle
    ''' </summary>
    Public Shared Sub InitializeFromProvider(grid As ClimateGrid, provider As ITemperatureFieldProvider, startYear As Double)
        Dim height As Integer = grid.Height
        Dim width As Integer = grid.Width

        For latIndex As Integer = 0 To height - 1
            For lonIndex As Integer = 0 To width - 1
                Dim cell = grid.GetCell(latIndex, lonIndex)

                '1) Flächengewicht (∝ cos(lat))
                Dim latRad As Double = cell.LatitudeDeg * Math.PI / 180.0
                Dim areaWeight As Double = Math.Cos(latRad)
                areaWeight = Math.Max(areaWeight, MinAreaWeight)
                cell.AreaWeight = areaWeight

                '2) Basistemperatur in Kelvin aus dem Provider
                Dim Tbase As Double = provider.GetInitialTemperatureK(cell.LatitudeDeg, cell.LongitudeDeg, startYear)

                '3) Höhenkorrektur wie im Modell:
                Dim effectiveHeight As Double = Math.Max(0.0, cell.HeightM)
                Dim deltaT As Double = ElevationLapseRateKPerM * effectiveHeight

                Dim Telev As Double = Tbase - deltaT

                '4) Albedo-Korrektur (benutzt dieselben Paramter wie das Modell
                Dim alphaCell As Double = cell.Albedo
                Dim alphaRef As Double = ClimateModel2D.AlbedoReference
                Dim kAlbedo As Double = ClimateModel2D.AlbedoSensitivityKPerUnit

                Dim deltaT_albedo As Double = kAlbedo * (alphaRef - alphaCell)

                Dim Tinit As Double = Telev + deltaT_albedo

                'kleines Rauschen (+/- 1K), damit Diffusion "etwas zu tun hat"
                Dim noise As Double = (_rnd.NextDouble() - 0.5) * 2.0
                cell.TemperatureK = Tinit + noise
            Next
        Next
    End Sub

    Public Shared Sub InitializeFromModelEquilibrium(grid As ClimateGrid, model As ClimateModel2D)
        Dim height As Integer = grid.Height
        Dim width As Integer = grid.Width

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                Dim cell As ClimateCell = grid.GetCell(lat, lon)

                '1) Gleichgewichtstemperatur des aktuellen Modells
                Dim Teq As Double = model.EquilibriumTemperatureForCell(cell)

                '2) kleines Rauschen (+/- 0,5K)
                Dim noise As Double = (_rnd.NextDouble() - 0.5) * 2.0

                cell.TemperatureK = Teq + noise
            Next
        Next

    End Sub

End Class
