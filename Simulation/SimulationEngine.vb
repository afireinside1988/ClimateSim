Public Class SimulationEngine
    '2D-Komponente
    Public Property Grid As ClimateGrid
    Public Property Model As ClimateModel2D

    'Zeitkomponente
    Public Property StartYear As Integer
    Public Property CurrentYear As Double
    Public Property SimTimeYears As Double

    'Schnittstellen
    Public Property TemperatureProvider As ITemperatureFieldProvider
    Public Property EarthSurfaceProvider As IEarthSurfaceProvider
    Public Property CO2Scenario As ICo2Scenario

    'Aufzeichnung
    Public ReadOnly Property History As List(Of SimulationRecord)
    Public ReadOnly Property Snapshots As List(Of GridSnapshot)

    Public Sub New()
        History = New List(Of SimulationRecord)
        Snapshots = New List(Of GridSnapshot)
    End Sub

    '''<summary>
    '''Initialisiert Grid, Modell und Zeit mit einer neuen Auflösung und Startjahr
    '''</summary>
    Public Sub Initialize(width As Integer, height As Integer, startYear As Integer)
        Me.StartYear = startYear
        Me.SimTimeYears = 0.0
        Me.CurrentYear = startYear

        'Grid anlegen
        Grid = New ClimateGrid(width, height)

        '1) Erdoberfläche intitialiseren, falls Provider gesetzt ist
        If EarthSurfaceProvider IsNot Nothing Then
            EarthInitializer.InitializeSurface(Grid, EarthSurfaceProvider)
        End If

        '2) Modell anlegen, Parameter setzen
        Model = New ClimateModel2D(Grid)
        Model.CO2Base = 280.0 'Basis-Co2
        Model.RelaxationTimescaleYears = 30.0 'Systemträgheit
        Model.DiffusionCoefficient = 0.001 'Horizontale Diffusion
        Model.ClimateSensitivityLambda = 0.5 'Klimasensitivität
        Model.BaseTemperatureOffsetK = 0.0

        '3) CO2 passend zum Startjahr setzen
        Dim co2Now As Double = If(CO2Scenario IsNot Nothing, CO2Scenario.GetCO2ForYear(CurrentYear), 280.0)
        Model.CO2ppm = co2Now

        '4) Temperaturfeld NICHT mehr über TemperatureProvider, sondern direkt auf Modell-Gleichgewicht initialisieren (TemperatureProvider für spätere Realdaten aufheben)
        ClimateInitializer.InitializeFromModelEquilibrium(Grid, Model)

        'Verlauf und Snapshots zurücksetzen
        History.Clear()
        Snapshots.Clear()

        '5) Anfangswerte berechnen
        Dim initialMeanC As Double = Grid.ComputeGlobalMeanTemperatureC()


        '6) ersten Verlaufseintrag erzeugen
        History.Add(New SimulationRecord With {
                    .SimTimeYears = SimTimeYears,
                    .Year = CurrentYear,
                    .GlobalMeanTempC = initialMeanC,
                    .CO2ppm = co2Now
                    })

        '7) Grid-Snapshot schreiben
        Dim initialSnap As GridSnapshot = CreateSnapshotFromGrid()
        If initialSnap IsNot Nothing Then Snapshots.Add(initialSnap)

    End Sub

    '''<summary>
    '''Führt einen Simulationsschritt mit dt (in Jahren) aus und aktualisiert Zeit, CO2 und History.
    ''' </summary>
    Public Sub StepSimulation(dtYears As Double)
        If Grid Is Nothing OrElse Model Is Nothing Then Return

        Model.StepSimulation(dtYears)
        SimTimeYears += dtYears
        CurrentYear = StartYear + SimTimeYears

        Dim co2Now As Double = If(CO2Scenario IsNot Nothing, CO2Scenario.GetCO2ForYear(CurrentYear), Model.CO2ppm)
        Model.CO2ppm = co2Now

        Dim meanC As Double = Grid.ComputeGlobalMeanTemperatureC()

        History.Add(New SimulationRecord With {
            .SimTimeYears = SimTimeYears,
            .Year = CurrentYear,
            .GlobalMeanTempC = meanC,
            .CO2ppm = co2Now
                    })

        '--- Snapshot speichern ---
        Dim snap As GridSnapshot = CreateSnapshotFromGrid()
        If snap IsNot Nothing Then
            Snapshots.Add(snap)
        End If

    End Sub

    '''<summary>
    '''Springt zu einem bestimmten Index in der History
    ''' </summary>
    Public Sub JumpToIndex(index As Integer)
        If Grid Is Nothing OrElse Model Is Nothing Then Return
        If index < 0 OrElse index >= History.Count Then Return
        If index < 0 OrElse index >= Snapshots.Count Then Return

        Dim rec As SimulationRecord = History(index)
        Dim snap As GridSnapshot = Snapshots(index)

        'Zeit & Jahr setzen
        Me.SimTimeYears = rec.SimTimeYears
        Me.CurrentYear = rec.Year
        Me.Model.CO2ppm = rec.CO2ppm

        'Grid-Temperaturen zurückschreiben (nur wenn Größe passt)
        If snap.Width <> Grid.Width OrElse snap.Height <> Grid.Height Then
            Return
        End If

        For lat As Integer = 0 To snap.Height - 1
            For lon As Integer = 0 To snap.Width - 1
                Grid.GetCell(lat, lon).TemperatureK = snap.TemperaturesK(lat, lon)
            Next
        Next
    End Sub

    '''<summary>
    '''Erstellt Snapshots aus dem TemperaturGrid
    ''' </summary>
    Private Function CreateSnapshotFromGrid() As GridSnapshot
        If Grid Is Nothing Then Return Nothing

        Dim w As Integer = Grid.Width
        Dim h As Integer = Grid.Height

        Dim snap As New GridSnapshot() With {
            .Width = w,
            .Height = h
            }

        ReDim snap.TemperaturesK(h - 1, w - 1)

        For lat As Integer = 0 To h - 1
            For lon As Integer = 0 To w - 1
                snap.TemperaturesK(lat, lon) = Grid.GetCell(lat, lon).TemperatureK
            Next
        Next

        Return snap
    End Function
End Class

Public Class GridSnapshot
    Public Property Width As Integer
    Public Property Height As Integer
    Public TemperaturesK(,) As Double

End Class
