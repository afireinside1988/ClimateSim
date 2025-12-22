Imports Microsoft.Xaml.Behaviors

#Disable Warning CA1416 'Plattformkompatibilität überprüfen
Public Class PanAndZoomBehavior
    Inherits Behavior(Of FrameworkElement)

    Private _isPanning As Boolean

    Public Property HoverCommand As ICommand
        Get
            Return CType(GetValue(HoverCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(HoverCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly HoverCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(HoverCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property BeginPanCommand As ICommand
        Get
            Return CType(GetValue(BeginPanCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(BeginPanCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly BeginPanCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(BeginPanCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property PanCommand As ICommand
        Get
            Return CType(GetValue(PanCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(PanCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly PanCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(PanCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property EndPanCommand As ICommand
        Get
            Return CType(GetValue(EndPanCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(EndPanCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly EndPanCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(EndPanCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property ZoomCommand As ICommand
        Get
            Return CType(GetValue(ZoomCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(ZoomCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ZoomCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ZoomCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property ViewportChangedCommand As ICommand
        Get
            Return CType(GetValue(ViewportChangedCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(ViewportChangedCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ViewportChangedCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(ViewportChangedCommand), GetType(ICommand), GetType(PanAndZoomBehavior))


    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()

        AddHandler AssociatedObject.Loaded, AddressOf OnLoaded
        AddHandler AssociatedObject.SizeChanged, AddressOf OnSizeChanged

        AddHandler AssociatedObject.MouseLeftButtonDown, AddressOf OnMouseDown
        AddHandler AssociatedObject.MouseMove, AddressOf OnMouseMove
        AddHandler AssociatedObject.MouseLeftButtonUp, AddressOf OnMouseUp
        AddHandler AssociatedObject.MouseWheel, AddressOf OnMouseWheel


    End Sub

    Protected Overrides Sub OnDetaching()

        RemoveHandler AssociatedObject.Loaded, AddressOf OnLoaded
        RemoveHandler AssociatedObject.SizeChanged, AddressOf OnSizeChanged

        RemoveHandler AssociatedObject.MouseLeftButtonDown, AddressOf OnMouseDown
        RemoveHandler AssociatedObject.MouseMove, AddressOf OnMouseMove
        RemoveHandler AssociatedObject.MouseLeftButtonUp, AddressOf OnMouseUp
        RemoveHandler AssociatedObject.MouseWheel, AddressOf OnMouseWheel



        MyBase.OnDetaching()
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        FireViewportChanged()
    End Sub

    Private Sub OnSizeChanged(sender As Object, e As SizeChangedEventArgs)
        FireViewportChanged()
    End Sub

    Private Sub OnMouseDown(sender As Object, e As MouseButtonEventArgs)

        _isPanning = True

        AssociatedObject.CaptureMouse()


        Dim payload As New PanRequest With {
            .MousePos = e.GetPosition(AssociatedObject),
            .ViewPortSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
        }

        If BeginPanCommand IsNot Nothing AndAlso BeginPanCommand.CanExecute(payload) Then
            BeginPanCommand.Execute(payload)
            e.Handled = True
        End If

    End Sub

    Private Sub OnMouseMove(sender As Object, e As MouseEventArgs)

        'Hover immer melden (Position relativ zu MapHost!)
        If HoverCommand IsNot Nothing Then
            Dim hover As New HoverRequest With {
                .MousePos = e.GetPosition(AssociatedObject),
                .ViewPortSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
            }
            If HoverCommand.CanExecute(hover) Then HoverCommand.Execute(hover)

        End If

        '2) Panning nur bei gedrückter linker Maustaste
        If e.LeftButton <> MouseButtonState.Pressed Then Return


        Dim payload As New PanRequest With {
            .MousePos = e.GetPosition(AssociatedObject),
            .ViewPortSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
        }

        If PanCommand IsNot Nothing AndAlso PanCommand.CanExecute(payload) Then
            PanCommand.Execute(payload)
            e.Handled = True
        End If

    End Sub

    Private Sub OnMouseUp(sender As Object, e As MouseButtonEventArgs)

        If Not _isPanning Then Return

        _isPanning = False

        If AssociatedObject.IsMouseCaptured Then
            AssociatedObject.ReleaseMouseCapture()
        End If

        If EndPanCommand IsNot Nothing AndAlso EndPanCommand.CanExecute(Nothing) Then
            EndPanCommand.Execute(Nothing)
            e.Handled = True
        End If

    End Sub

    Private Sub OnMouseWheel(sender As Object, e As MouseWheelEventArgs)

        Dim p As Point = e.GetPosition(AssociatedObject)

        Dim payload As New ZoomRequest With {
            .MousePos = p,
            .Delta = e.Delta,
            .ViewportSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
        }

        If ZoomCommand IsNot Nothing AndAlso ZoomCommand.CanExecute(payload) Then
            ZoomCommand.Execute(payload)
            e.Handled = True
        End If

    End Sub

    Private Sub FireViewportChanged()

        If ViewportChangedCommand Is Nothing Then Return

        Dim vp As New ViewportChangedRequest With {
            .ViewPortSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
        }

        If ViewportChangedCommand.CanExecute(vp) Then ViewportChangedCommand.Execute(vp)
    End Sub

End Class

Public Class ZoomRequest
    Public Property MousePos As Point
    Public Property Delta As Integer
    Public Property ViewportSize As Size
End Class

Public Class PanRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size
End Class

Public Class HoverRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size
End Class

Public Class ViewportChangedRequest
    Public Property ViewPortSize As Size
End Class

#Enable Warning CA1416 ' Plattformkompatibilität überprüfen