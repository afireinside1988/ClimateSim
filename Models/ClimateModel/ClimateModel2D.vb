Public Class ClimateModel2D

    Private ReadOnly _grid As ClimateGrid

    '--- Physikalische Modellparameter ---
    Public Property RelaxationTimescaleYears As Double = 30.0 'Zeitkonstante, mit der die Temperatur zur Gleichgewichtstemperatur zurückläuft (in Jahren)
    Public Property DiffusionCoefficient As Double = 0.01 'Stärke der horizontalen Diffusion (Spielparamter, dimensionslos)
    Public Property CO2ppm As Double = 420.0 'aktuelles Atmosphären-CO2
    Public Property CO2Base As Double = 280.0 'Referenz Atmosphäre-CO2
    Public Property ClimateSensitivityLambda As Double = 0.5 'Klimasensitivität λ [K/(W/m²)]

    'EBM-Paramter
    Public Shared Property InsolationShapeS2 As Double = -0.48 'Formfaktor für Breitenverlauf (klassische EBM-Ordnung): Negativ -> Äquator wärmer, Pole kälter
    Public Shared Property InsolationAmplitudeK As Double = 35.0 'Stärke des latitudinalen Gradienten (K)
    Public Shared Property BaseTemperatureK As Double = 287.0 'Globale Referenz-Temperatur (etwa 18°C)

    'Albedo-Paramter
    Public Shared Property AlbedoReference As Double = 0.3 'Albedo-Referenzwert
    Public Shared Property AlbedoSensitivityKPerUnit As Double = 25.0 'Albedo-Sensitivität (delta K/Albedo-Wert)


    Public Property BaseTemperatureOffsetK As Double = 0.0 'globaler Temperatur-Offset, um das gesamte Gleichgewichtsniveau zu verschieben

    '--- Ende physikalische Modellparameter ---

    '--- Jahreszeitensteuerung ---
    Public Property CurrentYearFraction As Double = 0.0 'Jahresphase (0..1), 0 = Jahresanfang, 0.25 = Frühling usw.

    ''' <summary>
    ''' Erstellt ein neues 2D-Klimamodell auf dem gegebenen Gitter.
    ''' </summary>
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

        '--- CO2-Forcing berechnen (global) ---
        Dim Fco2 As Double = 0.0
        If CO2ppm > 0 AndAlso CO2Base > 0 Then
            Fco2 = 5.35 * Math.Log(CO2ppm / CO2Base) 'W/m²
        End If

        'Effektive zusätzliche Gleichgewichtserwärmung in Kelvin
        Dim deltaTeqCO2 As Double = ClimateSensitivityLambda * Fco2

        For lat As Integer = 0 To height - 1
            For lon As Integer = 0 To width - 1
                Dim cell = _grid.GetCell(lat, lon)
                Dim T As Double = cell.TemperatureK

                ' 1) Basis-Gleichgewichtstemperatur abhängig von der Breite und der Höhe
                Dim TeqBase As Double = EquilibriumTemperatureForCell(cell)

                'CO2-bedingte Anpassung der Gleichgewichtstemperatur
                Dim Teq As Double = TeqBase + deltaTeqCO2

                ' Relaxation in Richtung (TeqBase + CO2-Effekt)
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
                Dim localC As Double = cell.HeatCapacityFactor
                If localC <= 0 Then localC = 1.0

                Dim dTdt As Double = (relaxTerm + diffTerm) / localC

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
    Public Function EquilibriumTemperatureForCell(cell As ClimateCell) As Double
        Dim latitudeDeg As Double = cell.LatitudeDeg

        '1) Jahresphase aus dem Modell (0..1)
        Dim yearFrac As Double = CurrentYearFraction
        If yearFrac < 0.0 Then yearFrac = 0.0
        If yearFrac >= 1.0 Then yearFrac -= Math.Floor(yearFrac)

        '2) Tagesgemittelte Einstrahlung Q(φ, t)
        Dim Q As Double = ComputeDailyMeanInsolation(latitudeDeg, yearFrac)

        '3) Normierung auf Referenzwert (globaler Mittelwert ~341.3 W/m²)
        Const Qref As Double = 341.3
        Dim qNorm As Double = If(Qref > 0.0, Q / Qref, 1.0)
        If qNorm < 0.0 Then qNorm = 0.0

        '4) "zonale" EBM-Temperatur aus qNorm
        Dim Tzonal As Double = BaseTemperatureK + InsolationAmplitudeK * (qNorm - 1.0)

        '2) Höhenkorrektur: nur für positive Höhen (Land), Meer bleibt bei 0m
        Dim effectiveHeight As Double = Math.Max(0.0, cell.HeightM)
        Dim deltaT_height As Double = ClimateConstants.ElevationLapseRateKPerM * effectiveHeight

        Dim Teq_noAlbedo As Double = Tzonal - deltaT_height

        '3) Albedo-Effekt
        Dim alphaCell As Double = cell.Albedo
        Dim alphaRef As Double = AlbedoReference

        Dim deltaT_albedo As Double = AlbedoSensitivityKPerUnit * (alphaRef - alphaCell)

        '4) Gesamte Gleichgewichtstemperatur
        Dim Teq As Double = Teq_noAlbedo + deltaT_albedo + BaseTemperatureOffsetK

        Return Teq

    End Function

    <Obsolete("Altes EBM")>
    Private Function ComputeInsolationFactor(latitudeDeg As Double) As Double
        Dim latRad As Double = latitudeDeg * Math.PI / 180
        Dim sinPhi As Double = Math.Sin(latRad)

        ' P2(sin φ) = 0.5 * (3 sin²φ - 1)
        Dim P2 As Double = 0.5 * (3.0 * sinPhi * sinPhi - 1.0)

        Dim s2 As Double = InsolationShapeS2

        ' q(φ) = 1 + s2 * P2(sin φ)
        Dim q As Double = 1.0 + s2 * P2

        Return q
    End Function

    ''' <summary>
    ''' Tagesgemittelte solare Einstrahlung am TOA (W/m²) für gegebene Breite und Jahresphase.
    ''' Vereinfachtes Astronomie-Modell (kreisförmige Bahn, konstante Bahngeschwindigkeit).
    ''' </summary>
    ''' <param name="latitudeDeg"></param>
    ''' <param name="yearFraction"></param>
    ''' <returns></returns>
    Private Function ComputeDailyMeanInsolation(latitudeDeg As Double, yearFraction As Double) As Double

        '1) --- Eingaben normieren ---
        If yearFraction < 0.0 Then
            yearFraction = 0.0
        ElseIf yearFraction >= 1.0 Then
            yearFraction -= Math.Floor(yearFraction)
        End If

        Dim dayOfYear As Double = yearFraction * ClimateConstants.DaysPerYear

        '2) --- Solare Deklination δ(N) ---
        ' δ ≈ ε * sin( 2π * (N + 284) / 365 )
        Dim gamma As Double = 2.0 * Math.PI * (dayOfYear + 284.0) / DaysPerYear
        Dim decl As Double = ClimateConstants.EarthObliquityRad * Math.Sin(gamma)

        '3) --- Breite in Radiant ---
        Dim phi As Double = latitudeDeg * Math.PI / 180.0

        '4) --- Tageslänge über Stundenwinkel H0 ---
        ' cos(H0) = -tanφ * tanδ
        Dim tanPhi As Double = Math.Tan(phi)
        Dim tanDecl As Double = Math.Tan(decl)
        Dim arg As Double = -tanPhi * tanDecl

        Dim H0 As Double

        If arg >= 1.0 Then
            'Polarnacht: 0 Tageslicht
            H0 = 0.0
        ElseIf arg <= -1.0 Then
            'Polartag: 24h Sonne
            H0 = Math.PI
        Else
            H0 = Math.Acos(arg)
        End If

        '5) --- Tagesmittel Q(φ,δ) = (S0/π) * [H0 sinφ sinδ + cosφ cosδ sinH0] ---
        Dim sinPhi As Double = Math.Sin(phi)
        Dim cosPhi As Double = Math.Cos(phi)
        Dim sinDecl As Double = Math.Sin(decl)
        Dim cosDecl As Double = Math.Cos(decl)

        Dim Q As Double = 0.0

        If H0 > 0.0 Then
            Q = (ClimateConstants.SolarConstantWm2 / Math.PI) * (H0 * sinPhi * sinDecl + cosPhi * cosDecl * Math.Sin(H0))

            If Q < 0.0 Then Q = 0.0
        End If

        Return Q 'W/m²
    End Function
End Class
