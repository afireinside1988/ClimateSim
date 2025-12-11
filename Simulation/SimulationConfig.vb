Public Class SimulationConfig

#Region "Öffentliche Felder"
    '--- Allgemeine Simulations-/Modellparameter ---

    Public Property StartYear As Integer
    Public Property EndYear As Integer

    Public Property GridWidth As Integer
    Public Property GridHeight As Integer

    Public Property TimeStepMode As TimeStepMode

    'Globale Klimasensitivität λ
    Public Property Lambda As Double

    '--- Solare Forcings/SimpleCycleMode ---
    Public Property SolarCycleMode As SolarCycleMode

    'Schwabe-Zyklus (~11 Jahre)
    Public Property UseSchwabeCycle As Boolean
    Public Property SchwabeAmplitude As Double
    Public Property SchwabePeriodYears As Double
    Public Property SchwabePhaseDeg As Double

    'Magnetischer Zyklus (~22 Jahre)
    Public Property UseMagneticCycle As Boolean
    Public Property MagneticAmplitude As Double
    Public Property MagneticPeriodYears As Double
    Public Property MagneticPhaseDeg As Double

    'Gleissberg-Zyklus (~80-100 Jahre)
    Public Property UseGleissbergCycle As Boolean
    Public Property GleissbergAmplitude As Double
    Public Property GleissbergPeriodYears As Double
    Public Property GleissbergPhaseDeg As Double

    'de Vries/Suess (~200 Jahre)
    Public Property UseDeVriesSuessCycle As Boolean
    Public Property DeVriesAmplitude As Double
    Public Property DeVriesPeriodYears As Double
    Public Property DeVriesPhaseDeg As Double

#End Region

    ''' <summary>
    ''' Erstellt eine Default-Konfiguration
    ''' </summary>
    ''' <returns></returns>
    Public Shared Function CreateDefault() As SimulationConfig
        Dim cfg As New SimulationConfig()

        'Allgemein
        cfg.StartYear = 1850
        cfg.EndYear = 2100
        cfg.GridWidth = 360
        cfg.GridHeight = 180
        cfg.TimeStepMode = TimeStepMode.Year
        cfg.Lambda = 0.5

        'Solare Zyklen: zunächst aus, aber sinnvolle Parameter voreinstellen
        cfg.SolarCycleMode = SolarCycleMode.None

        cfg.UseSchwabeCycle = True
        cfg.SchwabeAmplitude = 0.0005       '0.05% Halb-Amplitude
        cfg.SchwabePeriodYears = 11.04      'https://arxiv.org/abs/2004.10028
        cfg.SchwabePhaseDeg = 0.0

        cfg.UseMagneticCycle = True
        cfg.MagneticAmplitude = 0.0001      '0.01% Halb-Amplitude
        cfg.MagneticPeriodYears = 22.08     'https://en.wikipedia.org/wiki/Solar_cycle
        cfg.MagneticPhaseDeg = 0.0

        cfg.UseGleissbergCycle = True
        cfg.GleissbergAmplitude = 0.0002    '0.02% Halb-Amplitude
        cfg.GleissbergPeriodYears = 87.8    'https://en.wikipedia.org/wiki/Solar_cycle
        cfg.GleissbergPhaseDeg = 0.0

        cfg.UseDeVriesSuessCycle = True
        cfg.DeVriesAmplitude = 0.00025      '0.025% Halb-Amplitude
        cfg.DeVriesPeriodYears = 208.0      'https://en.wikipedia.org/wiki/Solar_cycle
        cfg.DeVriesPhaseDeg = 0.0

        Return cfg
    End Function

    Public Function Clone() As SimulationConfig
        Dim cfg As New SimulationConfig()

        'Allgemein
        cfg.StartYear = Me.StartYear
        cfg.EndYear = Me.EndYear
        cfg.GridWidth = Me.GridWidth
        cfg.GridHeight = Me.GridHeight
        cfg.TimeStepMode = Me.TimeStepMode
        cfg.Lambda = Me.Lambda

        'Solare Zyklen
        cfg.SolarCycleMode = Me.SolarCycleMode

        cfg.UseSchwabeCycle = Me.UseSchwabeCycle
        cfg.SchwabeAmplitude = Me.SchwabeAmplitude
        cfg.SchwabePeriodYears = Me.SchwabePeriodYears
        cfg.SchwabePhaseDeg = Me.SchwabePhaseDeg

        cfg.UseMagneticCycle = Me.UseMagneticCycle
        cfg.MagneticAmplitude = Me.MagneticAmplitude
        cfg.MagneticPeriodYears = Me.MagneticPeriodYears
        cfg.MagneticPhaseDeg = Me.MagneticPhaseDeg

        cfg.UseGleissbergCycle = Me.UseGleissbergCycle
        cfg.GleissbergAmplitude = Me.GleissbergAmplitude
        cfg.GleissbergPeriodYears = Me.GleissbergPeriodYears
        cfg.GleissbergPhaseDeg = Me.GleissbergPhaseDeg

        cfg.UseDeVriesSuessCycle = Me.UseDeVriesSuessCycle
        cfg.DeVriesAmplitude = Me.DeVriesAmplitude
        cfg.DeVriesPeriodYears = Me.DeVriesPeriodYears
        cfg.DeVriesPhaseDeg = Me.DeVriesPhaseDeg

        Return cfg
    End Function
End Class
