Imports System.Threading
Imports System.Transactions
Imports System.Windows.Media.Media3D

Public Class GlobePreviewViewModel
    Inherits ViewModelBase

    Private ReadOnly _p As GlobePreviewPayload

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
        _yawDeg = -90.0
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

    Private ReadOnly _cacheGate As New Object()

    'Sphere-Cache
    Private ReadOnly _sphereCache As New Dictionary(Of SphereKey, MeshGeometry3D)
    Private ReadOnly _displacedCache As New Dictionary(Of DisplacedSphereKey, MeshGeometry3D)

    'Material-Cache
    Private ReadOnly _materialCacheHQ As New Dictionary(Of ImageSource, Material)
    Private ReadOnly _materialCacheLQ As New Dictionary(Of ImageSource, Material)

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
    Private Function GetMaterial(img As ImageSource, hq As Boolean) As Material

        If img Is Nothing Then Return If(hq, _fallbackMaterialHQ, _fallbackMaterialLQ)

        Dim cache = If(hq, _materialCacheHQ, _materialCacheLQ)

        Dim m As Material = Nothing
        If cache.TryGetValue(img, m) Then Return m

        m = BuildImageMaterial(img, hq)
        cache(img) = m
        Return m
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
            _materialCacheHQ.Clear()
            _materialCacheLQ.Clear()
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
            bytes += (_materialCacheHQ.Count + _materialCacheLQ.Count) * 4096L
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

    Private _displacementExaggeration As Double = 40.0
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

    Private _showGrid As Boolean
    Public Property ShowGrid As Boolean
        Get
            Return _showGrid
        End Get
        Set(value As Boolean)
            SetProperty(_showGrid, value)
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

#Region "Commands"

    Public ReadOnly Property BeginRotateCommand As ICommand
    Public ReadOnly Property RotateCommand As ICommand
    Public ReadOnly Property EndRotateCommand As ICommand
    Public ReadOnly Property ZoomCommand As ICommand
    Public ReadOnly Property ViewportChangedCommand As ICommand

    Public ReadOnly Property MouseMoveCommand As ICommand
    Public ReadOnly Property MouseDownCommand As ICommand
    Public ReadOnly Property MouseUpCommand As ICommand

    Public ReadOnly Property ResetCameraCommand As ICommand
    Public ReadOnly Property ClearCacheCommand As ICommand

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
        'später Raycast für Hover Lat/lon
    End Sub

    Private Sub OnMouseDown(r As MapMouseDownRequest)
        'später evtl Pick/Info
    End Sub

    Private Sub OnMouseUp(r As MapMouseUpRequest)
        'später evtl. Ende Pick/Info
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

        ResetCameraCommand = New RelayCommand(Of Object)(Sub(o) ResetCamera())
        ClearCacheCommand = New RelayCommand(Of Object)(Sub(o) ClearCache())

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
        Dim mesh = GlobeMeshFactory.CreateSphereMesh(radius:=1.0, lonSegments:=lon, latSegments:=lat)
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

        Dim mesh = GlobeMeshFactory.CreateDisplacedMesh(
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

    Private Function CreateAxisModel() As GeometryModel3D

        'Globus-Radius = 1.0 -> Achse soll etwas überstehen
        Dim protrude As Double = 0.2
        Dim height As Double = 2.0 * (1.0 + protrude)
        Dim radius As Double = 0.001

        Dim mesh As MeshGeometry3D = GlobeMeshFactory.CreateCylinderMeshY(radius, height, segments:=8, cap:=True)

        Dim brush As New SolidColorBrush(Color.FromRgb(255, 255, 255))
        If brush.CanFreeze Then brush.Freeze()

        Dim mat As New DiffuseMaterial(brush)
        If mat.CanFreeze Then mat.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mat,
            .BackMaterial = mat
        }

        If gm.CanFreeze Then gm.Freeze()

        Return gm

    End Function

    Private Shared Function BuildImageMaterial(img As ImageSource, hq As Boolean) As Material

        Dim brush As Brush

        If img Is Nothing Then
            brush = New SolidColorBrush(Color.FromRgb(40, 40, 40))
            If brush.CanFreeze Then brush.Freeze()
        Else
            Dim ib As New ImageBrush(img) With {
                .Stretch = Stretch.Fill,
                .TileMode = TileMode.Tile,
                .ViewportUnits = BrushMappingMode.Absolute,
                .Viewport = New Rect(0, 0, 1, 1),
                .AlignmentX = AlignmentX.Center,
                .AlignmentY = AlignmentY.Center
            }

            'Während Drag später LQ-Material nutzen, aber Brush kann gleich bleiben
            RenderOptions.SetBitmapScalingMode(ib, BitmapScalingMode.NearestNeighbor)

            If ib.CanFreeze Then ib.Freeze()
            brush = ib
        End If

        If Not hq Then
            Dim m As New DiffuseMaterial(brush)
            If m.CanFreeze Then m.Freeze()
            Return m
        End If

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(brush))

        'dezentes Highlight (je höher, desto kleiner/knackiger)
        Dim sb As New SolidColorBrush(Color.FromArgb(10, 255, 255, 255))
        If sb.CanFreeze Then sb.Freeze()

        mg.Children.Add(New SpecularMaterial(sb, 50))

        If mg.CanFreeze Then mg.Freeze()
        Return mg
    End Function

    Private Sub ApplyInteractionMaterialState(Optional force As Boolean = False)

        'Soll LQ gerade aktiv sein?
        Dim wantLowSpec As Boolean = _isDragging

        If Not force AndAlso wantLowSpec = _isLowSpecActive Then Return
        _isLowSpecActive = wantLowSpec

        'Aktuelles Base-Image bestimmen
        Dim baseImg As ImageSource = GetBaseImageForMaterial()

        Dim mat As Material = GetMaterial(baseImg, hq:=Not wantLowSpec)
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

        _materialCacheHQ.Clear()
        _materialCacheLQ.Clear()

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

End Class
