Module TidHelpers

    Public Const ManualTidCode As Byte = 254

    Public Function TidByteToCode(value As Byte) As Integer

        If value = 255 Then Return 255
        Return CInt(value)
    End Function

    Public Function TryGetTidByte(cache As EarthSurfaceCache, linearIndex As Integer, ByRef tid As Byte) As Boolean
        tid = 255

        If cache Is Nothing OrElse cache.Tid Is Nothing Then Return False
        If linearIndex < 0 OrElse linearIndex >= cache.Tid.Length Then Return False
        tid = cache.Tid(linearIndex)
        Return True
    End Function

End Module
