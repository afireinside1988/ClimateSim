Imports System.Diagnostics.Eventing.Reader

Public NotInheritable Class CompositeEditCommand
    Implements IEditCommand

    Private ReadOnly _commands As New List(Of IEditCommand)

    Public ReadOnly Property Count As Integer
        Get
            Return _commands.Count
        End Get
    End Property

    Public Sub Add(cmd As IEditCommand)
        If cmd Is Nothing Then Return
        _commands.Add(cmd)
    End Sub

    Public ReadOnly Property Description As String Implements IEditCommand.Description
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Sub Apply(session As EarthSurfaceEditSession) Implements IEditCommand.Apply
        For Each c In _commands
            c.Apply(session)
        Next
    End Sub

    Public Sub Revert(session As EarthSurfaceEditSession) Implements IEditCommand.Revert

        For i As Integer = _commands.Count - 1 To 0 Step -1
            _commands(i).Revert(session)
        Next
    End Sub

End Class
