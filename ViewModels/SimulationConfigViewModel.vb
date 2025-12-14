Imports System.ComponentModel
Imports System.Windows.Media
Imports System.Runtime.InteropServices
Imports System.Windows.Input
Imports System.CodeDom
Imports System.Collections
Imports System.Globalization
Imports System.Xml
Imports System.Reflection.Metadata

Public Class SimulationConfigViewModel
    Inherits ViewModelBase
    Implements INotifyDataErrorInfo

    Private ReadOnly _config As SimulationConfig
    Private ReadOnly _originalConfig As SimulationConfig

    Private _testDouble As Double

    Private _memoryEstimateText As String = ""
    Private _memoryEstimateBrush As Brush = Brushes.Black

    '--- Commands ---
    Public Event OkRequested As EventHandler
    Public Event CancelRequested As EventHandler
    Public Event FocusFirstErrorRequested As EventHandler
    Public ReadOnly Property OkCommand As ICommand

#Region "Felder für Speicherprognose"

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

#End Region

#Region "Allgemeine Parameter"

    Public Property TestDouble As Double
        Get
            Return _testDouble
        End Get
        Set(value As Double)
            If Math.Abs(_testDouble - value) > 0.0001 Then
                _testDouble = value
                OnPropertyChanged(NameOf(TestDouble))
            End If
        End Set
    End Property
    Private _testDoubleText As String = "1,3"
    Public Property TestDoubleText As String
        Get
            Return _testDoubletext
        End Get
        Set(value As String)
            If SetProperty(_testDoubleText, value) Then
                ValidateTestDouble()
            End If
        End Set
    End Property

    Public Property StartYear As Integer
        Get
            Return _config.StartYear
        End Get
        Set(value As Integer)
            If _config.StartYear <> value Then
                _config.StartYear = value
                OnPropertyChanged(NameOf(StartYear))
                OnPropertyChanged(NameOf(EndYear))
                UpdateMemoryEstimate()
            End If
        End Set
    End Property
    Private _startYearText As String
    Public Property StartYearText As String
        Get
            Return _startYearText
        End Get
        Set(value As String)
            If SetProperty(_startYearText, value) Then
                ValidateStartYear()
                ValidateYearRelation()
                ApplyToConfigIfPossible()
            End If
        End Set
    End Property
    Public Property EndYear As Integer
        Get
            Return _config.EndYear
        End Get
        Set(value As Integer)
            If _config.EndYear <> value Then
                _config.EndYear = value
                OnPropertyChanged(NameOf(EndYear))
                OnPropertyChanged(NameOf(StartYear))
                UpdateMemoryEstimate()
            End If
        End Set
    End Property
    Private _endYearText As String
    Public Property EndYearText As String
        Get
            Return _endYearText
        End Get
        Set(value As String)
            If SetProperty(_endYearText, value) Then
                ValidateEndYear()
                ValidateYearRelation()
                ApplyToConfigIfPossible()
            End If
        End Set
    End Property

    Public Property GridWidth As Integer
        Get
            Return _config.GridWidth
        End Get
        Set(value As Integer)
            If _config.GridWidth <> value Then
                _config.GridWidth = value
                OnPropertyChanged(NameOf(GridWidth))
                UpdateMemoryEstimate()
            End If
        End Set
    End Property
    Private _gridWidthText As String
    Public Property GridWidthText As String
        Get
            Return _gridWidthText
        End Get
        Set(value As String)
            If SetProperty(_gridWidthText, value) Then
                ValidateGridWidth()
                ApplyToConfigIfPossible()
            End If
        End Set
    End Property
    Public Property GridHeight As Integer
        Get
            Return _config.GridHeight
        End Get
        Set(value As Integer)
            If _config.GridHeight <> value Then
                _config.GridHeight = value
                OnPropertyChanged(NameOf(GridHeight))
                UpdateMemoryEstimate()
            End If
        End Set
    End Property
    Private _gridHeightText As String
    Public Property GridHeightText As String
        Get
            Return _gridHeightText
        End Get
        Set(value As String)
            If SetProperty(_gridHeightText, value) Then
                ValidateGridHeight()
                ApplyToConfigIfPossible()
            End If
        End Set
    End Property

    Public Property TimeStepMode As TimeStepMode
        Get
            Return _config.TimeStepMode
        End Get
        Set(value As TimeStepMode)
            If _config.TimeStepMode <> value Then
                _config.TimeStepMode = value
                OnPropertyChanged(NameOf(TimeStepMode))
                OnPropertyChanged(NameOf(TimeStepDescription))
                OnPropertyChanged(NameOf(TimeStepIndex))
                UpdateMemoryEstimate()
            End If
        End Set
    End Property
    Public ReadOnly Property TimeStepDescription As String
        Get
            Select Case TimeStepMode
                Case TimeStepMode.Month
                    Return "1 Monat"
                Case TimeStepMode.Quarter
                    Return "1 Quartal"
                Case TimeStepMode.Year
                    Return "1 Jahr"
                Case TimeStepMode.Decade
                    Return "10 Jahre"
                Case Else
                    Return ""
            End Select
        End Get
    End Property
    Public Property TimeStepIndex As Integer
        Get
            Select Case TimeStepMode
                Case TimeStepMode.Month
                    Return 0
                Case TimeStepMode.Quarter
                    Return 1
                Case TimeStepMode.Year
                    Return 2
                Case TimeStepMode.Decade
                    Return 3
                Case Else
                    Return 2
            End Select
        End Get
        Set(value As Integer)
            Dim newMode As TimeStepMode
            Select Case value
                Case 0
                    newMode = TimeStepMode.Month
                Case 1
                    newMode = TimeStepMode.Quarter
                Case 2
                    newMode = TimeStepMode.Year
                Case 3
                    newMode = TimeStepMode.Decade
                Case Else
                    newMode = TimeStepMode.Year
            End Select

            If newMode <> TimeStepMode Then
                TimeStepMode = newMode 'triggert PropertyChanged für Mode & Description
                OnPropertyChanged(NameOf(TimeStepIndex))
            End If
        End Set
    End Property

    Public Property Lambda As Double
        Get
            Return _config.Lambda
        End Get
        Set(value As Double)
            If Math.Abs(_config.Lambda - value) > 0.0001 Then
                _config.Lambda = value
                OnPropertyChanged(NameOf(Lambda))
            End If
        End Set
    End Property

#End Region

#Region "Solare Forcings / SimpleCycles"

    Public Property SolarCycleMode As SolarCycleMode
        Get
            Return _config.SolarCycleMode
        End Get
        Set(value As SolarCycleMode)
            If _config.SolarCycleMode <> value Then
                _config.SolarCycleMode = value
                OnPropertyChanged(NameOf(SolarCycleMode))
                OnPropertyChanged(NameOf(IsSimpleCyclesMode))
            End If
        End Set
    End Property

    Public ReadOnly Property IsSimpleCyclesMode As Boolean
        Get
            Return SolarCycleMode = SolarCycleMode.SimpleCycles
        End Get
    End Property

    'Schwabe
    Public Property UseSchwabeCycle As Boolean
        Get
            Return _config.UseSchwabeCycle
        End Get
        Set(value As Boolean)
            If _config.UseSchwabeCycle <> value Then
                _config.UseSchwabeCycle = value
                OnPropertyChanged(NameOf(UseSchwabeCycle))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Amplitude in Prozent (z.B. 0,10% statt 0,001).
    ''' </summary>
    ''' <returns></returns>
    Public Property SchwabeAmplitudePercent As Double
        Get
            Return _config.SchwabeAmplitude * 100.0
        End Get
        Set(value As Double)
            Dim newAmp As Double = value / 100.0
            If Math.Abs(_config.SchwabeAmplitude - newAmp) > 0.000001 Then
                _config.SchwabeAmplitude = newAmp
                OnPropertyChanged(NameOf(SchwabeAmplitudePercent))
            End If
        End Set
    End Property
    Public Property SchwabePeriodYears As Double
        Get
            Return _config.SchwabePeriodYears
        End Get
        Set(value As Double)
            If Math.Abs(_config.SchwabePeriodYears - value) > 0.0001 Then
                _config.SchwabePeriodYears = value
                OnPropertyChanged(NameOf(SchwabePeriodYears))
            End If
        End Set
    End Property
    Public Property SchwabePhaseDeg As Double
        Get
            Return _config.SchwabePhaseDeg
        End Get
        Set(value As Double)
            If Math.Abs(_config.SchwabePhaseDeg - value) > 0.0001 Then
                _config.SchwabePhaseDeg = value
                OnPropertyChanged(NameOf(SchwabePhaseDeg))
            End If
        End Set
    End Property

    'Magentischer Zyklus
    Public Property UseMagneticCycle As Boolean
        Get
            Return _config.UseMagneticCycle
        End Get
        Set(value As Boolean)
            If _config.UseMagneticCycle <> value Then
                _config.UseMagneticCycle = value
                OnPropertyChanged(NameOf(UseMagneticCycle))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Amplitude in Prozent (z.B. 0,10% statt 0,001).
    ''' </summary>
    ''' <returns></returns>
    Public Property MagneticAmplitudePercent As Double
        Get
            Return _config.MagneticAmplitude * 100.0
        End Get
        Set(value As Double)
            Dim newAmp As Double = value / 100.0
            If Math.Abs(_config.MagneticAmplitude - newAmp) > 0.000001 Then
                _config.MagneticAmplitude = newAmp
                OnPropertyChanged(NameOf(MagneticAmplitudePercent))
            End If
        End Set
    End Property
    Public Property MagneticPeriodYears As Double
        Get
            Return _config.MagneticPeriodYears
        End Get
        Set(value As Double)
            If Math.Abs(_config.MagneticPeriodYears - value) > 0.0001 Then
                _config.MagneticPeriodYears = value
                OnPropertyChanged(NameOf(MagneticPeriodYears))
            End If
        End Set
    End Property
    Public Property MagneticPhaseDeg As Double
        Get
            Return _config.MagneticPhaseDeg
        End Get
        Set(value As Double)
            If Math.Abs(_config.MagneticPhaseDeg - value) > 0.0001 Then
                _config.MagneticPhaseDeg = value
                OnPropertyChanged(NameOf(MagneticPhaseDeg))
            End If
        End Set
    End Property

    'Gleissberg
    Public Property UseGleissbergCycle As Boolean
        Get
            Return _config.UseGleissbergCycle
        End Get
        Set(value As Boolean)
            If _config.UseGleissbergCycle <> value Then
                _config.UseGleissbergCycle = value
                OnPropertyChanged(NameOf(UseGleissbergCycle))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Amplitude in Prozent (z.B. 0,10% statt 0,001).
    ''' </summary>
    ''' <returns></returns>
    Public Property GleissbergAmplitudePercent As Double
        Get
            Return _config.GleissbergAmplitude * 100.0
        End Get
        Set(value As Double)
            Dim newAmp As Double = value / 100.0
            If Math.Abs(_config.GleissbergAmplitude - newAmp) > 0.000001 Then
                _config.GleissbergAmplitude = newAmp
                OnPropertyChanged(NameOf(GleissbergAmplitudePercent))
            End If
        End Set
    End Property
    Public Property GleissbergPeriodYears As Double
        Get
            Return _config.GleissbergPeriodYears
        End Get
        Set(value As Double)
            If Math.Abs(_config.GleissbergPeriodYears - value) > 0.001 Then
                _config.GleissbergPeriodYears = value
                OnPropertyChanged(NameOf(GleissbergPeriodYears))
            End If
        End Set
    End Property
    Public Property GleissbergPhaseDeg As Double
        Get
            Return _config.GleissbergPhaseDeg
        End Get
        Set(value As Double)
            If Math.Abs(_config.GleissbergPhaseDeg - value) > 0.0001 Then
                _config.GleissbergPhaseDeg = value
                OnPropertyChanged(NameOf(GleissbergPhaseDeg))
            End If
        End Set
    End Property

    'De Vriess/Suess
    Public Property UseDeVriesSuessCycle As Boolean
        Get
            Return _config.UseDeVriesSuessCycle
        End Get
        Set(value As Boolean)
            If _config.UseDeVriesSuessCycle <> value Then
                _config.UseDeVriesSuessCycle = value
                OnPropertyChanged(NameOf(UseDeVriesSuessCycle))
            End If
        End Set
    End Property

    ''' <summary>
    ''' Amplitude in Prozent (z.B. 0,10% statt 0,001).
    ''' </summary>
    ''' <returns></returns>
    Public Property DeVriesAmplitudePercent As Double
        Get
            Return _config.DeVriesAmplitude * 100.0
        End Get
        Set(value As Double)
            Dim newAmp As Double = value / 100.0
            If Math.Abs(_config.DeVriesAmplitude - newAmp) > 0.000001 Then
                _config.DeVriesAmplitude = newAmp
                OnPropertyChanged(NameOf(DeVriesAmplitudePercent))
            End If
        End Set
    End Property
    Public Property DeVriesPeriodYears As Double
        Get
            Return _config.DeVriesPeriodYears
        End Get
        Set(value As Double)
            If Math.Abs(_config.DeVriesPeriodYears - value) > 0.001 Then
                _config.DeVriesPeriodYears = value
                OnPropertyChanged(NameOf(DeVriesPeriodYears))
            End If
        End Set
    End Property
    Public Property DeVriesPhaseDeg As Double
        Get
            Return _config.DeVriesPhaseDeg
        End Get
        Set(value As Double)
            If Math.Abs(_config.DeVriesPhaseDeg - value) > 0.0001 Then
                _config.DeVriesPhaseDeg = value
                OnPropertyChanged(NameOf(DeVriesPhaseDeg))
            End If
        End Set
    End Property

#End Region

    ''' <summary>
    ''' Die intern bearbeitete Konfiguration (Klon des Originals).
    ''' Wird beim OK im Dialog an MainViewModel zurückgegeben.
    ''' </summary>
    ''' <returns></returns>
    Public ReadOnly Property Config As SimulationConfig
        Get
            Return _config
        End Get
    End Property

    Public Sub New(sourceConfig As SimulationConfig)
        If sourceConfig IsNot Nothing Then
            _config = sourceConfig.Clone()
        Else
            _config = SimulationConfig.CreateDefault()
        End If

        'Snapshot für späteren Vergleich
        _originalConfig = _config.Clone()

        'Editor-Strings initialisieren (Culture-aware)
        StartYearText = _config.StartYear.ToString(_culture)
        EndYearText = _config.EndYear.ToString(_culture)
        GridWidthText = _config.GridWidth.ToString(_culture)
        GridHeightText = _config.GridHeight.ToString(_culture)

        'Optional TestDouble
        TestDoubleText = _testDoubleText.ToString(_culture)

        'initial validieren (damit OK initial korrekt disabled ist)
        ValidateStartYear()
        ValidateEndYear()
        ValidateYearRelation()
        ValidateGridWidth()
        ValidateGridHeight()

        ValidateTestDouble()

        ApplyToConfigIfPossible()

        '--- Commands initialisieren ---
        OkCommand = New RelayCommand(Of Object)(
            Sub(o As Object)
                ExecuteOk()
            End Sub, Function(o) True)

        UpdateMemoryEstimate()
    End Sub

    Private Sub ExecuteOk()

        'Wenn Errors: Fokus auf erstes Feld anfordern (Ok sollte eigentlich disabled sein)
        If HasErrors Then
            RaiseEvent FocusFirstErrorRequested(Me, EventArgs.Empty)
            Return
        End If

        'Sicherstellen, dass Editor -> Config geschrieben ist
        ApplyToConfigIfPossible()
        If HasErrors Then
            RaiseEvent FocusFirstErrorRequested(Me, EventArgs.Empty)
            Return
        End If

        '1) Auf Änderungen prüfen
        If _config.IsEqualTo(_originalConfig) Then
            RaiseEvent CancelRequested(Me, EventArgs.Empty)
            Return
        End If

        '3) Wenn alles ok -> Dialog schließen lassen
        RaiseEvent OkRequested(Me, EventArgs.Empty)
    End Sub

#Region "Memory-Helper"
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

    Private Shared Function EstimateMemoryUsageBytes(width As Integer, height As Integer,
                                                 startYear As Integer, endYear As Integer,
                                                 dtYears As Double) As Long
        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)
        If dtYears <= 0.0 OrElse totalYears <= 0.0 Then Return 0

        Dim steps As Long = CLng(Math.Ceiling(totalYears / dtYears))
        Dim cells As Long = CLng(width) * CLng(height)

        Dim bytesPerSnapshot As Double = cells * 8.0
        Dim overheadFactor As Double = 1.3

        Dim totalBytes As Double = steps * bytesPerSnapshot * overheadFactor
        If totalBytes > Long.MaxValue Then
            Return Long.MaxValue
        End If

        Return CLng(totalBytes)
    End Function

    Public Sub UpdateMemoryEstimate()
        If _config Is Nothing Then
            MemoryEstimateText = "Geschätzer Arbeitsspeicherbedarf: n/a"
            MemoryEstimateBrush = Brushes.Gray
            Return
        End If

        Dim width As Integer = _config.GridWidth
        Dim height As Integer = _config.GridHeight
        Dim startYear As Integer = _config.StartYear
        Dim endYear As Integer = _config.EndYear
        Dim dtYears As Double = _config.GetDtYearsFromMode()

        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)

        If width <= 0 OrElse height <= 0 OrElse totalYears <= 0 OrElse dtYears <= 0 Then
            MemoryEstimateText = "Geschätzer Arbeitsspeicherbedarf: n/a"
            MemoryEstimateBrush = Brushes.Gray
            Return
        End If

        Dim estimatedBytes As Long = EstimateMemoryUsageBytes(width, height, startYear, endYear, dtYears)
        Dim availableBytes As Long = GetAvailablePhysicalMemoryBytes()

        Dim estGiB As Double = estimatedBytes / (1024 ^ 3)
        Dim availGiB As Double = availableBytes / (1024 ^ 3)

        MemoryEstimateText = $"Geschätzer Arbeitsspeicherbedarf: ~{estGiB:F2} GiB (frei: {availGiB:F2} GiB)"

        If estimatedBytes > availableBytes Then
            MemoryEstimateBrush = Brushes.Red
        Else
            MemoryEstimateBrush = Brushes.Black
        End If
    End Sub

#End Region

#Region "Validation/Errors"

    Private ReadOnly _errors As New Dictionary(Of String, List(Of String))()
    Private ReadOnly _culture As CultureInfo = CultureInfo.CurrentUICulture
    Public Event ErrorsChanged As EventHandler(Of DataErrorsChangedEventArgs) Implements INotifyDataErrorInfo.ErrorsChanged

    Public ReadOnly Property HasErrors As Boolean Implements INotifyDataErrorInfo.HasErrors
        Get
            Return _errors.Values.Any(Function(l) l IsNot Nothing AndAlso l.Count > 0)
        End Get
    End Property

    Public Function GetErrors(propertyName As String) As IEnumerable Implements INotifyDataErrorInfo.GetErrors
        If String.IsNullOrEmpty(propertyName) Then
            Return _errors.SelectMany(Function(kv) kv.Value).ToList()
        End If

        Dim list As List(Of String) = Nothing
        If _errors.TryGetValue(propertyName, list) Then
            Return list
        End If

        Return Enumerable.Empty(Of String)()
    End Function

    Private Sub SetErrors(propertyName As String, ParamArray messages() As String)
        Dim clean As List(Of String) = If(messages, Array.Empty(Of String)()).Where(Function(m) Not String.IsNullOrWhiteSpace(m)).ToList()

        If clean.Count = 0 Then
            If _errors.Remove(propertyName) Then
                RaiseEvent ErrorsChanged(Me, New DataErrorsChangedEventArgs(propertyName))
                OnPropertyChanged(NameOf(HasErrors))
            End If
            Return
        End If

        _errors(propertyName) = clean
        RaiseEvent ErrorsChanged(Me, New DataErrorsChangedEventArgs(propertyName))
        OnPropertyChanged(NameOf(HasErrors))
    End Sub

    Private Function TryParseInt(text As String, ByRef value As Integer) As Boolean
        Return Integer.TryParse(text, NumberStyles.Integer, _culture, value)
    End Function

    Private Function TryParseDouble(text As String, ByRef value As Double) As Boolean
        Return Double.TryParse(text, NumberStyles.Float, _culture, value)
    End Function

    Private Sub ValidateStartYear()
        Dim v As Integer
        If String.IsNullOrWhiteSpace(StartYearText) Then
            SetErrors(NameOf(StartYearText), "Bitte ein Startjahr eingeben.")
        ElseIf Not TryParseInt(StartYearText, v) Then
            SetErrors(NameOf(StartYearText), "Ungültige Zahl. Bitte ein ganzzahliges Jahr eingeben. (z.B. -500, 1850)")
        Else
            SetErrors(NameOf(StartYearText))
        End If
    End Sub

    Private Sub ValidateEndYear()
        Dim v As Integer
        If String.IsNullOrWhiteSpace(EndYearText) Then
            SetErrors(NameOf(EndYearText), "Bitte ein Endjahr eingeben.")
        ElseIf Not TryParseInt(EndYearText, v) Then
            SetErrors(NameOf(EndYearText), "Ungültige Zahl. Bitte ein ganzzahliges Jahr eingeben. (z.B. -500, 1850)")
        Else
            SetErrors(NameOf(EndYearText))
        End If
    End Sub

    Private Sub ValidateYearRelation()
        Dim s As Integer, e As Integer
        If TryParseInt(StartYearText, s) AndAlso TryParseInt(EndYearText, e) Then
            If e <= s Then
                SetErrors(NameOf(StartYearText), "Das Startjahr muss kleiner als das Endjahr sein.")
                SetErrors(NameOf(EndYearText), "Das Endjahr muss größer als das Startjahr sein.")
            Else
                SetErrors(NameOf(StartYearText))
                SetErrors(NameOf(EndYearText))
            End If
        End If
    End Sub

    Private Sub ValidateGridWidth()
        Dim v As Integer
        If String.IsNullOrWhiteSpace(GridWidthText) Then
            SetErrors(NameOf(GridWidthText), "Bitte eine Gitternetzbreite eingeben.")
        ElseIf Not TryParseInt(GridWidthText, v) Then
            SetErrors(NameOf(GridWidthText), "Ungültige Zahl. Bitte eine ganze Zahl eingeben.")
        ElseIf v <= 0 Then
            SetErrors(NameOf(GridWidthText), "Die Gitternetzbreite muss größer als 0 sein.")
        Else
            SetErrors(NameOf(GridWidthText))
        End If
    End Sub

    Private Sub ValidateGridHeight()
        Dim v As Integer
        If String.IsNullOrWhiteSpace(GridHeightText) Then
            SetErrors(NameOf(GridHeightText), "Bitte eine Gitternetzhöhe eingeben.")
        ElseIf Not TryParseInt(GridHeightText, v) Then
            SetErrors(NameOf(GridHeightText), "Ungültige Zahl. Bitte eine ganze Zahl eingeben.")
        ElseIf v <= 0 Then
            SetErrors(NameOf(GridHeightText), "Die Gitternetzhöhe muss größer als 0 sein.")
        Else
            SetErrors(NameOf(GridHeightText))
        End If
    End Sub

    Private Sub ValidateTestDouble()
        Dim v As Double
        If String.IsNullOrWhiteSpace(TestDoubleText) Then
            SetErrors(NameOf(TestDoubleText), "Bitte eine Zahl eingeben.")
        ElseIf Not TryParseDouble(TestDoubleText, v) Then
            SetErrors(NameOf(TestDoubleText), "Ungültige Zahl. Bitte eine Dezimalzahl eingeben.")
        Else
            SetErrors(NameOf(TestDoubleText))
        End If
    End Sub

    Private Sub ApplyToConfigIfPossible()
        'Wenn irgendwas (noch) fehlerhaft ist: Config nicht überschreiben
        If HasErrors Then
            'Optional: MemoryEstimate auf n/a setzen
            Return
        End If

        Dim sYear As Integer
        Dim eYear As Integer
        Dim gWidth As Integer
        Dim gHeight As Integer

        If Not TryParseInt(StartYearText, sYear) Then Return
        If Not TryParseInt(EndYearText, eYear) Then Return
        If Not TryParseInt(GridWidthText, gWidth) Then Return
        If Not TryParseInt(GridHeightText, gHeight) Then Return

        _config.StartYear = sYear
        _config.EndYear = eYear
        _config.GridWidth = gWidth
        _config.GridHeight = gHeight

        UpdateMemoryEstimate()
    End Sub
#End Region

End Class
