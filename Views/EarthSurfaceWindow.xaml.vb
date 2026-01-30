Imports System.Windows.Controls.Primitives

Public Class EarthSurfaceWindow

    Public Sub New()
        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        DataContext = New EarthSurfaceViewModel()
    End Sub


    Private Sub TidLegendThumb_DragDelta(sender As Object, e As DragDeltaEventArgs)
        Dim vm = TryCast(Me.DataContext, EarthSurfaceViewModel)
        If vm Is Nothing Then Return

        'Viewport (Overlay) muss eine Größe haben
        If MapHost Is Nothing OrElse MapHost.ActualWidth <= 0 OrElse MapHost.ActualHeight <= 0 Then

            Dim p As New Point(vm.TidLegendPoint.X + e.HorizontalChange,
                               vm.TidLegendPoint.Y + e.VerticalChange)

            vm.TidLegendPoint = p
            Return
        End If

        'Legende muss gemessen sein
        If TidLegendBorder Is Nothing Then
            Dim p As New Point(vm.TidLegendPoint.X + e.HorizontalChange,
                               vm.TidLegendPoint.Y + e.VerticalChange)

            vm.TidLegendPoint = p
            Return
        End If

        Dim margin As Double = 2        'kleiner Sicherheitsabstand zum Rand

        Dim legendW As Double = TidLegendBorder.ActualWidth
        Dim legendH As Double = TidLegendBorder.ActualHeight

        'Fallback: wenn noch 0 (direkt nach Öffnen), DesiredSize probieren

        If legendW <= 0 Then legendW = TidLegendBorder.DesiredSize.Width
        If legendH <= 0 Then legendH = TidLegendBorder.DesiredSize.Height

        Dim vpW As Double = MapHost.ActualWidth
        Dim vpH As Double = MapHost.ActualHeight

        'Neue Position
        Dim nx As Double = vm.TidLegendPoint.X + e.HorizontalChange
        Dim ny As Double = vm.TidLegendPoint.Y + e.VerticalChange

        'Clamp
        ' - Normalfall: Legende komplett im Viewport halten
        ' - Sonderfall: Legende größer als Viewport -> wenigstens oben/links anheften + Margin
        Dim minX As Double = margin
        Dim minY As Double = margin
        Dim maxX As Double = Math.Max(margin, vpW - legendW - margin)
        Dim maxY As Double = Math.Max(margin, vpH - legendH - margin)

        nx = Clamp(nx, minX, maxX)
        ny = Clamp(ny, minY, maxY)

        vm.TidLegendPoint = New Point(nx, ny)
    End Sub

End Class
