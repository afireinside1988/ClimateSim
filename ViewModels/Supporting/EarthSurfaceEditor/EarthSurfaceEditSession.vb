Imports System.CodeDom

Public NotInheritable Class EarthSurfaceEditSession

    Public ReadOnly Property BaseCache As EarthSurfaceCache
    Public ReadOnly Property Delta As DeltaStore
    Public ReadOnly Property History As CellHistoryIndex

    Private ReadOnly _maxUndo As Integer
    Private ReadOnly _undo As New Stack(Of IEditCommand)()
    Private ReadOnly _redo As New Stack(Of IEditCommand)()

    Public ReadOnly Property HasUnsavedChanges As Boolean
        Get
            Return Delta.LandMaskOverrides.Count > 0 OrElse _undo.Count > 0
        End Get
    End Property

    Public ReadOnly Property UndoCount As Integer
        Get
            Return _undo.Count
        End Get
    End Property

    Public ReadOnly Property RedoCount As Integer
        Get
            Return _redo.Count
        End Get
    End Property

    Public Sub New(baseCache As EarthSurfaceCache,
                   Optional maxUndoCommands As Integer = 200,
                   Optional historyCapPerCell As Integer = 50)

        ArgumentNullException.ThrowIfNull(baseCache)
        Me.BaseCache = baseCache

        _maxUndo = Math.Max(1, maxUndoCommands)

        Me.Delta = New DeltaStore()
        Me.History = New CellHistoryIndex(historyCapPerCell)
    End Sub

    Public Function GetBaseLandMask(idx As Integer) As Byte
        If BaseCache.LandMask Is Nothing OrElse idx < 0 OrElse idx >= BaseCache.LandMask.Length Then
            Return 0    'Fallback Wasser
        End If
        Return BaseCache.LandMask(idx)
    End Function

    Public Function GetEffectiveLandMask(idx As Integer) As Byte
        Dim v As Byte
        If Delta.TryGetLandMask(idx, v) Then Return v
        Return GetBaseLandMask(idx)
    End Function

    Public Function ApplyCommand(cmd As IEditCommand) As Boolean
        If cmd Is Nothing Then Return False

        'Neue Aktion löscht Redo
        _redo.Clear()

        cmd.Apply(Me)
        _undo.Push(cmd)

        'Cap 200: wenn über Limit: unten rauswerfen
        While _undo.Count > _maxUndo
            'Stack kann nicht direkt unten poppen -> minimal: in Liste umpacken
            Dim tmp = _undo.ToArray()   'top->bottom
            Array.Reverse(tmp)          'bottom->top
            Dim list = tmp.Skip(1).ToList()
            _undo.Clear()
            For i As Integer = list.Count - 1 To 0 Step -1
                _undo.Push(list(i))
            Next
        End While

        Return True
    End Function

    Public Function CanUndo() As Boolean
        Return _undo.Count > 0
    End Function

    Public Function CanRedo() As Boolean
        Return _redo.Count > 0
    End Function

    Public Function Undo(ByRef cmd As IEditCommand) As Boolean
        cmd = Nothing
        If _undo.Count = 0 Then Return False

        cmd = _undo.Pop()
        cmd.Revert(Me)
        _redo.Push(cmd)
        Return True
    End Function

    Public Function Redo(ByRef cmd As IEditCommand) As Boolean
        cmd = Nothing
        If _redo.Count = 0 Then Return False

        cmd = _redo.Pop()
        cmd.Apply(Me)
        _undo.Push(cmd)
        Return True
    End Function

    Public Sub Reset()
        Delta.Clear()
        History.Clear()
        _undo.Clear()
        _redo.Clear()
    End Sub

End Class
