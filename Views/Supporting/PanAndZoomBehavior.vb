Imports Microsoft.Xaml.Behaviors

#Disable Warning CA1416 'Plattformkompatibilität überprüfen
Public Class PanAndZoomBehavior
    Inherits Behavior(Of FrameworkElement)

    Private _isPanning As Boolean


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

    Public Property MouseMoveCommand As ICommand
        Get
            Return CType(GetValue(MouseMoveCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(MouseMoveCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly MouseMoveCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MouseMoveCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property MouseDownCommand As ICommand
        Get
            Return CType(GetValue(MouseDownCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(MouseDownCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly MouseDownCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MouseDownCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Public Property MouseUpCommand As ICommand
        Get
            Return CType(GetValue(MouseUpCommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(MouseUpCommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly MouseUpCommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(MouseUpCommand), GetType(ICommand), GetType(PanAndZoomBehavior))

    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()

        AddHandler AssociatedObject.Loaded, AddressOf OnLoaded
        AddHandler AssociatedObject.SizeChanged, AddressOf OnSizeChanged

        AddHandler AssociatedObject.MouseLeftButtonDown, AddressOf OnMouseDown
        AddHandler AssociatedObject.MouseMove, AddressOf OnMouseMove
        AddHandler AssociatedObject.MouseLeftButtonUp, AddressOf OnMouseUp
        AddHandler AssociatedObject.MouseWheel, AddressOf OnMouseWheel
        AddHandler AssociatedObject.MouseLeave, AddressOf OnMouseLeave
        AddHandler AssociatedObject.LostMouseCapture, AddressOf OnLostMouseCapture


    End Sub

    Protected Overrides Sub OnDetaching()

        RemoveHandler AssociatedObject.Loaded, AddressOf OnLoaded
        RemoveHandler AssociatedObject.SizeChanged, AddressOf OnSizeChanged

        RemoveHandler AssociatedObject.MouseLeftButtonDown, AddressOf OnMouseDown
        RemoveHandler AssociatedObject.MouseMove, AddressOf OnMouseMove
        RemoveHandler AssociatedObject.MouseLeftButtonUp, AddressOf OnMouseUp
        RemoveHandler AssociatedObject.MouseWheel, AddressOf OnMouseWheel
        RemoveHandler AssociatedObject.MouseLeave, AddressOf OnMouseLeave
        RemoveHandler AssociatedObject.LostMouseCapture, AddressOf OnLostMouseCapture

        MyBase.OnDetaching()
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        FireViewportChanged()
    End Sub

    Private Sub OnSizeChanged(sender As Object, e As SizeChangedEventArgs)
        FireViewportChanged()
    End Sub

    Private Sub OnMouseDown(sender As Object, e As MouseButtonEventArgs)

        If e.ChangedButton <> MouseButton.Left Then Return

        Dim ctrl As Boolean, alt As Boolean, shift As Boolean
        ReadModifiers(ctrl, alt, shift)

        'A) Edit-Click (Strg oder Alt) -> VM informieren, KEIN Panning starten
        If ctrl OrElse alt Then

            Dim req As New MapMouseDownRequest With {
                .MousePos = e.GetPosition(AssociatedObject),
                .ViewPortSize = CurrentViewportSize(),
                .IsLeftButton = True,
                .Ctrl = ctrl,
                .Alt = alt,
                .Shift = shift
            }

            If MouseDownCommand IsNot Nothing AndAlso MouseDownCommand.CanExecute(req) Then
                MouseDownCommand.Execute(req)
                e.Handled = True
            End If

            Return
        End If

        'B) normales Panning (ohne Modifier)
        _isPanning = True

        AssociatedObject.CaptureMouse()


        Dim payload As New PanRequest With {
            .MousePos = e.GetPosition(AssociatedObject),
            .ViewPortSize = CurrentViewportSize()
        }

        If BeginPanCommand IsNot Nothing AndAlso BeginPanCommand.CanExecute(payload) Then
            BeginPanCommand.Execute(payload)
            e.Handled = True
        End If

    End Sub

    Private Sub OnMouseMove(sender As Object, e As MouseEventArgs)

        Dim ctrl As Boolean, alt As Boolean, shift As Boolean
        ReadModifiers(ctrl, alt, shift)

        'A) Immer PointerMove an VM melden
        If MouseMoveCommand IsNot Nothing Then
            Dim req As New MapMouseMoveRequest With {
                .MousePos = e.GetPosition(AssociatedObject),
                .ViewPortSize = CurrentViewportSize(),
                .IsLeftButtonDown = (e.LeftButton = MouseButtonState.Pressed),
                .Ctrl = ctrl,
                .Alt = alt,
                .Shift = shift
            }

            If MouseMoveCommand.CanExecute(req) Then
                MouseMoveCommand.Execute(req)
                'Handled hier nicht setzen, damit Panning weiterhin klappt
            End If
        End If

        'B) Panning
        If Not _isPanning Then Return
        If e.LeftButton <> MouseButtonState.Pressed Then Return

        Dim payload As New PanRequest With {
            .MousePos = e.GetPosition(AssociatedObject),
            .ViewPortSize = CurrentViewportSize()
        }

        If PanCommand IsNot Nothing AndAlso PanCommand.CanExecute(payload) Then
            PanCommand.Execute(payload)
            e.Handled = True
        End If
    End Sub

    Private Sub OnMouseUp(sender As Object, e As MouseButtonEventArgs)

        Dim ctrl As Boolean, alt As Boolean, shift As Boolean
        ReadModifiers(ctrl, alt, shift)

        'Edit-Ende melden (auch wenn nicht gepannt wurde)
        If ctrl OrElse alt Then
            Dim req As New MapMouseUpRequest With {
                .MousePos = e.GetPosition(AssociatedObject),
               .ViewPortSize = CurrentViewportSize(),
               .Ctrl = ctrl,
               .Alt = alt,
               .Shift = shift
            }

            If MouseUpCommand IsNot Nothing AndAlso MouseUpCommand.CanExecute(req) Then
                MouseUpCommand.Execute(req)
                e.Handled = True
            End If
        End If

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
            .ViewPortSize = New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
        }

        If ZoomCommand IsNot Nothing AndAlso ZoomCommand.CanExecute(payload) Then
            ZoomCommand.Execute(payload)
            e.Handled = True
        End If

    End Sub

    Private Sub OnLostMouseCapture(sender As Object, e As MouseEventArgs)
        If _isPanning Then
            _isPanning = False
            If EndPanCommand IsNot Nothing AndAlso EndPanCommand.CanExecute(Nothing) Then
                EndPanCommand.Execute(Nothing)
            End If
        End If
    End Sub

    Private Sub OnMouseLeave(sender As Object, e As MouseEventArgs)
        If _isPanning AndAlso AssociatedObject.IsMouseCaptured Then
            AssociatedObject.ReleaseMouseCapture()
        End If
    End Sub

    Private Sub FireViewportChanged()

        If ViewportChangedCommand Is Nothing Then Return

        Dim vp As New ViewportChangedRequest With {
            .ViewPortSize = CurrentViewportSize()
        }

        If ViewportChangedCommand.CanExecute(vp) Then ViewportChangedCommand.Execute(vp)
    End Sub

    Private Function CurrentViewportSize() As Size
        Return New Size(AssociatedObject.RenderSize.Width, AssociatedObject.RenderSize.Height)
    End Function

    Private Shared Sub ReadModifiers(ByRef ctrl As Boolean, ByRef alt As Boolean, ByRef shift As Boolean)
        Dim mods = Keyboard.Modifiers

        ctrl = (mods And ModifierKeys.Control) = ModifierKeys.Control
        alt = (mods And ModifierKeys.Alt) = ModifierKeys.Alt
        shift = (mods And ModifierKeys.Shift) = ModifierKeys.Shift
    End Sub
End Class

Public Class ZoomRequest
    Public Property MousePos As Point
    Public Property Delta As Integer
    Public Property ViewPortSize As Size
End Class

Public Class PanRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size
End Class

Public Class ViewportChangedRequest
    Public Property ViewPortSize As Size
End Class

Public Class MapMouseDownRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size
    Public Property IsLeftButton As Boolean
    Public Property Ctrl As Boolean
    Public Property Alt As Boolean
    Public Property Shift As Boolean

End Class

Public Class MapMouseUpRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size
    Public Property Ctrl As Boolean
    Public Property Alt As Boolean
    Public Property Shift As Boolean

End Class

Public Class MapMouseMoveRequest
    Public Property MousePos As Point
    Public Property ViewPortSize As Size

    Public Property IsLeftButtonDown As Boolean

    Public Property Ctrl As Boolean
    Public Property Alt As Boolean

    Public Property Shift As Boolean

End Class

#Enable Warning CA1416 ' Plattformkompatibilität überprüfen