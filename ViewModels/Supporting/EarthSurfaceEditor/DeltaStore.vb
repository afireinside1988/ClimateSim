Public NotInheritable Class DeltaStore

    Public ReadOnly Property LandMaskOverrides As Dictionary(Of Integer, Byte)
    Public ReadOnly Property HeightOverrides As Dictionary(Of Integer, Single)

    Public Sub New()
        LandMaskOverrides = New Dictionary(Of Integer, Byte)
        HeightOverrides = New Dictionary(Of Integer, Single)
    End Sub

    Public Sub Clear()
        LandMaskOverrides.Clear()
        HeightOverrides.Clear()
    End Sub

#Region "LandMask"
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

#End Region

#Region "Height"

    Public Function TryGetHeight(idx As Integer, ByRef value As Single) As Boolean
        Return HeightOverrides.TryGetValue(idx, value)
    End Function

    Public Sub SetHeight(idx As Integer, value As Single)
        HeightOverrides(idx) = value
    End Sub

    Public Sub RemoveHeight(idx As Integer)
        HeightOverrides.Remove(idx)
    End Sub

    Public Function HasHeightOverride(idx As Integer) As Boolean
        Return HeightOverrides.ContainsKey(idx)
    End Function

#End Region

End Class
