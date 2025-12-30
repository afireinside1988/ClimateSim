Public NotInheritable Class DeltaStore

    Public ReadOnly Property LandMaskOverrides As Dictionary(Of Integer, Byte)

    Public Sub New()
        LandMaskOverrides = New Dictionary(Of Integer, Byte)
    End Sub

    Public Function TryGetLandMask(idx As Integer, ByRef value As Byte) As Boolean
        Return LandMaskOverrides.TryGetValue(idx, value)
    End Function

    Public Sub SetLandMask(idx As Integer, value As Byte)
        LandMaskOverrides(idx) = value
    End Sub

    Public Sub RemoveLandMask(idx As Integer)
        LandMaskOverrides.Remove(idx)
    End Sub

    Public Function HasLandMaskOverride(idx As Integer) As Boolean
        Return LandMaskOverrides.ContainsKey(idx)
    End Function

    Public Sub Clear()
        LandMaskOverrides.Clear()
    End Sub

End Class
