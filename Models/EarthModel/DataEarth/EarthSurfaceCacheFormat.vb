Imports System.IO
Imports System.Text

''' <summary>
''' Binärformat für EarthSurface-Cache.
''' 
''' Datei-Aufbau (v1, little-endian):
''' [HEADER]
'''     0..3    : Magic "ESCF" (Earth Surface Cache File)
'''     4..7    : Int32 Version (=1)
'''     8..11   : Int32 LatCount
'''    12..15   : Int32 LonCount
'''    16..23   : Double CellSizeDeg
'''    24..27   : Int32 Flags (Bitfeld)
'''               Bit0 = HasHeight
'''               Bit1 = HasTid
'''    28..31   : Int32 Reserved (0)
'''    
''' [PAYLOAD]
'''     Wenn HasHeight: HeightM float32[LatCount*LonCount]
'''        Wenn HasTiD: Tid     float32[LatCount*LonCount]
'''        
''' Meta wird seperat als JSON gespeichert (.meta.json) und ist "User-facing".
''' </summary>
Public NotInheritable Class EarthSurfaceCacheFormat

    Public Const CurrentVersion As Integer = 1
    Private Const Magic As String = "ESCF"

    <Flags>
    Public Enum CacheFlags As Integer
        None = 0
        HasHeight = 1
        HasTid = 2
    End Enum

    Private Sub New()

    End Sub

    Public Shared Sub WriteCache(binPath As String, cache As EarthSurfaceCache)
        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim meta = cache.Meta

        Dim expectedLen As Integer = meta.LatCount * meta.LonCount
        If meta.HasHeight AndAlso (cache.HeightM Is Nothing OrElse cache.HeightM.Length <> expectedLen) Then
            Throw New InvalidDataException("HeightM-Array fehlt oder hat eine falsche Länge.")
        End If
        If meta.HasTid AndAlso (cache.Tid Is Nothing OrElse cache.Tid.Length <> expectedLen) Then
            Throw New InvalidDataException("Tid-Array fehlt oder hat eine falsche Länge.")
        End If

        Dim flags As CacheFlags = CacheFlags.None
        If meta.HasHeight Then flags = flags Or CacheFlags.HasHeight
        If meta.HasTid Then flags = flags Or CacheFlags.HasTid

        Directory.CreateDirectory(Path.GetDirectoryName(binPath))

        'Atomisch schreiben: temp -> replace
        Dim tmp As String = binPath & ".tmp"

        Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None)
            Using bw As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=False)

                'Header
                bw.Write(Encoding.ASCII.GetBytes(Magic))    '4 bytes
                bw.Write(CurrentVersion)                    'Int32
                bw.Write(meta.LatCount)                     'Int32
                bw.Write(meta.LonCount)                     'Int32
                bw.Write(meta.CellSizeDeg)                  'Double
                bw.Write(CInt(flags))                       'Int32
                bw.Write(0)                                 'Reserved Int32

                'Payload
                If meta.HasHeight Then
                    For i As Integer = 0 To cache.HeightM.Length - 1
                        bw.Write(cache.HeightM(i))          'Single
                    Next
                End If

                If meta.HasTid Then
                    For i As Integer = 0 To cache.Tid.Length - 1
                        bw.Write(cache.Tid(i))              'Single
                    Next
                End If
            End Using
        End Using

        If File.Exists(binPath) Then
            File.Replace(tmp, binPath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, binPath)
        End If
    End Sub


End Class
