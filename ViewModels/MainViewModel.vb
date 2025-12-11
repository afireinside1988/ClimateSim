Imports System.ComponentModel
Imports System.Windows.Input
Imports System.Threading.Tasks
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports System.Runtime.InteropServices
Imports System.CodeDom

Public Class MainViewModel
    Inherits ViewModelBase

    Public ReadOnly Property AppTitle As String = AppInfoViewModel.AppTitleWithVersion
    Public ReadOnly Property ProductName As String = AppInfoViewModel.ProductName

#Region "Private Felder"

    'Aktuelle Simulationskonfiguratiuon (später JSON-persistierbar)
    Private _currentConfig As SimulationConfig

    Private _startYear As Integer
    Private _endYear As Integer
    Private _gridWidth As Integer
    Private _gridHeight As Integer

    Private _timeStepMode As TimeStepMode
    Private _timeStepIndex As Integer = 2                   '0=Monat, 1=Quartal, 2=Jahr, 3=Dekade
    Private _timeModeDescription As String = "1 Jahr"

    Private _co2Value As Double = 420.0

    Private _statusText As String = "Bitte Spin-Up starten."
    Private _statusLatText As String = "Lat: -"
    Private _statusLonText As String = "Lon: -"
    Private _statusTempText As String = "Temp: -"
    Private _statusSurfaceText As String = "Surface: -"

    Private _globalMeanText As String = "0,00 °C"
    Private _currentYearText As String = "1850"
    Private _simTimeText As String = "0,0 Jahre"
    Private _memoryEstimateText As String = ""
    Private _memoryEstimateBrush As Brush = Brushes.Black

    Private _lambda As Double = 0.5

    Private _surfaceImage As ImageSource
    Private _temperatureImage As ImageSource
    Private _temperatureOpacity As Double = 1.0
#End Region

#Region "--- Events, die die View abonnieren kann ---"

    Public Event StartSimulationRequested As EventHandler
    Public Event StopSimulationRequested As EventHandler
    Public Event SpinUpRequested As EventHandler
    Public Event StepRequested As EventHandler
    Public Event ShowHistoryRequested As EventHandler
    Public Event SimulationConfigRequested As EventHandler

#End Region

#Region "--- Commands ---"

    Public ReadOnly Property StartCommand As ICommand
    Public ReadOnly Property StopCommand As ICommand
    Public ReadOnly Property SpinUpCommand As ICommand
    Public ReadOnly Property StepCommand As ICommand
    Public ReadOnly Property ShowHistoryCommand As ICommand
    Public ReadOnly Property SimulationConfigCommand As ICommand

#End Region

    ' --- SimulationEngine bleibt erstmal hier drin, damit MainWindow weniger Felder hat ---
    Public Property Engine As SimulationEngine

#Region "--- Statusflags, damit Commands CanExecute nutzen können ---"

    Private _isInitialized As Boolean
    Private _isSimulationRunning As Boolean
    Private _isTemperatureLayerVisible As Boolean

#End Region

    Public Sub New()
        Engine = New SimulationEngine()

        '---Simulationskonfiguration initialisieren ---
        CurrentConfig = SimulationConfig.CreateDefault()
        'View-Properties an die Config anpassen (damit alles synchron startet)
        SyncViewFromConfig()

        Engine.CO2Scenario = New DefaultCo2Scenario()
        Engine.EarthSurfaceProvider = New ToyEarthSurfaceProvider()

        '--- Commands initialisieren ---

        'Start: async Command
        StartCommand = New AsyncRelayCommand(Of Object)(
            Async Function(o As Object) As Task
                'Code-Behind reagiert dann im Event-Handler (async Sub)
                RaiseEvent StartSimulationRequested(Me, EventArgs.Empty)
                Await Task.CompletedTask
            End Function,
            Function(o As Object) As Boolean
                'Start nur möglich, wenn initialisiert und nicht gerade eine Simulation läuft
                Return IsInitialized AndAlso Not IsSimulationRunning
            End Function)

        'Stop: synchron
        StopCommand = New RelayCommand(Of Object)(
            Sub(o As Object)
                RaiseEvent StopSimulationRequested(Me, EventArgs.Empty)
            End Sub,
            Function(o As Object) As Boolean
                'Stop nur sinnvoll, wenn gerade etwas läuft
                Return IsSimulationRunning
            End Function)

        'Spin-Up: async Command
        SpinUpCommand = New AsyncRelayCommand(Of Object)(
            Async Function(o As Object) As Task
                RaiseEvent SpinUpRequested(Me, EventArgs.Empty)
                Await Task.CompletedTask
            End Function,
            Function(o As Object) As Boolean
                'Spin-Up starten, wenn aktuell nichts läuft
                Return Not IsSimulationRunning
            End Function)

        'Step: synchron
        StepCommand = New RelayCommand(Of Object)(
            Sub(o As Object)
                RaiseEvent StepRequested(Me, EventArgs.Empty)
            End Sub,
            Function(o As Object) As Boolean
                'Einzelschritt nur, wenn initialisiert wurde und nichts läuft
                Return IsInitialized AndAlso Not IsSimulationRunning
            End Function)

        'Verlauf aufrufen: synchron
        ShowHistoryCommand = New RelayCommand(Of Object)(
            Sub(o As Object)
                RaiseEvent ShowHistoryRequested(Me, EventArgs.Empty)
            End Sub,
            Function(o As Object) As Boolean
                'Verlauf nur, wenn initialisiert ist und es Daten gibt
                Return IsInitialized AndAlso Engine IsNot Nothing AndAlso Engine.History IsNot Nothing AndAlso Engine.History.Count > 0
            End Function)

        'Simulations-Konfiguration aufrufen
        SimulationConfigCommand = New RelayCommand(Of Object)(
            Sub(o As Object)
                RaiseEvent SimulationConfigRequested(Me, EventArgs.Empty)
            End Sub,
            Function(o As Object) As Boolean
                'Konfiguration nur ändern, wenn gerade keine Simulation läuft
                Return Not IsSimulationRunning
            End Function
            )

        '--- Speicherprognose aktualisieren
        UpdateMemoryEstimate()
    End Sub

#Region "--- Properties für Bindings ---"

    Public Property CurrentConfig As SimulationConfig
        Get
            Return _currentConfig
        End Get
        Set(value As SimulationConfig)
            If Not Object.ReferenceEquals(_currentConfig, value) Then

                If _currentConfig IsNot Nothing Then
                    RemoveHandler _currentConfig.PropertyChanged, AddressOf OnConfigPropertyChanged
                End If

                _currentConfig = value

                If _currentConfig IsNot Nothing Then
                    AddHandler _currentConfig.PropertyChanged, AddressOf OnConfigPropertyChanged
                End If

                OnPropertyChanged(NameOf(CurrentConfig))

                SyncViewFromConfig()
                UpdateMemoryEstimate()
            End If
        End Set
    End Property

    Public Property StartYear As Integer
        Get
            Return _startYear
        End Get
        Set(value As Integer)
            SetProperty(_startYear, value)
            UpdateMemoryEstimate()
        End Set
    End Property
    Public Property EndYear As Integer
        Get
            Return _endYear
        End Get
        Set(value As Integer)
            SetProperty(_endYear, value)
            UpdateMemoryEstimate()
        End Set
    End Property
    Public Property GridWidth As Integer
        Get
            Return _gridWidth
        End Get
        Set(value As Integer)
            SetProperty(_gridWidth, value)
            UpdateMemoryEstimate()
        End Set
    End Property
    Public Property GridHeigth As Integer
        Get
            Return _gridHeight
        End Get
        Set(value As Integer)
            SetProperty(_gridHeight, value)
            UpdateMemoryEstimate()
        End Set
    End Property

    Public Property TimeStepMode As TimeStepMode
        Get
            Return _timeStepMode
        End Get
        Set(value As TimeStepMode)
            SetProperty(_timeStepMode, value)
            UpdateMemoryEstimate()
        End Set
    End Property
    Public Property TimeStepIndex As Integer
        Get
            Return _timeStepIndex
        End Get
        Set(value As Integer)
            SetProperty(_timeStepIndex, value)

            'Mapping Index -> Mode
            Select Case value
                Case 0
                    TimeStepMode = TimeStepMode.Month
                    TimeModeDescription = "1 Monat"
                Case 1
                    TimeStepMode = TimeStepMode.Quarter
                    TimeModeDescription = "1 Quartal"
                Case 2
                    TimeStepMode = TimeStepMode.Year
                    TimeModeDescription = "1 Jahr"
                Case 3
                    TimeStepMode = TimeStepMode.Decade
                    TimeModeDescription = "10 Jahre"
            End Select
        End Set
    End Property
    Public Property TimeModeDescription As String
        Get
            Return _timeModeDescription
        End Get
        Set(value As String)
            SetProperty(_timeModeDescription, value)
        End Set
    End Property

    Public Property CO2Value As Double
        Get
            Return _co2Value
        End Get
        Set(value As Double)
            If Math.Abs(_co2Value - value) > 0.0001 Then
                _co2Value = value
                OnPropertyChanged(NameOf(CO2Value))
            End If
        End Set
    End Property

    Public Property StatusText As String
        Get
            Return _statusText
        End Get
        Set(value As String)
            SetProperty(_statusText, value)
        End Set
    End Property
    Public Property StatusLatText As String
        Get
            Return _statusLatText
        End Get
        Set(value As String)
            SetProperty(_statusLatText, value)
        End Set
    End Property
    Public Property StatusLonText As String
        Get
            Return _statusLonText
        End Get
        Set(value As String)
            SetProperty(_statusLonText, value)
            OnPropertyChanged(NameOf(StatusLonText))
        End Set
    End Property
    Public Property StatusTempText As String
        Get
            Return _statusTempText
        End Get
        Set(value As String)
            SetProperty(_statusTempText, value)
        End Set
    End Property
    Public Property StatusSurfaceText As String
        Get
            Return _statusSurfaceText
        End Get
        Set(value As String)
            SetProperty(_statusSurfaceText, value)
        End Set
    End Property

    Public Property GlobalMeanText As String
        Get
            Return _globalMeanText
        End Get
        Set(value As String)
            SetProperty(_globalMeanText, value)
        End Set
    End Property

    Public Property CurrentYearText As String
        Get
            Return _currentYearText
        End Get
        Set(value As String)
            SetProperty(_currentYearText, value)
        End Set
    End Property

    Public Property SimTimeText As String
        Get
            Return _simTimeText
        End Get
        Set(value As String)
            SetProperty(_simTimeText, value)
        End Set
    End Property

    Public Property MemoryEstimateText As String
        Get
            Return _memoryEstimateText
        End Get
        Set(value As String)
            SetProperty(_memoryEstimateText, value)
        End Set
    End Property
    Public Property MemoryEstimateBrush As Brush
        Get
            Return _memoryEstimateBrush
        End Get
        Set(value As Brush)
            SetProperty(_memoryEstimateBrush, value)
        End Set
    End Property

    Public Property Lambda As Double
        Get
            Return _lambda
        End Get
        Set(value As Double)
            If Math.Abs(_lambda - value) > 0.0001 Then
                _lambda = value
                OnPropertyChanged(NameOf(Lambda))
            End If
        End Set
    End Property

    Public Property SurfaceImage As ImageSource
        Get
            Return _surfaceImage
        End Get
        Set(value As ImageSource)
            If Not Equals(_surfaceImage, value) Then
                _surfaceImage = value
                OnPropertyChanged(NameOf(SurfaceImage))
            End If
        End Set
    End Property
    Public Property TemperatureImage As ImageSource
        Get
            Return _temperatureImage
        End Get
        Set(value As ImageSource)
            If Not Equals(_temperatureImage, value) Then
                _temperatureImage = value
                OnPropertyChanged(NameOf(TemperatureImage))
            End If
        End Set
    End Property

    Public Property IsTemperatureLayerVisible As Boolean
        Get
            Return _isTemperatureLayerVisible
        End Get
        Set(value As Boolean)
            SetProperty(_isTemperatureLayerVisible, value)
        End Set
    End Property

    Public ReadOnly Property AreLayerControlsEnabled As Boolean
        Get
            'Entspricht dem bisherigen Verhalten:
            '- vor Spin-Up: False
            '- während Spin-Up: False (IsInitialized = False)
            '- nach Spin-Up: True (IsInitialized = True), egal ob Simulation läuft oder nicht
            Return IsInitialized AndAlso Not IsSimulationRunning
        End Get
    End Property

    Public Property TemperatureOpacity As Double
        Get
            Return _temperatureOpacity
        End Get
        Set(value As Double)
            If Math.Abs(_temperatureOpacity - value) > 0.0001 Then
                _temperatureOpacity = value
                OnPropertyChanged(NameOf(TemperatureOpacity))
            End If
        End Set
    End Property

    Public Property IsInitialized As Boolean
        Get
            Return _isInitialized
        End Get
        Set(value As Boolean)
            SetProperty(_isInitialized, value)
            OnPropertyChanged(NameOf(AreLayerControlsEnabled))
            CommandManager.InvalidateRequerySuggested()
        End Set
    End Property

    Public Property IsSimulationRunning As Boolean
        Get
            Return _isSimulationRunning
        End Get
        Set(value As Boolean)
            SetProperty(_isSimulationRunning, value)
            OnPropertyChanged(NameOf(AreLayerControlsEnabled))
            CommandManager.InvalidateRequerySuggested()
        End Set
    End Property

#End Region

#Region "--- Konfig-Sync (View <-> Config) ---"
    ''' <summary>
    ''' Überträgt die Werte aus CurrentConfig in die ViewModel-Properties
    ''' </summary>
    Public Sub SyncViewFromConfig()
        If CurrentConfig Is Nothing Then Return

        'Allgemeine Parameter
        StartYear = CurrentConfig.StartYear
        EndYear = CurrentConfig.EndYear
        GridWidth = CurrentConfig.GridWidth
        GridHeigth = CurrentConfig.GridHeight
        TimeStepMode = CurrentConfig.TimeStepMode

        'TimeStepIndex mitziehen, damit der Slider passt
        Select Case TimeStepMode
            Case TimeStepMode.Month
                TimeStepIndex = 0
            Case TimeStepMode.Quarter
                TimeStepIndex = 1
            Case TimeStepMode.Year
                TimeStepIndex = 2
            Case TimeStepMode.Decade
                TimeStepIndex = 3
        End Select

        Lambda = CurrentConfig.Lambda
    End Sub

    ''' <summary>
    ''' Überträgt die aktuellen ViewModel-Properties in CurrentConfig
    ''' </summary>
    Public Sub SyncConfigFromView()
        If CurrentConfig Is Nothing Then
            CurrentConfig = New SimulationConfig()
        End If

        'Allgemeine Paramter
        CurrentConfig.StartYear = Me.StartYear
        CurrentConfig.EndYear = Me.EndYear
        CurrentConfig.GridWidth = Me.GridWidth
        CurrentConfig.GridHeight = Me.GridHeigth
        CurrentConfig.TimeStepMode = Me.TimeStepMode
        CurrentConfig.Lambda = Me.Lambda

    End Sub

    Private Sub OnConfigPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        'Wenn sich eine der speicherrelevanten Größen ändert, Speichervorhersage aktualisieren
        Select Case e.PropertyName
            Case NameOf(SimulationConfig.StartYear),
                 NameOf(SimulationConfig.EndYear),
                 NameOf(SimulationConfig.GridWidth),
                 NameOf(SimulationConfig.GridHeight),
                 NameOf(SimulationConfig.TimeStepMode)

                SyncViewFromConfig()
                UpdateMemoryEstimate()
        End Select
    End Sub

#End Region

#Region "--- Memory-Helfer ---"

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Structure MEMORYSTATUSEX
        Public dwLength As UInteger
        Public dwMemoryLoad As UInteger
        Public ullTotalPhys As ULong
        Public ullAvailPhys As ULong
        Public ullTotalPageFile As ULong
        Public ullAvailPageFile As ULong
        Public ullTotalVirtual As ULong
        Public ullAvailVirtual As ULong
        Public ullAvailExtendedVirtual As ULong
    End Structure

    <DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GlobalMemoryStatusEx(ByRef lpBuffer As MEMORYSTATUSEX) As Boolean
    End Function

    Private Shared Function GetAvailablePhysicalMemoryBytes() As Long
        Dim mem As New MEMORYSTATUSEX()
        mem.dwLength = CUInt(Marshal.SizeOf(Of MEMORYSTATUSEX)())

        If Not GlobalMemoryStatusEx(mem) Then
            Return 0
        End If

        If mem.ullAvailPhys > Long.MaxValue Then
            Return Long.MaxValue
        End If

        Return CLng(mem.ullAvailPhys)
    End Function

    Private Shared Function EstimateMemoryUsageBytes(width As Integer, height As Integer, startYear As Integer, endYear As Integer, dtYears As Double) As Long
        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)
        If dtYears <= 0.0 OrElse totalYears <= 0.0 Then Return 0

        Dim steps As Long = CLng(Math.Ceiling(totalYears / dtYears))
        Dim cells As Long = CLng(width) * CLng(height)

        ' Double pro Zelle
        Dim bytesPerSnapshot As Double = cells * 8.0

        ' Overhead-Faktor
        Dim overheadFactor As Double = 1.3 '30% Overhead

        Dim totalBytes As Double = steps * bytesPerSnapshot * overheadFactor
        If totalBytes > Long.MaxValue Then
            Return Long.MaxValue
        End If

        Return CLng(totalBytes)
    End Function

    Public Sub UpdateMemoryEstimate()
        If CurrentConfig Is Nothing Then
            MemoryEstimateText = "Speicherprognose: n/a"
            MemoryEstimateBrush = Brushes.Gray
            Return
        End If

        Dim width As Integer = CurrentConfig.GridWidth
        Dim height As Integer = CurrentConfig.GridHeight
        Dim startYear As Integer = CurrentConfig.StartYear
        Dim endYear As Integer = CurrentConfig.EndYear

        Dim dtYears As Double = GetDtYearsFromMode()

        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)
        If width <= 0 OrElse height <= 0 OrElse totalYears <= 0 OrElse dtYears <= 0 Then
            MemoryEstimateText = "Speicherprognose: n/a"
            MemoryEstimateBrush = Brushes.Gray
            Return
        End If

        Dim estimatedBytes As Long = EstimateMemoryUsageBytes(width, height, startYear, endYear, dtYears)
        Dim availableBytes As Long = GetAvailablePhysicalMemoryBytes()

        Dim estGiB As Double = estimatedBytes / (1024 ^ 3)
        Dim availGiB As Double = availableBytes / (1024 ^ 3)

        MemoryEstimateText = $"Speicherprognose: ~{estGiB:F2} GiB (frei: {availGiB:F2} GiB)"

        If estimatedBytes > availableBytes Then
            MemoryEstimateBrush = Brushes.Red
        Else
            MemoryEstimateBrush = Brushes.Black
        End If
    End Sub

#End Region

#Region "--- DtYears-Helfer ---"

    Public Function GetDtYearsFromMode() As Double
        Select Case _timeStepMode
            Case TimeStepMode.Month
                Return (1.0 / 12.0)
            Case TimeStepMode.Quarter
                Return 0.25
            Case TimeStepMode.Year
                Return 1
            Case TimeStepMode.Decade
                Return 10
            Case Else
                Return 1
        End Select
    End Function

#End Region

End Class
