Public NotInheritable Class CellHistoryIndex

    Private ReadOnly _capPerCell As Integer
    Private ReadOnly _map As New Dictionary(Of Integer, LinkedList(Of EditEvent))

    Public Sub New(Optional capPerCell As Integer = 50)
        _capPerCell = Math.Max(1, capPerCell)
    End Sub

    Public Sub Add(idx As Integer, ev As EditEvent)
        If Not _map.TryGetValue(idx, Nothing) Then
            _map(idx) = New LinkedList(Of EditEvent)()
        End If

        Dim list = _map(idx)
        list.AddFirst(ev)

        While list.Count > _capPerCell
            list.RemoveLast()
        End While
    End Sub

    Public Function GetRecent(idx As Integer, Optional maxN As Integer = 10) As List(Of EditEvent)
        Dim result As New List(Of EditEvent)()
        Dim list As LinkedList(Of EditEvent) = Nothing
        If Not _map.TryGetValue(idx, list) OrElse list Is Nothing Then Return result

        Dim n As Integer = 0
        For Each ev In list
            result.Add(ev)
            n += 1
            If n >= maxN Then Exit For
        Next
        Return result
    End Function

    Public Sub Clear()
        _map.Clear()
    End Sub

End Class
