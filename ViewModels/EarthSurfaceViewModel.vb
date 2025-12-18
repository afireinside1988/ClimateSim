Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Windows
Imports System.Windows.Media
Imports Microsoft.Win32

Public Enum ResamplingMode
    Nearest
    Bilinear
End Enum

Public Enum CellSizePreset
    Deg1
    Deg0_5
    Deg0_25
End Enum

Public Enum LandMaskMode
    FromHeight
    FromTid0
    ExternalSource
End Enum

Public Class EarthSurfaceViewModel
    Inherits ViewModelBase

#Region "Input"

    Private _sourceName As String = "GEBCO_2025"
    Public Property SourceName As String
        Get
            Return _sourceName
        End Get
        Set(value As String)
            If SetProperty(_sourceName, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
                OnPropertyChanged(NameOf(LandMaskVariantTag))
            End If
        End Set
    End Property

    Private _rawHeightFile As String
    Public Property RawHeightFile As String
        Get
            Return _rawHeightFile
        End Get
        Set(value As String)
            If SetProperty(_rawHeightFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawTidFile As String
    Public Property RawTidFile As String
        Get
            Return _rawTidFile
        End Get
        Set(value As String)
            If SetProperty(_rawTidFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawLandMaskFile As String
    Public Property RawLandMaskFile As String
        Get
            Return _rawLandMaskFile
        End Get
        Set(value As String)
            If SetProperty(_rawLandMaskFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

#End Region

#Region "Raster / Resampling"

    Private _selectedCellSize As CellSizePreset = CellSizePreset.Deg1
    Public Property SelectedCellSize As CellSizePreset
        Get
            Return _selectedCellSize
        End Get
        Set(value As CellSizePreset)
            If SetProperty(_selectedCellSize, value) Then
                OnPropertyChanged(NameOf(CellSizeDeg))
                OnPropertyChanged(NameOf(LandMaskVariantTag))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property
    Public ReadOnly Property CellSizeDeg As Double
        Get
            Select Case SelectedCellSize
                Case CellSizePreset.Deg1 : Return 1.0
                Case CellSizePreset.Deg0_5 : Return 0.5
                Case CellSizePreset.Deg0_25 : Return 0.25
                Case Else : Return 1.0
            End Select
        End Get
    End Property

    Private _selectedResampling As ResamplingMode = ResamplingMode.Nearest
    Public Property SelectedResampling As ResamplingMode
        Get
            Return _selectedResampling
        End Get
        Set(value As ResamplingMode)
            If SetProperty(_selectedResampling, value) Then
                OnPropertyChanged(NameOf(ResamplingKey))
                OnPropertyChanged(NameOf(LandMaskVariantTag))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property
    Public ReadOnly Property ResamplingKey As String
        Get
            Return If(SelectedResampling = ResamplingMode.Bilinear, "bilinear", "nearest")
        End Get
    End Property

#End Region

#Region "LandMask"

    Private _selectedLandMaskMode As LandMaskMode = LandMaskMode.FromHeight
    Public Property SelectedLandMaskMode As LandMaskMode
        Get
            Return _selectedLandMaskMode
        End Get
        Set(value As LandMaskMode)
            If SetProperty(_selectedLandMaskMode, value) Then
                OnPropertyChanged(NameOf(IsLandMaskBuilderEnabled))
                OnPropertyChanged(NameOf(IsHysteresisIterationsEnabled))
                OnPropertyChanged(NameOf(CanGenerateCache))
                OnPropertyChanged(NameOf(LandMaskVariantTag))
            End If
        End Set
    End Property

    Private _useHysteresis As Boolean = True
    Public Property UseHysteresis As Boolean
        Get
            Return _useHysteresis
        End Get
        Set(value As Boolean)
            If SetProperty(_useHysteresis, value) Then
                OnPropertyChanged(NameOf(IsHysteresisIterationsEnabled))
                OnPropertyChanged(NameOf(LandMaskVariantTag))
            End If
        End Set
    End Property

    Private _hysteresisIterations As Integer = 1
    Public Property HysteresisIterations As Integer
        Get
            Return _hysteresisIterations
        End Get
        Set(value As Integer)
            value = Math.Max(0, Math.Min(10, value))
            If SetProperty(_hysteresisIterations, value) Then
                OnPropertyChanged(NameOf(LandMaskVariantTag))
            End If
        End Set
    End Property

    Public ReadOnly Property IsLandMaskBuilderEnabled As Boolean
        Get
            Return SelectedLandMaskMode = LandMaskMode.FromHeight
        End Get
    End Property
    Public ReadOnly Property IsHysteresisIterationsEnabled As Boolean
        Get
            Return IsLandMaskBuilderEnabled AndAlso UseHysteresis
        End Get
    End Property

    Public ReadOnly Property LandMaskVariantTag As String
        Get
            Select Case SelectedLandMaskMode
                Case LandMaskMode.FromHeight
                    Dim hyst As Integer = If(UseHysteresis, 1, 0)
                    Dim it As Integer = If(UseHysteresis, HysteresisIterations, 0)
                    Return $"lm_fromHeight_hyst{hyst}_it{it:00}"
                Case LandMaskMode.FromTid0
                    Return "lm_fromTid0"
                Case Else
                    'später echte Varianten
                    Return "lm_external"
            End Select

        End Get
    End Property

#End Region

#Region "Preview"

    Private _previewImage As ImageSource
    Public Property PreviewImage As ImageSource
        Get
            Return _previewImage
        End Get
        Set(value As ImageSource)
            SetProperty(_previewImage, value)
        End Set
    End Property

    Private _showLayerHeight As Boolean = True
    Public Property ShowLayerHeight As Boolean
        Get
            Return _showLayerHeight
        End Get
        Set(value As Boolean)
            SetProperty(_showLayerHeight, value)
        End Set
    End Property

    Private _showLayerLandMask As Boolean = True
    Public Property ShowLayerLandMask As Boolean
        Get
            Return _showLayerLandMask
        End Get
        Set(value As Boolean)
            SetProperty(_showLayerLandMask, value)
        End Set
    End Property

    Private _showLayerTid As Boolean = False
    Public Property ShowLayerTid As Boolean
        Get
            Return _showLayerTid
        End Get
        Set(value As Boolean)
            SetProperty(_showLayerTid, value)
        End Set
    End Property

    Private _hoverText As String = ""
    Public Property HoverText As String
        Get
            Return _hoverText
        End Get
        Set(value As String)
            SetProperty(_hoverText, value)
        End Set
    End Property

#End Region

#Region "Status"

    Private _lastReport As String = "Bereit."
    Public Property LastReport As String
        Get
            Return _lastReport
        End Get
        Set(value As String)
            SetProperty(_lastReport, value)
        End Set
    End Property

    Public ReadOnly Property CanGenerateCache As Boolean
        Get
            If String.IsNullOrWhiteSpace(SourceName) Then Return False
            If String.IsNullOrWhiteSpace(RawHeightFile) OrElse Not File.Exists(RawHeightFile) Then Return False

            If SelectedLandMaskMode = LandMaskMode.ExternalSource Then
                If String.IsNullOrWhiteSpace(RawLandMaskFile) OrElse Not File.Exists(RawLandMaskFile) Then Return False
            End If

            Return True
        End Get
    End Property

#End Region

#Region "Commands"

    Public ReadOnly Property BrowseHeightCommand As ICommand
    Public ReadOnly Property BrowseTidCommand As ICommand
    Public ReadOnly Property BrowseLandMaskCommand As ICommand
    Public ReadOnly Property GenerateCacheCommand As ICommand
    Public ReadOnly Property OpenCacheFolderCommand As ICommand

#End Region

    Public Sub New()

        BrowseHeightCommand = New RelayCommand(Of Object)(Sub(o) BrowseHeight())
        BrowseTidCommand = New RelayCommand(Of Object)(Sub(o) BrowseTid())
        BrowseLandMaskCommand = New RelayCommand(Of Object)(Sub(o) BrowseLandMask())

        GenerateCacheCommand = New RelayCommand(Of Object)(
            Async Sub(o)
                Await RunSmokeTestAsync()
            End Sub, Function(o) CanGenerateCache AndAlso Not IsBusy)

        OpenCacheFolderCommand = New RelayCommand(Of Object)(
            Sub(o)
                Try
                    Dim dir As String = EarthSurfaceCacheStore.CacheDir
                    If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
                    Process.Start(New ProcessStartInfo(dir) With {.UseShellExecute = True})
                Catch ex As Exception
                    MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
                End Try
            End Sub)
    End Sub

    Private Sub BrowseHeight()
        Dim dlg As New OpenFileDialog With {
            .Title = "GEBCO Height-Datei auswählen",
            .Filter = "ESRI ASCII Zip (*.zip)|*.zip",
            .InitialDirectory = EarthSurfacePaths.RawDirectory,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            RawHeightFile = dlg.FileName
            LastReport = $"GEBCO-Height gewählt: {RawHeightFile}"
        End If
    End Sub

    Private Sub BrowseTid()
        Dim dlg As New OpenFileDialog With {
            .Title = "GEBCO TID-Datei auswählen",
            .Filter = "ESRI ASCII Zip (*.zip)|*.zip",
            .InitialDirectory = EarthSurfacePaths.RawDirectory,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            RawTidFile = dlg.FileName
            LastReport = $"GEBCO-TID gewählt: {RawTidFile}"
        End If
    End Sub

    Private Sub BrowseLandMask()
        Dim dlg As New OpenFileDialog With {
            .Title = "LandMask-Datei auswählen",
            .Filter = "Alle Dateien (*.*)|*.*",
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            RawLandMaskFile = dlg.FileName
            LastReport = $"LandMask gewählt: {RawLandMaskFile}"
        End If
    End Sub

    'Etappe A: Dummy-Generate, nur zum Test von BusyOverlay + Cancel + UI-Fluss
    Private Async Function DummyGenerateAsync() As Task
        Await BusyRunner.RunAsync(Me, "EarthSurface Cache", Sub(progress, ct)
                                                                progress?.Report(New ProgressInfo("Vorbereitung...", ProgressInfo.Indeterminate))
                                                                Thread.Sleep(300)

                                                                For i As Integer = 0 To 100 Step 5
                                                                    ct.ThrowIfCancellationRequested()
                                                                    progress?.Report(New ProgressInfo($"Dummy-Generate... {i}%", i))
                                                                    Thread.Sleep(50)
                                                                Next

                                                                progress?.Report(New ProgressInfo("Fertig (Dummy).", 100))
                                                            End Sub, canCancel:=True, showOverlay:=True)

        LastReport = $"Dummy fertig. Variante={LandMaskVariantTag}, {CellSizeDeg}°, {ResamplingKey}"
    End Function

    'Etappe B: Smoke-Test mit ESRI ASCII
    Private Async Function RunSmokeTestAsync() As Task(Of String)

        Try
            Dim result As String = Await BusyRunner.RunAsync(Of String)(
                Me, "EarthSurface: Smoke-Test",
                    Async Function(progress, ct)
                        'Height ist Pflicht
                        Dim sb As New StringBuilder()

                        sb.AppendLine(GebcoSmokeTest.RunZipSmokeTest(RawHeightFile, "HEIGHT", progress, ct))
                        sb.AppendLine()

                        'TID optional
                        If Not String.IsNullOrWhiteSpace(RawTidFile) AndAlso File.Exists(RawTidFile) Then
                            sb.AppendLine(GebcoSmokeTest.RunZipSmokeTest(RawTidFile, "TID", progress, ct))
                        Else
                            sb.AppendLine("[TID] Kein ZIP gewählt - übersprungen.")
                        End If

                        Return sb.ToString()
                    End Function,
                    canCancel:=True,
                    showOverlay:=True,
                    runInBackground:=True)

            LastReport = result
        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
        Catch ex As Exception
            LastReport = "Fehler: " & ex.Message
        End Try
    End Function
End Class
