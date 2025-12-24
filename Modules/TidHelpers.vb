Module TidHelpers

    Public Function TidValueToCode(value As Single) As Integer

        'NaN / Infinity -> Unknown
        If Single.IsNaN(value) OrElse Single.IsInfinity(value) Then Return 255

        'GEBCO speichert TID als Single, Ursprung ist Byte
        'Runden ist korrekt (nicht Floor!)

        Dim code As Integer = CInt(Math.Round(value))
        If code < 0 Then Return 255
        If code > 255 Then Return 255

        Return code
    End Function

    Public Function GetTidCode(cache As EarthSurfaceCache, linearIndex As Integer) As Integer

        If cache Is Nothing OrElse cache.Tid Is Nothing Then Return 255
        If linearIndex < 0 OrElse linearIndex >= cache.Tid.Length Then Return 255

        Return TidValueToCode(cache.Tid(linearIndex))
    End Function
End Module
