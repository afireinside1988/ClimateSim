Imports System.Windows.Input
Imports System.Threading.Tasks

Public Class AsyncRelayCommand(Of T)
    Implements ICommand

    Private ReadOnly _executeAsync As Func(Of T, Task)
    Private ReadOnly _canExecute As Func(Of T, Boolean)
    Private _isExecuting As Boolean = False

    Public Sub New(executeAsync As Func(Of T, Task),
                   Optional canExecute As Func(Of T, Boolean) = Nothing)
        ArgumentNullException.ThrowIfNull(executeAsync)
        _executeAsync = executeAsync
        _canExecute = If(canExecute, Function(o As T) True)
    End Sub

    Public Custom Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged
        AddHandler(value As EventHandler)
            AddHandler CommandManager.RequerySuggested, value
        End AddHandler

        RemoveHandler(value As EventHandler)
            AddHandler CommandManager.RequerySuggested, value
        End RemoveHandler

        RaiseEvent(sender As Object, e As EventArgs)
            'Keine Implementierung notwendig
        End RaiseEvent
    End Event

    Public Async Sub Execute(parameter As Object) Implements ICommand.Execute
        ' Dim p As T = Nothing
        'If parameter IsNot Nothing Then
        'p = CType(parameter, T)
        'End If

        _isExecuting = True
        RaiseCanExecuteChanged()

        Try
            Await _executeAsync(CType(parameter, T))
        Finally
            _isExecuting = False
            RaiseCanExecuteChanged()
        End Try
    End Sub

    Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
        If _isExecuting Then
            Return False
        End If

        'Dim p As T = Nothing
        'If parameter IsNot Nothing Then
        'p = CType(parameter, T)
        'End If

        Return _canExecute(CType(parameter, T))
    End Function

    Public Sub RaiseCanExecuteChanged()
        CommandManager.InvalidateRequerySuggested()
    End Sub
End Class
