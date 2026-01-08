Imports System.Windows.Media.Media3D

Public Class GlobePreviewViewModel
    Inherits ViewModelBase

    Private ReadOnly _p As GlobePreviewPayload
    Private Shared ReadOnly _sphereMesh As MeshGeometry3D =
        GlobeMeshFactory.CreateSphereMesh(radius:=1.0, lonSegments:=256, latSegments:=128)

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

#End Region

    '=== 3D-Model ===
    Private _globeModel As Model3D
    Public Property GlobeModel As Model3D
        Get
            Return _globeModel
        End Get
        Set(value As Model3D)
            SetProperty(_globeModel, value)
        End Set
    End Property


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
                RebuildGlobeModel()
            End If
        End Set
    End Property

    Private _baseLayerImage As ImageSource
    Public Property BaseLayerImage As ImageSource
        Get
            Return _baseLayerImage
        End Get
        Set(value As ImageSource)
            SetProperty(_baseLayerImage, value)
        End Set
    End Property

    Private _overlayImage As ImageSource
    Public Property OverlayImage As ImageSource
        Get
            Return _overlayImage
        End Get
        Set(value As ImageSource)
            SetProperty(_overlayImage, value)
        End Set
    End Property

    Public ReadOnly Property TopoLayer As ImageSource
        Get
            Return _p?.Topo
        End Get
    End Property

    Public ReadOnly Property ReliefOverlay As ImageSource
        Get
            Return _p?.Relief
        End Get
    End Property

    Public ReadOnly Property LandMaskLayer As ImageSource
        Get
            Return _p?.LandMask
        End Get
    End Property

    Public ReadOnly Property ShoreLinesOverlay As ImageSource
        Get
            Return _p?.ShoreLines
        End Get
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
                RebuildGlobeModel()
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

    Private _isReliefOptionVisible As Boolean
    Public Property IsReliefOptionVisible As Boolean
        Get
            Return _isReliefOptionVisible
        End Get
        Set(value As Boolean)
            SetProperty(_isReliefOptionVisible, value)
        End Set
    End Property

    Private _isShoreLinesOptionVisible As Boolean
    Public Property IsShoreLinesOptionVisible As Boolean
        Get
            Return _isShoreLinesOptionVisible
        End Get
        Set(value As Boolean)
            SetProperty(_isShoreLinesOptionVisible, value)
        End Set
    End Property


    Private Sub RefreshOverlayAvailability()

        IsReliefOptionVisible = (SelectedBaseLayer = GlobeBaseLayer.Topo)
        IsShoreLinesOptionVisible = (SelectedBaseLayer = GlobeBaseLayer.LandMask)

        Dim overlayAllowed As Boolean =
            (SelectedBaseLayer = GlobeBaseLayer.Topo AndAlso ReliefOverlay IsNot Nothing) OrElse
            (SelectedBaseLayer = GlobeBaseLayer.LandMask AndAlso ShoreLinesOverlay IsNot Nothing)

        If Not overlayAllowed AndAlso ShowOverlay Then
            _showOverlay = False
            OnPropertyChanged(NameOf(ShowOverlay))
        End If
    End Sub

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

    Private Sub BeginRotate(r As PanRequest)

        _isDragging = True
        _dragStart = r.MousePos
        _yawStart = _yawDeg
        _pitchStart = _pitchDeg

    End Sub

    Private Sub Rotate(r As PanRequest)

        If Not _isDragging Then Return

        Dim dx As Double = r.MousePos.X - _dragStart.X
        Dim dy As Double = r.MousePos.Y - _dragStart.Y

        ' "Gefühl" feinjustieren
        Const degPerPixel As Double = 0.25

        _yawDeg = _yawStart + dx * degPerPixel
        _pitchDeg = _pitchStart - dy * degPerPixel

        UpdateCamera()

    End Sub

    Private Sub EndRotate()
        _isDragging = False
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

    Private Sub OnMouseMove(r As MapMouseMoveRequest)
        'später Raycast für Hover Lat/lon
    End Sub

    Private Sub OnMouseDown(r As MapMouseDownRequest)
        'später evtl Pick/Info
    End Sub

    Private Sub OnMouseUp(r As MapMouseUpRequest)
        'später evtl. Ende Pick/Info
    End Sub
#End Region

    Public Sub New(payload As GlobePreviewPayload)

        _p = payload

        'Defaults setzen:
        _selectedBaseLayer = If(_p?.Topo IsNot Nothing, GlobeBaseLayer.Topo,
                       If(_p?.LandMask IsNot Nothing, GlobeBaseLayer.LandMask,
                       GlobeBaseLayer.Tid))

        ' Default Overlay: an, wenn für den Start-Layer ein Overlay existiert
        _showOverlay =
        (_selectedBaseLayer = GlobeBaseLayer.Topo AndAlso _p?.Relief IsNot Nothing) OrElse
        (_selectedBaseLayer = GlobeBaseLayer.LandMask AndAlso _p?.ShoreLines IsNot Nothing)

        _showGrid = False

        'Intiales Refreshen
        RefreshOverlayAvailability()
        RebuildGlobeModel()
        UpdateCamera()

        'Commands initialisieren
        BeginRotateCommand = New RelayCommand(Of PanRequest)(Sub(r) BeginRotate(r))
        RotateCommand = New RelayCommand(Of PanRequest)(Sub(r) Rotate(r))
        EndRotateCommand = New RelayCommand(Of Object)(Sub(o) EndRotate())

        ZoomCommand = New RelayCommand(Of ZoomRequest)(Sub(z) Zoom(z))
        ViewportChangedCommand = New RelayCommand(Of ViewportChangedRequest)(Sub(v) OnViewportChanged(v))

        MouseMoveCommand = New RelayCommand(Of MapMouseMoveRequest)(Sub(r) OnMouseMove(r))
        MouseDownCommand = New RelayCommand(Of MapMouseDownRequest)(Sub(r) OnMouseDown(r))
        MouseUpCommand = New RelayCommand(Of MapMouseUpRequest)(Sub(r) OnMouseUp(r))
    End Sub

#Region "Model-Building"

    Private Sub RebuildGlobeModel()

        Dim baseImg As ImageSource = Nothing
        Dim overlayImg As ImageSource = Nothing

        Select Case SelectedBaseLayer
            Case GlobeBaseLayer.Topo
                baseImg = TopoLayer
                overlayImg = If(ShowOverlay, ReliefOverlay, Nothing)
            Case GlobeBaseLayer.LandMask
                baseImg = LandMaskLayer
                overlayImg = If(ShowOverlay, ShoreLinesOverlay, Nothing)
            Case GlobeBaseLayer.Tid
                baseImg = TidLayer
                overlayImg = Nothing
        End Select

        Dim group As New Model3DGroup

        'Basislayer-Sphere
        Dim baseMat As Material = BuildImageMaterial(baseImg)
        Dim baseGeo As New GeometryModel3D With {
            .Geometry = _sphereMesh,
            .Material = baseMat,
            .BackMaterial = baseMat
        }
        group.Children.Add(baseGeo)

        'Overlay-Sphere (minimal größer, damit es nicht z-fightet)
        If overlayImg IsNot Nothing Then
            Dim overlayMat As Material = BuildImageMaterial(overlayImg)

            'Skalierung über Transform, statt ein zweites Mesh zu bauen
            Dim overlayGeo As New GeometryModel3D With {
                .Geometry = _sphereMesh,
                .Material = overlayMat,
                .BackMaterial = overlayMat,
                .Transform = New ScaleTransform3D(1.0001, 1.0001, 1.0001)
            }
            group.Children.Add(overlayGeo)
        End If

        If group.CanFreeze Then
            'Derzeit nicht freezen für Layerwechsel
        End If

        GlobeModel = group
    End Sub

    Private Shared Function BuildImageMaterial(img As ImageSource) As Material

        If img Is Nothing Then
            'Fallback: dunkles Material
            Dim b As New SolidColorBrush(Color.FromRgb(40, 40, 40))
            Return New DiffuseMaterial(b)
        End If

        Dim brush As New ImageBrush(img) With {
                .Stretch = Stretch.Fill,
                .TileMode = TileMode.Tile,
                .ViewportUnits = BrushMappingMode.Absolute,
                .Viewport = New Rect(0, 0, 1, 1),
                .AlignmentX = AlignmentX.Center,
                .AlignmentY = AlignmentY.Center
            }

        RenderOptions.SetBitmapScalingMode(brush, BitmapScalingMode.NearestNeighbor)
        Return New DiffuseMaterial(brush)
    End Function

#End Region
End Class
