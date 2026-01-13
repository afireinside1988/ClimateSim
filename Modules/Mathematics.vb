Module Mathematics
    Public Function DegToRad(deg As Double) As Double
        Return deg * Math.PI / 180.0
    End Function

    Public Function RadToDeg(rad As Double) As Double
        Return rad * 180.0 / Math.PI
    End Function

    Public Function Clamp(x As Double, lo As Double, hi As Double) As Double

        If x < lo Then Return lo
        If x > hi Then Return hi
        Return x
    End Function

    Public Function Clamp(x As Integer, lo As Integer, hi As Integer) As Integer

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

    Public Function Wrap360(deg As Double) As Double
        deg = deg Mod 360.0
        If deg < 0 Then deg += 360.0
        Return deg
    End Function

    Public Function Wrap180(deg As Double) As Double
        deg = Wrap360(deg)
        If deg > 180.0 Then deg -= 360.0
        Return deg
    End Function

End Module
