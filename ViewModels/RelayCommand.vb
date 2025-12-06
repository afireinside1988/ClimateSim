Imports System.Windows.Input

Public Class RelayCommand(Of T)
    Implements ICommand

    Private ReadOnly _execute As Action(Of T)
    Private ReadOnly _canExecute As Func(Of T, Boolean)

    Public Sub New(execute As Action(Of T), Optional canExecute As Func(Of T, Boolean) = Nothing)
        ArgumentNullException.ThrowIfNull(execute)
        _execute = execute
        _canExecute = If(canExecute, Function(o As T) True)
    End Sub

    Public Custom Event CanExecuteChanged As EventHandler Implements ICommand.CanExecuteChanged
        AddHandler(value As EventHandler)
            AddHandler CommandManager.RequerySuggested, value
        End AddHandler

        RemoveHandler(value As EventHandler)
            RemoveHandler CommandManager.RequerySuggested, value
        End RemoveHandler

        RaiseEvent(sender As Object, e As EventArgs)
            'Keine Implementierung notwendig
        End RaiseEvent
    End Event

    Public Sub Execute(parameter As Object) Implements ICommand.Execute
        'Dim p As T = Nothing
        'If parameter IsNot Nothing Then
        'p = CType(parameter, T)
        'End If
        _execute(CType(parameter, T))
    End Sub

    Public Function CanExecute(parameter As Object) As Boolean Implements ICommand.CanExecute
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
