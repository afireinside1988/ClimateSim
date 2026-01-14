Imports System.Globalization
Imports System.Net.Security
Imports System.Reflection.Metadata
Imports System.Runtime.InteropServices
Imports System.Threading
Imports System.Transactions
Imports System.Windows.Media.Media3D

Public Class GlobePreviewViewModel
    Inherits ViewModelBase

    Private ReadOnly _p As GlobePreviewPayload
    Private _viewport As Viewport3D

#Region "Busy-Overlay"

    Private _isRendering As Boolean
    Public Property IsRendering As Boolean
        Get
            Return _isRendering
        End Get
        Set(value As Boolean)
            SetProperty(_isRendering, value)
        End Set
    End Property

    Private _renderingTitle As String = "Globus wird gerendert…"
    Public Property RenderingTitle As String
        Get
            Return _renderingTitle
        End Get
        Set(value As String)
            SetProperty(_renderingTitle, value)
        End Set
    End Property

    Private _renderingMessage As String = ""
    Public Property RenderingMessage As String
        Get
            Return _renderingMessage
        End Get
        Set(value As String)
            SetProperty(_renderingMessage, value)
        End Set
    End Property

    Private _renderCts As CancellationTokenSource
    Private ReadOnly _renderGate As New Object()

#End Region

#Region "Kamera"

    Private _yawDeg As Double = 0.0             'Rotation um Y
    Private _pitchDeg As Double = 0.0           'Rotation um x (oben/unten)
    Private _distance As Double = 3.0           'Kamera-Abstand

    Private _dragStart As Point
    Private _yawStart As Double
    Private _pitchStart As Double
    Private _isDragging As Boolean

    Private _viewportW As Double = 1.0
    Private _viewportH As Double = 1.0

    Private _lastDragCameraTicks As Long
    Private Shared ReadOnly _tickPerFrame As Long = CLng(Stopwatch.Frequency / 60.0)    '60fps

    '=== Kamera-Properties ====
    Private _cameraPosition As Point3D
    Public Property CameraPosition As Point3D
        Get
            Return _cameraPosition
        End Get
        Set(value As Point3D)
            SetProperty(_cameraPosition, value)
        End Set
    End Property

    Private _cameraLook As Vector3D
    Public Property CameraLookDirection As Vector3D
        Get
            Return _cameraLook
        End Get
        Set(value As Vector3D)
            SetProperty(_cameraLook, value)
        End Set
    End Property

    Private _cameraUp As Vector3D = New Vector3D(0, 1, 0)
    Public Property CameraUpDirection As Vector3D
        Get
            Return _cameraUp
        End Get
        Set(value As Vector3D)
            SetProperty(_cameraUp, value)
        End Set
    End Property

    Private _cameraFov As Double = 45.0
    Public Property CameraFov As Double
        Get
            Return _cameraFov
        End Get
        Set(value As Double)
            SetProperty(_cameraFov, value)
        End Set
    End Property

    Private Sub UpdateCamera()

        'Clamp pitch
        _pitchDeg = Clamp(_pitchDeg, -89.0, 89.0)
        _distance = Clamp(_distance, 1.5, 10.0)

        Dim yaw As Double = _yawDeg * Math.PI / 180.0
        Dim pitch As Double = _pitchDeg * Math.PI / 180.0

        'Orbit um Ursprung
        Dim x As Double = _distance * Math.Cos(pitch) * Math.Sin(yaw)
        Dim y As Double = _distance * Math.Sin(pitch)
        Dim z As Double = _distance * Math.Cos(pitch) * Math.Cos(yaw)

        Dim pos As New Point3D(x, y, z)

        CameraPosition = pos
        CameraLookDirection = New Vector3D(-x, -y, -z)  'Auf Ursprung schauen
        CameraUpDirection = New Vector3D(0, 1, 0)

    End Sub

    Private Sub ResetCamera()
        _yawDeg = 90.0
        _pitchDeg = 0.0
        _distance = 3.0
        UpdateCamera()
    End Sub

#End Region

#Region "Caching"

    Private _baseGeo As GeometryModel3D
    Private _group As Model3DGroup

    Private Structure SphereKey
        Public LonSegments As Integer
        Public LatSegments As Integer
    End Structure

    Private Structure DisplacedSphereKey
        Public LonSeg As Integer
        Public LatSeg As Integer
        Public Exaggeration As Integer      'quantisiert
        Public Sampling As ResamplingMode
        Public IncludeBathymetry As Boolean
    End Structure

    Private Structure MaterialKey
        Public Img As ImageSource
        Public Hq As Boolean
        Public Grid As Boolean
    End Structure

    Private ReadOnly _cacheGate As New Object()

    'Sphere-Cache
    Private ReadOnly _sphereCache As New Dictionary(Of SphereKey, MeshGeometry3D)
    Private ReadOnly _displacedCache As New Dictionary(Of DisplacedSphereKey, MeshGeometry3D)

    'Material-Cache
    Private ReadOnly _materialCache As New Dictionary(Of MaterialKey, Material)

    Private ReadOnly _fallbackMaterialHQ As Material = CreateFallbackMaterial(hq:=True)
    Private ReadOnly _fallbackMaterialLQ As Material = CreateFallbackMaterial(hq:=False)

    Private _isLowSpecActive As Boolean

    Private Shared Function CreateFallbackMaterial(hq As Boolean) As Material

        Dim b As New SolidColorBrush(Color.FromRgb(40, 40, 40))
        If b.CanFreeze Then b.Freeze()

        If Not hq Then
            Dim m As New DiffuseMaterial(b)
            If m.CanFreeze Then m.Freeze()
            Return m
        End If

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(b))

        Dim sb As New SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
        If sb.CanFreeze Then sb.Freeze()
        mg.Children.Add(New SpecularMaterial(sb, 50))

        If mg.CanFreeze Then mg.Freeze()
        Return mg

    End Function

    Private _cacheSize As String = "0 MB"
    Public Property CacheSize As String
        Get
            Return _cacheSize
        End Get
        Set(value As String)
            SetProperty(_cacheSize, value)
        End Set
    End Property

    Private Sub ClearCache()
        SyncLock _cacheGate
            _sphereCache.Clear()
            _displacedCache.Clear()
            _materialCache.Clear()
        End SyncLock

        UpdateCacheSize()

        'Nach Cache-Leerung neu rendern, damit kein leeres Ergebnis entsteht
        QueueRender("Cache geleert")
    End Sub

    Private Sub UpdateCacheSize()
        Dim bytes As Long = 0

        SyncLock _cacheGate

            For Each m As MeshGeometry3D In _sphereCache.Values
                bytes += EstimateMeshBytes(m)
            Next
            For Each m As MeshGeometry3D In _displacedCache.Values
                bytes += EstimateMeshBytes(m)
            Next

            'Materials nur grob weil vernachlässigbar
            bytes += (_materialCache.Count) * 4096L
        End SyncLock

        CacheSize = FormatBytes(bytes)
    End Sub

    Private Shared Function EstimateMeshBytes(mesh As MeshGeometry3D) As Long

        If mesh Is Nothing Then Return 0

        Dim bytes As Long = 0

        'Point3D = 3 * Double (24 bytes)
        If mesh.Positions IsNot Nothing Then bytes += CLng(mesh.Positions.Count) * 24L

        'Vector3D = 3 * Double (24 bytes)
        If mesh.Normals IsNot Nothing Then bytes += CLng(mesh.Normals.Count) * 24L

        'Point = 2 * Double (16 bytes)
        If mesh.TextureCoordinates IsNot Nothing Then bytes += CLng(mesh.TextureCoordinates.Count) * 16L

        'Int32 (4 bytes)
        If mesh.TriangleIndices IsNot Nothing Then bytes += CLng(mesh.TriangleIndices.Count) * 4L

        Return bytes

    End Function

    Private Shared Function FormatBytes(bytes As Long) As String
        Dim mb As Double = bytes / (1024 * 1024)
        If mb < 0.95 Then Return $"{mb:0.0} MB"
        Return $"{mb:0.0} MB"
    End Function
#End Region

#Region "Globus-Modell"

    Private _globeModel As Model3D
    Public Property GlobeModel As Model3D
        Get
            Return _globeModel
        End Get
        Set(value As Model3D)
            SetProperty(_globeModel, value)
        End Set
    End Property

    Private Structure MeshRes
        Public Lon As Integer
        Public Lat As Integer
    End Structure

    Private _meshResolution As Integer = 2
    Public Property MeshResolution As Integer
        Get
            Return _meshResolution
        End Get
        Set(value As Integer)
            value = Clamp(value, 0, 4)

            If Not SetProperty(_meshResolution, value) Then Return

            'Ziel-Auflösung aus Tick ableiten
            Dim res As MeshRes = MapResolution(value)

            'Nur setzen, wenn wirklich anders um unnötige Renders zu vermeiden
            If _lonSegments <> res.Lon OrElse _latSegments <> res.Lat Then
                LonSegments = res.Lon
                LatSegments = res.Lat
            End If

        End Set
    End Property

    Private _lonSegments As Integer = 512
    Public Property LonSegments As Integer
        Get
            Return _lonSegments
        End Get
        Set(value As Integer)
            value = Clamp(value, 8, 2048)
            If SetProperty(_lonSegments, value) Then
                QueueRender("Auflösung geändert")
            End If
        End Set
    End Property

    Private _latSegments As Integer = 256
    Public Property LatSegments As Integer
        Get
            Return _latSegments
        End Get
        Set(value As Integer)
            value = Clamp(value, 6, 1024)
            If SetProperty(_latSegments, value) Then
                QueueRender("Auflösung geändert")
            End If
        End Set
    End Property

    Private Shared Function MapResolution(tick As Integer) As MeshRes
        Dim lon As Integer = CInt(128 * (2 ^ tick))
        Dim lat As Integer = CInt(64 * (2 ^ tick))
        Return New MeshRes With {.Lon = lon, .Lat = lat}
    End Function

#End Region

#Region "Pol-Achs-Modell"

    Private _axisGeo As GeometryModel3D
    Private _axisVisible As Boolean

    Private _showAxis As Boolean
    Public Property ShowAxis As Boolean
        Get
            Return _showAxis
        End Get
        Set(value As Boolean)
            If SetProperty(_showAxis, value) Then
                UpdateAxisModel()
            End If
        End Set
    End Property

    Private Sub UpdateAxisModel()

        If _group Is Nothing OrElse _axisGeo Is Nothing Then Return

        If ShowAxis Then
            If Not _axisVisible Then
                _group.Children.Add(_axisGeo)
                _axisVisible = True
            End If
        Else
            If _axisVisible Then
                _group.Children.Remove(_axisGeo)
                _axisVisible = False
            End If
        End If
    End Sub

#End Region

#Region "Displacement-Mapping"

    Private _showDisplacement As Boolean = True
    Public Property ShowDisplacement As Boolean
        Get
            Return _showDisplacement
        End Get
        Set(value As Boolean)
            If SetProperty(_showDisplacement, value) Then
                QueueRender("Displacementmapping geändert")
            End If
        End Set
    End Property

    Private _displacementExaggeration As Double = 10.0
    Public Property DisplacementExaggeration As Double
        Get
            Return _displacementExaggeration
        End Get
        Set(value As Double)
            value = Clamp(value, 0.0, 100.0)          'Displacement-Faktor auf maximal 100.0 begrenzen, 0 schaltet das Displacement quasi ab
            If SetProperty(_displacementExaggeration, value) Then
                QueueRender("Displacementmapping geändert")
            End If
        End Set
    End Property

    Private _heightSamping As ResamplingMode = ResamplingMode.Bilinear
    Public Property HeightSampling As ResamplingMode
        Get
            Return _heightSamping
        End Get
        Set(value As ResamplingMode)
            If SetProperty(_heightSamping, value) Then
                QueueRender("Resampling geändert")
            End If
        End Set
    End Property

#End Region

#Region "Layer"

    Public Enum GlobeBaseLayer
        Topo
        LandMask
        Tid
    End Enum

    Private _selectedBaseLayer As GlobeBaseLayer
    Public Property SelectedBaseLayer As GlobeBaseLayer
        Get
            Return _selectedBaseLayer
        End Get
        Set(value As GlobeBaseLayer)
            If SetProperty(_selectedBaseLayer, value) Then
                RefreshOverlayAvailability()
                ApplyInteractionMaterialState(force:=True)
            End If
        End Set
    End Property

    Public ReadOnly Property TopoLayer As ImageSource
        Get
            Return _p?.Topo
        End Get
    End Property

    Private _isCompositingDone As Boolean

    Private _topoWithReliefLayer As ImageSource
    Public Property TopoWithReliefLayer As ImageSource
        Get
            Return _topoWithReliefLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_topoWithReliefLayer, value)
        End Set
    End Property

    Public ReadOnly Property LandMaskLayer As ImageSource
        Get
            Return _p?.LandMask
        End Get
    End Property

    Private _landMaskWithShoreLinesLayer As ImageSource
    Public Property LandMaskWithShoreLinesLayer As ImageSource
        Get
            Return _landMaskWithShoreLinesLayer
        End Get
        Set(value As ImageSource)
            SetProperty(_landMaskWithShoreLinesLayer, value)
        End Set
    End Property

    Public ReadOnly Property TidLayer As ImageSource
        Get
            Return _p?.Tid
        End Get
    End Property

    Private _showOverlay As Boolean
    Public Property ShowOverlay As Boolean
        Get
            Return _showOverlay
        End Get
        Set(value As Boolean)
            If SetProperty(_showOverlay, value) Then
                RefreshOverlayAvailability()

                If _showOverlay Then
                    If Not _isCompositingDone Then
                        EnsureCompositeAndRefreshMaterial()
                    Else
                        ApplyInteractionMaterialState(force:=True)
                    End If
                Else
                    ApplyInteractionMaterialState(force:=True)
                End If
            End If
        End Set
    End Property


    Private _isReliefOptionAvailable As Boolean
    Public Property IsReliefOptionAvailable As Boolean
        Get
            Return _isReliefOptionAvailable
        End Get
        Set(value As Boolean)
            SetProperty(_isReliefOptionAvailable, value)
        End Set
    End Property

    Private _isShoreLinesOptionAvailable As Boolean
    Public Property IsShoreLinesOptionAvailable As Boolean
        Get
            Return _isShoreLinesOptionAvailable
        End Get
        Set(value As Boolean)
            SetProperty(_isShoreLinesOptionAvailable, value)
        End Set
    End Property

    Public ReadOnly Property HeightMap As Single()
        Get
            Return _p.HeightMap
        End Get
    End Property

    Private Sub RefreshOverlayAvailability()

        IsReliefOptionAvailable = (SelectedBaseLayer = GlobeBaseLayer.Topo)
        IsShoreLinesOptionAvailable = (SelectedBaseLayer = GlobeBaseLayer.LandMask)

        Dim overlayAllowed As Boolean =
            (SelectedBaseLayer = GlobeBaseLayer.Topo AndAlso TopoWithReliefLayer IsNot Nothing) OrElse
            (SelectedBaseLayer = GlobeBaseLayer.LandMask AndAlso LandMaskWithShoreLinesLayer IsNot Nothing)

        If Not overlayAllowed AndAlso ShowOverlay Then
            _showOverlay = False
            OnPropertyChanged(NameOf(ShowOverlay))
        End If
    End Sub

    Private Function GetBaseImageForMaterial() As ImageSource
        Select Case SelectedBaseLayer
            Case GlobeBaseLayer.Topo
                If ShowOverlay Then
                    Return If(TopoWithReliefLayer, TopoLayer)
                Else
                    Return TopoLayer
                End If

            Case GlobeBaseLayer.LandMask
                If ShowOverlay Then
                    Return If(LandMaskWithShoreLinesLayer, LandMaskLayer)
                Else
                    Return LandMaskLayer
                End If

            Case GlobeBaseLayer.Tid
                Return TidLayer
        End Select

        Return Nothing
    End Function

#End Region

#Region "Gitternetz-Layer"

    Private _gridBuildTask As Task = Nothing
    Private _gridOverlayImage As ImageSource
    Private _gridOverlayBrush As Brush

    Private _showGrid As Boolean
    Public Property ShowGrid As Boolean
        Get
            Return _showGrid
        End Get
        Set(value As Boolean)
            If Not SetProperty(_showGrid, value) Then Return

            If _showGrid Then
                Dim ignore As Task = EnsureGridAsync().ContinueWith(Sub(t)
                                                                        Application.Current.Dispatcher.Invoke(Sub()
                                                                                                                  ApplyInteractionMaterialState(force:=True)
                                                                                                              End Sub)
                                                                    End Sub)
            Else
                ApplyInteractionMaterialState(force:=True)
            End If
        End Set
    End Property

    Private Async Function GenerateGridAsync() As Task
        IsRendering = True
        RenderingTitle = "Texturen werden vorbereitet..."
        RenderingMessage = "Erzeuge Gitternetz..."

        Try

            Await Application.Current.Dispatcher.InvokeAsync(Sub()

                                                                 _gridOverlayImage = GeoGridOverlay3D.BuildGridOverlayImage(4096, 2048)

                                                                 Dim gb As New ImageBrush(_gridOverlayImage) With {
                                                                        .Stretch = Stretch.Fill,
                                                                        .AlignmentX = AlignmentX.Center,
                                                                        .AlignmentY = AlignmentY.Center
                                                                 }

                                                                 RenderOptions.SetBitmapScalingMode(gb, BitmapScalingMode.NearestNeighbor)
                                                                 If gb.CanFreeze() Then gb.Freeze()
                                                                 _gridOverlayBrush = gb


                                                             End Sub)

        Finally

            IsRendering = False
            RenderingMessage = ""

        End Try

    End Function

    Private Function EnsureGridAsync() As Task
        If _gridOverlayBrush IsNot Nothing Then Return Task.CompletedTask

        If _gridBuildTask IsNot Nothing Then Return _gridBuildTask

        _gridBuildTask = GenerateGridAsync()
        Return _gridBuildTask
    End Function
#End Region

#Region "Commands"

    Public ReadOnly Property BeginRotateCommand As ICommand
    Public ReadOnly Property RotateCommand As ICommand
    Public ReadOnly Property EndRotateCommand As ICommand
    Public ReadOnly Property ZoomCommand As ICommand
    Public ReadOnly Property ViewportChangedCommand As ICommand

    Public ReadOnly Property MouseMoveCommand As ICommand
    Public ReadOnly Property MouseDownCommand As ICommand
    Public ReadOnly Property MouseUpCommand As ICommand
    Public ReadOnly Property MouseLeaveCommand As ICommand

    Public ReadOnly Property ResetCameraCommand As ICommand
    Public ReadOnly Property ClearCacheCommand As ICommand

    Public Event RequestSetUtc As EventHandler
    Public ReadOnly Property SetSimulationUtcCommand As ICommand

    Private Sub BeginRotate(r As PanRequest)

        _isDragging = True
        _dragStart = r.MousePos
        _yawStart = _yawDeg
        _pitchStart = _pitchDeg

        _lastDragCameraTicks = 0
        UpdateCamera()
        ApplyInteractionMaterialState(force:=True)
    End Sub

    Private Sub Rotate(r As PanRequest)

        If Not _isDragging Then Return

        Dim dx As Double = r.MousePos.X - _dragStart.X
        Dim dy As Double = r.MousePos.Y - _dragStart.Y

        ' "Gefühl" feinjustieren
        Const degPerPixel As Double = 0.25

        _yawDeg = _yawStart + dx * degPerPixel
        _pitchDeg = _pitchStart - dy * degPerPixel

        'Kamera-Updates drosseln
        Dim nowTicks As Long = Stopwatch.GetTimestamp()
        If (nowTicks - _lastDragCameraTicks) >= _tickPerFrame Then
            _lastDragCameraTicks = nowTicks
            UpdateCamera()
        End If

    End Sub

    Private Sub EndRotate()
        _isDragging = False

        _lastDragCameraTicks = 0
        UpdateCamera()
        ApplyInteractionMaterialState(force:=True)
    End Sub

    Private Sub Zoom(z As ZoomRequest)


        'Wheeldelta: + rein, - raus
        Dim factor As Double = If(z.Delta > 0, 0.9, 1.0 / 0.9)
        _distance *= factor

        UpdateCamera()

    End Sub

    Private Sub OnViewportChanged(v As ViewportChangedRequest)

        If v Is Nothing Then Return
        _viewportW = Math.Max(1, v.ViewPortSize.Width)
        _viewportH = Math.Max(1, v.ViewPortSize.Height)

    End Sub

#Disable Warning IDE0060
#Disable Warning CA1822

    Private Sub OnMouseMove(r As MapMouseMoveRequest)

        If r Is Nothing Then Return

        Dim nowTicks As Long = Stopwatch.GetTimestamp()
        If (nowTicks - _lastHoverTicks) >= _hoverTickPerFrame Then
            _lastMouseInViewport = True
            _lastMousePos = r.MousePos
            _lastHoverTicks = nowTicks
            UpdateHoverFromScreenPoint(_lastMousePos)
        End If
    End Sub

    Private Sub OnMouseDown(r As MapMouseDownRequest)
        'später evtl Pick/Info
    End Sub

    Private Sub OnMouseUp(r As MapMouseUpRequest)
        'später evtl. Ende Pick/Info
    End Sub

    Private Sub OnMouseLeave()
        _lastMouseInViewport = False
        ClearHoverText()
    End Sub

#Enable Warning IDE0060
#Enable Warning CA1822

#End Region


    Public Sub New(payload As GlobePreviewPayload)

        _p = payload

        'Commands initialisieren
        BeginRotateCommand = New RelayCommand(Of PanRequest)(Sub(r) BeginRotate(r))
        RotateCommand = New RelayCommand(Of PanRequest)(Sub(r) Rotate(r))
        EndRotateCommand = New RelayCommand(Of Object)(Sub(o) EndRotate())

        ZoomCommand = New RelayCommand(Of ZoomRequest)(Sub(z) Zoom(z))
        ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(Sub(v) OnViewportChanged(v))

        MouseMoveCommand = New RelayCommand(Of MapMouseMoveRequest)(Sub(r) OnMouseMove(r))
        MouseDownCommand = New RelayCommand(Of MapMouseDownRequest)(Sub(r) OnMouseDown(r))
        MouseUpCommand = New RelayCommand(Of MapMouseUpRequest)(Sub(r) OnMouseUp(r))
        MouseLeaveCommand = New RelayCommand(Of Object)(Sub(r) OnMouseLeave())

        ResetCameraCommand = New RelayCommand(Of Object)(Sub(o) ResetCamera())
        ClearCacheCommand = New RelayCommand(Of Object)(Sub(o) ClearCache())

        SetSimulationUtcCommand = New RelayCommand(Of Object)(Sub(o) RaiseEvent RequestSetUtc(Me, EventArgs.Empty))

        'Defaults setzen:
        _selectedBaseLayer = If(_p?.Topo IsNot Nothing, GlobeBaseLayer.Topo,
                             If(_p?.LandMask IsNot Nothing, GlobeBaseLayer.LandMask,
                             GlobeBaseLayer.Tid))
        MeshResolution = 2
        OnPropertyChanged(NameOf(MeshResolution))
        _showAxis = False

        ' Default Overlay: an, wenn für den Start-Layer ein Overlay existiert
        _showOverlay =
        (_selectedBaseLayer = GlobeBaseLayer.Topo AndAlso _p?.ReliefOverlay IsNot Nothing) OrElse
        (_selectedBaseLayer = GlobeBaseLayer.LandMask AndAlso _p?.ShoreLinesOverlay IsNot Nothing)

        _showGrid = False

        'Intiales Setup
        EnsureModelCreated()
        UpdateAxisModel()
        RefreshOverlayAvailability()
        UpdateCamera()
        StartInitialRender()
        UpdateCacheSize()
    End Sub

    Public Async Sub StartInitialRender()
        Await EnsureCompositedLayerAsync()
        QueueRender("Initial")
    End Sub

    Public Sub AttachViewport(vp As Viewport3D)
        _viewport = vp
    End Sub

#Region "Model-Building"

    Private Sub QueueRender(reason As String)

        Dim ct As CancellationToken

        SyncLock _renderGate
            _renderCts?.Cancel()
            _renderCts?.Dispose()
            _renderCts = New CancellationTokenSource()
            ct = _renderCts.Token
        End SyncLock

        'Overlay an
        IsRendering = True
        RenderingTitle = "Globus wird gerendert..."
        RenderingMessage = If(reason = "Initial", "Erzeuge Mesh...", $"Neuaufbau ({reason})...")

        Dim ignore As Task = RenderGlobeAsync(ct)
    End Sub

    Private Function GetSphereMeshFor(lon As Integer, lat As Integer) As MeshGeometry3D

        Dim key As New SphereKey With {
            .LonSegments = lon,
            .LatSegments = lat
        }

        SyncLock _cacheGate
            Dim cached As MeshGeometry3D = Nothing
            If _sphereCache.TryGetValue(key, cached) Then Return cached
        End SyncLock

        'Build außerhalb Lock
        Dim mesh = GlobeMeshRenderer.RenderSphereMesh(radius:=1.0, lonSegments:=lon, latSegments:=lat)
        If mesh.CanFreeze Then mesh.Freeze()

        'Mesh cachen
        SyncLock _cacheGate
            Dim cached As MeshGeometry3D = Nothing
            If _sphereCache.TryGetValue(key, cached) Then Return cached
            _sphereCache(key) = mesh
            Return mesh
        End SyncLock

    End Function

    Private Function GetDisplacedMeshFor(lon As Integer, lat As Integer, baseSphere As MeshGeometry3D) As MeshGeometry3D

        Dim key As New DisplacedSphereKey With {
            .LonSeg = lon,
            .LatSeg = lat,
            .Exaggeration = CInt(Math.Round(DisplacementExaggeration * 10)),
            .Sampling = HeightSampling,
            .IncludeBathymetry = True
        }

        SyncLock _cacheGate
            Dim cached As MeshGeometry3D = Nothing
            If _displacedCache.TryGetValue(key, cached) Then Return cached
        End SyncLock

        Dim mesh = GlobeMeshRenderer.RenderDisplacedMesh(
            source:=baseSphere,
            height:=HeightMap,
            w:=_p.CacheMeta.LonCount,
            h:=_p.CacheMeta.LatCount,
            exaggeration:=DisplacementExaggeration,
            includeBathymetry:=True,
            resamplingMode:=HeightSampling)

        If mesh.CanFreeze Then mesh.Freeze()

        SyncLock _cacheGate
            Dim cached As MeshGeometry3D = Nothing
            If _displacedCache.TryGetValue(key, cached) Then Return cached
            _displacedCache(key) = mesh
            Return mesh
        End SyncLock

    End Function

    Private Async Function RenderGlobeAsync(ct As CancellationToken) As Task

        Dim mesh As MeshGeometry3D = Nothing
        Dim errTitle As String = Nothing
        Dim errMsg As String = Nothing
        Dim wasCancelled As Boolean = False

        Try
            Dim lon As Integer = LonSegments
            Dim lat As Integer = LatSegments
            Dim useDisp As Boolean = (ShowDisplacement AndAlso DisplacementExaggeration > 0.0001 AndAlso HeightMap IsNot Nothing)

            RenderingMessage = "Erzeuge Globus.."

            mesh = Await Task.Run(Function()

                                      ct.ThrowIfCancellationRequested()

                                      '1) Sphere
                                      Dim sphere = GetSphereMeshFor(lon, lat)
                                      ct.ThrowIfCancellationRequested()

                                      If Not useDisp Then Return sphere

                                      '2) Displacement
                                      Return GetDisplacedMeshFor(lon, lat, sphere)

                                  End Function, ct)

            ct.ThrowIfCancellationRequested()

        Catch ex As OperationCanceledException
            'normaler Cancel
            wasCancelled = True
        Catch ex As Exception
            'Overlay bleibt an und zeigt Fehler
            errTitle = "Render-Fehler"
            errMsg = ex.Message
        End Try

        If wasCancelled Then Return

        'Wenn nicht abgebrochen wurde: Render-Ergebnis an UI-Thread übergeben
        Await Application.Current.Dispatcher.InvokeAsync(Sub()

                                                             If errTitle IsNot Nothing Then
                                                                 RenderingTitle = errTitle
                                                                 RenderingMessage = errMsg
                                                                 IsRendering = True
                                                                 Return
                                                             End If

                                                             _baseGeo.Geometry = mesh
                                                             ApplyInteractionMaterialState(force:=True)
                                                             UpdateCacheSize()

                                                             IsRendering = False
                                                             RenderingMessage = ""
                                                         End Sub)

    End Function

    Private Shared Function CreateAxisModel() As GeometryModel3D

        'Globus-Radius = 1.0 -> Achse soll etwas überstehen
        Dim protrude As Double = 0.2
        Dim height As Double = 2.0 * (1.0 + protrude)
        Dim radius As Double = 0.001

        Dim mesh As MeshGeometry3D = GlobeMeshRenderer.RenderCylinderMeshY(radius, height, segments:=8, cap:=True)

        Dim brush As New SolidColorBrush(Color.FromRgb(255, 255, 255))
        If brush.CanFreeze Then brush.Freeze()

        Dim mat As New EmissiveMaterial(brush)
        If mat.CanFreeze Then mat.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mat,
            .BackMaterial = mat
        }

        If gm.CanFreeze Then gm.Freeze()

        Return gm

    End Function

    Private Function BuildImageMaterial(img As ImageSource, hq As Boolean, grid As Boolean) As Material

        Dim ib As New ImageBrush(img) With {
                .Stretch = Stretch.Fill,
                .TileMode = TileMode.Tile,
                .AlignmentX = AlignmentX.Center,
                .AlignmentY = AlignmentY.Center
            }
        RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.NearestNeighbor)
        If ib.CanFreeze() Then ib.Freeze()

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(ib))

        If hq Then
            Dim sb As New SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
            If sb.CanFreeze Then sb.Freeze()
            mg.Children.Add(New SpecularMaterial(sb, 50))
        End If

        If grid AndAlso _gridOverlayBrush IsNot Nothing Then
            'Emissive: unabhängig vom Licht immer gut sichtbar und konstant
            mg.Children.Add(New EmissiveMaterial(_gridOverlayBrush))
        End If

        If mg.CanFreeze Then mg.Freeze()
        Return mg
    End Function

    Private Function GetMaterial(img As ImageSource, hq As Boolean, grid As Boolean) As Material

        If img Is Nothing Then Return If(hq, _fallbackMaterialHQ, _fallbackMaterialLQ)

        Dim key As New MaterialKey With {
            .Img = img,
            .Hq = hq,
            .Grid = grid
        }

        Dim m As Material = Nothing
        If _materialCache.TryGetValue(key, m) Then Return m

        m = BuildImageMaterial(img, hq, grid)
        _materialCache(key) = m
        Return m
    End Function

    Private Sub ApplyInteractionMaterialState(Optional force As Boolean = False)

        'Soll LQ gerade aktiv sein?
        Dim wantLowSpec As Boolean = _isDragging

        If Not force AndAlso wantLowSpec = _isLowSpecActive AndAlso Not ShowGrid Then Return
        _isLowSpecActive = wantLowSpec

        'Aktuelles Base-Image bestimmen
        Dim baseImg As ImageSource = GetBaseImageForMaterial()

        Dim mat As Material = GetMaterial(baseImg, hq:=Not wantLowSpec, grid:=ShowGrid)

        _baseGeo.Material = mat
        _baseGeo.BackMaterial = mat

    End Sub

    Private Sub EnsureModelCreated()

        If _group IsNot Nothing Then Return

        _group = New Model3DGroup

        _baseGeo = New GeometryModel3D()

        _group.Children.Add(_baseGeo)

        'Achse vorbereiten, aber noch nicht hinzufügen
        _axisGeo = CreateAxisModel()

        'Subsolar-Marker vorbereiten
        _subsolarGeo = GlobeMeshRenderer.RenderSubsolarMarkerModel()
        _subsolarTf = New TranslateTransform3D(0, 0, 0)
        _subsolarGeo.Transform = _subsolarTf
        _subsolarVisible = False

        'Tag-Nacht-Terminator vorbereiten
        _dayNightTerminatorGeo = GlobeMeshRenderer.RenderDayNightTerminatorModel()
        _dayNightTerminatorVisible = False

        'Dawn-Zone vorbereiten
        _dawnZoneGeo = GlobeMeshRenderer.RenderDawnModel()
        _dawnTerminatorVisible = False

        'Initial die Marker updaten
        UpdateSubsolarMarkerVisibility()
        UpdateDayNightVisibility()

        'Transforms: Spin (um Y) + Tilt (um X)
        _spinRot = New AxisAngleRotation3D(New Vector3D(0, 1, 0), 0.0)
        _tiltRot = New AxisAngleRotation3D(New Vector3D(1, 0, 0), ClimateConstants.EarthObliquityDeg)  'Erdachsneigung

        Dim spinTf As New RotateTransform3D(_spinRot)
        Dim tiltTf As New RotateTransform3D(_tiltRot)

        _animTransform = New Transform3DGroup
        'Wichtig: erst Spin, dann Tilt -> Spin-Achse wird mitgeneigt
        _animTransform.Children.Add(spinTf)
        _animTransform.Children.Add(tiltTf)

        _group.Transform = _animTransform

        GlobeModel = _group
    End Sub

    Private Async Function EnsureCompositedLayerAsync() As Task

        'nur nötig, wenn überhaupt Overlay existiert
        Dim needTopo As Boolean = (_p?.Topo IsNot Nothing AndAlso _p?.ReliefOverlay IsNot Nothing)
        Dim needLm As Boolean = (_p?.LandMask IsNot Nothing AndAlso _p?.ShoreLinesOverlay IsNot Nothing)

        If Not needTopo AndAlso Not needLm Then
            _isCompositingDone = True
            Return
        End If

        IsRendering = True
        RenderingTitle = "Texturen werden vorbereitet..."
        RenderingMessage = "Komponiere Overlays..."

        Dim topoResult As ImageSource = Nothing
        Dim lmResult As ImageSource = Nothing
        Dim err As Exception = Nothing

        Try

            Await Task.Run(Sub()

                               If needTopo Then
                                   topoResult = ComposeOver(_p.Topo, _p.ReliefOverlay, overlayOpacity:=1.0)
                                   FreezeIfPossible(topoResult)
                               End If

                               If needLm Then
                                   lmResult = ComposeOver(_p.LandMask, _p.ShoreLinesOverlay, overlayOpacity:=1.0)
                                   FreezeIfPossible(lmResult)
                               End If

                           End Sub)

        Catch ex As Exception
            err = ex
        End Try

        'An UI übergeben
        If err IsNot Nothing Then
            RenderingTitle = "Fehler"
            RenderingMessage = err.Message
            IsRendering = True
            Return
        End If

        TopoWithReliefLayer = topoResult
        LandMaskWithShoreLinesLayer = lmResult
        _isCompositingDone = True

        _materialCache.Clear()

        UpdateCacheSize()

        RenderingTitle = "Globus wird gerendert..."
        RenderingMessage = "Erzeuge Mesh..."
    End Function

    Private Shared Sub FreezeIfPossible(obj As Object)
        Dim f As Freezable = TryCast(obj, Freezable)
        If f IsNot Nothing AndAlso f.CanFreeze Then f.Freeze()
    End Sub

    Private Async Sub EnsureCompositeAndRefreshMaterial()
        Await EnsureCompositedLayerAsync()
        ApplyInteractionMaterialState(force:=True)
    End Sub
#End Region

#Region "Animation"

    Public Enum GlobeFrameMode
        EarthFixed          'Erde steht fest
        RotatingEarth       'Erde rotiert täglich
    End Enum

    Private _frameMode As GlobeFrameMode = GlobeFrameMode.RotatingEarth
    Public Property FrameMode As GlobeFrameMode
        Get
            Return _frameMode
        End Get
        Set(value As GlobeFrameMode)
            If SetProperty(_frameMode, value) Then
                If _isAnimating Then
                    _lastFrameTicks = Stopwatch.GetTimestamp()
                End If
            End If
        End Set
    End Property

    Private _isAnimating As Boolean
    Private _lastFrameTicks As Long

    Private _gmst0Deg As Double

    Private _spinRot As AxisAngleRotation3D
    Private _tiltRot As AxisAngleRotation3D
    Private _animTransform As Transform3DGroup

    Private _simStartUtc As DateTime
    Private _simSecondsTotal As Double

    Private _subsolarGeo As GeometryModel3D
    Private _subsolarTf As TranslateTransform3D
    Private _dayNightTerminatorGeo As GeometryModel3D
    Private _dawnZoneGeo As GeometryModel3D
    Private _subsolarVisible As Boolean
    Private _dayNightTerminatorVisible As Boolean
    Private _dawnTerminatorVisible As Boolean

    Private _isEarthRotationEnabled As Boolean
    Public Property IsEarthRotationEnabled As Boolean
        Get
            Return _isEarthRotationEnabled
        End Get
        Set(value As Boolean)
            If SetProperty(_isEarthRotationEnabled, value) Then
                If value Then
                    StartAnimation()
                Else
                    StopAnimation()
                End If
            End If
        End Set
    End Property

    Private _showSubsolarMarker As Boolean = False
    Public Property ShowSubsolarMarker As Boolean
        Get
            Return _showSubsolarMarker
        End Get
        Set(value As Boolean)
            If SetProperty(_showSubsolarMarker, value) Then
                UpdateSubsolarMarkerVisibility()
            End If
        End Set
    End Property

    Private _showDayNightTerminator As Boolean = False
    Public Property ShowDayNightTerminator As Boolean
        Get
            Return _showDayNightTerminator
        End Get
        Set(value As Boolean)
            If SetProperty(_showDayNightTerminator, value) Then
                UpdateDayNightVisibility()
                OnPropertyChanged(NameOf(ShowDawnZone))
            End If
        End Set
    End Property

    Private _showDawnZone As Boolean = False
    Public Property ShowDawnZone As Boolean
        Get
            Return _showDawnZone
        End Get
        Set(value As Boolean)
            If SetProperty(_showDawnZone, value) Then
                UpdateDayNightVisibility()
            End If
        End Set
    End Property

    Private _animationSpeedTick As Integer = 2
    Public Property AnimationSpeedTick As Integer
        Get
            Return _animationSpeedTick
        End Get
        Set(value As Integer)
            value = Clamp(value, -6, 6)
            If SetProperty(_animationSpeedTick, value) Then
                OnPropertyChanged(NameOf(AnimationSpeedText))
            End If
        End Set
    End Property

    Public ReadOnly Property AnimationSpeedText As String
        Get
            Dim absTick As Integer = Math.Abs(_animationSpeedTick)
            Dim f As Double = Math.Pow(10, absTick)          '10er-Potenz als Faktor
            Dim dir As String = If(_animationSpeedTick < 0, "-", "")
            Return dir & f.ToString("N0", CultureInfo.CurrentCulture) & " x"
        End Get
    End Property

    Private _sunDirection As Vector3D = New Vector3D(-0.6, -0.3, -1)
    Public Property SunDirection As Vector3D
        Get
            Return _sunDirection
        End Get
        Set(value As Vector3D)
            SetProperty(_sunDirection, value)
        End Set
    End Property

    Private _solarDeclinationDeg As Double
    Public Property SolarDeclinationDeg As Double
        Get
            Return _solarDeclinationDeg
        End Get
        Set(value As Double)
            If SetProperty(_solarDeclinationDeg, value) Then
                OnPropertyChanged(NameOf(SolarDeclinationText))
            End If
        End Set
    End Property

    Public ReadOnly Property SolarDeclinationText As String
        Get
            If Not _isAnimating Then Return ""
            Return $"{SolarDeclinationDeg:+0.00;-0.00;0.00}°"
        End Get
    End Property

    Private _subsolarLatDeg As Double
    Public Property SubsolarLatDeg As Double
        Get
            Return _subsolarLatDeg
        End Get
        Set(value As Double)
            If SetProperty(_subsolarLatDeg, value) Then
                OnPropertyChanged(NameOf(SubsolarText))
            End If
        End Set
    End Property

    Private _subsolarLonDeg As Double
    Public Property SubsolarLonDeg As Double
        Get
            Return _subsolarLonDeg
        End Get
        Set(value As Double)
            If SetProperty(_subsolarLonDeg, value) Then
                OnPropertyChanged(NameOf(SubsolarText))
            End If
        End Set
    End Property

    Public ReadOnly Property SubsolarText As String
        Get
            If Not _isAnimating Then Return ""
            Return $"Lat {SubsolarLatDeg:+0.00;-0.00;0.00}°, Lon {SubsolarLonDeg:+0.00;-0.00;0.00}°"
        End Get
    End Property

    Private _simulationUtc As DateTime
    Public Property SimulationUtc As DateTime
        Get
            Return _simulationUtc
        End Get
        Set(value As DateTime)
            SetProperty(_simulationUtc, value)
            OnPropertyChanged(NameOf(SimulationUtcText))
        End Set
    End Property

    Private _ambientLightColor As Color = Color.FromRgb(100, 100, 100)
    Public Property AmbientLightColor As Color
        Get
            Return _ambientLightColor
        End Get
        Set(value As Color)
            SetProperty(_ambientLightColor, value)
        End Set
    End Property

    Public ReadOnly Property SimulationUtcText As String
        Get
            If SimulationUtc = DateTime.MinValue Then Return ""
            Return SimulationUtc.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) & " UTC"
        End Get
    End Property

    Private _sunRaDeg As Double
    Public Property SunRaDeg As Double
        Get
            Return _sunRaDeg
        End Get
        Set(value As Double)
            If SetProperty(_sunRaDeg, value) Then
                OnPropertyChanged(NameOf(SunRaText))
            End If
        End Set
    End Property
    Public ReadOnly Property SunRaText As String
        Get
            If Not _isAnimating Then Return ""
            Return $"{SunRaDeg:0.00}°"
        End Get
    End Property

    Private _gmstDeg As Double
    Public Property GmstDeg As Double
        Get
            Return _gmstDeg
        End Get
        Set(value As Double)
            If SetProperty(_gmstDeg, value) Then
                OnPropertyChanged(NameOf(GmstText))
            End If
        End Set
    End Property
    Public ReadOnly Property GmstText As String
        Get
            If Not _isAnimating Then Return ""
            Return $"{GmstDeg:0.000}°"
        End Get
    End Property

    Private _equationOfTimeMin As Double
    Public Property EquationOfTime As Double
        Get
            Return _equationOfTimeMin
        End Get
        Set(value As Double)
            If SetProperty(_equationOfTimeMin, value) Then
                OnPropertyChanged(NameOf(EquationOfTimeText))
            End If
        End Set
    End Property
    Public ReadOnly Property EquationOfTimeText As String
        Get
            If Not _isAnimating Then Return ""
            Return $"{EquationOfTime:+0.00;-0.00;0.00} min"
        End Get
    End Property

    Private Sub UpdateSubsolarMarker()
        If _subsolarTf Is Nothing Then Return

        Dim bodyV As Vector3D = Astronomics.BodyVectorFromLatLon(SubsolarLatDeg, SubsolarLonDeg)

        Dim r As Double = 1.02      'leicht über der Oberfläche anzeigen
        _subsolarTf.OffsetX = bodyV.X * r
        _subsolarTf.OffsetY = bodyV.Y * r
        _subsolarTf.OffsetZ = bodyV.Z * r

    End Sub

    Private Sub UpdateSubsolarMarkerFromSun()
        If _subsolarTf Is Nothing OrElse _animTransform Is Nothing Then Return

        Dim sunWorld As New Vector3D(-SunDirection.X, -SunDirection.Y, -SunDirection.Z)
        If sunWorld.LengthSquared > 0 Then sunWorld.Normalize() Else Return

        'World->Body
        Dim m As Matrix3D = _animTransform.Value
        If Not m.HasInverse Then Return
        m.Invert()

        Dim s As Vector3D = m.Transform(sunWorld)
        If s.LengthSquared > 0 Then s.Normalize() Else Return

        Dim r As Double = 1.02      'leicht über der Oberfläche anzeigen
        _subsolarTf.OffsetX = s.X * r
        _subsolarTf.OffsetY = s.Y * r
        _subsolarTf.OffsetZ = s.Z * r
    End Sub

    Private Sub UpdateSubsolarMarkerVisibility()
        If _group Is Nothing OrElse _subsolarGeo Is Nothing Then Return

        If ShowSubsolarMarker Then
            If Not _subsolarVisible Then
                _group.Children.Add(_subsolarGeo)
                _subsolarVisible = True
            End If
        Else
            If _subsolarVisible Then
                _group.Children.Remove(_subsolarGeo)
                _subsolarVisible = False
            End If
        End If
    End Sub

    Private Sub UpdateDayNightTerminator()
        If _dayNightTerminatorGeo Is Nothing Then Return

        'Sonnenrichtung in Body--Space (Erde->Sonne)
        Dim sun As New Vector3D(-SunDirection.X, -SunDirection.Y, -SunDirection.Z)
        If sun.LengthSquared > 0 Then sun.Normalize() Else Return

        Dim m As Matrix3D = _animTransform.Value
        If Not m.HasInverse Then Return
        m.Invert()

        Dim s As Vector3D = m.Transform(sun)
        If s.LengthSquared > 0 Then s.Normalize() Else Return

        'Basis u,v in der Ebene senkrecht zu s
        Dim up As New Vector3D(0, 1, 0)
        Dim u As Vector3D = Vector3D.CrossProduct(s, up)
        If u.LengthSquared < 0.00000001 Then
            u = Vector3D.CrossProduct(s, New Vector3D(1, 0, 0))
        End If
        u.Normalize()

        Dim v As Vector3D = Vector3D.CrossProduct(s, u)
        v.Normalize()

        Const N As Integer = 180
        Dim r As Double = 1.1
        Dim halfW As Double = 0.005 'Bandbreite

        Dim pos As New Point3DCollection((N + 1) * 2)
        Dim idx As New Int32Collection(N * 6)

        For i As Integer = 0 To N
            Dim t As Double = (i / CDbl(N)) * (2.0 * Math.PI)
            Dim c As Double = Math.Cos(t)
            Dim si As Double = Math.Sin(t)

            'Punkt auf Terminator-Großkreis
            Dim p As New Vector3D(u.X * c + v.X * si,
                                  u.Y * c + v.Y * si,
                                  u.Z * c + v.Z * si)
            p.Normalize()

            'Band-Offset in Tangentialrichtung ~ s (weil s x p = 0 auf Terminator)
            Dim p1 As Vector3D = New Vector3D(p.X * r + s.X * halfW, p.Y * r + s.Y * halfW, p.Z * r + s.Z * halfW)
            Dim p2 As Vector3D = New Vector3D(p.X * r - s.X * halfW, p.Y * r + s.Y * halfW, p.Z * r - s.Z * halfW)

            pos.Add(New Point3D(p1.X, p1.Y, p1.Z))
            pos.Add(New Point3D(p2.X, p2.Y, p2.Z))
        Next

        'Quads als 2 Dreiecke
        For i As Integer = 0 To N - 1
            Dim a0 As Integer = i * 2
            Dim a1 As Integer = a0 + 1
            Dim b0 As Integer = a0 + 2
            Dim b1 As Integer = a0 + 3

            idx.Add(a0) : idx.Add(b0) : idx.Add(a1)
            idx.Add(a1) : idx.Add(b0) : idx.Add(b1)
        Next

        Dim mesh As New MeshGeometry3D With {
            .Positions = pos,
            .TriangleIndices = idx
        }
        If mesh.CanFreeze Then mesh.Freeze()

        _dayNightTerminatorGeo.Geometry = mesh
    End Sub

    Private Sub UpdateDawnZone(Optional twilightNightDeg As Double = 6.0,
                               Optional twilightDayDeg As Double = 2.0)

        If _dawnZoneGeo Is Nothing Then Return
        If _animTransform Is Nothing Then Return

        'Sonnenrichtung in Bodyspace (Erde->Sonne)
        Dim sun As New Vector3D(-SunDirection.X, -SunDirection.Y, -SunDirection.Z)
        If sun.LengthSquared <= 0 Then Return
        sun.Normalize()

        Dim m As Matrix3D = _animTransform.Value
        If Not m.HasInverse Then Return
        m.Invert()

        Dim s As Vector3D = m.Transform(sun)
        If s.LengthSquared <= 0 Then Return
        s.Normalize()

        'Basis u,v in Ebene senkrecht zu s
        Dim up As New Vector3D(0, 1, 0)
        Dim u As Vector3D = Vector3D.CrossProduct(s, up)
        If u.LengthSquared < 0.000000000001 Then u = Vector3D.CrossProduct(s, New Vector3D(1, 0, 0))
        u.Normalize()

        Dim v As Vector3D = Vector3D.CrossProduct(s, u)
        v.Normalize()

        Dim aNight As Double = DegToRad(twilightNightDeg)
        Dim cosNight As Double = Math.Cos(aNight)
        Dim sinNight As Double = Math.Sin(aNight)
        Dim aDay As Double = DegToRad(twilightDayDeg)
        Dim cosDay As Double = Math.Cos(aDay)
        Dim sinDay As Double = Math.Sin(aDay)

        Const N As Integer = 180
        Dim r As Double = 1.1  'leicht über der Oberfläche

        Dim pos As New Point3DCollection((N + 1) * 2)
        Dim tc As New PointCollection((N + 1) * 2)
        Dim idx As New Int32Collection(N * 6)

        For i As Integer = 0 To N
            Dim t As Double = (i / CDbl(N)) * (2.0 * Math.PI)
            Dim c As Double = Math.Cos(t)
            Dim si As Double = Math.Sin(t)

            'Terminator-Basispunkt p (|p|=1, p ⟂ s)
            Dim p As New Vector3D(u.X * c + v.X * si,
                                  u.Y * c + v.Y * si,
                                  u.Z * c + v.Z * si)
            p.Normalize()

            'Tagkante
            Dim pDay As New Vector3D(p.X * cosDay + s.X * sinDay,
                                      p.Y * cosDay + s.Y * sinDay,
                                      p.Z * cosDay + s.Z * sinDay)
            pDay.Normalize()

            'Nachtkante
            Dim pNight As New Vector3D(p.X * cosNight - s.X * sinNight,
                                       p.Y * cosNight - s.Y * sinNight,
                                       p.Z * cosNight - s.Z * sinNight)
            pNight.Normalize()

            pos.Add(New Point3D(pDay.X * r, pDay.Y * r, pDay.Z * r))
            pos.Add(New Point3D(pNight.X * r, pNight.Y * r, pNight.Z * r))

            'Textur: u entland, v quer (0..1), Peak ist im Material bei 0.5
            Dim uu As Double = i / CDbl(N)
            tc.Add(New Point(uu, 0.0))
            tc.Add(New Point(uu, 1.0))
        Next

        For i As Integer = 0 To N - 1
            Dim a0 As Integer = i * 2
            Dim a1 As Integer = a0 + 1
            Dim b0 As Integer = a0 + 2
            Dim b1 As Integer = a0 + 3

            idx.Add(a0) : idx.Add(b0) : idx.Add(a1)
            idx.Add(a1) : idx.Add(b0) : idx.Add(b1)
        Next

        Dim mesh As New MeshGeometry3D With {
            .Positions = pos,
            .TextureCoordinates = tc,
            .TriangleIndices = idx
        }
        If mesh.CanFreeze Then mesh.Freeze()

        _dawnZoneGeo.Geometry = mesh

    End Sub

    Private Sub UpdateDayNightVisibility()
        If _group Is Nothing Then Return

        '--- Terminator (harte Linie) ---
        If _dayNightTerminatorGeo IsNot Nothing Then
            If ShowDayNightTerminator Then
                If Not _dayNightTerminatorVisible Then
                    If Not _group.Children.Contains(_dayNightTerminatorGeo) Then _group.Children.Add(_dayNightTerminatorGeo)
                    _dayNightTerminatorVisible = True
                End If
            Else
                If _dayNightTerminatorVisible Then
                    If _group.Children.Contains(_dayNightTerminatorGeo) Then _group.Children.Remove(_dayNightTerminatorGeo)
                    _dayNightTerminatorVisible = False
                End If
            End If
        End If

        '--- DawnZone (Soft-Zone) ---
        If _dawnZoneGeo IsNot Nothing Then
            If ShowDawnZone Then
                If Not _dawnTerminatorVisible Then
                    If Not _group.Children.Contains(_dawnZoneGeo) Then _group.Children.Add(_dawnZoneGeo)
                    _dawnTerminatorVisible = True
                End If
            Else
                If _dawnTerminatorVisible Then
                    If _group.Children.Contains(_dawnZoneGeo) Then _group.Children.Remove(_dawnZoneGeo)
                    _dawnTerminatorVisible = False
                End If
            End If
        End If
    End Sub

    Private Sub StartAnimation()
        If _isAnimating Then Return

        _isAnimating = True
        _lastFrameTicks = Stopwatch.GetTimestamp()

        _simStartUtc = DateTime.UtcNow
        _simSecondsTotal = 0
        SimulationUtc = _simStartUtc

        Dim jd0 As Double = Astronomics.JulianDateUtc(SimulationUtc)
        _gmst0Deg = Astronomics.GmstDeg(jd0)

        _tiltRot.Angle = ClimateConstants.EarthObliquityDeg
        AmbientLightColor = Color.FromRgb(20, 20, 20)

        AddHandler CompositionTarget.Rendering, AddressOf OnRenderingFrame
        ResetCamera()
    End Sub

    Private Sub StopAnimation()
        If Not _isAnimating Then Return

        _isAnimating = False
        RemoveHandler CompositionTarget.Rendering, AddressOf OnRenderingFrame

        _spinRot.Angle = 0.0
        _tiltRot.Angle = 0.0

        SimulationUtc = DateTime.UtcNow

        'Sonnen-Marker deaktivieren
        ShowSubsolarMarker = False
        ShowDayNightTerminator = False
        ShowDawnZone = False

        SunDirection = New Vector3D(-0.6, -0.3, -1)     'Default Sonne wiederherstellen
        AmbientLightColor = Color.FromRgb(100, 100, 100)
        SolarDeclinationDeg = 0.0
        OnPropertyChanged(NameOf(SolarDeclinationText))
    End Sub

    Private Sub OnRenderingFrame(sender As Object, e As EventArgs)

        If Not _isAnimating Then Return

        Dim nowTicks As Long = Stopwatch.GetTimestamp()
        Dim dtReal As Double = (nowTicks - _lastFrameTicks) / CDbl(Stopwatch.Frequency)
        _lastFrameTicks = nowTicks

        'Zeitbasis: 1s real = 1s Simulation * 10^tick
        Dim speedFactor As Double = Math.Pow(10, Math.Abs(AnimationSpeedTick))
        Dim dirSign As Double = If(AnimationSpeedTick < 0, -1.0, 1.0)
        Dim simSeconds As Double = dtReal * speedFactor * dirSign

        'Zeitberechnung
        _simSecondsTotal += simSeconds
        SimulationUtc = _simStartUtc.AddSeconds(_simSecondsTotal)

        Dim jd As Double = Astronomics.JulianDateUtc(SimulationUtc)

        Dim ra As Double, dec As Double
        Astronomics.SunRaDecDeg(jd, ra, dec)
        Dim gmst As Double = Astronomics.GmstDeg(jd)
        Dim eot As Double = Astronomics.EquationOfTimeMinutes(jd)

        SunRaDeg = ra
        GmstDeg = gmst
        EquationOfTime = eot

        'Subsolar (Earth-fixed) für Anzeige
        Dim ssLat As Double = dec
        Dim ssLon As Double = Wrap180(ra - gmst)        'Ost-positiv

        SubsolarLatDeg = ssLat
        SubsolarLonDeg = ssLon

        '=== Frame-Mode ===
        Select Case FrameMode
            Case GlobeFrameMode.EarthFixed          'Erde steht fest, Sonne rotiert um Erde
                _spinRot.Angle = 0.0

                'Sun in Body aus Subsolar-Lat/Lon
                Dim sunBody As Vector3D = Astronomics.BodyVectorFromLatLon(ssLat, ssLon)

                'Body->World (Tilt wirkt)
                Dim sunWorld As Vector3D = _animTransform.Value.Transform(sunBody)
                sunWorld.Normalize()

                SunDirection = New Vector3D(-sunWorld.X, -sunWorld.Y, -sunWorld.Z)

                'Marker/Terminator aus Subsolar-Lat/Lon ist hier ok
                SolarDeclinationDeg = dec

                If ShowSubsolarMarker Then UpdateSubsolarMarker()
                If ShowDayNightTerminator Then UpdateDayNightTerminator()
                If ShowDawnZone Then UpdateDawnZone()

            Case GlobeFrameMode.RotatingEarth       'Erde rotiert: Spin = (GMST - GMST0)

                _spinRot.Angle = Wrap360(gmst - _gmst0Deg)

                'Sonne inertial: keine tägliche Komponente -> verwende (RA - GMST0) als konstante Referenz
                Dim sunLonInertial As Double = Wrap180(ra - _gmst0Deg)
                Dim sunBodyNoDaily As Vector3D = Astronomics.BodyVectorFromLatLon(dec, sunLonInertial)

                'WICHTIG: SunDirection im World-Space soll NICHT vom Spin abhängen,
                'sondern nur vom Tilt + Jahreslauf -> deshalb nur Tilt anwenden
                Dim tiltOnly As New RotateTransform3D(_tiltRot)
                Dim sunWorld As Vector3D = tiltOnly.Transform(sunBodyNoDaily)
                sunWorld.Normalize()

                SunDirection = New Vector3D(-sunWorld.X, -sunWorld.Y, -sunWorld.Z)
                SolarDeclinationDeg = dec

                'In RotatingEarth muss der Marker aus der echten Sonnenrichtung im Body kommen:
                '-> Body-Sun = inverse(globeTransform) * (Earth->Sun in World)
                If ShowSubsolarMarker Then UpdateSubsolarMarkerFromSun()
                If ShowDayNightTerminator Then UpdateDayNightTerminator()
                If ShowDawnZone Then UpdateDawnZone()

        End Select

        'Hover live aktualisieren
        If _lastMouseInViewport Then
            Dim nowTicksNew As Long = Stopwatch.GetTimestamp()
            If (nowTicksNew - _lastHoverTicks) >= _hoverTickPerFrame Then
                _lastHoverTicks = nowTicksNew
                UpdateHoverFromScreenPoint(_lastMousePos)
            End If
        End If

    End Sub

    Public Sub DisposeAnimation()
        StopAnimation()
    End Sub

    Public Sub SetSimulationUtc(newUtc As DateTime)
        newUtc = DateTime.SpecifyKind(newUtc, DateTimeKind.Utc)

        If Not _isAnimating Then
            SimulationUtc = newUtc
        End If

        'Simulation neu referenzieren, ohne dt-Glitches
        _simStartUtc = newUtc
        _simSecondsTotal = 0
        SimulationUtc = newUtc

        'GMST0 neu setzen
        Dim jd0 As Double = Astronomics.JulianDateUtc(newUtc)
        _gmst0Deg = Astronomics.GmstDeg(jd0)

        'einmal alles updaten
        _lastFrameTicks = Stopwatch.GetTimestamp()
    End Sub

#End Region

#Region "RayCast & Hover"

    Private _lastMouseInViewport As Boolean
    Private _lastMousePos As Point

    Private _lastHoverTicks As Long
    Private Shared ReadOnly _hoverTickPerFrame As Long = CLng(Stopwatch.Frequency / 30.0)   '30Hz für Hover-Aktualisierung

    Private _hoverLatText As String
    Public Property HoverLatText As String
        Get
            Return _hoverLatText
        End Get
        Set(value As String)
            SetProperty(_hoverLatText, value)
        End Set
    End Property

    Private _hoverLonText As String
    Public Property HoverLonText As String
        Get
            Return _hoverLonText
        End Get
        Set(value As String)
            SetProperty(_hoverLonText, value)
        End Set
    End Property

    Private _hoverSunHeightText As String
    Public Property HoverSunHeightText As String
        Get
            Return _hoverSunHeightText
        End Get
        Set(value As String)
            SetProperty(_hoverSunHeightText, value)
        End Set
    End Property

    Private _hoverSunAzimuthDeg As Double
    Public Property HoverSunAzimuthDeg As Double
        Get
            Return _hoverSunAzimuthDeg
        End Get
        Set(value As Double)
            If SetProperty(_hoverSunAzimuthDeg, value) Then
                OnPropertyChanged(NameOf(HoverSunAzimuthText))
            End If
        End Set
    End Property

    Private _hoverSunAzimuthText As String
    Public Property HoverSunAzimuthText As String
        Get
            Return _hoverSunAzimuthText
        End Get
        Set(value As String)
            SetProperty(_hoverSunAzimuthText, value)
        End Set
    End Property

    Private _hoverDayLengthText As String
    Public Property HoverDayLengthText As String
        Get
            Return _hoverDayLengthText
        End Get
        Set(value As String)
            SetProperty(_hoverDayLengthText, value)
        End Set
    End Property

    Private _hoverNightLengthText As String
    Public Property HoverNightLengthText As String
        Get
            Return _hoverNightLengthText
        End Get
        Set(value As String)
            SetProperty(_hoverNightLengthText, value)
        End Set
    End Property

    Private Sub UpdateHoverFromWorldHit(hitWorld As Point3D)

        If _animTransform Is Nothing Then Return

        Dim m As Matrix3D = _animTransform.Value
        If Not m.HasInverse Then Return
        m.Invert()

        Dim pBody As Point3D = m.Transform(hitWorld)

        'Normieren, falls Displacement/Radius minimal anders
        Dim v As New Vector3D(pBody.X, pBody.Y, pBody.Z)
        If v.LengthSquared < 0.000000000001 Then
            Return
        End If
        v.Normalize()

        'Lat/Lon aus Body-Vektor gemäß der Konvention:
        Dim latRad As Double = Math.Asin(Clamp(v.Y, -1.0, 1.0))

        'Lon: 0° bei +X, +90° bei +Z
        Dim lonRad As Double = Math.Atan2(-v.Z, v.X)

        Dim latDeg As Double = RadToDeg(latRad)
        Dim lonDeg As Double = Wrap180(RadToDeg(lonRad))

        '=== Hover-Texte ===
        '1) Koordinaten
        HoverLatText = $"Lat: {latDeg:+00.00;-00.00;00.00}°"
        HoverLonText = $"Lon: {lonDeg:+000.00;-000.00;000.00}°"

        '2) Tages/Nacht-Länge
        Dim dayLength As Integer, nightLength As Integer
        ComputeDayNightLength(latDeg, SolarDeclinationDeg, dayLength, nightLength)

        Dim dayHours As Integer, dayMinutes As Integer
        Dim nightHours As Integer, nightMinutes As Integer

        Astronomics.SplitDayMinutes(dayLength, dayHours, dayMinutes)
        Astronomics.SplitDayMinutes(nightLength, nightHours, nightMinutes)

        HoverDayLengthText = $"Tag: {dayHours:00}:{dayMinutes:00}h"
        HoverNightLengthText = $"Nacht: {nightHours:00}:{nightMinutes:00}h"

        '3) Sonnenhöhe/Azimut
        Dim sunH As Double, sunAz As Double
        Astronomics.ComputeSunHeightAzimuthAtPoint(latDeg, lonDeg, SubsolarLatDeg, SubsolarLonDeg, sunH, sunAz)

        HoverSunAzimuthDeg = Wrap360(sunAz)

        HoverSunHeightText = $"Höhe: {sunH:+00.0;-00.0;00.0}°"
        HoverSunAzimuthText = $"Azimut: {sunAz:000.0}°"
    End Sub

    Private Sub UpdateHoverFromScreenPoint(p As Point)

        If _viewport Is Nothing Then
            Return
        End If

        Dim hitPointWorld As Nullable(Of Point3D) = Nothing

        VisualTreeHelper.HitTest(
            _viewport,
            Nothing,
            Function(result As HitTestResult)

                Dim ray = TryCast(result, RayHitTestResult)
                If ray Is Nothing Then Return HitTestResultBehavior.Continue

                Dim meshHit = TryCast(ray, RayMeshGeometry3DHitTestResult)
                If meshHit Is Nothing Then Return HitTestResultBehavior.Continue

                hitPointWorld = meshHit.PointHit
                Return HitTestResultBehavior.Stop


            End Function,
            New PointHitTestParameters(p)
            )

        If Not hitPointWorld.HasValue Then
            Return
        End If

        UpdateHoverFromWorldHit(hitPointWorld.Value)
    End Sub

    Private Sub ClearHoverText()
        HoverLatText = ""
        HoverLonText = ""
        HoverSunHeightText = ""
        HoverSunAzimuthText = ""
        HoverDayLengthText = ""
        HoverNightLengthText = ""
    End Sub

#End Region

End Class
