Public Class SimulationEngine
    Public Property Grid As ClimateGrid
    Public Property Model As ClimateModel2D

    Public Property StartYear As Integer
    Public Property CurrentYear As Double
    Public Property SimTimeYears As Double

    Public Property CO2Scenario As ICo2Scenario

    Public ReadOnly Property History As List(Of SimulationRecord)

    Public Sub New()
        History = New List(Of SimulationRecord)
    End Sub

    '''<summary>
    '''Initialisiert Gird, Model und Zeit mit einer neuen Auflösung und Startjahr
    '''</summary>
    Public Sub Initialize(width As Integer, height As Integer, startYear As Integer)
        Me.StartYear = startYear
        Me.SimTimeYears = 0.0
        Me.CurrentYear = startYear

        Grid = New ClimateGrid(width, height)
        ClimateInitializer.InitializeSimpleLatitudeProfile(Grid)

        Model = New ClimateModel2D(Grid)
        Model.CO2Base = 280.0 'Als Basis das vorindustrielle Niveau
        'Default-Werte, können von außen überschrieben werden
        Model.RelaxationTimescaleYears = 5
        Model.DiffusionCoefficient = 0.1
        Model.ClimateSensitivityLambda = 0.5

        History.Clear()

        Dim initialMeanC As Double = Grid.ComputeGlobalMeanTemperatureC()
        Dim co2Now As Double = If(CO2Scenario IsNot Nothing, CO2Scenario.GetCO2ForYear(CurrentYear), 280.0)
        Model.CO2ppm = co2Now

        History.Add(New SimulationRecord With {
            .SimTimeYears = SimTimeYears,
            .Year = CurrentYear,
            .GlobalMeanTempC = initialMeanC,
            .CO2ppm = co2Now
                    })
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
    End Sub
End Class
