Public Class BedMachineCommon

    Public Const ProgressThrottleRowInterval As Integer = 128

    Public Shared Function ReadThickness(val As Single, noData As Double, hasNoData As Integer) As Single

        If hasNoData <> 0 AndAlso Math.Abs(val - noData) < 1.0E+30 Then
            Return Single.NaN
        End If
        Return val
    End Function

End Class
