
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports Microsoft.Win32
Imports OSGeo.GDAL
Imports MaxRev.Gdal

Public Class LandCoverViewModel
    Inherits ViewModelBase

#Region "Konstruktor"

    Public Sub New()

        SourceName = "COPERNICUS_LC100"
        EpochYear = DateTime.UtcNow.Year

        ' Defaults Layer
        _showGridLayer = False
        _showConfidenceOverlay = False
        _showLandIceThicknessOverlay = False

        ' Defaults Pan/Zoom
        _zoom = 1.0
        PanPoint = New Point(0, 0)

        ' Defaults Hover
        _showHoverOverlay = False
        _hoverOverlayText = ""
        HoverOverlayPoint = New Point(0, 0)

        ' Defaults Statusbar
        ClearStatusBar()

        ' Commands (Menu/Actions)
        LoadCacheCommand = New AsyncRelayCommand(Of Object)(Function(o) LoadCacheAsync(), Function(o) Not IsBusy)
        GenerateCacheCommand = New AsyncRelayCommand(Of Object)(Function(o) GenerateCacheAsync(), Function(o) CanGenerateCache AndAlso Not IsBusy)
        OpenGlobePreviewCommand = New RelayCommand(Of Object)(Sub(o) OpenGlobePreview(), Function(o) CanOpenGlobePreview AndAlso Not IsBusy)

        ' Commands (Browse/Clear)
        BrowseBaseEarthSurfaceCacheCommand = New RelayCommand(Of Object)(Sub(o) BrowseBaseEarthSurfaceCache(), Function(o) Not IsBusy)
        ClearBaseEarthSurfaceCacheCommand = New RelayCommand(Of Object)(Sub(o) ClearBaseEarthSurfaceCache(), Function(o) Not String.IsNullOrWhiteSpace(BaseEarthSurfaceCachePath))

        BrowseCopernicusClassCommand = New RelayCommand(Of Object)(Sub(o) BrowseCopernicusClass(), Function(o) Not IsBusy)
        ClearCopernicusClassFileCommand = New RelayCommand(Of Object)(Sub(o) ClearCopernicusClassFile(), Function(o) RawCopernicusClassFile IsNot Nothing)

        BrowseCopernicusProbaCommand = New RelayCommand(Of Object)(Sub(o) BrowseCopernicusProba(), Function(o) Not IsBusy)
        ClearCopernicusProbaFileCommand = New RelayCommand(Of Object)(Sub(o) ClearCopernicusProbaFile(), Function(o) RawCopernicusProbaFile IsNot Nothing)

        BrowseBedMachineGreenlandCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineGreenland(), Function(o) Not IsBusy)
        ClearBedMachineGreenlandFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineGreenlandFile(), Function(o) Not String.IsNullOrWhiteSpace(RawBedMachineGreenlandFile))

        BrowseBedMachineAntarcticaCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineAntarctica(), Function(o) Not IsBusy)
        ClearBedMachineAntarcticaFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineAntarcticaFile(), Function(o) Not String.IsNullOrWhiteSpace(RawBedMachineAntarcticaFile))

        BrowseRgiGlobalCommand = New RelayCommand(Of Object)(Sub(o) BrowseRgiGlobal(), Function(o) Not IsBusy)
        ClearRgiGlobalFileCommand = New RelayCommand(Of Object)(Sub(o) ClearRgiGlobalFile(), Function(o) Not String.IsNullOrWhiteSpace(RawRgiGlobalFile))

        BrowseRgiRegionsCommand = New RelayCommand(Of Object)(Sub(o) BrowseRgiRegions(), Function(o) Not IsBusy)
        ClearRgiRegionsFileCommand = New RelayCommand(Of Object)(Sub(o) ClearRgiRegionsFile(), Function(o) Not String.IsNullOrWhiteSpace(RawRgiRegionsFile))

        BrowseGlaThiDaCommand = New RelayCommand(Of Object)(Sub(o) BrowseGlaThiDa(), Function(o) Not IsBusy)
        ClearGlaThiDaFileCommand = New RelayCommand(Of Object)(Sub(o) ClearGlaThiDaFile(), Function(o) Not String.IsNullOrWhiteSpace(RawGlaThiDaFile))



        ' Commands (Pan/Zoom/Mouse; exakt passend zum Behavior)
        BeginPanCommand = New RelayCommand(Of PanRequest)(AddressOf BeginPan, Function(r) Not IsBusy)
        PanCommand = New RelayCommand(Of PanRequest)(AddressOf Pan, Function(r) Not IsBusy)
        EndPanCommand = New RelayCommand(Of Object)(AddressOf EndPan, Function(o) Not IsBusy)
        ZoomCommand = New RelayCommand(Of ZoomRequest)(AddressOf ZoomMap, Function(r) Not IsBusy)
        ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(AddressOf ViewportChanged, Function(r) Not IsBusy)

        MapMouseDownCommand = New RelayCommand(Of MapMouseDownRequest)(AddressOf MapMouseDown, Function(r) Not IsBusy)
        MapMouseUpCommand = New RelayCommand(Of MapMouseUpRequest)(AddressOf MapMouseUp, Function(r) Not IsBusy)
        MapMouseMoveCommand = New RelayCommand(Of MapMouseMoveRequest)(AddressOf MapMouseMove, Function(r) Not IsBusy)

        MapMouseLeaveCommand = New RelayCommand(Of Object)(Sub(o) MapMouseLeave(), Function(o) True)

        ' Report
        LastReport = ""
    End Sub

#End Region

#Region "Commands - public (Bindings)"

    Public ReadOnly Property LoadCacheCommand As ICommand
    Public ReadOnly Property GenerateCacheCommand As ICommand

    Public ReadOnly Property OpenGlobePreviewCommand As ICommand

    Public ReadOnly Property BrowseBaseEarthSurfaceCacheCommand As ICommand
    Public ReadOnly Property ClearBaseEarthSurfaceCacheCommand As ICommand
    Public ReadOnly Property BrowseCopernicusClassCommand As ICommand
    Public ReadOnly Property ClearCopernicusClassFileCommand As ICommand
    Public ReadOnly Property BrowseCopernicusProbaCommand As ICommand
    Public ReadOnly Property ClearCopernicusProbaFileCommand As ICommand

    Public ReadOnly Property BrowseBedMachineGreenlandCommand As ICommand
    Public ReadOnly Property ClearBedMachineGreenlandFileCommand As ICommand
    Public ReadOnly Property BrowseBedMachineAntarcticaCommand As ICommand
    Public ReadOnly Property ClearBedMachineAntarcticaFileCommand As ICommand

    Public ReadOnly Property BrowseRgiGlobalCommand As ICommand
    Public ReadOnly Property ClearRgiGlobalFileCommand As ICommand
    Public ReadOnly Property BrowseRgiRegionsCommand As ICommand
    Public ReadOnly Property ClearRgiRegionsFileCommand As ICommand

    Public ReadOnly Property BrowseGlaThiDaCommand As ICommand
    Public ReadOnly Property ClearGlaThiDaFileCommand As ICommand


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

    Private _rawCopernicusClassFile As String
    Public Property RawCopernicusClassFile As String
        Get
            Return _rawCopernicusClassFile
        End Get
        Set(value As String)
            If SetProperty(_rawCopernicusClassFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawCopernicusProbaFile As String
    Public Property RawCopernicusProbaFile As String
        Get
            Return _rawCopernicusProbaFile
        End Get
        Set(value As String)
            If SetProperty(_rawCopernicusProbaFile, value) Then
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
            If SetProperty(_rawBedMachineGreenlandFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawBedMachineAntarcticaFile As String
    Public Property RawBedMachineAntarcticaFile As String
        Get
            Return _rawBedMachineAntarcticaFile
        End Get
        Set(value As String)
            If SetProperty(_rawBedMachineAntarcticaFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawRgiGlobalFile As String
    Public Property RawRgiGlobalFile As String
        Get
            Return _rawRgiGlobalFile
        End Get
        Set(value As String)
            If SetProperty(_rawRgiGlobalFile, value) Then
                OnPropertyChanged(NameOf(HasRgiGlobalFile))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Public ReadOnly Property HasRgiGlobalFile As Boolean
        Get
            Return RawRgiGlobalFile IsNot Nothing
        End Get
    End Property

    Private _rawRgiRegionsFile As String
    Public Property RawRgiRegionsFile As String
        Get
            Return _rawRgiRegionsFile
        End Get
        Set(value As String)
            If SetProperty(_rawRgiRegionsFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _rawGlaThiDaDaFile As String
    Public Property RawGlaThiDaFile As String
        Get
            Return _rawGlaThiDaDaFile
        End Get
        Set(value As String)
            If SetProperty(_rawGlaThiDaDaFile, value) Then
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
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
                Return EarthSurfaceCacheMeta.CellSizeDeg > 0 AndAlso RawCopernicusClassFile IsNot Nothing
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

#Region "Pan/Zoom + Content Size"

    Private _lastViewportSize As Size
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

    Private _panPoint As Point
    Public Property PanPoint As Point
        Get
            Return _panPoint
        End Get
        Set(value As Point)
            SetProperty(_panPoint, value)
        End Set
    End Property

    Private _contentSize As Size
    Public Property ContentSize As Size
        Get
            Return _contentSize
        End Get
        Set(value As Size)
            SetProperty(_contentSize, value)
        End Set
    End Property

    Private _contentCellSizeDeg As Double
    Public Property ContentCellSizeDeg As Double
        Get
            Return _contentCellSizeDeg
        End Get
        Set(value As Double)
            SetProperty(_contentCellSizeDeg, value)
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

    Private _hoverOverlayPoint As Point
    Public Property HoverOverlayPoint As Point
        Get
            Return _hoverOverlayPoint
        End Get
        Set(value As Point)
            SetProperty(_hoverOverlayPoint, value)
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

    Private Sub UpdateHoverOverlay(hit As EquiRectangularViewportHelper.CellHit)

        Dim idx As Integer = hit.Index

        If hit.Index = Nothing Then Return

        Dim clsV As Byte
        Dim confV As Byte
        Dim iceV As Single

        Dim sb As New StringBuilder()

        If LoadedLandCoverCache.LandCoverClass IsNot Nothing AndAlso idx >= 0 Then

            If idx < LoadedLandCoverCache.LandCoverClass.Length Then
                clsV = LoadedLandCoverCache.LandCoverClass(idx)

                Dim landclass As LandCoverClass = CType(clsV, LandCoverClass)
                If landclass <> Nothing Then
                    sb.AppendLine($"LC: {LandCoverSchema.GetDisplayName(landclass)}")
                Else
                    sb.AppendLine("LC: -")
                End If
            End If

            If LoadedLandCoverCache.Meta.HasConfidence Then
                If idx < LoadedLandCoverCache.Confidence.Length Then
                    confV = LoadedLandCoverCache.Confidence(idx)
                    If confV <> Nothing AndAlso Not confV = 255 Then
                        sb.AppendLine($"Conf: {confV:N0}%")
                    Else
                        sb.AppendLine("Conf: -")
                    End If
                End If
            End If

            If LoadedLandCoverCache.Meta.HasLandIceThickness Then
                If idx < LoadedLandCoverCache.LandIceThicknessM.Length Then
                    iceV = LoadedLandCoverCache.LandIceThicknessM(idx)
                    If Not Single.IsNaN(iceV) AndAlso Not Single.IsInfinity(iceV) Then
                        sb.AppendLine($"Ice: {iceV:0.00}m")
                    ElseIf Single.IsNaN(iceV) Then
                        sb.AppendLine("Ice: -")
                    End If
                End If
            End If
        End If

        HoverOverlayText = sb.ToString().TrimEnd()

    End Sub

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

    Private Sub UpdateStatusBar(hit As EquiRectangularViewportHelper.CellHit)

        Dim idx As Integer = hit.Index

        StatusLatText = $"Lat: {hit.Lat:0.00}°"
        StatusLonText = $"Lon: {hit.Lon:0.00}°"

        Dim clsV As Byte
        Dim confV As Byte
        Dim iceV As Single

        If LoadedLandCoverCache.LandCoverClass IsNot Nothing AndAlso idx >= 0 Then

            If idx < LoadedLandCoverCache.LandCoverClass.Length Then
                clsV = LoadedLandCoverCache.LandCoverClass(idx)

                Dim landclass As LandCoverClass = CType(clsV, LandCoverClass)
                If landclass <> Nothing Then
                    StatusLandCoverClassText = $"LC: {LandCoverSchema.GetDisplayName(landclass)}"
                Else
                    StatusLandCoverClassText = "LC: -"
                End If
            End If

            If LoadedLandCoverCache.Meta.HasConfidence Then
                If idx < LoadedLandCoverCache.Confidence.Length Then
                    confV = LoadedLandCoverCache.Confidence(idx)
                    If confV <> Nothing AndAlso Not confV = 255 Then
                        StatusConfidenceText = $"Conf: {confV:N0}%"
                    Else
                        StatusConfidenceText = "Conf: -"
                    End If
                End If
            End If

            If LoadedLandCoverCache.Meta.HasLandIceThickness Then
                If idx < LoadedLandCoverCache.LandIceThicknessM.Length Then
                    iceV = LoadedLandCoverCache.LandIceThicknessM(idx)
                    If Not Single.IsNaN(iceV) AndAlso Not Single.IsInfinity(iceV) Then
                        StatusLandIceThicknessText = $"Ice: {iceV:0.00}m"
                    ElseIf Single.IsNaN(iceV) Then
                        StatusLandIceThicknessText = "Ice: -"
                    End If
                End If
            End If
        End If

    End Sub

    Private Sub ClearStatusBar()
        StatusLatText = "Lat: --.--"
        StatusLonText = "Lon: --.--"
        StatusLandCoverClassText = "LC: -"
        StatusConfidenceText = "Conf: -"
        StatusLandIceThicknessText = "Ice: -"
        StatusZoomText = $"Zoom: {Zoom:0.###}x"
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
            SourceName = openedCache.Meta.Source
            EpochYear = openedCache.Meta.EpochYear
            BaseEarthSurfaceCachePath = openedCache.Meta.EarthSurfaceRef
            RawCopernicusClassFile = openedCache.Meta.RawCopernicusLC100ClassFile
            RawCopernicusProbaFile = openedCache.Meta.RawCopernicusLC100ProbaFile


            'Cache merken
            'SetLoadedCachePathsFromMetaPath(metaPath)
            LoadedLandCoverCache = openedCache

            LastReport = $"Cache geladen: {Path.GetFileName(Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), Nothing))}"

            'Referenz-EarthSurfaceCache prüfen und Meta laden
            Dim EarthSurfaceEk As CacheOpenErrorKind
            Dim EarthSurfaceEm As String = Nothing
            Dim HasEarthSurfaceReference As Boolean = EarthSurfaceCacheStore.TryOpenMetaFromFile(openedCache.Meta.EarthSurfaceRef, EarthSurfaceCacheMeta, EarthSurfaceEk, EarthSurfaceEm, True)

            If Not HasEarthSurfaceReference Then
                'Wenn Referenz-Cache nicht geladen werden konnte: Fehlermeldung anzeigen
                LastReport = LastReport & Environment.NewLine & $"Referenz-EarthSurface-Cache konnte nicht geladen werden: {EarthSurfaceEk} - {EarthSurfaceEm}"
            Else
                'Sonst Target-Texte aktualisieren
                OnPropertyChanged(NameOf(TargetResolutionText))
                OnPropertyChanged(NameOf(TargetCellSizeText))
            End If

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

                        Dim opts As New LandCoverCacheBuilder.BuildOptions With {
                            .SourceName = SourceName,
                            .EpochYear = EpochYear,
                            .RawCopernicusLc100ClassTifPath = RawCopernicusClassFile,
                            .RawCopernicusLc100ProbaTifPath = RawCopernicusProbaFile,
                            .RawBedMachineGreenlandNcPath = If(RawBedMachineGreenlandFile, Nothing),
                            .RawBedMachineAntarcticaNcPath = If(RawBedMachineAntarcticaFile, Nothing),
                            .TargetCellSizeDeg = EarthSurfaceCacheMeta.CellSizeDeg,
                            .EarthSurfaceReference = BaseEarthSurfaceCachePath,
                            .EarthSurfaceCreateUTC = EarthSurfaceCacheMeta.CreateUtc
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

    Private Sub BrowseCopernicusClass()
        Dim dlg As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff",
            .Title = "Copernicus Class-map GeoTIFF auswählen",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .Multiselect = False,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawCopernicusClassFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If

        End If
    End Sub

    Private Sub ClearCopernicusClassFile()
        RawCopernicusClassFile = Nothing
    End Sub

    Private Sub BrowseCopernicusProba()
        Dim dlg As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff",
            .Title = "Copernicus Proba-map GeoTIFF auswählen",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .Multiselect = False,
            .CheckFileExists = True
        }

        If dlg.ShowDialog() = True Then
            If File.Exists(dlg.FileName) Then
                RawCopernicusProbaFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If

        End If
    End Sub

    Private Sub ClearCopernicusProbaFile()
        RawCopernicusProbaFile = Nothing
    End Sub

    Private Sub BrowseBedMachineGreenland()

        Dim dlg As New OpenFileDialog With {
            .Title = "BedMachine Greenland auswählen",
            .Filter = "netCDF (*.nc)|*.nc",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawBedMachineGreenlandFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If

    End Sub

    Private Sub ClearBedMachineGreenlandFile()
        RawBedMachineGreenlandFile = Nothing
    End Sub

    Private Sub BrowseBedMachineAntarctica()

        Dim dlg As New OpenFileDialog With {
            .Title = "BedMachine Antarctica auswählen",
            .Filter = "netCDF (*.nc)|*.nc",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawBedMachineAntarcticaFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If

    End Sub

    Private Sub ClearBedMachineAntarcticaFile()
        RawBedMachineAntarcticaFile = Nothing
    End Sub

    Private Sub BrowseRgiGlobal()

        Dim dlg As New OpenFileDialog With {
            .Title = "RGI Global-Glacier-Produkt auswählen",
            .Filter = "ZIP (*.zip)|*.zip",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawRgiGlobalFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If

    End Sub

    Private Sub ClearRgiGlobalFile()
        RawRgiGlobalFile = Nothing
    End Sub

    Private Sub BrowseRgiRegions()

        Dim dlg As New OpenFileDialog With {
            .Title = "RGI Regionen-Produkt auswählen",
            .Filter = "ZIP (*.zip)|*.zip",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawRgiRegionsFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If

    End Sub

    Private Sub ClearRgiRegionsFile()
        RawRgiRegionsFile = Nothing
    End Sub

    Private Async Sub BrowseGlaThiDa()

        Dim dlg As New OpenFileDialog With {
            .Title = "GlaThiDa auswählen",
            .Filter = "ZIP (*.zip)|*.zip",
            .InitialDirectory = DataEarthPaths.RawDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() Then
            If File.Exists(dlg.FileName) Then
                RawGlaThiDaFile = dlg.FileName
                OnPropertyChanged(NameOf(CanGenerateCache))
            Else
                MessageBox.Show("Datei nicht gefunden.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            End If
        End If


        Dim opts As New LandCoverCacheBuilder.BuildOptions With {
              .RawRgiRegionsZipPath = RawRgiRegionsFile,
              .RawGlaThiDaZipPath = RawGlaThiDaFile
            }

        'Dim resultRgiRegion As RgiRegionProcessResult = Await BusyRunner.RunAsync(Of RgiRegionProcessResult)(
        '                                                                    Me,
        '                                                                    "RGI Region",
        '                                                                    Function(progress, ct)

        '                                                                        Return RgiRegionProcessor.Process(opts, progress, ct)

        '                                                                    End Function)


        'LastReport = resultRgiRegion.Report

        Dim resultGlathida As GlaThiDaProcessResult = Await BusyRunner.RunAsync(Of GlaThiDaProcessResult)(
                                                                        Me,
                                                                        "GlaThiDa",
                                                                        Function(progress, ct)

                                                                            Return GlaThiDaProcessor.Process(opts, progress, ct)

                                                                        End Function)

        LastReport = resultGlathida.Report
    End Sub

    Private Sub ClearGlaThiDaFile()
        RawGlaThiDaFile = Nothing
    End Sub

#End Region

#Region "Command Handler - Pan/Zoom/Mouse (Behavior Requests)"

    ' --- Panning ---
    Private _isPanning As Boolean
    Private _panStartMouse As Point
    Private _panStartPoint As Point

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
        _panStartPoint = _panPoint
        ShowHoverOverlay = False
    End Sub

    Private Sub Pan(r As PanRequest)

        If Not _isPanning Then Return

        If LoadedLandCoverCache Is Nothing OrElse LoadedLandCoverCache.Meta Is Nothing Then Return

        Dim dx As Double = r.MousePos.X - _panStartMouse.X
        Dim dy As Double = r.MousePos.Y - _panStartMouse.Y

        Dim p As New Point(_panStartPoint.X + dx, _panStartPoint.Y + dy)

        EquiRectangularViewportHelper.ClampPan(r.ViewPortSize, ContentSize, Zoom, p)

        PanPoint = p

    End Sub

    Private Sub EndPan(arg As Object)
        _isPanning = False
    End Sub

    ' --- Zoom ---
    Private Sub ZoomMap(z As ZoomRequest)

        If LoadedLandCoverCache Is Nothing OrElse LoadedLandCoverCache.Meta Is Nothing Then Return


        Const minZoom As Double = 0.25
        Const maxZoom As Double = 20.0

        Dim oldZoom As Double = Zoom

        'Dynamischer Zoom-Faktor, damit man sich nicht "totscrollt"
        Dim zoomFactor As Double = If(z.Delta > 0, 1.1, 1 / 1.1)
        Dim rawZoom As Double = Clamp(oldZoom * zoomFactor, minZoom, maxZoom)

        'Snap auf "runde" Werte (in Scrollrichtung)
        Dim newZoom As Double = EquiRectangularViewportHelper.SnapZoom(rawZoom, z.Delta, minZoom, maxZoom)

        If Math.Abs(newZoom - oldZoom) < 0.0000001 Then Return

        'Cursor in Content Space ermitteln (vor Zoom)
        Dim cx As Double = (z.MousePos.X - _panPoint.X) / oldZoom
        Dim cy As Double = (z.MousePos.Y - _panPoint.Y) / oldZoom

        Zoom = newZoom

        'Pan so korrigieren, dass (cx,cy) unter Cursor bleibt
        Dim p As New Point(z.MousePos.X - cx * newZoom, z.MousePos.Y - cy * newZoom)

        EquiRectangularViewportHelper.ClampPan(z.ViewPortSize, ContentSize, Zoom, p)
        PanPoint = p

        'Anzeige: wenn Zoom=1.0 -> 100%
        StatusZoomText = $"Zoom: {Zoom * 100:0.##}%"
    End Sub

    Private Sub ViewportChanged(r As ViewportChangedRequest)
        If r Is Nothing Then Return

        _lastViewportSize = r.ViewPortSize

        If LoadedLandCoverCache Is Nothing OrElse LoadedLandCoverCache.Meta Is Nothing Then Return

        If _pendingFitToViewport Then
            EquiRectangularViewportHelper.FitToViewport(_lastViewportSize, ContentSize, Zoom, PanPoint)
            _pendingFitToViewport = False
        Else
            'bei Resize nur clampen/zentrieren
            EquiRectangularViewportHelper.ClampPan(_lastViewportSize, ContentSize, Zoom, PanPoint)
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

        If LoadedLandCoverCache Is Nothing OrElse LoadedLandCoverCache.Meta Is Nothing Then
            MapMouseLeave()
            Return
        End If

        If r Is Nothing Then Return

        EquiRectangularViewportHelper.RememberViewportSize(r.ViewPortSize, _lastViewportSize)


        Dim hit As EquiRectangularViewportHelper.CellHit
        If Not TryHitCell(r.MousePos, r.ViewPortSize, ContentSize, LoadedLandCoverCache.Meta.CellSizeDeg, Camera, Zoom, PanPoint, hit) Then
            MapMouseLeave()
            Return
        End If

        ' Screen-space Overlay positionieren
        HoverOverlayPoint = New Point(r.MousePos.X + 14,
                                      r.MousePos.Y + 14)

        'TODO: Nur True setzen, wenn ein Layer gerendert ist
        If LandCoverLayer IsNot Nothing Then
            ShowHoverOverlay = True
        Else
            ShowHoverOverlay = False
        End If

        UpdateHoverOverlay(hit)
        UpdateStatusBar(hit)
    End Sub

    Private Sub MapMouseLeave()
        ShowHoverOverlay = False
        HoverOverlayText = ""
        ClearStatusBar()
    End Sub


#End Region

#Region "Rendering"

    Private _renderCts As CancellationTokenSource

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
        Dim meta As LandCoverCacheMeta = cache.Meta

        Try

            Dim renderResult As LandCoverPreviewRenderResult =
                Await BusyRunner.RunAsync(Of LandCoverPreviewRenderResult)(
                    Me,
                    "LandCover: Preview rendern",
                    Function(progress, ct)

                        'kombiniere BusyRunner-CT und eigenes
                        token.ThrowIfCancellationRequested()
                        ct.ThrowIfCancellationRequested()

                        Dim w As Integer = meta.LonCount
                        Dim h As Integer = meta.LatCount

                        progress?.Report(New ProgressInfo("LandCover-Layer rendern...", 0))
                        Dim lcBmp As WriteableBitmap = LandCoverRenderer.RenderLandCoverLayer(cache,,, LandCoverSchema.LandCoverColorMode.Realistic)
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

            ContentSize = New Size(renderResult.Width, renderResult.Height)
            ContentCellSizeDeg = LoadedLandCoverCache.Meta.CellSizeDeg

            'Fit/Viewport
            If _lastViewportSize.Width > 0 AndAlso _lastViewportSize.Height > 0 Then
                EquiRectangularViewportHelper.FitToViewport(_lastViewportSize, ContentSize, Zoom, PanPoint)
                _pendingFitToViewport = False
            Else
                _pendingFitToViewport = True
                Zoom = 1.0

                PanPoint = New Point(0, 0)
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


#End Region

End Class
