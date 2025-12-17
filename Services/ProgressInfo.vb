Public Class ProgressInfo
    Public Property Message As String
    Public Property Percent As Integer '0..100

    Public Sub New(message As String, percent As Integer)
        Me.Message = message
        Me.Percent = Math.Max(0, Math.Min(100, percent))
    End Sub
End Class
