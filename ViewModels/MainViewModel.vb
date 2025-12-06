Imports System.CodeDom
Imports System.ComponentModel
Imports System.Windows.Input
Imports System.Threading.Tasks

Public Class MainViewModel
    Implements INotifyPropertyChanged

    ' --- Simulations- / UI-Parameter, die aktuell in TextBoxen / Labels liegen ---

    Private _startYear As Integer = 1850
    Private _endYear As Integer = 2100
    Private _gridWidth As Integer = 360
    Private _gridHeight As Integer = 180

    Private _timeStepMode As TimeStepMode = TimeStepMode.Year
    Private _timeStepIndex As Integer = 2                   '0=Monat, 1=Quartal, 2=Jahr, 3=Dekade
    Private _dtModeText As String = "1 Jahr"

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


    ' --- Events, die die View abonnieren kann ---
    Public Event StartSimulationRequested As EventHandler
    Public Event StopSimulationRequested As EventHandler
    Public Event SpinUpRequested As EventHandler
    Public Event StepRequested As EventHandler
    Public Event ShowHistoryRequested As EventHandler

    ' --- Commands ---
    Public ReadOnly Property StartCommand As ICommand
    Public ReadOnly Property StopCommand As ICommand
    Public ReadOnly Property SpinUpCommand As ICommand
    Public ReadOnly Property StepCommand As ICommand
    Public ReadOnly Property ShowHistoryCommand As ICommand


    ' --- SimulationEngine bleibt erstmal hier drin, damit MainWindow weniger Felder hat ---
    Public Property Engine As SimulationEngine

    ' --- Statusflags, damit Commands später CanExecute nutzen können
    Private _isInitialized As Boolean
    Private _isSimulationRunning As Boolean

    Public Sub New()
        Engine = New SimulationEngine()
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
    End Sub

    ' --- Properties für Bindings ---
    Public Property StartYear As Integer
        Get
            Return _startYear
        End Get
        Set(value As Integer)
            If _startYear <> value Then
                _startYear = value
                OnPropertyChanged(NameOf(StartYear))
            End If
        End Set
    End Property

    Public Property EndYear As Integer
        Get
            Return _endYear
        End Get
        Set(value As Integer)
            If _endYear <> value Then
                _endYear = value
                OnPropertyChanged(NameOf(EndYear))
            End If
        End Set
    End Property

    Public Property GridWidth As Integer
        Get
            Return _gridWidth
        End Get
        Set(value As Integer)
            If _gridWidth <> value Then
                _gridWidth = value
                OnPropertyChanged(NameOf(GridWidth))
            End If
        End Set
    End Property

    Public Property GridHeigth As Integer
        Get
            Return _gridHeight
        End Get
        Set(value As Integer)
            If _gridHeight <> value Then
                _gridHeight = value
                OnPropertyChanged(NameOf(GridHeigth))
            End If
        End Set
    End Property

    Public Property TimeStepMode As TimeStepMode
        Get
            Return _timeStepMode
        End Get
        Set(value As TimeStepMode)
            If _timeStepMode <> value Then
                _timeStepMode = value
                OnPropertyChanged(NameOf(TimeStepMode))
            End If
        End Set
    End Property

    Public Property TimeStepIndex As Integer
        Get
            Return _timeStepIndex
        End Get
        Set(value As Integer)
            If _timeStepIndex <> value Then
                _timeStepIndex = value
                OnPropertyChanged(NameOf(TimeStepIndex))

                'Mapping Index -> Mode
                Select Case value
                    Case 0
                        TimeStepMode = TimeStepMode.Month
                        DtModeText = "1 Monat"
                    Case 1
                        TimeStepMode = TimeStepMode.Quarter
                        DtModeText = "1 Quartal"
                    Case 2
                        TimeStepMode = TimeStepMode.Year
                        DtModeText = "1 Jahr"
                    Case 3
                        TimeStepMode = TimeStepMode.Decade
                        DtModeText = "10 Jahre"
                End Select
            End If
        End Set
    End Property

    Public Property DtModeText As String
        Get
            Return _dtModeText
        End Get
        Set(value As String)
            If _dtModeText <> value Then
                _dtModeText = value
                OnPropertyChanged(NameOf(DtModeText))
            End If
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
            If _statusText <> value Then
                _statusText = value
                OnPropertyChanged(NameOf(StatusText))
            End If
        End Set
    End Property

    Public Property StatusLatText As String
        Get
            Return _statusLatText
        End Get
        Set(value As String)
            If _statusLatText <> value Then
                _statusLatText = value
                OnPropertyChanged(NameOf(StatusLatText))
            End If
        End Set
    End Property

    Public Property StatusLonText As String
        Get
            Return _statusLonText
        End Get
        Set(value As String)
            If _statusLonText <> value Then
                _statusLonText = value
                OnPropertyChanged(NameOf(StatusLonText))
            End If
        End Set
    End Property

    Public Property StatusTempText As String
        Get
            Return _statusTempText
        End Get
        Set(value As String)
            If _statusTempText <> value Then
                _statusTempText = value
                OnPropertyChanged(NameOf(StatusTempText))
            End If
        End Set
    End Property

    Public Property StatusSurfaceText As String
        Get
            Return _statusSurfaceText
        End Get
        Set(value As String)
            If _statusSurfaceText <> value Then
                _statusSurfaceText = value
                OnPropertyChanged(NameOf(StatusSurfaceText))
            End If
        End Set
    End Property

    Public Property GlobalMeanText As String
        Get
            Return _globalMeanText
        End Get
        Set(value As String)
            If _globalMeanText <> value Then
                _globalMeanText = value
                OnPropertyChanged(NameOf(GlobalMeanText))
            End If
        End Set
    End Property

    Public Property CurrentYearText As String
        Get
            Return _currentYearText
        End Get
        Set(value As String)
            If _currentYearText <> value Then
                _currentYearText = value
                OnPropertyChanged(NameOf(CurrentYearText))
            End If
        End Set
    End Property

    Public Property SimTimeText As String
        Get
            Return _simTimeText
        End Get
        Set(value As String)
            If _simTimeText <> value Then
                _simTimeText = value
                OnPropertyChanged(NameOf(SimTimeText))
            End If
        End Set
    End Property

    Public Property MemoryEstimateText As String
        Get
            Return _memoryEstimateText
        End Get
        Set(value As String)
            If _memoryEstimateText <> value Then
                _memoryEstimateText = value
                OnPropertyChanged(NameOf(MemoryEstimateText))
            End If
        End Set
    End Property

    Public Property MemoryEstimateBrush As Brush
        Get
            Return _memoryEstimateBrush
        End Get
        Set(value As Brush)
            If _memoryEstimateBrush IsNot value Then
                _memoryEstimateBrush = value
                OnPropertyChanged(NameOf(MemoryEstimateBrush))
            End If
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

    Public Property IsInitialized As Boolean
        Get
            Return _isInitialized
        End Get
        Set(value As Boolean)
            If _isInitialized <> value Then
                _isInitialized = value
                OnPropertyChanged(NameOf(IsInitialized))
                CommandManager.InvalidateRequerySuggested()
            End If
        End Set
    End Property

    Public Property IsSimulationRunning As Boolean
        Get
            Return _isSimulationRunning
        End Get
        Set(value As Boolean)
            If _isSimulationRunning <> value Then
                _isSimulationRunning = value
                OnPropertyChanged(NameOf(IsSimulationRunning))
                CommandManager.InvalidateRequerySuggested()
            End If
        End Set
    End Property



    ' --- INotifyPropertyChanged-Implementierung ---
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub OnPropertyChanged(propName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propName))
    End Sub

End Class
