Imports Microsoft.Xaml.Behaviors
#Disable Warning CA1416 'Plattformkompatibilität überprüfen
Public Class ClearOnDeleteBehavior
    Inherits Behavior(Of TextBox)

    Public Property Command As ICommand
        Get
            Return CType(GetValue(CommandProperty), ICommand)
        End Get
        Set(value As ICommand)
            SetValue(CommandProperty, value)
        End Set
    End Property
    Public Shared ReadOnly CommandProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(Command), GetType(ICommand), GetType(ClearOnDeleteBehavior), New PropertyMetadata(Nothing))

    Public Property OnlyIfNotEmpty As Boolean
        Get
            Return CBool(GetValue(OnlyIfNotEmptyProperty))
        End Get
        Set(value As Boolean)
            SetValue(OnlyIfNotEmptyProperty, value)
        End Set
    End Property
    Public Shared ReadOnly OnlyIfNotEmptyProperty As DependencyProperty =
        DependencyProperty.Register(NameOf(OnlyIfNotEmpty), GetType(Boolean), GetType(ClearOnDeleteBehavior), New PropertyMetadata(Nothing))

    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()
        AddHandler AssociatedObject.PreviewKeyDown, AddressOf OnPreviewKeyDown
    End Sub

    Protected Overrides Sub OnDetaching()
        RemoveHandler AssociatedObject.PreviewKeyDown, AddressOf OnPreviewKeyDown
        MyBase.OnDetaching()
    End Sub

    Private Sub OnPreviewKeyDown(sender As Object, e As KeyEventArgs)

        If e.Key <> Key.Delete AndAlso e.Key <> Key.Back Then Return

        Dim tb = AssociatedObject
        If tb Is Nothing OrElse Not tb.IsKeyboardFocusWithin Then Return

        If OnlyIfNotEmpty AndAlso String.IsNullOrEmpty(tb.Text) Then Return

        Dim cmd = Command
        If cmd Is Nothing Then Return

        If cmd.CanExecute(Nothing) Then
            cmd.Execute(Nothing)
            e.Handled = True
        End If
    End Sub

End Class
#Enable Warning CA1416 'Plattformkompatibilität überprüfen