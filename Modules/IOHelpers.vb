Imports System.IO
Imports System.Text
Imports System.Threading

Module IOHelpers

    Public Sub WriteTextAtomic(savePath As String, content As String, Optional ct As CancellationToken = Nothing)

        ct.ThrowIfCancellationRequested()

        Directory.CreateDirectory(Path.GetDirectoryName(savePath))
        Dim tmp As String = savePath & ".tmp"
        File.WriteAllText(tmp, content, Encoding.UTF8)

        ct.ThrowIfCancellationRequested()

        If File.Exists(savePath) Then
            File.Replace(tmp, savePath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, savePath)
        End If
    End Sub

End Module
