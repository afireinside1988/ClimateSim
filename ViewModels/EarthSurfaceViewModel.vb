Imports System.DirectoryServices.ActiveDirectory
Imports System.Drawing.Drawing2D
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

    Private _loadedCache As EarthSurfaceCache
    Public Property LoadedCache As EarthSurfaceCache
        Get
            Return _loadedCache
        End Get
        Set(value As EarthSurfaceCache)
            SetProperty(_loadedCache, value)
        End Set
    End Property

    Private _provider As DataEarthSurfaceProvider

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

                If value = LandMaskMode.FromHeight Then
                    UseHysteresis = True
                Else
                    UseHysteresis = False
                End If

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

    Private _camera As New CameraState With {
        .CenterLat = 0.0,
        .CenterLon = 0.0,
        .SpanLat = 180.0,
        .SpanLon = 360.0
    }
    Public Property Camera As CameraState
        Get
            Return _camera
        End Get
        Set(value As CameraState)
            SetProperty(_camera, value)
        End Set
    End Property

    Private _zoom As Double = 1.0
    Public Property Zoom As Double
        Get
            Return _zoom
        End Get
        Set(value As Double)
            SetProperty(_zoom, Math.Max(0.1, Math.Min(50.0, value)))
        End Set
    End Property

    Private _isPanning As Boolean
    Private _panStartMouse As Point
    Private _panStartX As Double
    Private _panStartY As Double

    Private _lastViewportW As Double = 0
    Private _lastViewportH As Double = 0
    Private _pendingFitToViewport As Boolean = False

    Private _contentWidth As Double
    Public Property ContentWidth As Double
        Get
            Return _contentWidth
        End Get
        Set(value As Double)
            SetProperty(_contentWidth, value)
        End Set
    End Property

    Private _contentHeight As Double
    Public Property ContentHeight As Double
        Get
            Return _contentHeight
        End Get
        Set(value As Double)
            SetProperty(_contentHeight, value)
        End Set
    End Property

    Private _panX As Double = 0.0
    Public Property PanX As Double
        Get
            Return _panX
        End Get
        Set(value As Double)
            SetProperty(_panX, value)
        End Set
    End Property

    Private _panY As Double = 0.0
    Public Property PanY As Double
        Get
            Return _panY
        End Get
        Set(value As Double)
            SetProperty(_panY, value)
        End Set
    End Property

    Private _surfaceLayer As ImageSource
    Public Property SurfaceLayer As ImageSource
        Get
            Return _surfaceLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_surfaceLayer, value)
        End Set
    End Property

    Private _showReliefLayer As Boolean = True
    Public Property ShowReliefLayer As Boolean
        Get
            Return _showReliefLayer
        End Get
        Set(value As Boolean)
            If SetProperty(_showReliefLayer, value) Then

                If value = False Then
                    UseHillShading = False
                End If

                OnPropertyChanged(NameOf(UseHillShading))
            End If
        End Set
    End Property

    Private _useHillShading As Boolean = True
    Public Property UseHillShading As Boolean
        Get
            Return _useHillShading
        End Get
        Set(value As Boolean)
            SetProperty(_useHillShading, value)
        End Set
    End Property

    Private _showLandMaskLayer As Boolean = False
    Public Property ShowLandMaskLayer As Boolean
        Get
            Return _showLandMaskLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showLandMaskLayer, value)
        End Set
    End Property

    Private _showTidLayer As Boolean = False
    Public Property ShowTidLayer As Boolean
        Get
            Return _showTidLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showTidLayer, value)
        End Set
    End Property

    Private _showGridLayer As Boolean = False
    Public Property ShowGridLayer As Boolean
        Get
            Return _showGridLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showGridLayer, value)
        End Set
    End Property

    Private _hoverText As String = "Keine Vorschau..."
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

    Public ReadOnly Property CanLoadCache As Boolean
        Get
            If String.IsNullOrWhiteSpace(SourceName) Then Return False
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
    Public ReadOnly Property LoadCacheCommand As ICommand

    Public ReadOnly Property MapMouseMoveCommand As ICommand
    Public ReadOnly Property MapMouseLeaveCommand As ICommand

    Public ReadOnly Property BeginPanCommand As ICommand
    Public ReadOnly Property PanCommand As ICommand
    Public ReadOnly Property EndPanCommand As ICommand
    Public ReadOnly Property ZoomCommand As ICommand
    Public ReadOnly Property ViewportChangedCommand As ICommand

#End Region

    Public Sub New()

        BrowseHeightCommand = New RelayCommand(Of Object)(Sub(o) BrowseHeight())
        BrowseTidCommand = New RelayCommand(Of Object)(Sub(o) BrowseTid())
        BrowseLandMaskCommand = New RelayCommand(Of Object)(Sub(o) BrowseLandMask())

        MapMouseMoveCommand = New RelayCommand(Of Object)(Sub(p) OnMapMouseMove(p), Function(p) LoadedCache IsNot Nothing)
        MapMouseLeaveCommand = New RelayCommand(Of Object)(Sub(p) OnMapMouseLeave())

        BeginPanCommand = New RelayCommand(Of PanRequest)(Sub(p) BeginPan(p))
        PanCommand = New RelayCommand(Of PanRequest)(Sub(p) UpdatePan(p))
        EndPanCommand = New RelayCommand(Of Object)(Sub(p) EndPan())
        ZoomCommand = New RelayCommand(Of ZoomRequest)(Sub(z) ZoomAt(z))
        ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(Sub(r) OnViewportChanged(r))

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

        LoadCacheCommand = New RelayCommand(Of Object)(
            Async Sub(o)
                Await LoadCacheAsync()
            End Sub,
            Function(o) Not IsBusy)

    End Sub


#Region "Filepicker"

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

#End Region

#Region "Cache Laden"

    Private Async Function LoadCacheAsync() As Task(Of String)

        Dim dlg As New OpenFileDialog With {
            .Title = "EarthSurface-Cache laden",
            .Filter = "EarthSurface Meta-Datei (*.meta.json)|*.meta.json",
            .InitialDirectory = EarthSurfacePaths.CacheDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() <> True Then Return "Datei nicht gefunden."

        Dim metaPath As String = dlg.FileName

        Try

            Dim result As String = Await BusyRunner.RunAsync(Of String)(
                Me,
                "EarthSurface: Cache laden",
                Function(progress, ct)

                    Dim cache As EarthSurfaceCache = Nothing
                    Dim ek As CacheOpenErrorKind
                    Dim em As String = Nothing

                    Dim ok As Boolean = EarthSurfaceCacheStore.TryOpenCacheFromFiles(metaPath, cache, ek, em, progress, ct)
                    If Not ok OrElse cache Is Nothing Then
                        Throw New InvalidDataException($"Cache konnte nicht gelesen werden: {ek} - {em}")
                    End If

                    'Meta -> VM spiegeln
                    ApplyLoadedMetaToViewModel(cache.Meta)

                    'Cache merken
                    LoadedCache = cache

                    Dim msg As String = $"Cache geladen: {Path.GetFileName(Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), Nothing))}"
                    Return msg
                End Function,
                canCancel:=True,
                showOverlay:=True)

            LastReport = result

            'Nach dem Laden: Preview neu rendern
            RenderPreviewFromCache()

            Return "Cache geladen."

        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
            Return "Abgebrochen"
        Catch ex As Exception
            LastReport = $"Fehler: {ex.Message}"
            Return $"Fehler: {ex.Message}"
        End Try

    End Function

    Private Sub ApplyLoadedMetaToViewModel(meta As EarthSurfaceCacheMeta)

        If meta Is Nothing Then Return

        SourceName = meta.Source

        'CellSizePreset aus meta.CellSizeDeg
        SelectedCellSize = CellSizePresetFromDeg(meta.CellSizeDeg)

        If meta.HasHeight AndAlso File.Exists(meta.RawHeightFile) Then
            RawHeightFile = meta.RawHeightFile
        Else
            RawHeightFile = Nothing
        End If

        If meta.HasTid AndAlso File.Exists(meta.RawTidFile) Then
            RawTidFile = meta.RawTidFile
        Else
            RawTidFile = Nothing
        End If

        If meta.HasLandMask Then
            Select Case meta.LandMaskSource
                Case LandMaskMode.FromHeight.ToString()
                    SelectedLandMaskMode = LandMaskMode.FromHeight
                Case LandMaskMode.FromTid0.ToString()
                    SelectedLandMaskMode = LandMaskMode.FromTid0
                Case Else
                    SelectedLandMaskMode = LandMaskMode.ExternalSource
            End Select
        End If

        Select Case meta.Resampling
            Case "nearest"
                SelectedResampling = ResamplingMode.Nearest
            Case "bilinear"
                SelectedResampling = ResamplingMode.Bilinear
            Case Else
                SelectedResampling = Nothing
        End Select

        If meta.LandMaskSource = LandMaskMode.FromHeight.ToString() Then
            UseHysteresis = meta.UseHysteresis
            HysteresisIterations = meta.HysteresisIterations
        End If

    End Sub

#End Region

#Region "Rendering"

    Private Sub RenderPreviewFromCache()

        If LoadedCache Is Nothing Then
            SurfaceLayer = Nothing
            _provider = Nothing
            Return
        End If


        _provider = DataEarthSurfaceProvider.CreateFromCache(LoadedCache)

        Dim width As Integer = LoadedCache.Meta.LonCount
        Dim height As Integer = LoadedCache.Meta.LatCount

        'Beispiel: Preview in voller Auflösung
        Dim bmp As WriteableBitmap = EarthSurfaceRenderer.RenderSurfaceTypeCamera(_provider, width, height, Camera)
        SurfaceLayer = bmp

        ContentWidth = width
        ContentHeight = height

        _pendingFitToViewport = True

        If _lastViewportW > 0 AndAlso _lastViewportH > 0 Then
            FitToViewport(_lastViewportW, _lastViewportH)
            _pendingFitToViewport = False
        Else

            Zoom = 1.0
            PanX = 0
            PanY = 0

        End If

    End Sub

    Private Sub OnMapMouseMove(param As Object)

        If LoadedCache Is Nothing Then
            HoverText = ""
            Return
        End If

        Dim r As HoverRequest = TryCast(param, HoverRequest)
        If r Is Nothing Then Return

        RememberViewportSize(r.ViewPortSize)

        Dim viewportSize As Size = r.ViewPortSize
        Dim contentSize As New Size(LoadedCache.Meta.LonCount, LoadedCache.Meta.LatCount)

        Dim geo = ScreenToGeo(r.MousePos, viewportSize, contentSize, Camera, Zoom, PanX, PanY)
        If Double.IsNaN(geo.Lat) OrElse Double.IsNaN(geo.Lon) Then
            HoverText = ""
            Return
        End If

        Dim info As SurfaceInfo = _provider.GetSurfaceInfo(geo.Lat, geo.Lon)

        'Optional: Index bestimmen (falls wir mal latIdx/lonIdx zeigen wollen)
        'Dim latIdx As Integer = CInt(Math.Floor((90.0 - geo.Lat) / LoadedCache.Meta.CellSizeDeg))
        'Dim lonIdx As Integer = CInt(Math.Floor((180.0 - geo.Lon) / LoadedCache.Meta.CellSizeDeg))

        HoverText = $"lat={geo.Lat:0.###}°, lon={geo.Lon:0.###}°   Surface={info.Surface}    Height={info.HeightM:0.##}m"

    End Sub

    Private Sub OnMapMouseLeave()
        HoverText = ""
    End Sub

    Private Sub OnViewportChanged(r As ViewportChangedRequest)

        If r Is Nothing Then Return

        _lastViewportW = r.ViewPortSize.Width
        _lastViewportH = r.ViewPortSize.Height

        If LoadedCache Is Nothing Then Return

        If _pendingFitToViewport Then
            FitToViewport(_lastViewportW, _lastViewportH)
            _pendingFitToViewport = False
        Else
            'bei Resize nur clampen/zentrieren
            ClampPan(_lastViewportW, _lastViewportH)
        End If

    End Sub

    Public Sub BeginPan(r As PanRequest)

        _isPanning = True
        _panStartMouse = r.MousePos
        _panStartX = PanX
        _panStartY = PanY

    End Sub

    Public Sub UpdatePan(r As PanRequest)

        If Not _isPanning Then Return

        Dim dx As Double = r.MousePos.X - _panStartMouse.X
        Dim dy As Double = r.MousePos.Y - _panStartMouse.Y

        PanX = _panStartX + dx
        PanY = _panStartY + dy

        ClampPan(r.ViewPortSize.Width, r.ViewPortSize.Height)

    End Sub

    Public Sub EndPan()

        _isPanning = False

    End Sub

    Public Sub ZoomAt(z As ZoomRequest)

        Dim zoomFactor As Double = If(z.Delta > 0, 1.1, 1 / 1.1)

        Dim oldZoom As Double = Zoom
        Dim newZoom As Double = Clamp(oldZoom * zoomFactor, 0.25, 20.0)

        If Math.Abs(newZoom - oldZoom) < 0.0000001 Then Return

        'Cursor in Content Space (vor Zoom)
        Dim cx As Double = (z.MousePos.X - PanX) / oldZoom
        Dim cy As Double = (z.MousePos.Y - PanY) / oldZoom

        'Zoom setzen
        Zoom = newZoom

        'Pan so korrigieren, dass (cx,cy) unter Cursor bleibt
        PanX = z.MousePos.X - cx * newZoom
        PanY = z.MousePos.Y - cy * newZoom

        ClampPan(z.ViewportSize.Width, z.ViewportSize.Height)
    End Sub
#End Region


#Region "Cache erstellen"

    Private Async Function GenerateCacheAsync() As Task(Of EarthSurfaceCache)

        Try
            Dim resultTuple As Tuple(Of EarthSurfaceCache, String) =
                Await BusyRunner.RunAsync(Of Tuple(Of EarthSurfaceCache, String))(
                Me,
                "EarthSurface: Cache generieren",
                Function(progress, ct)

                    ct.ThrowIfCancellationRequested()

                    '------------------------------
                    'A) BuildOptions aus VM-Zustand
                    '------------------------------

                    Dim opts As New EarthSurfaceCacheBuilder.BuildOptions With {
                        .SourceName = Me.SourceName,
                        .HeightZipPath = Me.RawHeightFile,
                        .TidZipPath = If(String.IsNullOrWhiteSpace(Me.RawTidFile), Nothing, Me.RawTidFile),
                        .CellSizeDeg = Me.CellSizeDeg,
                        .Resampling = Me.ResamplingKey,
                        .LandMaskMode = Me.SelectedLandMaskMode,
                        .UseHysteresis = Me.UseHysteresis,
                        .HysteresisIterations = Me.HysteresisIterations,
                        .LandMaskVariant = Me.LandMaskVariantTag
                    }

                    '---------------
                    'B) Build & Save
                    '---------------

                    progress?.Report(New ProgressInfo("Starte Cache-Builder...", 0))
                    Dim buildReport As String = EarthSurfaceCacheBuilder.BuildAndSave(opts, progress, ct)

                    ct.ThrowIfCancellationRequested()

                    '-------------------------------
                    'C) TryOpenCache + 3 Stichproben
                    '-------------------------------
                    progress?.Report(New ProgressInfo("Öffne Cache zur Validierung...", 95))

                    Dim opened As EarthSurfaceCache = Nothing
                    Dim ek As CacheOpenErrorKind
                    Dim em As String = Nothing

                    Dim ok As Boolean = EarthSurfaceCacheStore.TryOpenCacheFromSourceName(
                        source:=opts.SourceName,
                        cellSizeDeg:=opts.CellSizeDeg,
                        resampling:=opts.Resampling,
                        cache:=opened,
                        errorKind:=ek,
                        errorMessage:=em,
                        landMaskVariant:=opts.LandMaskVariant,
                        progress:=progress,
                        ct:=ct)

                    If Not ok OrElse opened Is Nothing Then
                        Throw New InvalidDataException($"Cache konnte nicht wieder geöffnet werden: {ek} - {em}")
                    End If

                    'DEBUG
                    Dim bmp = EarthSurfacePreviewRenderer.BuildLandOceanBitmapFromHeight(opened)
                    EarthSurfacePreviewRenderer.SavePng(bmp, EarthSurfacePaths.CacheDirectory & "\preview.png")

                    ct.ThrowIfCancellationRequested()

                    Dim sampleReport As String = BuildGenerateReport(opened)



                    Dim reportStr As String = buildReport & Environment.NewLine & Environment.NewLine & sampleReport
                    progress?.Report(New ProgressInfo("Fertig.", 100))

                    Return Tuple.Create(opened, reportStr)

                End Function,
                canCancel:=True,
                showOverlay:=True)

            Dim cache As EarthSurfaceCache = resultTuple.Item1
            Dim report As String = resultTuple.Item2

            LoadedCache = cache
            LastReport = report

            Return cache

        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
            Return Nothing
        Catch ex As Exception
            LastReport = "Fehler: " & ex.Message
            Return Nothing
        End Try

    End Function

    Private Shared Function BuildGenerateReport(cache As EarthSurfaceCache) As String

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

        If hasTid Then
            Dim minTid As Integer = Integer.MaxValue
            Dim maxTid As Integer = Integer.MinValue
            Dim cntUnknown As Integer = 0
            Dim cntLand0 As Integer = 0
            Dim cntOther As Integer = 0

            For i As Integer = 0 To latCount * lonCount - 1
                Dim t As Single = cache.Tid(i)
                If Single.IsNaN(t) Then Continue For

                Dim v As Integer = CInt(Math.Round(t))      'Tid ist als Single gespeichert, aber kommt aus Byte

                If v < minTid Then minTid = v
                If v > maxTid Then maxTid = v

                If v = 255 Then
                    cntUnknown += 1
                ElseIf v = 0 Then
                    cntLand0 += 1
                Else
                    cntOther += 1
                End If
            Next

            If minTid = Integer.MaxValue Then minTid = 0
            If maxTid = Integer.MinValue Then maxTid = 0

            sb.AppendLine()
            sb.AppendLine("=== TID Statistik ===")
            sb.AppendLine($"  Unknown (255): {cntUnknown:N0}")
            sb.AppendLine($"  Land (0):      {cntLand0:N0}")
            sb.AppendLine($"  Other:         {cntOther:N0}")
            sb.AppendLine($"  Min/Max:       {minTid} / {maxTid}")
            sb.AppendLine()
        End If

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

            Dim latDeg As Double = LatCenterDeg(s.lat, m.CellSizeDeg)
            Dim lonDeg As Double = LonCenterDeg(s.lon, m.CellSizeDeg)

            sb.AppendLine($"{s.name}: latIdx={s.lat}, lonIdx={s.lon}  =>  lat={latDeg:0.###}°, lon={lonDeg:0.###}°")
            sb.AppendLine($"  Height={hStr}m")
            sb.AppendLine($"  TID={tidStr}")
            sb.AppendLine($"  LandMask={lmStr} (0=ocean, 1 =land)")
        Next

        Return sb.ToString()
    End Function

#End Region

#Region "Helper"

    Private Shared Function LatCenterDeg(latIndex As Integer, cellSizeDeg As Double) As Double
        'latIndex 0 = Nord (oben)
        Return 90.0 - (latIndex + 0.5) * cellSizeDeg
    End Function

    Private Shared Function LonCenterDeg(lonIndex As Integer, cellSizeDeg As Double) As Double
        'lonIndex 0 = West (links)
        Return -180 + (lonIndex + 0.5) * cellSizeDeg
    End Function

    Private Shared Function CellSizePresetFromDeg(cellSizeDeg As Double) As CellSizePreset
        Const eps As Double = 0.0000001

        If Math.Abs(cellSizeDeg - 1.0) < eps Then
            Return CellSizePreset.Deg1
        ElseIf Math.Abs(cellSizeDeg - 0.5) < eps Then
            Return CellSizePreset.Deg0_5
        ElseIf Math.Abs(cellSizeDeg - 0.25) < eps Then
            Return CellSizePreset.Deg0_25
        End If

        Throw New InvalidDataException($"Uunbekannte CellSizeDeg in Meta: {cellSizeDeg}. Erwarten: 1.0, 0.5 oder 0.25.")
    End Function

    Public Shared Function ScreenToGeo(mousePos As Point,
                                       viewPortSize As Size,
                                       contentSize As Size,
                                       camera As CameraState,
                                       zoom As Double,
                                       panX As Double,
                                       panY As Double) As (Lat As Double, Lon As Double)

        If viewPortSize.Width <= 0 OrElse viewPortSize.Height <= 0 Then
            Return (Double.NaN, Double.NaN)
        End If

        If contentSize.Width <= 0 OrElse contentSize.Height <= 0 Then
            Return (Double.NaN, Double.NaN)
        End If
        If zoom <= 0 Then Return (Double.NaN, Double.NaN)

        'Mausposition in "Content Space" zurückrechnen (Inverse des RenderTransforms)
        Dim xContent As Double = (mousePos.X - panX) / zoom
        Dim yContent As Double = (mousePos.Y - panY) / zoom

        If xContent < 0 OrElse xContent >= contentSize.Width OrElse yContent < 0 OrElse yContent >= contentSize.Height Then
            Return (Double.NaN, Double.NaN)
        End If

        'Normierte Koordinaten
        Dim xNorm As Double = xContent / contentSize.Width
        Dim yNorm As Double = yContent / contentSize.Height

        'Geo berechnen (inverse Render-Formel)
        Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
        Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat

        'Clamp/Wrap
        lat = Clamp(lat, -90.0, 90.0)
        lon = WrapLon180(lon)

        Return (lat, lon)

    End Function

    Private Sub ClampPan(viewportW As Double, viewportH As Double)

        If LoadedCache Is Nothing Then Return

        Dim contentW As Double = LoadedCache.Meta.LonCount
        Dim contentH As Double = LoadedCache.Meta.LatCount

        Dim scaledW As Double = contentW * Zoom
        Dim scaledH As Double = contentH * Zoom

        'Wenn Content kleiner als Viewport: zentrieren (statt oben links lassen)
        If scaledW <= viewportW Then
            PanX = (viewportW - scaledW) / 2.0
        Else
            Dim minX As Double = viewportW - scaledW
            PanX = Clamp(PanX, minX, 0)
        End If

        If scaledH <= viewportH Then
            PanY = (viewportH - scaledH) / 2.0
        Else
            Dim minY As Double = viewportH - scaledH
            PanY = Clamp(PanY, minY, 0)
        End If

    End Sub

    Private Sub FitToViewport(viewPortW As Double, viewPortH As Double)

        If LoadedCache Is Nothing Then Return
        If viewPortH <= 0 OrElse viewPortW <= 0 Then Return

        Dim contentW As Double = LoadedCache.Meta.LonCount
        Dim contentH As Double = LoadedCache.Meta.LatCount
        If contentW <= 0 OrElse contentH <= 0 Then Return

        Dim fitZoom As Double = Math.Min(viewPortW / contentW, viewPortH / contentH)

        'Optional: nicht größer als 1 hochskalieren
        fitZoom = Math.Min(fitZoom, 1.0)

        Zoom = Clamp(fitZoom, 0.05, 20.0)

        'Zentrieren
        PanX = (viewPortW - contentW * Zoom) / 2.0
        PanY = (viewPortH - contentH * Zoom) / 2.0

        'Sicherheit
        ClampPan(viewPortW, viewPortH)

    End Sub

    Private Sub RememberViewportSize(vp As Size)
        If vp.Width > 0 Then _lastViewportW = vp.Width
        If vp.Height > 0 Then _lastViewportH = vp.Height
    End Sub

#End Region

End Class
