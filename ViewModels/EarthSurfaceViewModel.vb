Imports System.IO
Imports System.Security.Cryptography
Imports System.Text
Imports System.Threading
Imports System.Windows
Imports System.Windows.Media
Imports Microsoft.Win32

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
                Await GenerateCacheAsync()
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

    'Etappe B3: Real-Cache-Builder aus ESRI ASCII
    Private Async Function GenerateCacheAsync() As Task(Of String)

        Try
            Dim result As String = Await BusyRunner.RunAsync(Of String)(
                Me,
                "EarthSurface: Cache generieren",
                Async Function(progress, ct)

                    ct.ThrowIfCancellationRequested()

                    '------------------------------
                    'A) BuildOptions aus VM-Zustand
                    '------------------------------

                    Dim opts As New EarthSurfaceCacheBuilder.BuildOptions With {
                        .SourceName = Me.SourceName,
                        .HeightZipPath = Me.RawHeightFile,
                        .TidZipPath = If(String.IsNullOrWhiteSpace(Me.RawTidFile), Nothing, Me.RawTidFile),
                        .CellSizeDeg = Me.CellSizeDeg,
                        .Resampling = "nearest", 'B3: erstmal fix
                        .LandMaskMode = Me.SelectedLandMaskMode,
                        .UseHysteresis = Me.UseHysteresis,
                        .HysteresisIterations = Me.HysteresisIterations,
                        .LandMaskVariant = Me.LandMaskVariantTag
                    }

                    '---------------
                    'B) Build & Save
                    '---------------

                    progress?.Report(New ProgressInfo("Starte Cache-Builder (Nearest)...", 0))
                    Dim buildReport As String = EarthSurfaceCacheBuilder.BuildAndSaveNearest(opts, progress, ct)

                    ct.ThrowIfCancellationRequested()

                    '-------------------------------
                    'C) TryOpenCache + 3 Stichproben
                    '-------------------------------
                    progress?.Report(New ProgressInfo("Öffne Cache zur Validierung...", 95))

                    Dim cache As EarthSurfaceCache = Nothing
                    Dim ek As CacheOpenErrorKind
                    Dim em As String = Nothing

                    Dim ok As Boolean = EarthSurfaceCacheStore.TryOpenCache(
                        source:=opts.SourceName,
                        cellSizeDeg:=opts.CellSizeDeg,
                        resampling:="nearest",
                        cache:=cache,
                        errorKind:=ek,
                        errorMessage:=em,
                        landMaskVariant:=opts.LandMaskVariant,
                        progress:=progress,
                        ct:=ct)

                    If Not ok OrElse cache Is Nothing Then
                        Throw New InvalidDataException($"Cache konnte nicht wieder geöffnet werden: {ek} - {em}")
                    End If

                    'DEBUG
                    Dim bmp = EarthSurfacePreviewRenderer.BuildLandOceanBitmapFromHeight(cache)
                    EarthSurfacePreviewRenderer.SavePng(bmp, CacheDirectory & "\preview.bmp")

                    ct.ThrowIfCancellationRequested()

                    Dim sampleReport As String = BuildSampleReport(cache)
                    progress?.Report(New ProgressInfo("Fertig.", 100))

                    Return buildReport & Environment.NewLine & Environment.NewLine & sampleReport

                End Function,
                canCancel:=True,
                showOverlay:=True,
                runInBackground:=True)

            LastReport = result

        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
            Return LastReport
        Catch ex As Exception
            LastReport = "Fehler: " & ex.Message
            Return LastReport
        End Try
        Return "Fehler!!!"
    End Function

    Private Function BuildSampleReport(cache As EarthSurfaceCache) As String

        Dim m = cache.Meta
        Dim latCount As Integer = m.LatCount
        Dim lonCount As Integer = m.LonCount

        Dim hasTid As Boolean = (m.HasTid AndAlso cache.Tid IsNot Nothing AndAlso cache.Tid.Length = latCount * lonCount)
        Dim hasLm As Boolean = (m.HasLandMask AndAlso cache.LandMask IsNot Nothing AndAlso cache.LandMask.Length = latCount * lonCount)

        Dim sb As New StringBuilder()
        sb.AppendLine("=== Cache Stichproben ===")
        sb.AppendLine($"Raster: {latCount} x {lonCount}  cell={m.CellSizeDeg}°")
        sb.AppendLine($"HasTid={m.HasTid}, HasLandMask={m.HasLandMask}")
        sb.AppendLine()

        '3 Samples: (0,0), Mitte, (lat-1,lon-1)
        Dim samples = New(name As String, lat As Integer, lon As Integer)() {
            ("NW (0,0)", 0, 0),
            ("Center", latCount \ 2, lonCount \ 2),
            ("SE (last,last)", latCount - 1, lonCount - 1)
        }

        For Each s In samples
            Dim idx As Integer = s.lat * lonCount + s.lon

            Dim h As Single = cache.HeightM(idx)
            Dim hStr As String = If(Single.IsNaN(h), "NaN(Void)", h.ToString("0.##", Globalization.CultureInfo.InvariantCulture))

            Dim tidStr As String = "(n/a)"
            If hasTid Then
                tidStr = cache.Tid(idx).ToString("0", Globalization.CultureInfo.InvariantCulture)
            End If

            Dim lmStr As String = "(n/a)"
            If hasLm Then
                lmStr = cache.LandMask(idx).ToString()
            End If

            Dim latDeg As Double = LatCenterDeg(s.lat, latCount, m.CellSizeDeg)
            Dim lonDeg As Double = LonCenterDeg(s.lon, lonCount, m.CellSizeDeg)

            sb.AppendLine($"{s.name}: latIdx={s.lat}, lonIdx={s.lon}  =>  lat={latDeg:0.###}°, lon={lonDeg:0.###}°")
            sb.AppendLine($"  Height={hStr}m")
            sb.AppendLine($"  TID={tidStr}")
            sb.AppendLine($"  LandMask={lmStr} (0=ocean, 1 =land)")
        Next

        Return sb.ToString()
    End Function

    Private Function LatCenterDeg(latIndex As Integer, latCount As Integer, cellSizeDeg As Double) As Double
        'latIndex 0 = Nord (oben)
        Return 90.0 - (latIndex + 0.5) * cellSizeDeg
    End Function

    Private Function LonCenterDeg(lonIndex As Integer, lonCount As Integer, cellSizeDeg As Double) As Double
        'lonIndex 0 = West (links)
        Return -180 + (lonIndex + 0.5) * cellSizeDeg
    End Function
End Class
