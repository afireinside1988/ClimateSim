Imports System.ComponentModel

Public Class SimulationConfig
    Inherits ViewModelBase

#Region "Private Felder"

    Private _startYear As Integer
    Private _endYear As Integer
    Private _gridWidth As Integer
    Private _gridHeight As Integer
    Private _timeStepMode As TimeStepMode
    Private _lambda As Double

    Private _solarCycleMode As SolarCycleMode

    Private _useSchwabeCycle As Boolean
    Private _schwabeAmplitude As Double
    Private _schwabePeriodYears As Double
    Private _schwabePhaseDeg As Double

    Private _useMagneticCycle As Boolean
    Private _magneticAmplitude As Double
    Private _magneticPeriodYears As Double
    Private _magneticPhaseDeg As Double

    Private _useGleissbergCycle As Boolean
    Private _gleissbergAmplitude As Double
    Private _gleissbergPeriodYears As Double
    Private _gleissbergPhaseDeg As Double

    Private _useDeVriesSuessCycle As Boolean
    Private _deVriesAmplitude As Double
    Private _deVriesPeriodYears As Double
    Private _deVriesPhaseDeg As Double

#End Region
#Region "Öffentliche Felder"
    '--- Allgemeine Simulations-/Modellparameter ---

    Public Property StartYear As Integer
        Get
            Return _startYear
        End Get
        Set(value As Integer)
            SetProperty(_startYear, value)
        End Set
    End Property
    Public Property EndYear As Integer
        Get
            Return _endYear
        End Get
        Set(value As Integer)
            SetProperty(_endYear, value)
        End Set
    End Property

    Public Property GridWidth As Integer
        Get
            Return _gridWidth
        End Get
        Set(value As Integer)
            SetProperty(_gridWidth, value)
        End Set
    End Property
    Public Property GridHeight As Integer
        Get
            Return _gridHeight
        End Get
        Set(value As Integer)
            SetProperty(_gridHeight, value)
        End Set
    End Property

    Public Property TimeStepMode As TimeStepMode
        Get
            Return _timeStepMode
        End Get
        Set(value As TimeStepMode)
            If SetProperty(_timeStepMode, value) Then
                'TimeStepDescription hängt davon ab
                OnPropertyChanged(NameOf(TimeStepDescription))
            End If
        End Set
    End Property
    Public ReadOnly Property TimeStepDescription As String
        Get
            Select Case Me.TimeStepMode
                Case TimeStepMode.Month
                    Return "1 Monat"
                Case TimeStepMode.Quarter
                    Return "1 Quartal"
                Case TimeStepMode.Year
                    Return "1 Jahr"
                Case TimeStepMode.Decade
                    Return "10 Jahre"
            End Select

            Return ""
        End Get
    End Property
    'Globale Klimasensitivität λ
    Public Property Lambda As Double
        Get
            Return _lambda
        End Get
        Set(value As Double)
            SetProperty(_lambda, value)
        End Set
    End Property

    '--- Solare Forcings/SimpleCycleMode ---
    Public Property SolarCycleMode As SolarCycleMode
        Get
            Return _solarCycleMode
        End Get
        Set(value As SolarCycleMode)
            SetProperty(_solarCycleMode, value)
        End Set
    End Property

    'Schwabe-Zyklus (~11 Jahre)
    Public Property UseSchwabeCycle As Boolean
        Get
            Return _useSchwabeCycle
        End Get
        Set(value As Boolean)
            SetProperty(_useSchwabeCycle, value)
        End Set
    End Property
    Public Property SchwabeAmplitude As Double
        Get
            Return _schwabeAmplitude
        End Get
        Set(value As Double)
            SetProperty(_schwabeAmplitude, value)
        End Set
    End Property
    Public Property SchwabePeriodYears As Double
        Get
            Return _schwabePeriodYears
        End Get
        Set(value As Double)
            SetProperty(_schwabePeriodYears, value)
        End Set
    End Property
    Public Property SchwabePhaseDeg As Double
        Get
            Return _schwabePhaseDeg
        End Get
        Set(value As Double)
            SetProperty(_schwabePhaseDeg, value)
        End Set
    End Property

    'Magnetischer Zyklus (~22 Jahre)
    Public Property UseMagneticCycle As Boolean
        Get
            Return _useMagneticCycle
        End Get
        Set(value As Boolean)
            SetProperty(_useMagneticCycle, value)
        End Set
    End Property
    Public Property MagneticAmplitude As Double
        Get
            Return _magneticAmplitude
        End Get
        Set(value As Double)
            SetProperty(_magneticAmplitude, value)
        End Set
    End Property
    Public Property MagneticPeriodYears As Double
        Get
            Return _magneticPeriodYears
        End Get
        Set(value As Double)
            SetProperty(_magneticPeriodYears, value)
        End Set
    End Property
    Public Property MagneticPhaseDeg As Double
        Get
            Return _magneticPhaseDeg
        End Get
        Set(value As Double)
            SetProperty(_magneticPhaseDeg, value)
        End Set
    End Property

    'Gleissberg-Zyklus (~80-100 Jahre)
    Public Property UseGleissbergCycle As Boolean
        Get
            Return _useGleissbergCycle
        End Get
        Set(value As Boolean)
            SetProperty(_useGleissbergCycle, value)
        End Set
    End Property
    Public Property GleissbergAmplitude As Double
        Get
            Return _gleissbergAmplitude
        End Get
        Set(value As Double)
            SetProperty(_gleissbergAmplitude, value)
        End Set
    End Property
    Public Property GleissbergPeriodYears As Double
        Get
            Return _gleissbergPeriodYears
        End Get
        Set(value As Double)
            SetProperty(_gleissbergPeriodYears, value)
        End Set
    End Property
    Public Property GleissbergPhaseDeg As Double
        Get
            Return _gleissbergPhaseDeg
        End Get
        Set(value As Double)
            SetProperty(_gleissbergPhaseDeg, value)
        End Set
    End Property

    'de Vries/Suess (~200 Jahre)
    Public Property UseDeVriesSuessCycle As Boolean
        Get
            Return _useDeVriesSuessCycle
        End Get
        Set(value As Boolean)
            SetProperty(_useDeVriesSuessCycle, value)
        End Set
    End Property
    Public Property DeVriesAmplitude As Double
        Get
            Return _deVriesAmplitude
        End Get
        Set(value As Double)
            SetProperty(_deVriesAmplitude, value)
        End Set
    End Property
    Public Property DeVriesPeriodYears As Double
        Get
            Return _deVriesPeriodYears
        End Get
        Set(value As Double)
            SetProperty(_deVriesPeriodYears, value)
        End Set
    End Property
    Public Property DeVriesPhaseDeg As Double
        Get
            Return _deVriesPhaseDeg
        End Get
        Set(value As Double)
            SetProperty(_deVriesPhaseDeg, value)
        End Set
    End Property

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
