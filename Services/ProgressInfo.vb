Public Class ProgressInfo

    Public Const Indeterminate As Integer = -1
    Public Property Message As String
    Public Property Percent As Integer '-1 (indeterminate) oder 0..100

    Public Sub New(message As String, percent As Integer)
        Me.Message = message

        If percent < 0 Then
            Me.Percent = Indeterminate
        Else
            Me.Percent = Math.Max(0, Math.Min(100, percent))
        End If
    End Sub
End Class
