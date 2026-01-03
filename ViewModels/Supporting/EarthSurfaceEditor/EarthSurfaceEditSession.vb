Imports System.CodeDom

Public NotInheritable Class EarthSurfaceEditSession

    Public ReadOnly Property BaseCache As EarthSurfaceCache
    Public ReadOnly Property Delta As DeltaStore
    Public ReadOnly Property History As CellHistoryIndex

    Private ReadOnly _maxUndo As Integer
    Private ReadOnly _undo As New Stack(Of IEditCommand)()
    Private ReadOnly _redo As New Stack(Of IEditCommand)()

    Private _activeGroup As CompositeEditCommand = Nothing
    Private _isGrouping As Boolean = False
    Private _redoClearedForGroup As Boolean = False

    Private Const TidManual As Byte = 254

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

        If _isGrouping AndAlso _activeGroup IsNot Nothing Then

            If Not _redoClearedForGroup Then
                _redo.Clear()
                _redoClearedForGroup = True
            End If

            cmd.Apply(Me)
            _activeGroup.Add(cmd)
            Return True
        End If

        _redo.Clear()

        cmd.Apply(Me)
        _undo.Push(cmd)

        CapUndo()
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

    Public Sub BeginGroup()

        If _isGrouping Then Return
        _isGrouping = True
        _redoClearedForGroup = False
        _activeGroup = New CompositeEditCommand()

    End Sub

    Public Function EndGroup() As Boolean
        If Not _isGrouping Then Return False

        Dim g = _activeGroup
        _activeGroup = Nothing
        _isGrouping = False
        _redoClearedForGroup = False

        If g Is Nothing OrElse g.Count = 0 Then
            Return False
        End If

        _undo.Push(g)
        CapUndo()
        Return True
    End Function

    Public Sub CancelGroup()
        If Not _isGrouping Then Return

        Dim g = _activeGroup
        _activeGroup = Nothing
        _isGrouping = False
        _redoClearedForGroup = False

        If g Is Nothing OrElse g.Count = 0 Then Return
        g.Revert(Me)
    End Sub

    Private Sub CapUndo()
        While _undo.Count > _maxUndo
            Dim tmp = _undo.ToArray()
            Array.Reverse(tmp)
            Dim list = tmp.Skip(1).ToList()
            _undo.Clear()
            For i As Integer = list.Count - 1 To 0 Step -1
                _undo.Push(list(i))
            Next
        End While
    End Sub

    Public Sub Reset()
        Delta.Clear()
        History.Clear()
        _undo.Clear()
        _redo.Clear()
    End Sub

    ''' <summary>
    ''' Wendet die aktuellen Deltas auf den BaseCache an.
    ''' Setzt für alle editierten Zellen (Overrides) optional TID=254.
    ''' Danach ist die Session "clean" (Delta+Undo/Redo leer).
    ''' </summary>
    ''' <returns>Gibt die Anzahl der Änderungen am Cache zurück</returns>
    Public Function CommitToBase(Optional markTidManual As Boolean = True) As Integer

        Dim meta = BaseCache.Meta
        Dim n As Integer = meta.LatCount * meta.LonCount

        If BaseCache.LandMask Is Nothing OrElse BaseCache.LandMask.Length <> n Then
            Throw New InvalidOperationException("Commit nicht möglich: BaseCache hat keine gültige LandMask.")
        End If

        Dim hasTid As Boolean = (meta.HasTid AndAlso BaseCache.Tid IsNot Nothing AndAlso BaseCache.Tid.Length = n)
        Dim editedCount As Integer = 0

        For Each kvp In Delta.LandMaskOverrides
            Dim idx As Integer = kvp.Key
            If idx < 0 OrElse idx >= n Then Continue For

            BaseCache.LandMask(idx) = kvp.Value

            If markTidManual AndAlso hasTid Then
                BaseCache.Tid(idx) = TidManual
            End If

            editedCount += 1
        Next

        'Meta anpassen (Optional, aber sinnvoll, damit man später sieht "war manuell")
        'Wir nutzen LandMaskNotes als Marker:
        BaseCache.Meta.LandMaskNotes = $"manuel edits commmited; tid={If(markTidManual AndAlso hasTid, TidManual.ToString(), "(unchanged)")}; utc={DateTime.UtcNow:O}"

        'Session cleanen
        Reset()

        Return editedCount
    End Function

End Class
