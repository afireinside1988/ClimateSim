
Imports System.IO
Imports System.Runtime.Intrinsics
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Media.Media3D
Imports Microsoft.Win32
Imports OSGeo.GDAL

Public Class LandCoverViewModel
    Inherits ViewModelBase

#Region "Konstruktor"

    Public Sub New()

        SourceName = "COPERNICUS_LC100"
        EpochYear = DateTime.UtcNow.Year

        ' Defaults wie im EarthSurfaceWindow (Grid an, Overlays aus)
        _showGridLayer = True
        _showConfidenceOverlay = False
        _showLandIceThicknessOverlay = False

        ' Defaults Pan/Zoom
        _zoom = 1.0
        _panX = 0.0
        _panY = 0.0

        ' Defaults Hover
        _showHoverOverlay = False
        _hoverOverlayText = ""
        _hoverOverlayX = 0
        _hoverOverlayY = 0

        ' Defaults Statusbar
        StatusLatText = "Lat: --.--"
        StatusLonText = "Lon: --.--"
        StatusLandCoverClassText = "LC: -"
        StatusConfidenceText = "Conf: -"
        StatusLandIceThicknessText = "Ice: -"
        StatusZoomText = $"Zoom: {Zoom:0.###}x"

        ' Commands (Menu/Actions)
        _LoadCacheCommand = New AsyncRelayCommand(Of Object)(Function(o) LoadCacheAsync(), Function(o) Not IsBusy)
        _GenerateCacheCommand = New AsyncRelayCommand(Of Object)(Function(o) GenerateCacheAsync(), Function(o) CanGenerateCache AndAlso Not IsBusy)
        _OpenGlobePreviewCommand = New RelayCommand(Of Object)(Sub(o) OpenGlobePreview(), Function(o) CanOpenGlobePreview AndAlso Not IsBusy)

        ' Commands (Browse/Clear)
        _BrowseBaseEarthSurfaceCacheCommand = New RelayCommand(Of Object)(Sub(o) BrowseBaseEarthSurfaceCache(), Function(o) Not IsBusy)
        _ClearBaseEarthSurfaceCacheCommand = New RelayCommand(Of Object)(Sub(o) ClearBaseEarthSurfaceCache(), Function(o) Not String.IsNullOrWhiteSpace(BaseEarthSurfaceCachePath))

        _BrowseClassTifCommand = New RelayCommand(Of Object)(Sub(o) BrowseClassTif(), Function(o) Not IsBusy)
        _ClearClassTifFileCommand = New RelayCommand(Of Object)(Sub(o) ClearClassTifFile(), Function(o) RawClassTifFile IsNot Nothing)

        _BrowseProbaTifCommand = New RelayCommand(Of Object)(Sub(o) BrowseProbaTif(), Function(o) Not IsBusy)
        _ClearProbaTifFileCommand = New RelayCommand(Of Object)(Sub(o) ClearProbaTifFile(), Function(o) RawProbaTifFile IsNot Nothing)

        _BrowseBedMachineGreenlandCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineGreenland(), Function(o) Not IsBusy)
        _ClearBedMachineGreenlandFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineGreenlandFile(), Function(o) Not String.IsNullOrWhiteSpace(RawBedMachineGreenlandFile))

        _BrowseBedMachineAntarcticaCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineAntarctica(), Function(o) Not IsBusy)
        _ClearBedMachineAntarcticaFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineAntarcticaFile(), Function(o) Not String.IsNullOrWhiteSpace(RawBedMachineAntarcticaFile))

        ' Commands (Pan/Zoom/Mouse; exakt passend zum Behavior)
        _BeginPanCommand = New RelayCommand(Of PanRequest)(AddressOf BeginPan, Function(r) Not IsBusy)
        _PanCommand = New RelayCommand(Of PanRequest)(AddressOf Pan, Function(r) Not IsBusy)
        _EndPanCommand = New RelayCommand(Of Object)(AddressOf EndPan, Function(o) Not IsBusy)
        _ZoomCommand = New RelayCommand(Of ZoomRequest)(AddressOf ZoomMap, Function(r) Not IsBusy)
        _ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(AddressOf ViewportChanged, Function(r) Not IsBusy)

        _MapMouseDownCommand = New RelayCommand(Of MapMouseDownRequest)(AddressOf MapMouseDown, Function(r) Not IsBusy)
        _MapMouseUpCommand = New RelayCommand(Of MapMouseUpRequest)(AddressOf MapMouseUp, Function(r) Not IsBusy)
        _MapMouseMoveCommand = New RelayCommand(Of MapMouseMoveRequest)(AddressOf MapMouseMove, Function(r) Not IsBusy)

        _MapMouseLeaveCommand = New RelayCommand(Of Object)(Sub(o) MapMouseLeave(), Function(o) True)

        ' Report
        _lastReport = ""
    End Sub

#End Region

#Region "Commands - public (Bindings)"

    Public ReadOnly Property LoadCacheCommand As ICommand
    Public ReadOnly Property GenerateCacheCommand As ICommand

    Public ReadOnly Property OpenGlobePreviewCommand As ICommand

    Public ReadOnly Property BrowseBaseEarthSurfaceCacheCommand As ICommand
    Public ReadOnly Property ClearBaseEarthSurfaceCacheCommand As ICommand
    Public ReadOnly Property BrowseClassTifCommand As ICommand
    Public ReadOnly Property ClearClassTifFileCommand As ICommand
    Public ReadOnly Property BrowseProbaTifCommand As ICommand
    Public ReadOnly Property ClearProbaTifFileCommand As ICommand

    Public ReadOnly Property BrowseBedMachineGreenlandCommand As ICommand
    Public ReadOnly Property ClearBedMachineGreenlandFileCommand As ICommand
    Public ReadOnly Property BrowseBedMachineAntarcticaCommand As ICommand
    Public ReadOnly Property ClearBedMachineAntarcticaFileCommand As ICommand

    ' Map / PanZoom
    Public ReadOnly Property BeginPanCommand As ICommand
    Public ReadOnly Property PanCommand As ICommand
    Public ReadOnly Property EndPanCommand As ICommand
    Public ReadOnly Property ZoomCommand As ICommand
    Public ReadOnly Property ViewportChangedCommand As ICommand
    Public ReadOnly Property MapMouseDownCommand As ICommand
    Public ReadOnly Property MapMouseUpCommand As ICommand
    Public ReadOnly Property MapMouseMoveCommand As ICommand
    Public ReadOnly Property MapMouseLeaveCommand As ICommand

#End Region

#Region "Settings"

    Private _sourceName As String
    Public Property SourceName As String
        Get
            Return _sourceName
        End Get
        Set(value As String)
            If SetProperty(_sourceName, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _epochYear As Integer
    Public Property EpochYear As Integer
        Get
            Return _epochYear
        End Get
        Set(value As Integer)
            SetProperty(_epochYear, value)
        End Set
    End Property

    Private _baseEarthSurfaceCachePath As String
    Public Property BaseEarthSurfaceCachePath As String
        Get
            Return _baseEarthSurfaceCachePath
        End Get
        Set(value As String)
            If SetProperty(_baseEarthSurfaceCachePath, value) Then
                OnPropertyChanged(NameOf(HasBaseEarthSurfaceCachePath))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Public ReadOnly Property HasBaseEarthSurfaceCachePath As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(_baseEarthSurfaceCachePath)
        End Get
    End Property

    Private _rawClassTifFile As String
    Public Property RawClassTifFile As String
        Get
            Return _rawClassTifFile
        End Get
        Set(value As String)
            If SetProperty(_rawClassTifFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawProbaTifFile As String
    Public Property RawProbaTifFile As String
        Get
            Return _rawProbaTifFile
        End Get
        Set(value As String)
            If SetProperty(_rawProbaTifFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawBedMachineGreenlandFile As String
    Public Property RawBedMachineGreenlandFile As String
        Get
            Return _rawBedMachineGreenlandFile
        End Get
        Set(value As String)
            SetProperty(_rawBedMachineGreenlandFile, value)
        End Set
    End Property

    Private _rawBedMachineAntarcticaFile As String
    Public Property RawBedMachineAntarcticaFile As String
        Get
            Return _rawBedMachineAntarcticaFile
        End Get
        Set(value As String)
            SetProperty(_rawBedMachineAntarcticaFile, value)
        End Set
    End Property

    Private _lastReport As String
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
            If EarthSurfaceCacheMeta Is Nothing Then
                Return False
            Else
                Return EarthSurfaceCacheMeta.CellSizeDeg > 0 AndAlso RawClassTifFile IsNot Nothing
            End If
        End Get
    End Property

    Public ReadOnly Property TargetCellSizeText As String
        Get
            If EarthSurfaceCacheMeta IsNot Nothing Then Return EarthSurfaceCacheMeta.CellSizeDeg.ToString() & "°"
            Return ""
        End Get
    End Property

    Public ReadOnly Property TargetResolutionText As String
        Get
            If EarthSurfaceCacheMeta IsNot Nothing Then Return EarthSurfaceCacheMeta.LonCount.ToString() & " x " & EarthSurfaceCacheMeta.LatCount.ToString()
            Return ""
        End Get
    End Property

#End Region

#Region "Layers"

    Private _landCoverLayer As ImageSource
    Public Property LandCoverLayer As ImageSource
        Get
            Return _landCoverLayer
        End Get
        Set(value As ImageSource)
            If SetProperty(_landCoverLayer, value) Then
                OnPropertyChanged(NameOf(CanOpenGlobePreview))
            End If
        End Set
    End Property

    Private _confidenceOverlay As ImageSource
    Public Property ConfidenceOverlay As ImageSource
        Get
            Return _confidenceOverlay
        End Get
        Set(value As ImageSource)
            SetProperty(_confidenceOverlay, value)
        End Set
    End Property

    Private _landIceThicknessOverlay As ImageSource
    Public Property LandIceThicknessOverlay As ImageSource
        Get
            Return _landIceThicknessOverlay
        End Get
        Set(value As ImageSource)
            SetProperty(_landIceThicknessOverlay, value)
        End Set
    End Property

    Private _earthSurfaceCacheMeta As EarthSurfaceCacheMeta
    Public Property EarthSurfaceCacheMeta As EarthSurfaceCacheMeta
        Get
            Return _earthSurfaceCacheMeta
        End Get
        Set(value As EarthSurfaceCacheMeta)
            SetProperty(_earthSurfaceCacheMeta, value)
        End Set
    End Property

    Private _loadedLandCoverCache As LandCoverCache
    Public Property LoadedLandCoverCache As LandCoverCache
        Get
            Return _loadedLandCoverCache
        End Get
        Set(value As LandCoverCache)
            SetProperty(_loadedLandCoverCache, value)
        End Set
    End Property

#End Region

#Region "Overlay Toggles"

    Private _showGridLayer As Boolean
    Public Property ShowGridLayer As Boolean
        Get
            Return _showGridLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showGridLayer, value)
        End Set
    End Property

    Private _showConfidenceOverlay As Boolean
    Public Property ShowConfidenceOverlay As Boolean
        Get
            Return _showConfidenceOverlay
        End Get
        Set(value As Boolean)
            SetProperty(_showConfidenceOverlay, value)
        End Set
    End Property

    Private _showLandIceThicknessOverlay As Boolean
    Public Property ShowLandIceThicknessOverlay As Boolean
        Get
            Return _showLandIceThicknessOverlay
        End Get
        Set(value As Boolean)
            SetProperty(_showLandIceThicknessOverlay, value)
        End Set
    End Property

#End Region

#Region "Pan/Zoom + Content Size (Bindings aus XAML)"

    Private _lastViewportW As Double
    Private _lastViewportH As Double
    Private _pendingFitToViewport As Boolean

    Private _zoom As Double
    Public Property Zoom As Double
        Get
            Return _zoom
        End Get
        Set(value As Double)
            If SetProperty(_zoom, value) Then
                StatusZoomText = $"Zoom: {Zoom:0.###}x"
            End If
        End Set
    End Property

    Private _panX As Double
    Public Property PanX As Double
        Get
            Return _panX
        End Get
        Set(value As Double)
            SetProperty(_panX, value)
        End Set
    End Property

    Private _panY As Double
    Public Property PanY As Double
        Get
            Return _panY
        End Get
        Set(value As Double)
            SetProperty(_panY, value)
        End Set
    End Property

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

#End Region

#Region "Hover Overlay (Floating Label)"

    Private _showHoverOverlay As Boolean
    Public Property ShowHoverOverlay As Boolean
        Get
            Return _showHoverOverlay
        End Get
        Set(value As Boolean)
            SetProperty(_showHoverOverlay, value)
        End Set
    End Property

    Private _hoverOverlayX As Double
    Public Property HoverOverlayX As Double
        Get
            Return _hoverOverlayX
        End Get
        Set(value As Double)
            SetProperty(_hoverOverlayX, value)
        End Set
    End Property

    Private _hoverOverlayY As Double
    Public Property HoverOverlayY As Double
        Get
            Return _hoverOverlayY
        End Get
        Set(value As Double)
            SetProperty(_hoverOverlayY, value)
        End Set
    End Property

    Private _hoverOverlayText As String
    Public Property HoverOverlayText As String
        Get
            Return _hoverOverlayText
        End Get
        Set(value As String)
            SetProperty(_hoverOverlayText, value)
        End Set
    End Property

#End Region

#Region "Statusbar"

    Private _statusLatText As String
    Public Property StatusLatText As String
        Get
            Return _statusLatText
        End Get
        Set(value As String)
            SetProperty(_statusLatText, value)
        End Set
    End Property

    Private _statusLonText As String
    Public Property StatusLonText As String
        Get
            Return _statusLonText
        End Get
        Set(value As String)
            SetProperty(_statusLonText, value)
        End Set
    End Property

    Private _statusLandCoverClassText As String
    Public Property StatusLandCoverClassText As String
        Get
            Return _statusLandCoverClassText
        End Get
        Set(value As String)
            SetProperty(_statusLandCoverClassText, value)
        End Set
    End Property

    Private _statusConfidenceText As String
    Public Property StatusConfidenceText As String
        Get
            Return _statusConfidenceText
        End Get
        Set(value As String)
            SetProperty(_statusConfidenceText, value)
        End Set
    End Property

    Private _statusLandIceThicknessText As String
    Public Property StatusLandIceThicknessText As String
        Get
            Return _statusLandIceThicknessText
        End Get
        Set(value As String)
            SetProperty(_statusLandIceThicknessText, value)
        End Set
    End Property

    Private _statusZoomText As String
    Public Property StatusZoomText As String
        Get
            Return _statusZoomText
        End Get
        Set(value As String)
            SetProperty(_statusZoomText, value)
        End Set
    End Property

    Public ReadOnly Property CanOpenGlobePreview As Boolean
        Get
            Return LandCoverLayer IsNot Nothing
        End Get
    End Property

    Private Sub UpdateStatusUi(hit As CellHit)

        Dim idx As Integer = hit.Index

        StatusLatText = $"Lat: {hit.Lat:0.##}°"
        StatusLonText = $"Lon: {hit.Lon:0.##}°"

        Dim clsV As Byte = Nothing
        Dim confV As Byte = Nothing
        Dim iceV As Double = Nothing

        If LoadedLandCoverCache.LandCoverClass IsNot Nothing AndAlso idx >= 0 Then

            If idx < LoadedLandCoverCache.LandCoverClass.Length Then
                clsV = LoadedLandCoverCache.LandCoverClass(idx)
            End If

            If LoadedLandCoverCache.Meta.HasConfidence Then
                If idx < LoadedLandCoverCache.Confidence.Length Then
                    confV = LoadedLandCoverCache.Confidence(idx)
                End If
            End If

            If LoadedLandCoverCache.Meta.HasLandIceThickness Then
                If idx < LoadedLandCoverCache.LandIceThicknessM.Length Then
                    Dim iceVV As Double = LoadedLandCoverCache.LandIceThicknessM(idx)
                    If Not Double.IsNaN(iceVV) OrElse Not Double.IsInfinity(iceVV) Then
                        iceV = iceVV
                    End If
                End If
            End If
        End If


        'If HasEditSession AndAlso IsEditMode AndAlso _editSession IsNot Nothing Then
        '    Dim hV As Single = _editSession.GetEffectiveHeight(idx)
        '    If Not Single.IsNaN(hV) OrElse Not Single.IsInfinity(hV) Then
        '        h = CDbl(hV)
        '    End If
        'ElseIf LoadedCache?.HeightM IsNot Nothing AndAlso idx >= 0 AndAlso idx < LoadedCache.HeightM.Length Then
        '    Dim hV As Single = LoadedCache.HeightM(idx)
        '    If Not Single.IsNaN(hV) OrElse Not Single.IsInfinity(hV) Then
        '        h = CDbl(hV)
        '    End If
        'End If

        'Dim surfaceText As String
        'If HasEditSession AndAlso IsEditMode Then
        '    surfaceText = SurfaceTextFromEditor(_editSession, idx)
        'Else
        '    surfaceText = SurfaceTextFromCache(LoadedCache, idx)
        'End If

        'SetStatusBar(hit.Lat, hit.Lon, h, surfaceText, Zoom)
    End Sub
#End Region

#Region "Command Handler - Menu/Actions"

    Private NotInheritable Class CacheOpenResult
        Public Property Ok As Boolean
        Public Property Cache As LandCoverCache
        Public Property ErrorKind As CacheOpenErrorKind
        Public Property ErrorMessage As String
    End Class

    Private Async Function LoadCacheAsync() As Task(Of String)

        Dim dlg As New OpenFileDialog With {
            .Title = "Landcover-Cache laden",
            .Filter = "LandCover Meta-Datei (*.meta.json)|*.meta.json",
            .InitialDirectory = DataEarthPaths.CacheDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() <> True Then Return "Datei nicht gefunden."

        Dim metaPath As String = dlg.FileName

        Try

            Dim result As CacheOpenResult = Await BusyRunner.RunAsync(Of CacheOpenResult)(
                Me,
                "EarthSurface: Cache laden",
                Function(progress, ct)

                    Dim cache As LandCoverCache = Nothing
                    Dim ek As CacheOpenErrorKind
                    Dim em As String = Nothing

                    Dim ok As Boolean = LandCoverCacheStore.TryOpenCacheFromFiles(metaPath, cache, ek, em, progress, ct)

                    Return New CacheOpenResult With {
                        .Ok = ok AndAlso cache IsNot Nothing,
                        .Cache = cache,
                        .ErrorKind = ek,
                        .ErrorMessage = em
                    }

                End Function,
                canCancel:=True,
                showOverlay:=True)

            If Not result.Ok Then
                Dim msg = $"Cache konnte nicht gelesen werden: {result.ErrorKind} - {result.ErrorMessage}"
                LastReport = $"Fehler beim Laden: {msg}"
                Return $"Fehler: {msg}"
            End If

            Dim openedCache As LandCoverCache = result.Cache

            'Meta -> VM spiegeln
            'ApplyLoadedMetaToViewModel(openedCache.Meta)

            'Cache merken
            'SetLoadedCachePathsFromMetaPath(metaPath)
            LoadedLandCoverCache = openedCache

            LastReport = $"Cache geladen: {Path.GetFileName(Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), Nothing))}"

            'Nach dem Laden: Preview neu rendern
            Await RenderPreviewFromCacheAsync()

            Return "Cache geladen."

        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
            Return "Abgebrochen"
        Catch ex As Exception
            LastReport = $"Fehler: {ex.Message}"
            Return $"Fehler: {ex.Message}"
        End Try
    End Function

    Private Async Function GenerateCacheAsync() As Task
        IsBusy = True
        Try
            LastReport = ""

            'TODO: in MainWindow initialisieren
            ' GDAL init
            Gdal.AllRegister()

            Dim resultTuple As Tuple(Of LandCoverCache, String) =
                Await BusyRunner.RunAsync(Of Tuple(Of LandCoverCache, String))(
                    Me,
                    "LandCover: Cache generieren...",
                    Function(progress, ct)

                        progress?.Report(New ProgressInfo("Initialiseren GDAL...", -1))
                        ct.ThrowIfCancellationRequested()
                        CopernicusLc100Processor.InitGdal()

                        Dim opts As New LandCoverCacheBuilder.BuildOptions With {
                            .SourceName = SourceName,
                            .EpochYear = EpochYear,
                            .ClassTifPath = RawClassTifFile,
                            .ProbaTifPath = RawProbaTifFile,
                            .TargetCellSizeDeg = EarthSurfaceCacheMeta.CellSizeDeg,
                            .EarthSurfaceReference = BaseEarthSurfaceCachePath,
                            .EarthSurfaceCreateUTC = EarthSurfaceCacheMeta.CreateUtc,
                            .IncludeConfidence = RawProbaTifFile IsNot Nothing
                        }

                        progress?.Report(New ProgressInfo("Starte Cache-Build...", 0))

                        Dim buildReport As String =
                            LandCoverCacheBuilder.BuildAndSave(opts, progress, ct)

                        ct.ThrowIfCancellationRequested()

                        'Generierten Cache öffnen und an das VM übergeben
                        Dim paths = LandCoverCacheStore.GetCachePaths(opts.SourceName, opts.EpochYear, opts.TargetCellSizeDeg)
                        Dim opened As LandCoverCache = Nothing
                        Dim kind As CacheOpenErrorKind = CacheOpenErrorKind.None
                        Dim msg As String = Nothing

                        If Not LandCoverCacheStore.TryOpenCacheFromFiles(paths.metaPath, opened, kind, msg, progress, ct) Then
                            Throw New InvalidDataException($"Cache wurde gespeichert, kann aber nicht wieder geöffnet werden: {kind} - {msg}")
                        End If

                        progress?.Report(New ProgressInfo("Fertig.", 100))
                        Return Tuple.Create(opened, buildReport)
                    End Function,
                    canCancel:=True,
                    showOverlay:=True)

            LoadedLandCoverCache = resultTuple.Item1

            If LoadedLandCoverCache IsNot Nothing Then Await RenderPreviewFromCacheAsync()

            LastReport = resultTuple.Item2
        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
        Catch ex As Exception
            LastReport = "Fehler:" & Environment.NewLine & FlattenException(ex, showStackTrace:=False)
        Finally
            IsBusy = False
            BusyIsIndeterminate = False
        End Try
    End Function

    Private Sub OpenGlobePreview()
        ' TODO: GlobePreview öffnen
        LastReport = "TODO: OpenGlobePreview"
    End Sub

#End Region

#Region "Command Handler - Browse/Clear"

    Private Sub BrowseBaseEarthSurfaceCache()

        'Open-File-Dialog anzeigen
        Dim dlg As New OpenFileDialog With {
            .Title = "EarthSurface Cache auswählen",
            .Filter = "EarthSurface Cache Meta (*.meta.json)|*.meta.json",
            .InitialDirectory = DataEarthPaths.CacheDirectory,
            .Multiselect = False,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() Then

            Dim metaPath As String

            If File.Exists(dlg.FileName) Then
                metaPath = dlg.FileName
            Else
                MessageBox.Show("Datei nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
                Return
            End If

            BaseEarthSurfaceCachePath = metaPath

            'Meta einlesen
            Dim metaJson As String = File.ReadAllText(metaPath, Encoding.UTF8)
            Dim meta As EarthSurfaceCacheMeta
            If Not String.IsNullOrWhiteSpace(metaJson) Then
                meta = JsonSerializer.Deserialize(Of EarthSurfaceCacheMeta)(metaJson, ConfigStore.JsonOptions)
            Else
                MessageBox.Show("Die EarthSurface-Cache Meta-Datei konnte nicht geöffnet werden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
                Return
            End If


            'Zielauflösung setzen
            If meta.CacheType = CacheType.EarthSurface Then

                If meta.CellSizeDeg >= 0 AndAlso meta.LatCount >= 0 AndAlso meta.LonCount >= 0 Then
                    EarthSurfaceCacheMeta = meta
                    OnPropertyChanged(NameOf(TargetCellSizeText))

                    OnPropertyChanged(NameOf(TargetResolutionText))
                    OnPropertyChanged(NameOf(CanGenerateCache))
                Else
                    MessageBox.Show("Der ausgewählte EarthSurface-Cache hat keine gültigen Rasterdimensionen.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
                End If
            Else
                MessageBox.Show("Die ausgewählte Datei ist kein EarthSurface-Cache.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If
    End Sub

    Private Sub ClearBaseEarthSurfaceCache()

        BaseEarthSurfaceCachePath = Nothing
        EarthSurfaceCacheMeta = Nothing

        OnPropertyChanged(NameOf(TargetCellSizeText))
        OnPropertyChanged(NameOf(TargetResolutionText))

        LastReport = "EarthSurface-Cache entfernt."
    End Sub

    Private Sub BrowseClassTif()
        Dim dlg As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff",
            .Title = "Copernicus Class-map GeoTIFF auswählen",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .Multiselect = False,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            If File.Exists(dlg.FileName) Then
                RawClassTifFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If

        End If
    End Sub

    Private Sub ClearClassTifFile()
        RawClassTifFile = Nothing
    End Sub

    Private Sub BrowseProbaTif()
        Dim dlg As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff",
            .Title = "Copernicus Proba-map GeoTIFF auswählen",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .Multiselect = False,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            If File.Exists(dlg.FileName) Then
                RawProbaTifFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If

        End If
    End Sub

    Private Sub ClearProbaTifFile()
        RawProbaTifFile = Nothing
    End Sub

    Private Sub BrowseBedMachineGreenland()
        LastReport = "TODO: BrowseBedMachineGreenland"
    End Sub

    Private Sub ClearBedMachineGreenlandFile()
        RawBedMachineGreenlandFile = Nothing
    End Sub

    Private Sub BrowseBedMachineAntarctica()
        LastReport = "TODO: BrowseBedMachineAntarctica"
    End Sub

    Private Sub ClearBedMachineAntarcticaFile()
        RawBedMachineAntarcticaFile = Nothing
    End Sub

#End Region

#Region "Command Handler - Pan/Zoom/Mouse (Behavior Requests)"

    ' --- Panning ---
    Private _isPanning As Boolean
    Private _panStartMouse As Point
    Private _panStartX As Double
    Private _panStartY As Double

    Private Structure CellHit
        Public LatIdx As Integer
        Public LonIdx As Integer
        Public Index As Integer
        Public Lat As Double
        Public Lon As Double
    End Structure

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

    Private Sub BeginPan(r As PanRequest)
        _isPanning = True
        _panStartMouse = r.MousePos
        _panStartX = PanX
        _panStartY = PanY
        ShowHoverOverlay = False
    End Sub

    Private Sub Pan(r As PanRequest)
        If Not _isPanning Then Return

        Dim dx As Double = r.MousePos.X - _panStartMouse.X
        Dim dy As Double = r.MousePos.Y - _panStartMouse.Y

        PanX = _panStartX + dx
        PanY = _panStartY + dy

        ClampPan(r.ViewPortSize.Width, r.ViewPortSize.Height)
    End Sub

    Private Sub EndPan(arg As Object)
        _isPanning = False
    End Sub

    ' --- Zoom ---
    Private Sub ZoomMap(z As ZoomRequest)
        If LoadedLandCoverCache Is Nothing Then Return

        Const minZoom As Double = 0.25
        Const maxZoom As Double = 20.0

        Dim oldZoom As Double = Zoom

        'Dynamischer Zoom-Faktor, damit man sich nicht "totscrollt"
        Dim zoomFactor As Double = If(z.Delta > 0, 1.1, 1 / 1.1)
        Dim rawZoom As Double = Clamp(oldZoom * zoomFactor, minZoom, maxZoom)

        'Snap auf "runde" Werte (in Scrollrichtung)
        Dim newZoom As Double = SnapZoom(rawZoom, z.Delta, minZoom, maxZoom)

        If Math.Abs(newZoom - oldZoom) < 0.0000001 Then Return

        'Cursor in Content Space ermitteln (vor Zoom)
        Dim cx As Double = (z.MousePos.X - PanX) / oldZoom
        Dim cy As Double = (z.MousePos.Y - PanY) / oldZoom

        Zoom = newZoom

        'Pan so korrigieren, dass (cx,cy) unter Cursor bleibt
        PanX = z.MousePos.X - cx * newZoom
        PanY = z.MousePos.Y - cy * newZoom

        ClampPan(z.ViewPortSize.Width, z.ViewPortSize.Height)

        'Anzeige: wenn Zoom=1.0 -> 100%
        StatusZoomText = $"Zoom: {Zoom * 100:0.##}%"
    End Sub

    Private Sub ViewportChanged(r As ViewportChangedRequest)
        If r Is Nothing Then Return

        _lastViewportW = r.ViewPortSize.Width
        _lastViewportH = r.ViewPortSize.Height

        If LoadedLandCoverCache Is Nothing Then Return

        If _pendingFitToViewport Then
            FitToViewport(_lastViewportW, _lastViewportH)
            _pendingFitToViewport = False
        Else
            'bei Resize nur clampen/zentrieren
            ClampPan(_lastViewportW, _lastViewportH)
        End If
    End Sub

    ' --- Mouse / Hover ---
    Private Sub MapMouseDown(r As MapMouseDownRequest)
        ' Kein Editor im LandCoverWindow -> aktuell no-op.
        ' (Ctrl/Alt wird vom Behavior als "Edit-Click" klassifiziert, kann später z.B. "Pin Observer" werden.)
    End Sub

    Private Sub MapMouseUp(r As MapMouseUpRequest)
        ' no-op
    End Sub

    Private Sub MapMouseMove(r As MapMouseMoveRequest)
        ' Hier kommt später die "wichtigste" Logik:
        ' ScreenMousePos -> ContentPixel -> CacheCell -> Klasse/Confidence/IceThickness lesen

        If LoadedLandCoverCache Is Nothing Then
            MapMouseLeave()
            Return
        End If

        If r Is Nothing Then Return

        RememberViewportSize(r.ViewPortSize)

        Dim hit As CellHit
        If Not TryHitCell(r.MousePos, r.ViewPortSize, hit) Then
            MapMouseLeave()
            Return
        End If

        ' Screen-space Overlay positionieren
        HoverOverlayX = r.MousePos.X + 14
        HoverOverlayY = r.MousePos.Y + 14

        'TODO: Nur True setzen, wenn ein Layer gerendert ist
        If LandCoverLayer IsNot Nothing Then ShowHoverOverlay = True

        ' Platzhaltertext
        HoverOverlayText = "LC: (TODO)" & Environment.NewLine &
                          "Conf: (TODO)" & Environment.NewLine &
                          "Ice: (TODO)"

        ' Statusbar (optional synchron)
        UpdateStatusUi(hit)
    End Sub

    Private Sub MapMouseLeave()
        ShowHoverOverlay = False
        HoverOverlayText = ""
    End Sub


    Private Shared Function SnapZoom(value As Double, wheelDelta As Integer, minZoom As Double, maxZoom As Double) As Double

        value = Clamp(value, minZoom, maxZoom)

        Dim stepSize As Double = GetZoomStepSize(value)

        If wheelDelta > 0 Then
            'hoch -> nächster Wert >= value
            Return Clamp(Math.Ceiling(value / stepSize) * stepSize, minZoom, maxZoom)
        ElseIf wheelDelta < 0 Then
            'runter -> nächster Wert <= value
            Return Clamp(Math.Floor(value / stepSize) * stepSize, minZoom, maxZoom)
        Else
            Return value
        End If
    End Function

    Private Shared Function GetZoomStepSize(z As Double) As Double
        'Schrittweite je nach Zoom-Bereich (fühlt sich "dynamisch" an)
        If z < 0.75 Then Return 0.05
        If z < 1.5 Then Return 0.1
        If z < 3.0 Then Return 0.25
        If z < 8.0 Then Return 0.5
        Return 1.0
    End Function
#End Region

#Region "Rendering"

    Private _renderCts As Threading.CancellationTokenSource

    Private NotInheritable Class LandCoverPreviewRenderResult
        Public Property Width As Integer
        Public Property Height As Integer

        Public Property LandCover As ImageSource
        Public Property Confidence As ImageSource
        Public Property LandIceThickness As ImageSource
    End Class

    Private Async Function RenderPreviewFromCacheAsync() As Task

        If LoadedLandCoverCache Is Nothing Then
            LandCoverLayer = Nothing
            ConfidenceOverlay = Nothing
            LandIceThicknessOverlay = Nothing
            Return
        End If

        'Falls ein Render noch läuft: abbrechen
        Try
            _renderCts?.Cancel()
        Catch
        End Try

        _renderCts = New Threading.CancellationTokenSource
        Dim token As CancellationToken = _renderCts.Token

        Dim cache As LandCoverCache = LoadedLandCoverCache

        Try

            Dim renderResult As LandCoverPreviewRenderResult =
                Await BusyRunner.RunAsync(Of LandCoverPreviewRenderResult)(
                    Me,
                    "LandCover: Preview rendern",
                    Function(progress, ct)

                        'kombiniere BusyRunner-CT und eigenes
                        token.ThrowIfCancellationRequested()
                        ct.ThrowIfCancellationRequested()

                        Dim w As Integer = cache.Meta.LonCount
                        Dim h As Integer = cache.Meta.LatCount

                        progress?.Report(New ProgressInfo("LandCover-Layer rendern...", 0))
                        Dim lcBmp As WriteableBitmap = LandCoverRenderer.RenderLandCoverLayer(cache)
                        lcBmp.Freeze()

                        token.ThrowIfCancellationRequested()
                        ct.ThrowIfCancellationRequested()

                        Dim conf As ImageSource = Nothing
                        If cache.Meta.HasConfidence AndAlso cache.Confidence IsNot Nothing Then
                            progress?.Report(New ProgressInfo("Confidence-Overlay rendern...", 45))
                            Dim confBmp = LandCoverRenderer.RenderConfidenceOverlay(cache)
                            confBmp.Freeze()

                            conf = confBmp
                        End If

                        token.ThrowIfCancellationRequested()
                        ct.ThrowIfCancellationRequested()

                        Dim ice As ImageSource = Nothing
                        If cache.Meta.HasLandIceThickness AndAlso cache.LandIceThicknessM IsNot Nothing Then
                            progress?.Report(New ProgressInfo("LandIceThickness-Overlay rendern...", 75))
                            'TODO: LandIceThicknessRenderer implementieren
                        End If

                        progress?.Report(New ProgressInfo("Fertig.", 100))

                        Return New LandCoverPreviewRenderResult With {
                            .Width = w,
                            .Height = h,
                            .LandCover = lcBmp,
                            .Confidence = conf,
                            .LandIceThickness = ice
                        }

                    End Function,
                    canCancel:=True,
                    showOverlay:=True)

            'Falls zwischendurch ein anderer Cache geladen wurde Ergebnis verwerfen
            If Not Object.ReferenceEquals(cache, LoadedLandCoverCache) Then Return

            'UI-Thread: VM befüllen
            LandCoverLayer = renderResult.LandCover
            ConfidenceOverlay = renderResult.Confidence
            LandIceThicknessOverlay = renderResult.LandIceThickness

            ContentWidth = renderResult.Width
            ContentHeight = renderResult.Height

            'Fit/Viewport
            If _lastViewportW > 0 AndAlso _lastViewportH > 0 Then
                FitToViewport(_lastViewportW, _lastViewportH)
                _pendingFitToViewport = False
            Else
                _pendingFitToViewport = True
                Zoom = 1.0
                PanX = 0
                PanY = 0
            End If

            'GlobePreview-Button aktivieren
            OnPropertyChanged(NameOf(CanOpenGlobePreview))

        Catch ex As OperationCanceledException
            'Abbruch ignorieren

        Catch ex As Exception
            LastReport = $"Fehler beim Rendern: {ex.Message}"
        End Try

    End Function


#End Region

#Region "Helper"

    Private Sub ClampPan(viewportW As Double, viewportH As Double)

        If LoadedLandCoverCache Is Nothing Then Return

        Dim contentW As Double = LoadedLandCoverCache.Meta.LonCount
        Dim contentH As Double = LoadedLandCoverCache.Meta.LatCount

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

        If LoadedLandCoverCache Is Nothing Then Return
        If viewPortH <= 0 OrElse viewPortW <= 0 Then Return

        Dim contentW As Double = LoadedLandCoverCache.Meta.LonCount
        Dim contentH As Double = LoadedLandCoverCache.Meta.LatCount
        If contentW <= 0 OrElse contentH <= 0 Then Return

        Dim fitZoom As Double = Math.Min(viewPortW / contentW, viewPortH / contentH)

        'Optional: nicht größer als 1 hochskalieren
        fitZoom = Math.Min(fitZoom, 1.0)

        Dim z As Double = Math.Floor(fitZoom * 100) / 100.0
        Zoom = Clamp(z, 0.05, 20.0)

        'Zentrieren
        PanX = (viewPortW - contentW * Zoom) / 2.0
        PanY = (viewPortH - contentH * Zoom) / 2.0

        'Sicherheit
        ClampPan(viewPortW, viewPortH)

    End Sub

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
        lon = Wrap180(lon)

        Return (lat, lon)

    End Function

    Private Function TryHitCell(mousePos As Point, viewport As Size, ByRef hit As CellHit) As Boolean

        hit = Nothing

        If LoadedLandCoverCache Is Nothing OrElse LoadedLandCoverCache.Meta Is Nothing Then Return False

        Dim meta As LandCoverCacheMeta = LoadedLandCoverCache.Meta
        Dim contentSize As New Size(meta.LonCount, meta.LatCount)

        Dim geo = ScreenToGeo(mousePos, viewport, contentSize, Camera, Zoom, PanX, PanY)
        If Double.IsNaN(geo.Lat) OrElse Double.IsNaN(geo.Lon) Then Return False

        Dim cell As Double = meta.CellSizeDeg

        Dim latIdx As Integer = CInt(Math.Floor((90.0 - geo.Lat) / cell))
        latIdx = Clamp(latIdx, 0, meta.LatCount - 1)

        Dim lonIdx As Integer = CInt(Math.Floor((geo.Lon + 180.0) / cell))
        lonIdx = Clamp(lonIdx, 0, meta.LonCount - 1)

        Dim idx As Integer = latIdx * meta.LonCount + lonIdx

        hit = New CellHit With {
            .LatIdx = latIdx,
            .LonIdx = lonIdx,
            .Index = idx,
            .Lat = geo.Lat,
            .Lon = geo.Lon
        }

        Return True
    End Function

    Private Sub RememberViewportSize(vp As Size)
        If vp.Width > 0 Then _lastViewportW = vp.Width
        If vp.Height > 0 Then _lastViewportH = vp.Height
    End Sub

#End Region
End Class
