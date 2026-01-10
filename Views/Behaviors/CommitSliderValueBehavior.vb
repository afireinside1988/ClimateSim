Imports Microsoft.Xaml.Behaviors
Imports System.Windows.Controls.Primitives

#Disable Warning CA1416
''' <summary>
''' Committet den Slider.Value-Binding-Source erst beim Loslassen des Thumbs.
''' Voraussetzung im XAML: Value Binding muss UpdateSourceTrigger=Explicit haben.
''' </summary>
Public Class CommitSliderValueBehavior
    Inherits Behavior(Of Slider)

    Private _isDragging As Boolean

    Public Property CommitOnKeyboard As Boolean = True
    Public Property CommitOnMouseWheel As Boolean = True
    Public Property CommitOnMouseUp As Boolean = True

    Private ReadOnly _dragStartedHandler As DragStartedEventHandler
    Private ReadOnly _dragCompletedHandler As DragCompletedEventHandler

    Public Sub New()
        _dragStartedHandler = New DragStartedEventHandler(AddressOf OnDragStarted)
        _dragCompletedHandler = New DragCompletedEventHandler(AddressOf OnDragCompleted)
    End Sub

    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()

        ' Routed Events: per UIElement.AddHandler(...) / RemoveHandler(...)
        AssociatedObject.AddHandler(Thumb.DragStartedEvent, _dragStartedHandler, True)
        AssociatedObject.AddHandler(Thumb.DragCompletedEvent, _dragCompletedHandler, True)

        ' CLR Events: per VB AddHandler
        If CommitOnKeyboard Then
            AddHandler AssociatedObject.PreviewKeyUp, AddressOf OnPreviewKeyUp
        End If

        If CommitOnMouseWheel Then
            AddHandler AssociatedObject.PreviewMouseWheel, AddressOf OnPreviewMouseWheel
        End If

        If CommitOnMouseUp Then
            AddHandler AssociatedObject.PreviewMouseLeftButtonUp, AddressOf OnPreviewMouseLeftButtonUp
        End If
    End Sub

    Protected Overrides Sub OnDetaching()
        MyBase.OnDetaching()

        AssociatedObject.RemoveHandler(Thumb.DragStartedEvent, _dragStartedHandler)
        AssociatedObject.RemoveHandler(Thumb.DragCompletedEvent, _dragCompletedHandler)

        If CommitOnKeyboard Then
            RemoveHandler AssociatedObject.PreviewKeyUp, AddressOf OnPreviewKeyUp
        End If

        If CommitOnMouseWheel Then
            RemoveHandler AssociatedObject.PreviewMouseWheel, AddressOf OnPreviewMouseWheel
        End If

        If CommitOnMouseUp Then
            RemoveHandler AssociatedObject.PreviewMouseLeftButtonUp, AddressOf OnPreviewMouseLeftButtonUp
        End If
    End Sub

    Private Sub OnDragStarted(sender As Object, e As DragStartedEventArgs)
        _isDragging = True
    End Sub

    Private Sub OnDragCompleted(sender As Object, e As DragCompletedEventArgs)
        _isDragging = False
        CommitValueToSource()
    End Sub

    Private Sub OnPreviewKeyUp(sender As Object, e As System.Windows.Input.KeyEventArgs)
        If _isDragging Then Return

        Select Case e.Key
            Case System.Windows.Input.Key.Left,
                 System.Windows.Input.Key.Right,
                 System.Windows.Input.Key.Up,
                 System.Windows.Input.Key.Down,
                 System.Windows.Input.Key.PageUp,
                 System.Windows.Input.Key.PageDown,
                 System.Windows.Input.Key.Home,
                 System.Windows.Input.Key.End

                CommitValueToSource()
        End Select
    End Sub

    Private Sub OnPreviewMouseWheel(sender As Object, e As System.Windows.Input.MouseWheelEventArgs)
        If _isDragging Then Return
        CommitValueToSource()
    End Sub

    Private Sub OnPreviewMouseLeftButtonUp(sender As Object, e As System.Windows.Input.MouseButtonEventArgs)
        If _isDragging Then Return
        CommitValueToSource()
    End Sub

    Private Sub CommitValueToSource()
        Dim be As BindingExpression = AssociatedObject.GetBindingExpression(Slider.ValueProperty)
        If be Is Nothing Then Return

        Try
            be.UpdateSource()
        Catch
            ' optional: Logging
        End Try
    End Sub
End Class

#Enable Warning CA1416