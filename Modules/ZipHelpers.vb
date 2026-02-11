Imports System.IO
Imports System.IO.Compression

Public Module ZipHelpers

    Public Function OpenZipEntryStream(zipPath As String, entryName As String) As Stream

        Dim fs As New FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read)
        Dim za As New ZipArchive(fs, ZipArchiveMode.Read, leaveOpen:=False)

        Dim entry = za.GetEntry(entryName)
        If entry Is Nothing Then
            za.Dispose()
            fs.Dispose()
            Throw New FileNotFoundException($"ZIP-Entry nicht gefunden: {entryName}")
        End If

        Dim entryStream As Stream = entry.Open()
        Return New CompositeReadStream(entryStream, za, fs)
    End Function

    Public Function FindZipEntryEndingWith(zipPath As String, suffix As String) As String

        Using fs As FileStream = File.OpenRead(zipPath)
            Using za As New ZipArchive(fs, ZipArchiveMode.Read, leaveOpen:=False)
                For Each e As ZipArchiveEntry In za.Entries
                    If e.FullName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) Then
                        Return e.FullName
                    End If
                Next
            End Using
        End Using

        Return Nothing
    End Function

    Public NotInheritable Class CompositeReadStream
        Inherits Stream

        Private ReadOnly _inner As Stream
        Private ReadOnly _za As ZipArchive
        Private ReadOnly _fs As FileStream

        Public Sub New(inner As Stream, za As ZipArchive, fs As FileStream)
            _inner = inner
            _za = za
            _fs = fs
        End Sub

        Protected Overrides Sub Dispose(disposing As Boolean)
            If disposing Then
                Try : _inner.Dispose() : Catch : End Try
                Try : _za.Dispose() : Catch : End Try
                Try : _fs.Dispose() : Catch : End Try
            End If
            MyBase.Dispose(disposing)
        End Sub

        Public Overrides ReadOnly Property CanRead As Boolean
            Get
                Return True
            End Get
        End Property

        Public Overrides ReadOnly Property CanSeek As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property CanWrite As Boolean
            Get
                Return False
            End Get
        End Property

        Public Overrides ReadOnly Property Length As Long
            Get
                Throw New NotSupportedException()
            End Get
        End Property

        Public Overrides Property Position As Long
            Get
                Throw New NotSupportedException()
            End Get
            Set(value As Long)
                Throw New NotSupportedException()
            End Set
        End Property

        Public Overrides Sub Flush()
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub SetLength(value As Long)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Sub Write(buffer() As Byte, offset As Integer, count As Integer)
            Throw New NotSupportedException()
        End Sub

        Public Overrides Function Read(buffer() As Byte, offset As Integer, count As Integer) As Integer
            Return _inner.Read(buffer, offset, count)
        End Function

        Public Overrides Function Seek(offset As Long, origin As SeekOrigin) As Long
            Throw New NotSupportedException()
        End Function
    End Class

End Module
