Imports System.Text

Module Exceptions

    Public Function FlattenException(ex As Exception, Optional showStackTrace As Boolean = True) As String
        Dim sb As New StringBuilder()

        Dim cur As Exception = ex
        Dim level As Integer = 0

        While cur IsNot Nothing
            sb.AppendLine($"[{level}] {cur.GetType().FullName}: {cur.Message}")
            If showStackTrace Then
                sb.AppendLine(cur.StackTrace)
            End If
            sb.AppendLine()
            cur = cur.InnerException
            level += 1
        End While

        Return sb.ToString()
    End Function

End Module
