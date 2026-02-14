Imports System.IO
Imports System.Text
Imports System.Threading

Module CSVHelpers

    Public Function CountCsvRowsFromMemory(csvBytes As Byte(), ct As CancellationToken) As Integer

        Using ms As New MemoryStream(csvBytes, writable:=False)

            Using sr As New StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks:=True, bufferSize:=65536, leaveOpen:=True)

                Dim first As Boolean = True
                Dim count As Integer = 0

                While Not sr.EndOfStream

                    ct.ThrowIfCancellationRequested()
                    Dim line As String = sr.ReadLine()
                    If line Is Nothing Then Exit While

                    If first Then
                        first = False   'Header
                    ElseIf line.Length > 0 Then
                        count += 1
                    End If

                End While

                Return count
            End Using

        End Using
    End Function

    Public Function FindHeaderIndex(header As String(), name As String) As Integer

        For i As Integer = 0 To header.Length - 1
            If String.Equals(header(i), name, StringComparison.OrdinalIgnoreCase) Then Return i
        Next

        Return -1

    End Function

    Public Function GetFieldSafe(fields As String(), idx As Integer) As String
        If idx < 0 OrElse idx >= fields.Length Then Return Nothing
        Return fields(idx)
    End Function


End Module
