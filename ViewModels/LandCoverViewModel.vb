Imports System.Windows.Input
Imports System.Windows.Media

Public Class LandCoverViewModel
    Inherits ViewModelBase

#Region "Konstruktor"

    Public Sub New()

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
        _ClearBaseEarthSurfaceCacheCommand = New RelayCommand(Of Object)(Sub(o) ClearBaseEarthSurfaceCache(), Function(o) HasBaseEarthSurfaceCachePath AndAlso Not IsBusy)

        _BrowseClassTifCommand = New RelayCommand(Of Object)(Sub(o) BrowseClassTif(), Function(o) Not IsBusy)
        _ClearClassTifFileCommand = New RelayCommand(Of Object)(Sub(o) ClearClassTifFile(), Function(o) HasClassTif AndAlso Not IsBusy)

        _BrowseProbaTifCommand = New RelayCommand(Of Object)(Sub(o) BrowseProbaTif(), Function(o) Not IsBusy)
        _ClearProbaTifFileCommand = New RelayCommand(Of Object)(Sub(o) ClearProbaTifFile(), Function(o) HasProbaTif AndAlso Not IsBusy)

        _BrowseBedMachineGreenlandCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineGreenland(), Function(o) Not IsBusy)
        _ClearBedMachineGreenlandFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineGreenlandFile(), Function(o) HasBedMachineGreenland AndAlso Not IsBusy)

        _BrowseBedMachineAntarcticaCommand = New RelayCommand(Of Object)(Sub(o) BrowseBedMachineAntarctica(), Function(o) Not IsBusy)
        _ClearBedMachineAntarcticaFileCommand = New RelayCommand(Of Object)(Sub(o) ClearBedMachineAntarcticaFile(), Function(o) HasBedMachineAntarctica AndAlso Not IsBusy)

        ' Commands (Pan/Zoom/Mouse; exakt passend zum Behavior)
        _BeginPanCommand = New RelayCommand(Of PanRequest)(AddressOf BeginPan, Function(r) Not IsBusy)
        _panCommand = New RelayCommand(Of PanRequest)(AddressOf Pan, Function(r) Not IsBusy)
        _endPanCommand = New RelayCommand(Of Object)(AddressOf EndPan, Function(o) Not IsBusy)
        _zoomCommand = New RelayCommand(Of ZoomRequest)(AddressOf ZoomMap, Function(r) Not IsBusy)
        _viewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(AddressOf ViewportChanged, Function(r) Not IsBusy)

        _mapMouseDownCommand = New RelayCommand(Of MapMouseDownRequest)(AddressOf MapMouseDown, Function(r) Not IsBusy)
        _mapMouseUpCommand = New RelayCommand(Of MapMouseUpRequest)(AddressOf MapMouseUp, Function(r) Not IsBusy)
        _mapMouseMoveCommand = New RelayCommand(Of MapMouseMoveRequest)(AddressOf MapMouseMove, Function(r) Not IsBusy)

        _mapMouseLeaveCommand = New RelayCommand(Of Object)(AddressOf MapMouseLeave, Function(o) True)

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
                OnPropertyChanged(NameOf(HasClassTif))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Public ReadOnly Property HasClassTif As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(_rawClassTifFile)
        End Get
    End Property

    Private _rawProbaTifFile As String
    Public Property RawProbaTifFile As String
        Get
            Return _rawProbaTifFile
        End Get
        Set(value As String)
            If SetProperty(_rawProbaTifFile, value) Then
                OnPropertyChanged(NameOf(HasProbaTif))
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Public ReadOnly Property HasProbaTif As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(_rawProbaTifFile)
        End Get
    End Property

    Private _rawBedMachineGreenlandFile As String
    Public Property RawBedMachineGreenlandFile As String
        Get
            Return _rawBedMachineGreenlandFile
        End Get
        Set(value As String)
            If SetProperty(_rawBedMachineGreenlandFile, value) Then
                OnPropertyChanged(NameOf(HasBedMachineGreenland))
            End If
        End Set
    End Property

    Public ReadOnly Property HasBedMachineGreenland As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(_rawBedMachineGreenlandFile)
        End Get
    End Property

    Private _rawBedMachineAntarcticaFile As String
    Public Property RawBedMachineAntarcticaFile As String
        Get
            Return _rawBedMachineAntarcticaFile
        End Get
        Set(value As String)
            If SetProperty(_rawBedMachineAntarcticaFile, value) Then
                OnPropertyChanged(NameOf(HasBedMachineAntarctica))
            End If
        End Set
    End Property

    Public ReadOnly Property HasBedMachineAntarctica As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(_rawBedMachineAntarcticaFile)
        End Get
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
            ' minimaler Satz gemäß deiner Vorgabe: Dimensionen kommen aus BaseEarthSurfaceCache
            Return HasBaseEarthSurfaceCachePath AndAlso HasClassTif AndAlso HasProbaTif
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

    ' Für GeoGridOverlay2D (bleibt bewusst als Object, bis wir euren Meta-Typ einhängen)
    Private _cacheMeta As Object
    Public Property CacheMeta As Object
        Get
            Return _cacheMeta
        End Get
        Set(value As Object)
            SetProperty(_cacheMeta, value)
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

#End Region

#Region "Command Handler - Menu/Actions"

    Private Async Function LoadCacheAsync() As Task
        ' TODO: FileDialog + Cache load + Render
        BusyTitle = "Cache laden..."
        BusyMessage = ""
        BusyIsIndeterminate = True
        IsBusy = True
        Try
            LastReport = "TODO: LoadCacheAsync"
        Finally
            IsBusy = False
            BusyIsIndeterminate = False
        End Try
    End Function

    Private Async Function GenerateCacheAsync() As Task
        ' TODO: Build + Render
        BusyTitle = "Cache generieren..."
        BusyMessage = ""
        BusyIsIndeterminate = True
        IsBusy = True
        Try
            LastReport = "TODO: GenerateCacheAsync"
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
        ' TODO: Dialog + BaseEarthSurfaceCachePath setzen
        LastReport = "TODO: BrowseBaseEarthSurfaceCache"
    End Sub

    Private Sub ClearBaseEarthSurfaceCache()
        BaseEarthSurfaceCachePath = Nothing
    End Sub

    Private Sub BrowseClassTif()
        LastReport = "TODO: BrowseClassTif"
    End Sub

    Private Sub ClearClassTifFile()
        RawClassTifFile = Nothing
    End Sub

    Private Sub BrowseProbaTif()
        LastReport = "TODO: BrowseProbaTif"
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
    Private _panStartMouse As Point
    Private _panStartX As Double
    Private _panStartY As Double

    Private Sub BeginPan(req As PanRequest)
        _panStartMouse = req.MousePos
        _panStartX = PanX
        _panStartY = PanY
    End Sub

    Private Sub Pan(req As PanRequest)
        Dim dx = req.MousePos.X - _panStartMouse.X
        Dim dy = req.MousePos.Y - _panStartMouse.Y

        PanX = _panStartX + dx
        PanY = _panStartY + dy
    End Sub

    Private Sub EndPan(arg As Object)
        ' no-op, aber als Hook praktisch
    End Sub

    ' --- Zoom ---
    Private Sub ZoomMap(req As ZoomRequest)
        ' Minimaler Zoom wie im EarthSurfaceWindow üblich:
        ' - Zoom um Mausposition (Screen-Space), PanX/Y korrigieren
        Dim oldZoom = Zoom
        Dim factor As Double = If(req.Delta > 0, 1.2, 1 / 1.2)

        Dim newZoom = oldZoom * factor
        newZoom = Math.Max(0.25, Math.Min(64.0, newZoom))

        If Math.Abs(newZoom - oldZoom) < 0.0000001 Then Return

        ' Mausposition relativ zum Content (vor Transform):
        ' screen = content*zoom + pan  -> content = (screen - pan) / zoom
        Dim contentX = (req.MousePos.X - PanX) / oldZoom
        Dim contentY = (req.MousePos.Y - PanY) / oldZoom

        Zoom = newZoom

        ' Pan so anpassen, dass der content-Punkt unter der Maus bleibt
        PanX = req.MousePos.X - contentX * newZoom
        PanY = req.MousePos.Y - contentY * newZoom

        StatusZoomText = $"Zoom: {Zoom:0.###}x"
    End Sub

    Private Sub ViewportChanged(req As ViewportChangedRequest)
        ' TODO: falls ihr bei Viewport-Change irgendwas invalidiert/cached
    End Sub

    ' --- Mouse / Hover ---
    Private Sub MapMouseDown(req As MapMouseDownRequest)
        ' Kein Editor im LandCoverWindow -> aktuell no-op.
        ' (Ctrl/Alt wird vom Behavior als "Edit-Click" klassifiziert, kann später z.B. "Pin Observer" werden.)
    End Sub

    Private Sub MapMouseUp(req As MapMouseUpRequest)
        ' no-op
    End Sub

    Private Sub MapMouseMove(req As MapMouseMoveRequest)
        ' Hier kommt später die "wichtigste" Logik:
        ' ScreenMousePos -> ContentPixel -> CacheCell -> Klasse/Confidence/IceThickness lesen

        ' Screen-space Overlay positionieren
        HoverOverlayX = req.MousePos.X + 14
        HoverOverlayY = req.MousePos.Y + 14

        'TODO: Nur True setzen, wenn ein Layer gerendert ist
        'ShowHoverOverlay = True

        ' Platzhaltertext
        HoverOverlayText = "LC: (TODO)" & Environment.NewLine &
                          "Conf: (TODO)" & Environment.NewLine &
                          "Ice: (TODO)"

        ' Statusbar (optional synchron)
        StatusLandCoverClassText = "LC: (TODO)"
        StatusConfidenceText = "Conf: (TODO)"
        StatusLandIceThicknessText = "Ice: (TODO)"
    End Sub

    Private Sub MapMouseLeave(arg As Object)
        ShowHoverOverlay = False
        HoverOverlayText = ""
    End Sub

#End Region

End Class
