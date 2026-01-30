Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports Microsoft.Win32

Public Class EarthSurfaceViewModel
    Inherits ViewModelBase

    Protected Overrides Sub OnIsBusyChanged()
        MyBase.OnIsBusyChanged()
        RefreshEditCommandState()
        RaiseEditCommandCanExecuteChanged()
        OnPropertyChanged(NameOf(CanOpenGlobePreview))
    End Sub

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
                HasTidFile = Not String.IsNullOrWhiteSpace(value) AndAlso File.Exists(value)
                OnPropertyChanged(NameOf(CanGenerateCache))
            End If
        End Set
    End Property

    Private _hasTidFile As Boolean = False
    Public Property HasTidFile As Boolean
        Get
            Return _hasTidFile
        End Get
        Set(value As Boolean)
            SetProperty(_hasTidFile, value)
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
            If SetProperty(_loadedCache, value) Then
                OnPropertyChanged(NameOf(CacheMeta))
                OnPropertyChanged(NameOf(CanOpenGlobePreview))

                ResetPerCacheUiState()
                RefreshEditCommandState()
            End If
        End Set
    End Property

    Public ReadOnly Property CacheMeta As EarthSurfaceCacheMeta
        Get
            Return LoadedCache?.Meta
        End Get
    End Property

    Private _loadedMetaPath As String
    Private _loadedBinPath As String

    Private Sub SetLoadedCachePathsFromMetaPath(metaPath As String)
        _loadedMetaPath = metaPath
        _loadedBinPath = If(String.IsNullOrWhiteSpace(metaPath), Nothing,
            Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), "escf"))
    End Sub

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
            Return CellSizeDegFromPreset(SelectedCellSize)
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

    Private ShoreLineColor As Color = Colors.Cyan

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
    Private _panStartPoint As Point

    Private _panPoint As Point
    Public Property PanPoint As Point
        Get
            Return _panPoint
        End Get
        Set(value As Point)
            SetProperty(_panPoint, value)
        End Set
    End Property

    Private _lastViewportSize As Size

    Private _pendingFitToViewport As Boolean = False

    Private _renderCts As Threading.CancellationTokenSource

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

    Private _surfaceLayer As ImageSource
    Public Property SurfaceLayer As ImageSource
        Get
            Return _surfaceLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_surfaceLayer, value)
        End Set
    End Property

    Private _showTopoLayer As Boolean = True
    Public Property ShowTopoLayer As Boolean
        Get
            Return _showTopoLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showTopoLayer, value)
        End Set
    End Property

    Private _topoLayer As ImageSource
    Public Property TopoLayer As ImageSource
        Get
            Return _topoLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_topoLayer, value)
        End Set
    End Property

    Private _showReliefLayer As Boolean = False
    Public Property ShowReliefLayer As Boolean
        Get
            Return _showReliefLayer
        End Get
        Set(value As Boolean)
            SetProperty(_showReliefLayer, value)
        End Set
    End Property

    Private _reliefLayer As ImageSource
    Public Property ReliefLayer As ImageSource
        Get
            Return _reliefLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_reliefLayer, value)
        End Set
    End Property

    Private _showLandMaskLayer As Boolean = False
    Public Property ShowLandMaskLayer As Boolean
        Get
            Return _showLandMaskLayer
        End Get
        Set(value As Boolean)
            If SetProperty(_showLandMaskLayer, value) Then
                If value = False Then
                    ShowShoreLines = False
                    IsHoverCellVisible = False
                Else
                    ShowTidLayer = False
                End If
            End If
        End Set
    End Property

    Private _landMaskLayer As ImageSource
    Public Property LandMaskLayer As ImageSource
        Get
            Return _landMaskLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_landMaskLayer, value)
        End Set
    End Property

    Private _shoreLineLayer As ImageSource
    Public Property ShoreLineLayer As ImageSource
        Get
            Return _shoreLineLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_shoreLineLayer, value)
        End Set
    End Property

    Private _showShoreLines As Boolean = False
    Public Property ShowShoreLines As Boolean
        Get
            Return _showShoreLines
        End Get
        Set(value As Boolean)
            SetProperty(_showShoreLines, value)
        End Set
    End Property

    Private _showTidLayer As Boolean = False
    Public Property ShowTidLayer As Boolean
        Get
            Return _showTidLayer
        End Get
        Set(value As Boolean)
            If SetProperty(_showTidLayer, value) Then
                If Not value Then
                    ShowHoverOverlay = False
                    IsTidLegendOpen = False
                Else
                    ShowLandMaskLayer = False
                End If
            End If
        End Set
    End Property

    Private _tidLayer As ImageSource
    Public Property TidLayer As ImageSource
        Get
            Return _tidLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_tidLayer, value)
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

    Private _statusLonText As String = "Lon: -"
    Public Property StatusLonText As String
        Get
            Return _statusLonText
        End Get
        Set(value As String)
            SetProperty(_statusLonText, value)
        End Set
    End Property

    Private _statusLatText As String = "Lat: -"
    Public Property StatusLatText As String
        Get
            Return _statusLatText
        End Get
        Set(value As String)
            SetProperty(_statusLatText, value)
        End Set
    End Property

    Private _statusHeightText As String = "Höhe: -"
    Public Property StatusHeightText As String
        Get
            Return _statusHeightText
        End Get
        Set(value As String)
            SetProperty(_statusHeightText, value)
        End Set
    End Property

    Private _statusSurfaceText As String = "Surface: -"
    Public Property StatusSurfaceText As String
        Get
            Return _statusSurfaceText
        End Get
        Set(value As String)
            SetProperty(_statusSurfaceText, value)
        End Set
    End Property

    Private _statusZoomText As String = "Zoom: -"
    Public Property StatusZoomText As String
        Get
            Return _statusZoomText
        End Get
        Set(value As String)
            SetProperty(_statusZoomText, value)
        End Set
    End Property

    Private _isHoverCellVisible As Boolean
    Public Property IsHoverCellVisible As Boolean
        Get
            Return _isHoverCellVisible
        End Get
        Set(value As Boolean)
            SetProperty(_isHoverCellVisible, value)
        End Set
    End Property

    Private _hoverCellPoint As Point
    Public Property HoverCellPoint As Point
        Get
            Return _hoverCellPoint
        End Get
        Set(value As Point)
            SetProperty(_hoverCellPoint, value)
        End Set
    End Property

    Private _hoverLatIdx As Integer
    Public Property HoverLatIdx As Integer
        Get
            Return _hoverLatIdx
        End Get
        Set(value As Integer)
            SetProperty(_hoverLatIdx, value)
        End Set
    End Property

    Private _hoverLonIdx As Integer
    Public Property HoverLonIdx As Integer
        Get
            Return _hoverLonIdx
        End Get
        Set(value As Integer)
            SetProperty(_hoverLonIdx, value)
        End Set
    End Property

    Private _hoverLinearIdx As Integer
    Public Property HoverLinearIdx As Integer
        Get
            Return _hoverLinearIdx
        End Get
        Set(value As Integer)
            SetProperty(_hoverLinearIdx, value)
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

    Private _hoverOverlayPoint As Point
    Public Property HoverOverlayPoint As Point
        Get
            Return _hoverOverlayPoint
        End Get
        Set(value As Point)
            SetProperty(_hoverOverlayPoint, value)
        End Set
    End Property

    Private _showHoverOverlay As Boolean
    Public Property ShowHoverOverlay As Boolean
        Get
            Return _showHoverOverlay
        End Get
        Set(value As Boolean)
            SetProperty(_showHoverOverlay, value)
        End Set
    End Property

    Private NotInheritable Class PreviewRenderResult
        Public Property Width As Integer
        Public Property Height As Integer

        Public Property TimingReport As String

        Public Property Surface As ImageSource
        Public Property Topo As ImageSource
        Public Property Relief As ImageSource
        Public Property LandMask As ImageSource
        Public Property ShoreLines As ImageSource
        Public Property Tid As ImageSource

    End Class

#End Region

#Region "GlobePreview"

    Public ReadOnly Property CanOpenGlobePreview As Boolean
        Get
            Return (LoadedCache IsNot Nothing AndAlso LoadedCache.Meta IsNot Nothing AndAlso TopoLayer IsNot Nothing)
        End Get
    End Property

    Private Sub OpenGlobePreview()

        If Not CanOpenGlobePreview Then Return



        Dim payload As New GlobePreviewPayload With {
            .CacheMeta = Me.CacheMeta,
            .HeightMap = Me.LoadedCache.HeightM,
            .Topo = Me.TopoLayer,
            .ReliefOverlay = Me.ReliefLayer,
            .LandMask = Me.LandMaskLayer,
            .ShoreLinesOverlay = Me.ShoreLineLayer,
            .Tid = Me.TidLayer
        }

        Dim wnd As New GlobePreviewWindow(payload)
        wnd.Owner = Application.Current?.Windows.OfType(Of Window)().FirstOrDefault(Function(w) TypeOf w Is EarthSurfaceWindow)
        wnd.WindowStartupLocation = WindowStartupLocation.CenterOwner
        wnd.Show()
    End Sub

#End Region

#Region "Editor"

    Private _editDirty As Boolean = False
    Public Property EditDirty As Boolean
        Get
            Return _editDirty
        End Get
        Set(value As Boolean)
            If SetProperty(_editDirty, value) Then
                RefreshEditCommandState()
            End If
        End Set
    End Property

    Private _undoCount As Integer = 0
    Public Property UndoCount As Integer
        Get
            Return _undoCount
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If SetProperty(_undoCount, value) Then RefreshEditCommandState()
        End Set
    End Property

    Private _redoCount As Integer = 0
    Public Property RedoCount As Integer
        Get
            Return _redoCount
        End Get
        Set(value As Integer)
            value = Math.Max(0, value)
            If SetProperty(_redoCount, value) Then RefreshEditCommandState()
        End Set
    End Property

    Private _isEditMode As Boolean = False
    Public Property IsEditMode As Boolean
        Get
            Return _isEditMode
        End Get
        Set(value As Boolean)
            If value = _isEditMode Then Return

            'Nur beim Ausschalten fragen
            If Not _suppressEditExitPrompt AndAlso _isEditMode AndAlso Not value AndAlso EditDirty Then

                Dim res = MessageBox.Show(
                $"Der Editor hat noch nicht gespeicherte Änderungen.{vbCrLf}{vbCrLf}Sollen die Änderungen gespeichert werden?",
                "Änderungen speichern?",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Warning)

                Select Case res
                    Case MessageBoxResult.Cancel
                        Return

                    Case MessageBoxResult.No
                        'weiter unten wird ausgeschaltet
                        Exit Select
                    Case MessageBoxResult.Yes
                        Dim ok As Boolean = SaveEditsInternalAsync(saveAs:=False).GetAwaiter().GetResult()
                        If Not ok Then Return
                End Select
            End If

            If SetProperty(_isEditMode, value) Then
                EnsureEditSession()
                RefreshEditCommandState()

                If _isEditMode Then
                    RebuildEditOverlay()
                Else
                    EditOverlayLayer = Nothing
                End If
            End If

        End Set
    End Property

    Private _editOverlayWb As WriteableBitmap
    Private _editOverlayLayer As ImageSource
    Public Property EditOverlayLayer As ImageSource
        Get
            Return _editOverlayLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_editOverlayLayer, value)
        End Set
    End Property

    Private _showHeightSpikeLayer As Boolean
    Public Property ShowHeightSpikeLayer As Boolean
        Get
            Return _showHeightSpikeLayer
        End Get
        Set(value As Boolean)
            If SetProperty(_showHeightSpikeLayer, value) Then
                If value Then
                    'Layer bei Bedarf rendern
                    If _heightSpikeMask IsNot Nothing AndAlso LoadedCache?.Meta IsNot Nothing Then
                        HeightSpikeLayer = HeightSpikeOverlayRenderer.RenderSpikeMask(_heightSpikeMask, LoadedCache.Meta.LonCount, LoadedCache.Meta.LatCount)
                    Else
                        Dim ignore = EnsureHeightSpikeAsync()
                    End If
                Else
                    HeightSpikeLayer = Nothing
                End If
            End If
        End Set
    End Property

    Private _heightSpikeLayer As ImageSource
    Public Property HeightSpikeLayer As ImageSource
        Get
            Return _heightSpikeLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_heightSpikeLayer, value)
        End Set
    End Property

    Private _heightSpikeCount As Integer
    Public Property HeightSpikeCount As Integer
        Get
            Return _heightSpikeCount
        End Get
        Set(value As Integer)
            SetProperty(_heightSpikeCount, Math.Max(0, value))
        End Set
    End Property

    Private _heightSpikeMask As Boolean()
    Private _heightSpikeIndices As Integer() = Array.Empty(Of Integer)()
    Private _heightSpikeCursor As Integer = -1
    Private _spikeRecalcCts As CancellationTokenSource

    Private _canJumpNextSpike As Boolean
    Public Property CanJumpNextSpike As Boolean
        Get
            Return _canJumpNextSpike
        End Get
        Set(value As Boolean)
            If SetProperty(_canJumpNextSpike, value) Then
                TryCast(JumpNextSpikeCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
            End If
        End Set
    End Property

    Private _selectedEditChannel As EditChannel = EditChannel.LandMask
    Public Property SelectedEditChannel As EditChannel
        Get
            Return _selectedEditChannel
        End Get
        Set(value As EditChannel)
            If SetProperty(_selectedEditChannel, value) Then
                If IsEditMode AndAlso value = EditChannel.Height Then
                    'Lazy-init der Spikes + Layer einblenden
                    ShowHeightSpikeLayer = True
                    Dim ignore = EnsureHeightSpikeAsync()
                Else
                    ShowHeightSpikeLayer = False

                End If
            End If
        End Set
    End Property
    Public Shared ReadOnly Property EditChannels As IEnumerable(Of EditChannel)
        Get
            Return [Enum].GetValues(Of EditChannel)().Cast(Of EditChannel)()
        End Get
    End Property

    Private _canUndoEdits As Boolean = False
    Public Property CanUndoEdits As Boolean
        Get
            Return _canUndoEdits
        End Get
        Set(value As Boolean)
            SetProperty(_canUndoEdits, value)
        End Set
    End Property

    Private _canRedoEdits As Boolean = False
    Public Property CanRedoEdits As Boolean
        Get
            Return _canRedoEdits
        End Get
        Set(value As Boolean)
            SetProperty(_canRedoEdits, value)
        End Set
    End Property

    Private _canSaveEdits As Boolean = False
    Public Property CanSaveEdits As Boolean
        Get
            Return _canSaveEdits
        End Get
        Set(value As Boolean)
            SetProperty(_canSaveEdits, value)
        End Set
    End Property

    Private _editSession As EarthSurfaceEditSession
    Private _suppressEditExitPrompt As Boolean = False

    Public ReadOnly Property HasEditSession As Boolean
        Get
            Return _editSession IsNot Nothing
        End Get
    End Property

    Private Sub RefreshEditCommandState()
        CanUndoEdits = IsEditMode AndAlso _undoCount > 0 AndAlso Not IsBusy
        CanRedoEdits = IsEditMode AndAlso _redoCount > 0 AndAlso Not IsBusy
        CanSaveEdits = IsEditMode AndAlso _editDirty AndAlso LoadedCache IsNot Nothing AndAlso Not IsBusy
        RaiseEditCommandCanExecuteChanged()
    End Sub

    Private Sub RaiseEditCommandCanExecuteChanged()
        TryCast(SaveEditsCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
        TryCast(SaveEditsAsCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
        TryCast(UndoEditsCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
        TryCast(RedoEditsCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
        TryCast(JumpNextSpikeCommand, RelayCommand(Of Object))?.RaiseCanExecuteChanged()
    End Sub

    Private Sub SetUndoRedoCounts(undoCount As Integer, redoCount As Integer)
        undoCount = Math.Max(0, undoCount)
        redoCount = Math.Max(0, redoCount)
        If _undoCount = undoCount AndAlso _redoCount = redoCount Then Return

        _undoCount = undoCount
        _redoCount = redoCount
        OnPropertyChanged(NameOf(Me.UndoCount))
        OnPropertyChanged(NameOf(Me.RedoCount))
        RefreshEditCommandState()
    End Sub

    Private Sub EnsureEditSession()
        If Not IsEditMode OrElse LoadedCache Is Nothing Then
            _editSession = Nothing
            OnPropertyChanged(NameOf(HasEditSession))
            SetUndoRedoCounts(0, 0)
            EditDirty = False
            Return
        End If

        'Wenn keine Session existiert ODER Cache gewechselt hat -> neue Session
        If _editSession Is Nothing OrElse Not Object.ReferenceEquals(_editSession.BaseCache, LoadedCache) Then
            _editSession = New EarthSurfaceEditSession(LoadedCache)
            OnPropertyChanged(NameOf(HasEditSession))
            SyncEditorStateFromSession()
        End If
    End Sub

    Private Sub ResetEditState()
        _editSession = Nothing
        OnPropertyChanged(NameOf(HasEditSession))
        SetUndoRedoCounts(0, 0)
        EditDirty = False
    End Sub

    Private Sub SyncEditorStateFromSession()
        If _editSession Is Nothing Then
            SetUndoRedoCounts(0, 0)
            EditDirty = False
            Return
        End If

        SetUndoRedoCounts(_editSession.UndoCount, _editSession.RedoCount)
        EditDirty = _editSession.HasUnsavedChanges
    End Sub

    Private Sub EnsureEditOverlayBitmap()
        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then
            _editOverlayWb = Nothing
            EditOverlayLayer = Nothing
            Return
        End If

        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount
        If w <= 0 OrElse h <= 0 Then
            _editOverlayWb = Nothing
            EditOverlayLayer = Nothing
            Return
        End If

        If _editOverlayWb Is Nothing OrElse _editOverlayWb.PixelWidth <> w OrElse _editOverlayWb.PixelHeight <> h Then
            _editOverlayWb = New WriteableBitmap(w, h, 96, 96, PixelFormats.Bgra32, Nothing)
            EditOverlayLayer = _editOverlayWb
            EditOverlayRenderer.ClearAll(_editOverlayWb)
        End If
    End Sub

    Private Sub RebuildEditOverlay()
        If Not IsEditMode OrElse _editSession Is Nothing OrElse LoadedCache Is Nothing Then
            'EditMode aus -> Overlay ausblenden
            EditOverlayLayer = Nothing
            Return
        End If

        EnsureEditOverlayBitmap()
        If _editOverlayWb Is Nothing Then Return

        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount

        EditOverlayRenderer.RebuildAll(_editOverlayWb, _editSession, w, h)

        'Layer sicher gesetzt
        EditOverlayLayer = _editOverlayWb
    End Sub

    Private Sub UpdateEditOverlayIndex(idx As Integer)
        If Not IsEditMode OrElse _editSession Is Nothing OrElse LoadedCache Is Nothing Then Return
        EnsureEditOverlayBitmap()

        If _editOverlayWb Is Nothing Then Return

        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount

        EditOverlayRenderer.UpdateOne(_editOverlayWb, _editSession, idx, w, h)
    End Sub

    Private Async Function SaveEditsInternalAsync(saveAs As Boolean, Optional confirm As Boolean = True) As Task(Of Boolean)

        If Not IsEditMode Then Return False
        EnsureEditSession()
        If _editSession Is Nothing OrElse LoadedCache Is Nothing Then Return False

        If Not _editSession.HasUnsavedChanges Then Return True

        'Pfad bestimmen
        Dim targetMetaPath As String = _loadedMetaPath
        Dim targetBinPath As String = _loadedBinPath

        If String.IsNullOrWhiteSpace(targetMetaPath) OrElse String.IsNullOrWhiteSpace(targetBinPath) Then
            MessageBox.Show("Aktueller Cache-Pfad ist unbekannt. Bitte Cache erneut laden.", "Speichern", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return False
        End If

        If saveAs Then
            Dim dlg As New SaveFileDialog With {
                .Title = "EarthSurface-Cache speichern unter",
                .Filter = "EarthSurface Meta-Datei (*.meta.json)|*.meta.json",
                .InitialDirectory = Path.GetDirectoryName(targetMetaPath),
                .FileName = Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(targetMetaPath)) & "_edited.meta.json",
                .OverwritePrompt = True
            }

            If dlg.ShowDialog() <> True Then
                LastReport = "Speichern unter abgebrochen."
                Return False
            End If

            targetMetaPath = dlg.FileName
            targetBinPath = Path.ChangeExtension(Path.ChangeExtension(targetMetaPath, Nothing), "bin")
        End If

        If Not saveAs AndAlso confirm Then
            Dim msgResult As MessageBoxResult = MessageBox.Show("Sollen die Änderungen wirklich gespeichert werden?", "Speichern", MessageBoxButton.YesNo, MessageBoxImage.Asterisk)
            If msgResult = MessageBoxResult.No Then Return False
        End If

        Try
            Dim editedCount As Integer = 0

            Await BusyRunner.RunAsync(Of Object)(
                Me,
                If(saveAs, "EarthSurface: Speichern unter", "EarthSurface: Speichern"),
                Function(progress, ct)

                    ct.ThrowIfCancellationRequested()

                    If saveAs Then
                        'Save As soll Originaldatei NICHT ändern -> wir speichern eine Kopie
                        Dim clone As EarthSurfaceCache = CloneCache(LoadedCache)
                        Dim tmpSession As New EarthSurfaceEditSession(clone)

                        'Deltas rüberkopieren (nur die Overrides reichen)
                        For Each kvp As KeyValuePair(Of Integer, Byte) In _editSession.Delta.LandMaskOverrides
                            tmpSession.Delta.SetLandMask(kvp.Key, kvp.Value)
                        Next
                        For Each kvp As KeyValuePair(Of Integer, Single) In _editSession.Delta.HeightOverrides
                            tmpSession.Delta.SetHeight(kvp.Key, kvp.Value)
                        Next

                        editedCount = tmpSession.CommitToBase(markTidManual:=True)

                        EarthSurfaceCacheStore.SaveCacheToFiles(targetBinPath, targetMetaPath, clone, progress, ct)

                        'Nach Save As: auf neue Datei wechseln
                        LoadedCache = clone
                    Else
                        'In-place speichern: BaseCache mutieren und in die gleiche Datei schreiben
                        editedCount = _editSession.CommitToBase(markTidManual:=True)

                        EarthSurfaceCacheStore.SaveCacheToFiles(targetBinPath, targetMetaPath, LoadedCache, progress, ct)
                    End If

                    Return True
                End Function,
                canCancel:=True,
                showOverlay:=True)

            'Pfade aktualisieren (bei Save As haben wir neue Dateien)
            SetLoadedCachePathsFromMetaPath(targetMetaPath)

            'Editor-UI sync: Session ist nach Commit reset, im Save-As-Fall wurde LoadedCache getauscht
            EnsureEditSession()
            SyncEditorStateFromSession()
            RebuildEditOverlay()

            LastReport = $"Editor: gespeichert. Änderungen: {editedCount:N0} (TID 254 gesetzt)"
            Await RenderPreviewFromCacheAsync(showOverlay:=True)

            Return True
        Catch ex As OperationCanceledException
            LastReport = "Speichern abgebrochen."
            Return False
        Catch ex As Exception
            Debug.WriteLine(ex.ToString())
            LastReport = $"Fehler beim Speichern: " & ex.Message & " | Source: " & ex.Source
            Return False
        End Try
    End Function

    Private NotInheritable Class HeightSpikeResult
        Public Property Mask As Boolean()
        Public Property Indices As Integer()
        Public Property Count As Integer
        Public Property Layer As ImageSource
    End Class

    Private Async Function EnsureHeightSpikeAsync(Optional forceRebuild As Boolean = False) As Task(Of Boolean)

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return False
        If LoadedCache.HeightM Is Nothing Then Return False

        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount
        If w <= 0 OrElse h <= 0 Then Return False

        'Layer schon berechnet?
        If Not forceRebuild AndAlso _heightSpikeMask IsNot Nothing AndAlso _heightSpikeMask.Length = w * h Then
            If HeightSpikeLayer Is Nothing Then
                HeightSpikeLayer = HeightSpikeOverlayRenderer.RenderSpikeMask(_heightSpikeMask, w, h)
            End If

            HeightSpikeCount = _heightSpikeIndices.Length
            CanJumpNextSpike = (IsEditMode AndAlso SelectedEditChannel = EditChannel.Height AndAlso _heightSpikeIndices.Length > 0 AndAlso Not IsBusy)
            Return True
        End If

        Dim opts As New HeightSpikeOptions With {
            .MinAbsDeviationM = 2000.0,
            .MinNeighborDiffM = 1800.0,
            .RobustFactor = 2.0,
            .UseOceanLandSeperate = False
        }

        Dim n As Integer = w * h
        Dim baseHeights As Single() = LoadedCache.HeightM
        Dim overridesSnapshot As KeyValuePair(Of Integer, Single)() = Array.Empty(Of KeyValuePair(Of Integer, Single))()

        If IsEditMode AndAlso _editSession IsNot Nothing AndAlso _editSession.Delta IsNot Nothing Then
            overridesSnapshot = _editSession.Delta.HeightOverrides.ToArray()
        End If

        Try
            Dim result As HeightSpikeResult = Await BusyRunner.RunAsync(Of HeightSpikeResult)(
                Me,
                "EarthSurface: Height-Spikes analysieren",
                Function(progress, ct)

                    ct.ThrowIfCancellationRequested()

                    Dim heightsToAnalyze As Single() = BuildHeightsForSpikeAnalysis(baseHeights, n, overridesSnapshot)
                    ct.ThrowIfCancellationRequested()

                    Dim mask As Boolean() = Nothing
                    Dim cnt As Integer = 0

                    mask = HeightSpikeAnalyzer.BuildSpikeMask(heightsToAnalyze, w, h, opts, cnt)
                    ct.ThrowIfCancellationRequested()

                    'Indices-Liste bauen
                    Dim list As New List(Of Integer)(cnt)
                    If mask IsNot Nothing Then
                        For i As Integer = 0 To mask.Length - 1
                            If mask(i) Then list.Add(i)
                        Next
                    End If

                    Dim layer As ImageSource = HeightSpikeOverlayRenderer.RenderSpikeMask(mask, w, h)

                    'WICHTIG: wenn es ein Freezable (BitmapSource) ist -> Freeze im Worker
                    Dim bmp = TryCast(layer, BitmapSource)
                    If bmp IsNot Nothing AndAlso bmp.CanFreeze Then bmp.Freeze()

                    Return New HeightSpikeResult With {
                        .Mask = mask,
                        .Indices = list.ToArray(),
                        .Count = list.Count,
                        .Layer = layer
                    }

                End Function,
                canCancel:=True,
                showOverlay:=True)

            'Schutz vor Cache-Wechsel während der Background-Work
            If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return False
            If w <> LoadedCache.Meta.LonCount OrElse h <> LoadedCache.Meta.LatCount Then Return False

            _heightSpikeMask = result.Mask
            _heightSpikeIndices = result.Indices
            HeightSpikeCount = result.Count
            HeightSpikeLayer = result.Layer

            'Cursor-Reset wenn Liste neu ist
            If _heightSpikeIndices.Length = 0 Then
                _heightSpikeCursor = -1
            ElseIf _heightSpikeCursor >= _heightSpikeIndices.Length Then
                _heightSpikeCursor = -1
            End If

            CanJumpNextSpike = (IsEditMode AndAlso SelectedEditChannel = EditChannel.Height AndAlso _heightSpikeIndices.Length > 0 AndAlso Not IsBusy)

            LastReport = $"Height-Spikes gefunden: {result.Count:N0}"

            Return True
        Catch ex As OperationCanceledException
            LastReport = "Height-Spike-Analyse abgebrochen."
            Return False
        Catch ex As Exception
            LastReport = "Fehler bei der Spike-Analyse: " & ex.Message
            Return False
        End Try

    End Function

    Private Async Function ConfirmLeaveEditorAsync(reason As String) As Task(Of Boolean)

        If Not IsEditMode Then Return True
        If Not EditDirty Then
            _suppressEditExitPrompt = True
            Try
                IsEditMode = False

            Finally
                _suppressEditExitPrompt = False
            End Try
            Return True
        End If

        Dim res As MessageBoxResult = MessageBox.Show(
            $"Der Editor hat noch nicht gespeicherte Änderungen.{vbCrLf}{vbCrLf}" &
            $"Sollen die Änderungen gespeichert werden, bevor {reason}?",
            "Änderungen speichern?",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning)

        Select Case res
            Case MessageBoxResult.Cancel
                Return False
            Case MessageBoxResult.Yes
                Dim ok As Boolean = Await SaveEditsInternalAsync(saveAs:=False, confirm:=False)
                If Not ok Then Return False
            Case MessageBoxResult.No
                'verwerfen
        End Select

        _suppressEditExitPrompt = True
        Try
            IsEditMode = False
        Finally
            _suppressEditExitPrompt = False
        End Try

        Return True
    End Function

    Private Sub InvalidateHeightSpikes()

        'nur wenn Height-Channel aktiv ist
        If Not IsEditMode OrElse SelectedEditChannel <> EditChannel.Height Then Return

        HeightSpikeLayer = Nothing
        _heightSpikeMask = Nothing
        _heightSpikeIndices = Array.Empty(Of Integer)()
        'HeightSpikeCount = 0
        CanJumpNextSpike = False

        'debound
        Try
            _spikeRecalcCts?.Cancel()
        Catch
        End Try

        _spikeRecalcCts = New CancellationTokenSource()
        Dim token = _spikeRecalcCts.Token

        Dim ignored As Task = Task.Run(
        Async Function()
            Try
                Await Task.Delay(200, token)
                token.ThrowIfCancellationRequested()

                'Recals leise (ohne Overlay)
                Await RecomputeHeightSpikesSilentAsync(token)
            Catch ex As Exception
            End Try
        End Function)
    End Sub

    Private Async Function RecomputeHeightSpikesSilentAsync(ct As CancellationToken) As Task

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return
        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount
        If w <= 0 OrElse h <= 0 Then Return

        Dim opts As New HeightSpikeOptions With {
            .MinAbsDeviationM = 2000.0,
            .MinNeighborDiffM = 1800.0,
            .RobustFactor = 2.0,
            .UseOceanLandSeperate = False
        }

        Dim n As Integer = w * h
        Dim baseHeights As Single() = LoadedCache.HeightM

        Dim overrideSnapshot As KeyValuePair(Of Integer, Single)() = Array.Empty(Of KeyValuePair(Of Integer, Single))()

        If IsEditMode AndAlso _editSession IsNot Nothing AndAlso _editSession.Delta IsNot Nothing Then
            overrideSnapshot = _editSession.Delta.HeightOverrides.ToArray()
        End If

        Dim heightsToAnalyze As Single() = BuildHeightsForSpikeAnalysis(baseHeights, n, overrideSnapshot)
        If heightsToAnalyze Is Nothing Then Return

        Dim mask As Boolean() = Nothing
        Dim cnt As Integer = 0

        'CPU-Work außerhalb der UI
        mask = HeightSpikeAnalyzer.BuildSpikeMask(heightsToAnalyze, w, h, opts, cnt)

        ct.ThrowIfCancellationRequested()

        Dim list As New List(Of Integer)(cnt)
        If mask IsNot Nothing Then
            For i As Integer = 0 To mask.Length - 1
                If mask(i) Then list.Add(i)
            Next
        End If
        Dim indices As Integer() = list.ToArray()

        'UI-Thread update
        Await Application.Current.Dispatcher.InvokeAsync(
            Sub()
                _heightSpikeMask = mask
                _heightSpikeIndices = indices
                HeightSpikeCount = indices.Length
                CanJumpNextSpike = (IsEditMode AndAlso SelectedEditChannel = EditChannel.Height AndAlso indices.Length > 0 AndAlso Not IsBusy)

                If ShowHeightSpikeLayer AndAlso mask IsNot Nothing Then
                    HeightSpikeLayer = HeightSpikeOverlayRenderer.RenderSpikeMask(mask, w, h)
                End If

                If indices.Length = 0 Then
                    _heightSpikeCursor = -1
                ElseIf _heightSpikeCursor >= indices.Length Then
                    _heightSpikeCursor = -1
                End If
            End Sub)
    End Function

    Private _stroke As StrokeState
    Private NotInheritable Class StrokeState

        Public Property Target As Byte
        Public ReadOnly Property Visited As HashSet(Of Integer)

        Public Sub New(target As Byte)
            Me.Target = target
            Me.Visited = New HashSet(Of Integer)()
        End Sub

    End Class

    Private Function StrokeIsActive() As Boolean
        Return _stroke IsNot Nothing
    End Function

    Private Sub StartStroke(target As Byte)
        EnsureEditSession()
        If _editSession Is Nothing Then Return

        _stroke = New StrokeState(target)
        _editSession.BeginGroup()
    End Sub

    Private Sub EndStroke()
        If _stroke Is Nothing Then Return
        _editSession?.EndGroup()
        _stroke = Nothing
    End Sub

    Private Sub ContinueStroke(idx As Integer)
        If _stroke Is Nothing Then Return

        'HashSet.Add liefert True, wenn es neu war
        If _stroke.Visited.Add(idx) Then
            ApplyLandMaskEdit(idx, _stroke.Target)
        End If
    End Sub

#End Region

#Region "Legend-PopUp"

    Private _isTidLegendOpen As Boolean = False
    Public Property IsTidLegendOpen As Boolean
        Get
            Return _isTidLegendOpen
        End Get
        Set(value As Boolean)
            SetProperty(_isTidLegendOpen, value)
        End Set
    End Property

    Private _tidLegendPoint As Point
    Public Property TidLegendPoint As Point
        Get
            Return _tidLegendPoint
        End Get
        Set(value As Point)
            SetProperty(_tidLegendPoint, value)
        End Set
    End Property

    Private _tidLegendItems As ObservableCollection(Of TidLegendItemViewModel)
    Public Property TidLegendItems As ObservableCollection(Of TidLegendItemViewModel)
        Get
            Return _tidLegendItems
        End Get
        Set(value As ObservableCollection(Of TidLegendItemViewModel))
            SetProperty(_tidLegendItems, value)
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
    Public ReadOnly Property ClearHeightFileCommand As ICommand
    Public ReadOnly Property ClearTidFileCommand As ICommand


    Public ReadOnly Property GenerateCacheCommand As ICommand
    Public ReadOnly Property OpenCacheFolderCommand As ICommand
    Public ReadOnly Property LoadCacheCommand As ICommand

    Public ReadOnly Property MapMouseMoveCommand As ICommand
    Public ReadOnly Property MapMouseLeaveCommand As ICommand
    Public ReadOnly Property MapMouseDownCommand As ICommand
    Public ReadOnly Property MapMouseUpCommand As ICommand


    Public ReadOnly Property BeginPanCommand As ICommand
    Public ReadOnly Property PanCommand As ICommand
    Public ReadOnly Property EndPanCommand As ICommand
    Public ReadOnly Property ZoomCommand As ICommand
    Public ReadOnly Property ViewportChangedCommand As ICommand

    Public ReadOnly Property CloseTidLegendCommand As ICommand
    Public ReadOnly Property ToggleTidLegendCommand As ICommand

    Public ReadOnly Property JumpNextSpikeCommand As ICommand

    Public ReadOnly Property SaveEditsCommand As ICommand
    Public ReadOnly Property SaveEditsAsCommand As ICommand
    Public ReadOnly Property UndoEditsCommand As ICommand
    Public ReadOnly Property RedoEditsCommand As ICommand

    Public ReadOnly Property OpenGlobePreviewCommand As ICommand

#End Region

    Public Sub New()

        TidLegendPoint = New Point(16, 16)

        BrowseHeightCommand = New RelayCommand(Of Object)(Sub(o) BrowseHeight())
        BrowseTidCommand = New RelayCommand(Of Object)(Sub(o) BrowseTid())
        BrowseLandMaskCommand = New RelayCommand(Of Object)(Sub(o) BrowseLandMask())

        MapMouseMoveCommand = New RelayCommand(Of Object)(Sub(o) OnMapMouseMove(o), Function(o) LoadedCache IsNot Nothing)
        MapMouseLeaveCommand = New RelayCommand(Of Object)(Sub(p) OnMapMouseLeave())
        MapMouseDownCommand = New RelayCommand(Of Object)(Sub(o) OnMapMouseDown(o))
        MapMouseUpCommand = New RelayCommand(Of Object)(Sub(o) OnMapMouseUp())

        BeginPanCommand = New RelayCommand(Of PanRequest)(Sub(p) BeginPan(p))
        PanCommand = New RelayCommand(Of PanRequest)(Sub(p) UpdatePan(p))
        EndPanCommand = New RelayCommand(Of Object)(Sub(p) EndPan())
        ZoomCommand = New RelayCommand(Of ZoomRequest)(Sub(z) ZoomAt(z))
        ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(Sub(r) OnViewportChanged(r))

        GenerateCacheCommand = New RelayCommand(Of Object)(Async Sub(o)
                                                               Await GenerateCacheAsync()
                                                           End Sub, Function(o) CanGenerateCache AndAlso Not IsBusy)
        OpenCacheFolderCommand = New RelayCommand(Of Object)(Sub(o)
                                                                 Try
                                                                     Dim dir As String = EarthSurfaceCacheStore.CacheDir
                                                                     If Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
                                                                     Process.Start(New ProcessStartInfo(dir) With {.UseShellExecute = True})
                                                                 Catch ex As Exception
                                                                     MessageBox.Show(ex.Message, "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
                                                                 End Try
                                                             End Sub)
        ClearHeightFileCommand = New RelayCommand(Of Object)(Sub(o)
                                                                 If String.IsNullOrWhiteSpace(RawHeightFile) Then Return
                                                                 RawHeightFile = Nothing
                                                                 LastReport = "GEBCO-Height-Datei entfernt."
                                                             End Sub, Function(o) Not String.IsNullOrWhiteSpace(RawHeightFile))
        ClearTidFileCommand = New RelayCommand(Of Object)(Sub(o)
                                                              If String.IsNullOrWhiteSpace(RawTidFile) Then Return
                                                              RawTidFile = Nothing
                                                              SelectedLandMaskMode = LandMaskMode.FromHeight
                                                              LastReport = "GEBCO-TID-Datei entfernt."
                                                          End Sub, Function(o) Not String.IsNullOrWhiteSpace(RawTidFile))

        LoadCacheCommand = New RelayCommand(Of Object)(Async Sub(o)
                                                           Await LoadCacheAsync()
                                                       End Sub, Function(o) Not IsBusy)

        CloseTidLegendCommand = New RelayCommand(Of Object)(Sub(o)
                                                                IsTidLegendOpen = False
                                                            End Sub)
        ToggleTidLegendCommand = New RelayCommand(Of Object)(Sub(o)
                                                                 IsTidLegendOpen = Not IsTidLegendOpen
                                                             End Sub)
        TidLegendItems = New ObservableCollection(Of TidLegendItemViewModel)(TidLegend.BuildDefaultItems())

        SaveEditsCommand = New RelayCommand(Of Object)(Async Sub(o)
                                                           Await SaveEditsInternalAsync(saveAs:=False)
                                                       End Sub, Function(o) CanSaveEdits)
        SaveEditsAsCommand = New RelayCommand(Of Object)(Async Sub(o)
                                                             Await SaveEditsInternalAsync(saveAs:=True)
                                                         End Sub, Function(o) CanSaveEdits)
        UndoEditsCommand = New RelayCommand(Of Object)(Sub(o)
                                                           If Not IsEditMode Then Return
                                                           EnsureEditSession()
                                                           If _editSession Is Nothing Then Return

                                                           Dim cmd As IEditCommand = Nothing
                                                           If _editSession.Undo(cmd) Then
                                                               SyncEditorStateFromSession()

                                                               'Index rausziehen
                                                               Dim lmCommand As SetLandMaskCommand = TryCast(cmd, SetLandMaskCommand)
                                                               If lmCommand IsNot Nothing Then
                                                                   UpdateEditOverlayIndex(lmCommand.Index)
                                                               Else
                                                                   Dim hCommand As SetHeightCommand = TryCast(cmd, SetHeightCommand)
                                                                   If hCommand IsNot Nothing Then
                                                                       UpdateEditOverlayIndex(hCommand.Index)
                                                                       InvalidateHeightSpikes()
                                                                   Else
                                                                       RebuildEditOverlay()
                                                                   End If
                                                               End If

                                                               LastReport = "Editor: Rückgängig."
                                                           End If
                                                       End Sub, Function(o) CanUndoEdits)
        RedoEditsCommand = New RelayCommand(Of Object)(Sub(o)
                                                           If Not IsEditMode Then Return
                                                           EnsureEditSession()
                                                           If _editSession Is Nothing Then Return

                                                           Dim cmd As IEditCommand = Nothing
                                                           If _editSession.Redo(cmd) Then
                                                               SyncEditorStateFromSession()

                                                               'Index rausziehen
                                                               Dim lmCommand As SetLandMaskCommand = TryCast(cmd, SetLandMaskCommand)
                                                               If lmCommand IsNot Nothing Then
                                                                   UpdateEditOverlayIndex(lmCommand.Index)
                                                               Else
                                                                   Dim hCommand As SetHeightCommand = TryCast(cmd, SetHeightCommand)
                                                                   If hCommand IsNot Nothing Then
                                                                       UpdateEditOverlayIndex(hCommand.Index)
                                                                       InvalidateHeightSpikes()
                                                                   Else
                                                                       RebuildEditOverlay()
                                                                   End If
                                                               End If

                                                               LastReport = "Editor: Wiederholen."
                                                           End If
                                                       End Sub, Function(o) CanRedoEdits)

        JumpNextSpikeCommand = New RelayCommand(Of Object)(Async Sub(o) Await JumpNextSpikeAsync(), Function(o) CanJumpNextSpike AndAlso Not IsBusy)

        OpenGlobePreviewCommand = New RelayCommand(Of Object)(Sub(o) OpenGlobePreview(), Function(o) CanOpenGlobePreview AndAlso Not IsBusy)

    End Sub

#Region "Filepicker"

    Private Sub BrowseHeight()
        Dim dlg As New OpenFileDialog With {
            .Title = "GEBCO Height-Datei auswählen",
            .Filter = "ESRI ASCII Zip (*.zip)|*.zip",
            .InitialDirectory = DataEarthPaths.RawDirectory,
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
            .InitialDirectory = DataEarthPaths.RawDirectory,
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

    Private NotInheritable Class CacheOpenResult
        Public Property Ok As Boolean
        Public Property Cache As EarthSurfaceCache
        Public Property ErrorKind As CacheOpenErrorKind
        Public Property ErrorMessage As String

    End Class

    Private Async Function LoadCacheAsync() As Task(Of String)

        Dim dlg As New OpenFileDialog With {
            .Title = "EarthSurface-Cache laden",
            .Filter = "EarthSurface Meta-Datei (*.meta.json)|*.meta.json",
            .InitialDirectory = DataEarthPaths.CacheDirectory,
            .CheckFileExists = True,
            .Multiselect = False
        }

        If dlg.ShowDialog() <> True Then Return "Datei nicht gefunden."

        Dim metaPath As String = dlg.FileName

        If Not Await ConfirmLeaveEditorAsync("ein anderer Cache geladen wird") Then
            LastReport = "Laden abgebrochen."
            Return "Abgebrochen"
        End If

        Try

            Dim result As CacheOpenResult = Await BusyRunner.RunAsync(Of CacheOpenResult)(
                Me,
                "EarthSurface: Cache laden",
                Function(progress, ct)

                    Dim cache As EarthSurfaceCache = Nothing
                    Dim ek As CacheOpenErrorKind
                    Dim em As String = Nothing

                    Dim ok As Boolean = EarthSurfaceCacheStore.TryOpenCacheFromFiles(metaPath, cache, ek, em, progress, ct)

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

            Dim openedCache As EarthSurfaceCache = result.Cache

            'Meta -> VM spiegeln
            ApplyLoadedMetaToViewModel(openedCache.Meta)

            'Cache merken
            SetLoadedCachePathsFromMetaPath(metaPath)
            LoadedCache = openedCache

            LastReport = $"Cache geladen: {Path.GetFileName(Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), Nothing))}"

            'Nach dem Laden: Preview neu rendern
            Await RenderPreviewFromCacheAsync(showOverlay:=True)

            Return "Cache geladen."

        Catch ex As OperationCanceledException
            LastReport = "Abgebrochen."
            Return "Abgebrochen"

        Catch ex As InvalidDataException
            LastReport = $"Fehler beim Laden: {ex.Message}"
            Return $"Fehler: {ex.Message}"

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
                SelectedResampling = ResamplingMode.Nearest
        End Select

        If meta.LandMaskSource = LandMaskMode.FromHeight.ToString() Then
            UseHysteresis = meta.UseHysteresis
            HysteresisIterations = meta.HysteresisIterations
        End If

    End Sub

#End Region

#Region "Rendering"

    Private Async Function RenderPreviewFromCacheAsync(Optional showOverlay As Boolean = True) As Task

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then
            SurfaceLayer = Nothing
            TopoLayer = Nothing
            ReliefLayer = Nothing
            LandMaskLayer = Nothing
            ShoreLineLayer = Nothing
            TidLayer = Nothing
            Return
        End If

        'Falls ein Render noch läuft: abbrechen
        Try
            _renderCts?.Cancel()
        Catch ex As Exception
        End Try

        _renderCts = New Threading.CancellationTokenSource()
        Dim token = _renderCts.Token

        Dim cache As EarthSurfaceCache = LoadedCache
        Dim meta As EarthSurfaceCacheMeta = cache.Meta

        Try

            Dim rr As PreviewRenderResult =
                Await BusyRunner.RunAsync(Of PreviewRenderResult)(
                Me,
                "EarthSurface: Preview rendern",
                Function(progress, ct)

                    'DEBUG
                    Dim timings As New List(Of RenderTiming)()
                    Dim wH As String = $"{meta.LonCount}x{meta.LatCount}"

                    'kombiniere erst ct von BusyRunner + eigenes
                    token.ThrowIfCancellationRequested()
                    ct.ThrowIfCancellationRequested()

                    Dim width As Integer = meta.LonCount
                    Dim height As Integer = meta.LatCount


                    progress?.Report(New ProgressInfo("Topografie-Layer rendern...", 0))
                    'Dim topoBmp = TopoRenderer.RenderTopoLayer(cache)
                    'topoBmp.Freeze()
                    'DEBUG:
                    Dim topoBmp = WithTiming("Topo", timings,
                                             Function()
                                                 Dim bmp = TopoRenderer.RenderTopoLayer(cache)
                                                 bmp.Freeze()
                                                 Return bmp
                                             End Function, extra:=wH)

                    token.ThrowIfCancellationRequested()
                    ct.ThrowIfCancellationRequested()

                    progress?.Report(New ProgressInfo("Relief-Layer rendern...", 45))
                    'Dim reliefBmp = ReliefRenderer.RenderRelief(cache)
                    'reliefBmp.Freeze()
                    'DEBUG:
                    Dim reliefBmp = WithTiming("Relief", timings,
                                             Function()
                                                 Dim bmp = ReliefRenderer.RenderRelief(cache)
                                                 bmp.Freeze()
                                                 Return bmp
                                             End Function, extra:=wH)

                    token.ThrowIfCancellationRequested()
                    ct.ThrowIfCancellationRequested()

                    progress?.Report(New ProgressInfo("LandMask/Küstenlinien rendern...", 90))
                    Dim lm As ImageSource = Nothing
                    Dim sl As ImageSource = Nothing

                    If cache.Meta.HasLandMask AndAlso cache.LandMask IsNot Nothing Then
                        'Dim lmBmp = LandMaskRenderer.RenderLandMask(cache)
                        'lmBmp.Freeze()
                        'DEBUG:
                        Dim lmBmp = WithTiming("LandMask", timings,
                                             Function()
                                                 Dim bmp = LandMaskRenderer.RenderLandMask(cache)
                                                 bmp.Freeze()
                                                 Return bmp
                                             End Function, extra:=wH)
                        lm = lmBmp

                        'Dim slBmp = LandMaskRenderer.RenderShoreLines(cache, ShoreLineColor, alpha:=160, includeDiagonal:=True)
                        'slBmp.Freeze()
                        'DEBUG:
                        Dim slBmp = WithTiming("ShoreLines", timings,
                                             Function()
                                                 Dim bmp = LandMaskRenderer.RenderShoreLines(cache)
                                                 bmp.Freeze()
                                                 Return bmp
                                             End Function, extra:=wH)
                        sl = slBmp
                    End If

                    token.ThrowIfCancellationRequested()
                    ct.ThrowIfCancellationRequested()

                    progress?.Report(New ProgressInfo("TID-Layer rendern...", 95))
                    Dim tid As ImageSource = Nothing
                    If meta.HasTid Then
                        'Dim tidBmp = TidRenderer.RenderTidLayer(cache, alpha:=255)
                        'tidBmp.Freeze()
                        'DEBUG:
                        Dim tidBmp = WithTiming("TID", timings,
                                             Function()
                                                 Dim bmp = TidRenderer.RenderTidLayer(cache)
                                                 bmp.Freeze()
                                                 Return bmp
                                             End Function, extra:=wH)
                        tid = tidBmp
                    End If

                    progress?.Report(New ProgressInfo("Fertig.", 100))
                    'DEBUG:
                    Dim timingReport As String = FormatTimings(timings)

                    Return New PreviewRenderResult With {
                        .Width = width,
                        .Height = height,
                        .TimingReport = timingReport,
                        .Surface = Nothing,
                        .Topo = topoBmp,
                        .Relief = reliefBmp,
                        .LandMask = lm,
                        .ShoreLines = sl,
                        .Tid = tid
                    }
                End Function,
                canCancel:=True,
                showOverlay:=showOverlay)

            If Not Object.ReferenceEquals(cache, LoadedCache) Then Return

            'UI-Thread: VM befüllen
            SurfaceLayer = Nothing
            TopoLayer = rr.Topo
            ReliefLayer = rr.Relief
            LandMaskLayer = rr.LandMask
            ShoreLineLayer = rr.ShoreLines
            TidLayer = rr.Tid

            ContentSize = New Size(rr.Width, rr.Height)
            ContentCellSizeDeg = LoadedCache.Meta.CellSizeDeg

            Debug.WriteLine(rr.TimingReport)

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
            'Abbruch
        Catch ex As Exception
            LastReport = $"Fehler beim Rendern: {ex.Message}"
        End Try

    End Function

    Private Sub OnMapMouseMove(param As Object)

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then
            OnMapMouseLeave()
            Return
        End If

        Dim r As MapMouseMoveRequest = TryCast(param, MapMouseMoveRequest)
        If r Is Nothing Then Return

        EquiRectangularViewportHelper.RememberViewportSize(r.ViewPortSize, _lastViewportSize)

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta
        Dim hit As EquiRectangularViewportHelper.CellHit
        If Not EquiRectangularViewportHelper.TryHitCell(r.MousePos, r.ViewPortSize, ContentSize, meta.CellSizeDeg, Camera, Zoom, _panPoint, hit) Then
            OnMapMouseLeave()
            Return
        End If

        'HoverRect + Statusbar immer
        UpdateHoverCellUi(hit)
        UpdateStatusUi(hit)

        Dim isEditDrag As Boolean = IsEditMode AndAlso r.IsLeftButtonDown AndAlso (r.Ctrl OrElse r.Alt)

        If isEditDrag Then

            ShowHoverOverlay = False        'TID-Overlay beim Drag nie

            If SelectedEditChannel = EditChannel.LandMask Then
                'Wenn ein Modifier wegfällt -> Stroke beenden
                If StrokeIsActive() AndAlso Not (r.Ctrl OrElse r.Alt) Then
                    EndStroke()
                    Return
                End If

                'Stroke fortsetzen, wenn aktiv
                If StrokeIsActive() Then
                    ContinueStroke(hit.Index)
                End If
            Else
                'Height: Kein Drag/Stroke-Edit. Wir lassen HoverRect/Status weiterlaufen, aber editieren nichts
            End If

            Return
        End If

        'Wenn kein Edit-Drag -> normaler Hover
        If StrokeIsActive() Then
            ShowHoverOverlay = False    'zur Sicherheit: falls Stroke noch aktiv ist, keinen Overlay anzeigen
        Else
            UpdateTidHoverOverlay(r.MousePos, hit.Index)
        End If
    End Sub

    Private Sub OnMapMouseLeave()
        IsHoverCellVisible = False
        ShowHoverOverlay = False
        ClearStatusBar()
        Return
    End Sub

    Private Sub OnMapMouseDown(param As Object)

        Dim r As MapMouseDownRequest = TryCast(param, MapMouseDownRequest)
        If r Is Nothing OrElse Not r.IsLeftButton Then Return

        'Edit nur wenn EditMode aktiv ist
        If Not IsEditMode Then Return

        'Nur Strg+Alt sind Edit-Modifier
        If Not r.Ctrl AndAlso Not r.Alt Then Return

        If LoadedCache Is Nothing OrElse LoadedCache.LandMask Is Nothing Then
            LastReport = "Edit nicht möglich: Cache hat keine gültige LandMask."
            Return
        End If

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta

        'Zelle bestimmen: gleiche Screen-Geo-Logik wie im Hover
        Dim hit As EquiRectangularViewportHelper.CellHit
        If Not EquiRectangularViewportHelper.TryHitCell(r.MousePos, r.ViewPortSize, ContentSize, meta.CellSizeDeg, Camera, Zoom, _panPoint, hit) Then Return

        Select Case SelectedEditChannel
            Case EditChannel.LandMask
                HandleLandMaskMouseDown(r, hit.Index)

            Case EditChannel.Height
                HandleHeightMouseDown(r, hit.Index)
            Case Else
                LastReport = "Edit: Dieser Kanal ist noch nicht implementiert."
                Return
        End Select

    End Sub

    Private Sub OnMapMouseUp()

        If StrokeIsActive() Then EndStroke()

    End Sub

    Private Sub OnViewportChanged(r As ViewportChangedRequest)

        If r Is Nothing Then Return

        _lastViewportSize = r.ViewPortSize

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return

        If _pendingFitToViewport Then
            EquiRectangularViewportHelper.FitToViewport(_lastViewportSize, ContentSize, Zoom, _panPoint)
            _pendingFitToViewport = False
        Else
            'bei Resize nur clampen/zentrieren
            EquiRectangularViewportHelper.ClampPan(_lastViewportSize, ContentSize, Zoom, _panPoint)
        End If

    End Sub

    Public Sub BeginPan(r As PanRequest)

        _isPanning = True
        _panStartMouse = r.MousePos
        _panStartPoint = _panPoint
        ShowHoverOverlay = False

    End Sub

    Public Sub UpdatePan(r As PanRequest)

        If Not _isPanning Then Return

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return

        Dim dx As Double = r.MousePos.X - _panStartMouse.X
        Dim dy As Double = r.MousePos.Y - _panStartMouse.Y

        Dim p As New Point(_panStartPoint.X + dx, _panStartPoint.Y + dy)

        EquiRectangularViewportHelper.ClampPan(r.ViewPortSize, ContentSize, Zoom, p)

        PanPoint = p

    End Sub

    Public Sub EndPan()

        _isPanning = False

    End Sub

    Public Sub ZoomAt(z As ZoomRequest)

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return


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
        Dim cx As Double = (z.MousePos.X - _panPoint.X) / oldZoom
        Dim cy As Double = (z.MousePos.Y - _panPoint.Y) / oldZoom

        Zoom = newZoom

        Dim p As New Point(z.MousePos.X - cx * newZoom, z.MousePos.Y - cy * newZoom)

        EquiRectangularViewportHelper.ClampPan(z.ViewPortSize, ContentSize, Zoom, p)
        PanPoint = p

        'Anzeige: wenn Zoom=1.0 -> 100%
        StatusZoomText = $"Zoom: {Zoom * 100:0.##}%"
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

            If cache IsNot Nothing Then
                If Not Await ConfirmLeaveEditorAsync("ein neuer Cache übernommen wird") Then
                    Return Nothing
                End If

                Dim paths = EarthSurfaceCacheStore.GetCachePaths(SourceName, CellSizeDeg, ResamplingKey, LandMaskVariantTag)
                SetLoadedCachePathsFromMetaPath(paths.metaPath)

                LoadedCache = cache
                Await RenderPreviewFromCacheAsync(showOverlay:=True)
            Else
                LastReport = "Cache konnte nicht generiert werden."
                Return Nothing
            End If

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

        Dim meta = cache.Meta
        Dim latCount As Integer = meta.LatCount
        Dim lonCount As Integer = meta.LonCount

        Dim hasTid As Boolean = (meta.HasTid AndAlso cache.Tid IsNot Nothing AndAlso cache.Tid.Length = latCount * lonCount)
        Dim hasLm As Boolean = (meta.HasLandMask AndAlso cache.LandMask IsNot Nothing AndAlso cache.LandMask.Length = latCount * lonCount)

        Dim sb As New StringBuilder()
        sb.AppendLine("=== Cache Stichproben ===")
        sb.AppendLine($"Raster: {latCount} x {lonCount}  cell={meta.CellSizeDeg}°")
        sb.AppendLine($"HasTid={meta.HasTid}, HasLandMask={meta.HasLandMask}")
        sb.AppendLine()

        If hasTid Then
            Dim minTid As Integer = Integer.MaxValue
            Dim maxTid As Integer = Integer.MinValue
            Dim cntUnknown As Integer = 0
            Dim cntLand0 As Integer = 0
            Dim cntOther As Integer = 0

            For i As Integer = 0 To latCount * lonCount - 1
                Dim v As Integer = CInt(cache.Tid(i))      '0..255

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

        Return sb.ToString()
    End Function

#End Region

#Region "Helper"

    Private Sub ClearStatusBar()
        StatusLatText = "Lat: -"
        StatusLonText = "Lon: -"
        StatusHeightText = "Höhe: -"
        StatusSurfaceText = "Surface: -"
        StatusZoomText = $"Zoom: {Zoom * 100:0.##}%"
    End Sub

    Private Sub SetStatusBar(lat As Double, lon As Double, height As Double, surface As String, zoom As Double)
        StatusLatText = $"Lat: {lat:0.##}°"
        StatusLonText = $"Lon: {lon:0.##}°"
        StatusHeightText = $"Höhe: {height:0.##} m"
        StatusSurfaceText = $"Surface: {surface}"
        StatusZoomText = $"Zoom: {zoom * 100:0.##}%"
    End Sub

    Private Shared Function SurfaceTextFromCache(cache As EarthSurfaceCache, idx As Integer) As String
        If cache Is Nothing OrElse idx < 0 Then Return "Unbekannt"

        If cache.LandMask IsNot Nothing AndAlso idx < cache.LandMask.Length Then
            Select Case cache.LandMask(idx)
                Case 1 : Return "Land"
                Case 0 : Return "Wasser"
                Case Else : Return "Unbekannt"
            End Select
        End If

        'Fallback für alte Caches ohne LandMask
        If cache.HeightM IsNot Nothing AndAlso idx < cache.HeightM.Length Then
            Dim h As Double = cache.HeightM(idx)
            If Double.IsNaN(h) OrElse Double.IsInfinity(h) Then Return "Unbekannt"
            If h < 0 Then Return "Wasser"
            If h > 0 Then Return "Land"
        End If

        Return "Unbekannt"
    End Function

    Private Shared Function SurfaceTextFromEditor(editSession As EarthSurfaceEditSession, idx As Integer) As String
        If editSession Is Nothing OrElse idx < 0 Then Return "Unbekannt"

        Select Case editSession.GetEffectiveLandMask(idx)
            Case 0 : Return "Wasser"
            Case 1 : Return "Land"
            Case Else : Return "Unbekannt"
        End Select

    End Function

    Private Shared Function CloneCache(src As EarthSurfaceCache) As EarthSurfaceCache

        If src Is Nothing Then Return Nothing

        Dim m As EarthSurfaceCacheMeta = JsonSerializer.Deserialize(Of EarthSurfaceCacheMeta)(
            JsonSerializer.Serialize(src.Meta, ConfigStore.JsonOptions),
            ConfigStore.JsonOptions)

        Dim h As Single() = Nothing
        If src.HeightM IsNot Nothing Then
            h = CType(src.HeightM.Clone(), Single())
        End If

        Dim t As Byte() = Nothing
        If src.Tid IsNot Nothing Then
            t = CType(src.Tid.Clone(), Byte())
        End If

        Dim lm As Byte() = Nothing
        If src.LandMask IsNot Nothing Then
            lm = CType(src.LandMask.Clone(), Byte())
        End If

        Return New EarthSurfaceCache(m, h, t, lm)

    End Function

    Private Sub ApplyLandMaskEdit(idx As Integer, target As Byte)

        If _editSession Is Nothing Then Return
        If LoadedCache Is Nothing Then Return

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta
        If idx < 0 OrElse idx >= meta.LatCount * meta.LonCount Then Return

        Dim effectiveBefore As Byte = _editSession.GetEffectiveLandMask(idx)
        If effectiveBefore = target Then Return

        Dim cmd As New SetLandMaskCommand(idx, target)
        If _editSession.ApplyCommand(cmd) Then
            SyncEditorStateFromSession()
            UpdateEditOverlayIndex(idx)
        End If
    End Sub

    Private Sub HandleLandMaskMouseDown(r As MapMouseDownRequest, idx As Integer)

        If LoadedCache Is Nothing OrElse LoadedCache.LandMask Is Nothing Then
            LastReport = "Edit nicht möglich: Cache hat keine gültige LandMask."
            Return
        End If

        'Zielwert bestimmen
        Dim target As Byte
        If r.Ctrl Then
            target = 1  'Land
        ElseIf r.Alt Then
            target = 0  'Wasser
        Else
            Return
        End If

        StartStroke(target)
        ContinueStroke(idx)

    End Sub

    Private Sub HandleHeightMouseDown(r As MapMouseDownRequest, idx As Integer)

        If LoadedCache Is Nothing OrElse LoadedCache.HeightM Is Nothing Then
            LastReport = "Edit nicht möglich: Cache hat keinen gültigen HeightM-Kanal."
            Return
        End If

        EnsureEditSession()
        If _editSession Is Nothing Then Return

        If r.Alt Then
            ResetHeightEdit(idx)
            Return
        End If

        If r.Ctrl Then
            SmoothHeightEdit(idx)
            Return
        End If

    End Sub

    Private Sub ResetHeightEdit(idx As Integer)

        If _editSession Is Nothing Then Return
        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta
        Dim n As Integer = meta.LatCount * meta.LonCount
        If idx < 0 OrElse idx >= n Then Return

        Dim baseV As Single = _editSession.GetBaseHeight(idx)
        If Single.IsNaN(baseV) OrElse Single.IsInfinity(baseV) Then
            LastReport = "Edit: Height-Reset nicht möglich: BaseHeight ungültig."
            Return
        End If

        Dim effectiveBefore As Single = _editSession.GetEffectiveHeight(idx)
        If Not Single.IsNaN(effectiveBefore) AndAlso Not Single.IsInfinity(effectiveBefore) Then
            If Math.Abs(CDbl(effectiveBefore) - CDbl(baseV)) < 0.0000001 Then
                Return
            End If
        End If

        Dim cmd As New SetHeightCommand(idx, baseV)
        If _editSession.ApplyCommand(cmd) Then
            SyncEditorStateFromSession()
            UpdateEditOverlayIndex(idx)
            LastReport = $"Edit: Height({idx}): Reset"

            InvalidateHeightSpikes()
        End If
    End Sub

    Private Sub SmoothHeightEdit(idx As Integer)

        If _editSession Is Nothing Then Return
        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta
        Dim n As Integer = meta.LatCount * meta.LonCount
        If idx < 0 OrElse idx >= n Then Return

        Dim median As Double
        If Not TryComputeNeighborMedianBase(idx, median) Then
            LastReport = "Edit: Height: Glätten nicht möglich (zu wenige gültige Nachbarn)."
            Return
        End If

        Dim newV As Single = CSng(median)

        Dim effectiveBefore As Single = _editSession.GetEffectiveHeight(idx)
        If Not Single.IsNaN(effectiveBefore) AndAlso Not Single.IsInfinity(effectiveBefore) Then
            If Math.Abs(CDbl(effectiveBefore) - CDbl(newV)) < 0.0000001 Then
                Return
            End If
        End If

        Dim cmd As New SetHeightCommand(idx, newV)
        If _editSession.ApplyCommand(cmd) Then
            SyncEditorStateFromSession()
            UpdateEditOverlayIndex(idx)
            LastReport = $"Edit: Height({idx}: geglättet -> {newV:0.##}m"

            InvalidateHeightSpikes()
        End If
    End Sub

    Private Function TryComputeNeighborMedianBase(centerIdx As Integer, ByRef median As Double) As Boolean

        median = 0.0

        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return False
        If _editSession Is Nothing Then Return False

        Dim w As Integer = LoadedCache.Meta.LonCount
        Dim h As Integer = LoadedCache.Meta.LatCount
        If w <= 0 OrElse h <= 0 Then Return False

        Dim x As Integer = centerIdx Mod w
        Dim y As Integer = centerIdx \ w
        If x < 0 OrElse x >= w OrElse y < 0 OrElse y >= h Then Return False

        Dim values As New List(Of Double)(8)

        For dy As Integer = -1 To 1

            For dx As Integer = -1 To 1

                If dx = 0 AndAlso dy = 0 Then Continue For

                Dim xx As Integer = x + dx
                Dim yy As Integer = y + dy

                If xx < 0 Then xx = 0
                If xx >= w Then xx = w - 1
                If yy < 0 Then yy = 0
                If yy >= h Then yy = h - 1

                Dim nIdx As Integer = yy * w + xx

                Dim v As Single = _editSession.GetBaseHeight(nIdx)
                If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Continue For

                values.Add(CDbl(v))
            Next
        Next

        If values.Count < 3 Then Return False

        values.Sort()

        Dim mid As Integer = values.Count \ 2
        If (values.Count Mod 2) = 1 Then
            median = values(mid)
        Else
            median = 0.5 * (values(mid - 1) + values(mid))
        End If

        Return True

    End Function

    Private Sub UpdateHoverCellUi(hit As CellHit)

        If HoverLatIdx <> hit.LatIdx Then HoverLatIdx = hit.LatIdx
        If HoverLonIdx <> hit.LonIdx Then HoverLonIdx = hit.LonIdx
        If HoverLinearIdx <> hit.Index Then HoverLinearIdx = hit.Index

        If ShowLandMaskLayer OrElse IsEditMode Then
            HoverCellPoint = New Point(hit.LonIdx,
                                       hit.LatIdx)
            IsHoverCellVisible = True
        Else
            IsHoverCellVisible = False
        End If
    End Sub

    Private Sub UpdateStatusUi(hit As CellHit)

        Dim idx As Integer = hit.Index

        Dim h As Double = Double.NaN

        If HasEditSession AndAlso IsEditMode AndAlso _editSession IsNot Nothing Then
            Dim hV As Single = _editSession.GetEffectiveHeight(idx)
            If Not Single.IsNaN(hV) OrElse Not Single.IsInfinity(hV) Then
                h = CDbl(hV)
            End If
        ElseIf LoadedCache?.HeightM IsNot Nothing AndAlso idx >= 0 AndAlso idx < LoadedCache.HeightM.Length Then
            Dim hV As Single = LoadedCache.HeightM(idx)
            If Not Single.IsNaN(hV) OrElse Not Single.IsInfinity(hV) Then
                h = CDbl(hV)
            End If
        End If

        Dim surfaceText As String
        If HasEditSession AndAlso IsEditMode Then
            surfaceText = SurfaceTextFromEditor(_editSession, idx)
        Else
            surfaceText = SurfaceTextFromCache(LoadedCache, idx)
        End If

        SetStatusBar(hit.Lat, hit.Lon, h, surfaceText, Zoom)
    End Sub

    Private Sub UpdateTidHoverOverlay(mousePos As Point, idx As Integer)

        If ShowTidLayer AndAlso LoadedCache?.Tid IsNot Nothing AndAlso idx >= 0 AndAlso idx < LoadedCache.Tid.Length Then
            Dim t As Byte = LoadedCache.Tid(idx)
            Dim code As Integer = TidHelpers.TidByteToCode(t)

            HoverOverlayText = TidLegend.TidText(code)
            HoverOverlayPoint = New Point(mousePos.X + 14,
                                          mousePos.Y + 14)
            ShowHoverOverlay = True
        Else
            ShowHoverOverlay = False
        End If

    End Sub

    Private Sub ResetPerCacheUiState()
        'Editor
        _stroke = Nothing
        ResetEditState()
        _editOverlayWb = Nothing
        EditOverlayLayer = Nothing

        'Spike-Analyse
        _heightSpikeMask = Nothing
        HeightSpikeLayer = Nothing
        HeightSpikeCount = 0
        ShowHeightSpikeLayer = False

        'Hover/TID Tooltip
        ShowHoverOverlay = False
        IsHoverCellVisible = False

        'Edit-Channel zurück auf Landmask
        _selectedEditChannel = EditChannel.LandMask
        OnPropertyChanged(NameOf(SelectedEditChannel))
    End Sub

    Private Shared Function BuildHeightsForSpikeAnalysis(baseHeights As Single(), n As Integer, overridesSnapshot As KeyValuePair(Of Integer, Single)()) As Single()

        If baseHeights Is Nothing OrElse baseHeights.Length < n Then Return Nothing

        If overridesSnapshot Is Nothing OrElse overridesSnapshot.Length = 0 Then
            'Wenn es keine Overrides gibt, einfach BaseHeights zurückgeben
            Return baseHeights
        End If

        'Zuerst die BaseHeights in das neue Array clonen
        Dim arr As Single() = CType(baseHeights.Clone(), Single())

        'Dann NUR die Overrides überschreiben
        For Each kvp In overridesSnapshot
            Dim idx As Integer = kvp.Key
            If idx >= 0 AndAlso idx < n Then
                arr(idx) = kvp.Value
            End If
        Next

        Return arr
    End Function

    Private Async Function JumpNextSpikeAsync() As Task

        If Not IsEditMode OrElse SelectedEditChannel <> EditChannel.Height Then Return
        If LoadedCache Is Nothing OrElse LoadedCache.Meta Is Nothing Then Return

        Dim meta As EarthSurfaceCacheMeta = LoadedCache.Meta

        'Spikes sicherstellen
        If _heightSpikeIndices Is Nothing OrElse _heightSpikeIndices.Length = 0 Then
            Dim ok As Boolean = Await EnsureHeightSpikeAsync(forceRebuild:=True)
            If Not ok Then Return
        End If
        If _heightSpikeIndices.Length = 0 Then Return

        'Nächster
        _heightSpikeCursor += 1
        If _heightSpikeCursor >= _heightSpikeIndices.Length Then _heightSpikeCursor = 0

        Dim idx As Integer = _heightSpikeIndices(_heightSpikeCursor)

        Dim contentW As Integer = meta.LonCount
        Dim contentH As Integer = meta.LatCount

        Dim x As Integer = idx Mod contentH
        Dim y As Integer = idx \ contentW

        'Max-Zoom: 2000% = 20.0
        Zoom = 20.0
        StatusZoomText = $"Zoom: {Zoom * 100:0}%"

        'Viewport muss bekannt sein
        If _lastViewportSize.Width <= 0 OrElse _lastViewportSize.Height <= 0 Then
            'Fallback: ohne echte Viewport-Daten nur grob zentrieren
            _panPoint.X = -((x + 0.5) * Zoom)
            _panPoint.Y = -((y + 0.5) * Zoom)
            Return
        End If

        'Zentrieren auf Zellmitte
        _panPoint.X = (_lastViewportSize.Width / 2.0) - ((x + 0.5) * Zoom)
        _panPoint.Y = (_lastViewportSize.Height / 2.0) - ((y + 0.5) * Zoom)

        EquiRectangularViewportHelper.ClampPan(_lastViewportSize, ContentSize, Zoom, _panPoint)

        LastReport = $"Spike {_heightSpikeCursor + 1:N0}/{_heightSpikeIndices.Length:N0} @ idx={idx} (x={x}, y={y})"
    End Function

#End Region

End Class
