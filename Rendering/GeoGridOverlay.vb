Imports System.ComponentModel
Imports System.Security.Policy
Imports System.Windows.Ink

Public Class GeoGridOverlay

    Inherits FrameworkElement

    '---------------------
    'Dependency Properties
    '---------------------

    Public Property Zoom As Double
        Get
            Return CDbl(GetValue(ZoomProperty))
        End Get
        Set(value As Double)
            SetValue(ZoomProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ZoomProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Zoom), GetType(Double), GetType(GeoGridOverlay),
            New FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property PanX As Double
        Get
            Return CDbl(GetValue(PanXProperty))
        End Get
        Set(value As Double)
            SetValue(PanXProperty, value)
        End Set
    End Property
    Public Shared ReadOnly PanXProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(PanX), GetType(Double), GetType(GeoGridOverlay),
            New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property PanY As Double
        Get
            Return CDbl(GetValue(PanYProperty))
        End Get
        Set(value As Double)
            SetValue(PanYProperty, value)
        End Set
    End Property
    Public Shared ReadOnly PanYProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(PanY), GetType(Double), GetType(GeoGridOverlay),
            New FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender))

    Public Property CacheMeta As EarthSurfaceCacheMeta
        Get
            Return CType(GetValue(CacheMetaProperty), EarthSurfaceCacheMeta)
        End Get
        Set(value As EarthSurfaceCacheMeta)
            SetValue(CacheMetaProperty, value)
        End Set
    End Property
    Public Shared ReadOnly CacheMetaProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(CacheMeta), GetType(EarthSurfaceCacheMeta), GetType(GeoGridOverlay),
            New FrameworkPropertyMetadata(Nothing, FrameworkPropertyMetadataOptions.AffectsRender))

    '---------
    'Rendering
    '---------

    Protected Overrides Sub OnRender(dc As DrawingContext)
        MyBase.OnRender(dc)

        Dim meta = CacheMeta
        If meta Is Nothing Then Return
        If Zoom <= 0 Then Return

        Dim vpW As Double = ActualWidth
        Dim vpH As Double = ActualHeight
        If vpW <= 0 OrElse vpH <= 0 Then Return

        'Grid-Config nach festgelegten Zoom-Bändern
        Dim cfg = GetGridConfig(Zoom)

        Dim cellDeg As Double = meta.CellSizeDeg
        Dim lonCount As Integer = meta.LonCount
        Dim latCount As Integer = meta.LatCount

        'Schrittweiten in Zellkanten (Integer, damit exakt auf Zellkanten)
        Dim majorStepCells As Integer = DegToCells(cfg.MajorDeg, cellDeg)
        Dim minorStepCells As Integer = If(cfg.MinorDeg > 0, DegToCells(cfg.MinorDeg, cellDeg), 0)

        If majorStepCells <= 0 Then Return

        'Sichtbarer Content-Bereich (in Zellkoordinaten)
        Dim x0 As Double = (-PanX) / Zoom
        Dim y0 As Double = (-PanY) / Zoom
        Dim x1 As Double = (vpW - PanX) / Zoom
        Dim y1 As Double = (vpH - PanY) / Zoom

        'Content-Grenzen in Screen-Space
        Dim contentLeft As Double = PanX
        Dim contentTop As Double = PanY
        Dim contentRight As Double = PanX + CacheMeta.LonCount * Zoom
        Dim contentBottom As Double = PanY + CacheMeta.LatCount * Zoom


        'Clamp an Content
        x0 = Clamp(x0, 0, lonCount)
        x1 = Clamp(x1, 0, lonCount)
        y0 = Clamp(y0, 0, latCount)
        y1 = Clamp(y1, 0, latCount)

        'Pens (1px und 2px in Screen-Space)
        Dim minorPen As Pen = Nothing
        If minorStepCells > 0 Then
            Dim b As Brush = New SolidColorBrush(Color.FromArgb(40, 255, 255, 255))
            b.Freeze()
            minorPen = New Pen(b, 1.0)
            minorPen.Freeze()
        End If

        Dim mb As Brush = New SolidColorBrush(Color.FromArgb(120, 255, 255, 255))
        mb.Freeze()
        Dim majorPen As New Pen(mb, 1.0)
        majorPen.Freeze()

        'Pixel-Snapping: Linien auf ganze Pixel runden (0.5 optional, je nach Pen-Thickness)
        'Für 1px/2px funktioniert "Round" sehr gut.

        'Minor zuerst, dann Major drüber
        If minorPen IsNot Nothing Then
            DrawGridLines(dc, minorPen, minorStepCells, x0, x1, y0, y1, vpW, vpH, contentTop, contentBottom, contentLeft, contentRight)
        End If

        DrawGridLines(dc, majorPen, majorStepCells, x0, x1, y0, y1, vpW, vpH, contentTop, contentBottom, contentLeft, contentRight)
    End Sub

    Private Sub DrawGridLines(dc As DrawingContext,
                              pen As Pen,
                              stepCells As Integer,
                              x0 As Double, x1 As Double,
                              y0 As Double, y1 As Double,
                              vpW As Double, vpH As Double,
                              contentTop As Double, contentBottom As Double,
                              contentLeft As Double, contentRight As Double)

        If stepCells <= 0 Then Return


        'Vertikale Linien: x = k * step
        Dim startX As Integer = FloorToMultiple(CInt(Math.Floor(x0)), stepCells)
        Dim endX As Integer = CInt(Math.Ceiling(x1))
        Dim yStart As Double = Math.Max(0, contentTop)
        Dim yEnd As Double = Math.Min(vpH, contentBottom)

        Dim x As Integer = startX
        While x <= endX

            Dim sxRaw As Double = x * Zoom + PanX

            Dim sx As Double = SnapToPixels(sxRaw, pen.Thickness)
            If yEnd > yStart Then
                dc.DrawLine(pen, New Point(sx, yStart), New Point(sx, yEnd))
            End If
            x += stepCells
        End While

        'Horizontale Linien: y = k * step
        Dim startY As Integer = FloorToMultiple(CInt(Math.Floor(y0)), stepCells)
        Dim endY As Integer = CInt(Math.Ceiling(y1))
        Dim xStart As Double = Math.Max(0, contentLeft)
        Dim xEnd As Double = Math.Min(vpW, contentRight)

        Dim y As Integer = startY
        While y <= endY

            Dim syRaw As Double = y * Zoom + PanY
            Dim sy As Double = SnapToPixels(syRaw, pen.Thickness)
            If xEnd > xStart Then
                dc.DrawLine(pen, New Point(xStart, sy), New Point(xEnd, sy))
            End If
            y += stepCells
        End While
    End Sub

#Region "Grid-Config"
    Private Structure GridConfig
        Public MajorDeg As Double
        Public MinorDeg As Double
    End Structure

    Private Shared Function GetGridConfig(zoom As Double) As GridConfig

        Dim zPct As Double = zoom * 100.0

        ' Regeln:
        ' 25% - 200%:   Major 10°, Minor aus
        ' 200% - 400%:  Major 10°, Minor 5°
        ' 400% - 1000%: Major 5°,  Minor 1°
        ' 1000% - 2000%: Major 1°, Minor 0.25°

        If zPct < 100.0 Then
            Return New GridConfig With {.MajorDeg = 20.0, .MinorDeg = 0.0}
        ElseIf zPct < 200.0 Then
            Return New GridConfig With {.MajorDeg = 10.0, .MinorDeg = 0.0}
        ElseIf zPct < 500.0 Then
            Return New GridConfig With {.MajorDeg = 10.0, .MinorDeg = 5.0}
        ElseIf zPct < 1000.0 Then
            Return New GridConfig With {.MajorDeg = 5.0, .MinorDeg = 1.0}
        Else
            Return New GridConfig With {.MajorDeg = 1.0, .MinorDeg = 0.25}
        End If
    End Function

#End Region

#Region "Helpers"

    Private Shared Function DegToCells(deg As Double, cellDeg As Double) As Integer

        If deg <= 0 OrElse cellDeg <= 0 Then Return 0

        Dim raw As Double = deg / cellDeg
        'Muss Integer sein, sonst kann es niemals exakt auf Zellkanten liegen
        Dim cellStep As Integer = CInt(Math.Round(raw))

        'Wenn es nicht sauber teilbar ist, fallback auf 1 Zellkante.
        '(Passiert z.b. wenn Cache cellDeg=1° und wir 0.25° wollen)
        If Math.Abs(raw - cellStep) > 0.000001 Then
            cellStep = 1
        End If

        Return Math.Max(1, cellStep)
    End Function

    Private Shared Function FloorToMultiple(v As Integer, m As Integer) As Integer

        If m <= 0 Then Return v
        If v >= 0 Then
            Return (v \ m) * m
        Else
            'für negative Werte korrekt (sicher ist sicher)
            Dim div As Integer = CInt(Math.Floor(v / CDbl(m)))
            Return div * m
        End If
    End Function

    Private Shared Function SnapToPixels(v As Double, thickness As Double) As Double
        'thickness in DIPs (1.0 / 2.0)
        If thickness <= 1.0 Then
            Return Math.Floor(v) + 0.5
        Else
            Return Math.Round(v)
        End If

    End Function
#End Region
End Class
