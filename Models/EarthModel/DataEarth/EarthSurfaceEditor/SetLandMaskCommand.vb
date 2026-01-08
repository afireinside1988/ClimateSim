Public NotInheritable Class SetLandMaskCommand
    Implements IEditCommand

    Private ReadOnly _idx As Integer
    Public ReadOnly Property Index As Integer
        Get
            Return _idx
        End Get
    End Property

    Private ReadOnly _newValue As Byte
    Public ReadOnly Property NewValue As Byte
        Get
            Return _newValue
        End Get
    End Property

    Private _hadOldOverride As Boolean
    Private _oldOverrideValue As Byte
    Private _baseValue As Byte

    Public Sub New(idx As Integer, newValue As Byte)
        _idx = idx
        _newValue = newValue
    End Sub

    Public ReadOnly Property Description As String Implements IEditCommand.Description
        Get
            Return $"Set LandMask[{_idx}] = {_newValue}"
        End Get
    End Property

    Public Sub Apply(session As EarthSurfaceEditSession) Implements IEditCommand.Apply
        _baseValue = session.GetBaseLandMask(_idx)

        _hadOldOverride = session.Delta.LandMaskOverrides.TryGetValue(_idx, _oldOverrideValue)

        'Wenn der neue Wert identisch zum Base ist: Override entfernen (-> "zurücksetzen")
        If _newValue = _baseValue Then
            session.Delta.RemoveLandMask(_idx)
        Else
            session.Delta.SetLandMask(_idx, _newValue)
        End If

        session.History.Add(_idx, New EditEvent With {
            .TimeStampUtc = DateTime.UtcNow,
            .Channel = EditChannel.LandMask,
            .OldValue = If(_hadOldOverride, CInt(_oldOverrideValue), CInt(_baseValue)),
            .NewValue = CInt(_newValue),
            .Label = "Gesetzt"
        })
    End Sub

    Public Sub Revert(session As EarthSurfaceEditSession) Implements IEditCommand.Revert

        If _hadOldOverride Then
            session.Delta.SetLandMask(_idx, _oldOverrideValue)
        Else
            session.Delta.RemoveLandMask(_idx)
        End If

        session.History.Add(_idx, New EditEvent With {
            .TimeStampUtc = DateTime.UtcNow,
            .Channel = EditChannel.LandMask,
            .OldValue = CInt(_newValue),
            .NewValue = If(_hadOldOverride, CInt(_oldOverrideValue), CInt(_baseValue)),
            .Label = "Rückgängig"
        })
    End Sub
End Class
