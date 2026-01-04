Public NotInheritable Class SetHeightCommand
    Implements IEditCommand

    Private ReadOnly _idx As Integer
    Public ReadOnly Property Index As Integer
        Get
            Return _idx
        End Get
    End Property

    Private ReadOnly _newValue As Single
    Public ReadOnly Property NewValue As Single
        Get
            Return _newValue
        End Get
    End Property

    Private _hadOldOverride As Boolean
    Private _oldOverrideValue As Single
    Private _baseValue As Single

    Public Sub New(idx As Integer, newValue As Single)
        _idx = idx
        _newValue = newValue
    End Sub

    Public ReadOnly Property Description As String Implements IEditCommand.Description
        Get
            Return $"Set Height[{_idx}] = {_newValue}"
        End Get
    End Property

    Public Sub Apply(session As EarthSurfaceEditSession) Implements IEditCommand.Apply

        If session Is Nothing Then Throw New ArgumentNullException(NameOf(session))

        _baseValue = session.GetBaseHeight(_idx)

        Dim tmp As Single
        _hadOldOverride = session.Delta.TryGetHeight(_idx, tmp)
        If _hadOldOverride Then _oldOverrideValue = tmp

        'Wenn neuer Wert identisch zum Base: Override entfernen
        If _newValue = _baseValue Then
            session.Delta.RemoveHeight(_idx)
        Else
            session.Delta.SetHeight(_idx, _newValue)
        End If

        session.History.Add(_idx, New EditEvent With {
            .TimeStampUtc = DateTime.UtcNow,
            .Channel = EditChannel.Height,
            .OldValue = HeightToHistoryInt(If(_hadOldOverride, _oldOverrideValue, _baseValue)),
            .NewValue = HeightToHistoryInt(_newValue),
            .Label = "Gesetzt"
        })
    End Sub

    Public Sub Revert(session As EarthSurfaceEditSession) Implements IEditCommand.Revert

        If session Is Nothing Then Throw New ArgumentNullException(NameOf(session))

        If _hadOldOverride Then
            session.Delta.SetHeight(_idx, _oldOverrideValue)
        Else
            session.Delta.RemoveHeight(_idx)
        End If

        session.History.Add(_idx, New EditEvent With {
            .TimeStampUtc = DateTime.UtcNow,
            .Channel = EditChannel.Height,
            .OldValue = HeightToHistoryInt(_newValue),
            .NewValue = HeightToHistoryInt(If(_hadOldOverride, _oldOverrideValue, _baseValue)),
            .Label = "Rückgängig"
        })
    End Sub

    'History speichert Integer -> wir definieren eine stabile Abbildung
    'Meter wird auf Integer gerundet
    Private Shared Function HeightToHistoryInt(v As Single) As Integer
        If Single.IsNaN(v) OrElse Single.IsInfinity(v) Then Return Integer.MinValue
        Dim d As Double = CDbl(v)
        Dim r As Integer = CInt(Math.Round(d, MidpointRounding.AwayFromZero))
        Return r
    End Function

End Class
