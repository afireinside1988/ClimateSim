Imports System.Collections.ObjectModel
Imports System.ComponentModel

Public Class HistoryViewModel
    Inherits ViewModelBase

    Private ReadOnly _engine As SimulationEngine

    Public ReadOnly Property Records As ObservableCollection(Of SimulationRecord)

    Private _selectedIndex As Integer
    Public Property SelectedIndex As Integer
        Get
            Return _selectedIndex
        End Get
        Set(value As Integer)
            If _selectedIndex <> value Then
                _selectedIndex = value
                OnPropertyChanged()
                UpdateSelectionDisplay()
            End If
        End Set
    End Property

    Private _timeStepMode As TimeStepMode
    Public Property TimeStepMode As TimeStepMode
        Get
            Return _timeStepMode
        End Get
        Set(value As TimeStepMode)
            If _timeStepMode <> value Then
                _timeStepMode = value
                OnPropertyChanged()
                UpdateSelectionDisplay()
            End If
        End Set
    End Property

    ' --- Bindbare Eigenschaften ---
    Private _selectedYearText As String
    Public Property SelectedYearText As String
        Get
            Return _selectedYearText
        End Get
        Set(value As String)
            SetProperty(_selectedYearText, value)
        End Set
    End Property

    Private _selectedTempText As String
    Public Property SelectedTempText As String
        Get
            Return _selectedTempText
        End Get
        Set(value As String)
            SetProperty(_selectedTempText, value)
        End Set
    End Property

    Private _selectedCO2Text As String
    Public Property SelectedCO2Text As String
        Get
            Return _selectedCO2Text
        End Get
        Set(value As String)
            SetProperty(_selectedCO2Text, value)
        End Set
    End Property

    Public Sub New(engine As SimulationEngine, timeStepMode As TimeStepMode)
        _engine = engine
        _timeStepMode = timeStepMode

        Records = New ObservableCollection(Of SimulationRecord)(engine.History)

        If Records.Count > 0 Then
            _selectedIndex = Records.Count - 1
            UpdateSelectionDisplay()
        End If
    End Sub

    Private Sub UpdateSelectionDisplay()
        If SelectedIndex < 0 OrElse SelectedIndex >= Records.Count Then Return

        Dim r = Records(SelectedIndex)

        SelectedYearText = TimeFormatting.FormatYearWithStepMode(r.Year, _timeStepMode)
        SelectedTempText = $"{r.GlobalMeanTempC:F2} °C"
        SelectedCO2Text = $"{r.CO2ppm:F0} ppm"

        'Engine synchronisieren
        _engine.JumpToIndex(SelectedIndex)
    End Sub

End Class