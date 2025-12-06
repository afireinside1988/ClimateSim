Imports System.ComponentModel

Public Class MainViewModel
    Implements INotifyPropertyChanged

    ' --- Simulations- / UI-Parameter, die aktuell in TextBoxen / Labels liegen ---

    Private _startYear As Integer = 1850
    Private _endYear As Integer = 2100
    Private _gridWidth As Integer = 360
    Private _gridHeight As Integer = 180

    Private _timeStepMode As TimeStepMode = TimeStepMode.Year

    Private _statusText As String = "Bitte Spin-Up starten."
    Private _globalMeanText As String = "0,00 °C"
    Private _currentYearText As String = "1850"
    Private _simTimeText As String = "0,0 Jahre"
    Private _memoryEstimateText As String = ""
    Private _memoryEstimateBrush As Brush = Brushes.Black

    Private _lambda As Double = 0.5
    ' --- SimulationEngine bleibt erstmal hier drin, damit MainWindow weniger Felder hat ---

    Public Property Engine As SimulationEngine

    Public Sub New()
        Engine = New SimulationEngine()
        Engine.CO2Scenario = New DefaultCo2Scenario()
        Engine.EarthSurfaceProvider = New ToyEarthSurfaceProvider()
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

    ' --- INotifyPropertyChanged-Implementierung ---
    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Protected Sub OnPropertyChanged(propName As String)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propName))
    End Sub

End Class
