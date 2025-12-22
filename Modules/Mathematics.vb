Module Mathematics
    Public Function DegToRad(deg As Double) As Double
        Return deg * Math.PI / 180.0
    End Function

    Public Function Clamp(x As Double, lo As Double, hi As Double) As Double

        If x < lo Then Return lo
        If x > hi Then Return hi
        Return x
    End Function

    ''' <summary>
    ''' Wrap auf [-180,+180]
    ''' </summary>
    Public Function WrapLon180(lonDeg As Double) As Double
        Dim x As Double = lonDeg

        x = ((x + 180.0) Mod 360.0 + 360.0) Mod 360.0
        Return x - 180.0
    End Function

End Module
