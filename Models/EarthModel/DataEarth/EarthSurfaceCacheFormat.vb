Imports System.IO
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Threading

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
'''               Bit2 = HasLandMask
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
        HasLandMask = 4
    End Enum

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Speichert Cache als Binary-Datei.
    ''' Progress ist optional und wird throttled (nicht bei jedem Element).
    ''' </summary>
    Public Shared Sub WriteCache(binPath As String, cache As EarthSurfaceCache,
                                 Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                 Optional ct As CancellationToken = Nothing)
        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim meta = cache.Meta

        Dim expectedLen As Integer = meta.LatCount * meta.LonCount
        If meta.HasHeight AndAlso (cache.HeightM Is Nothing OrElse cache.HeightM.Length <> expectedLen) Then
            Throw New InvalidDataException("HeightM-Array fehlt oder hat eine falsche Länge.")
        End If
        If meta.HasTid AndAlso (cache.Tid Is Nothing OrElse cache.Tid.Length <> expectedLen) Then
            Throw New InvalidDataException("Tid-Array fehlt oder hat eine falsche Länge.")
        End If
        If meta.HasLandMask AndAlso (cache.LandMask Is Nothing OrElse cache.LandMask.Length <> expectedLen) Then
            Throw New InvalidDataException("LandMask-Array fehlt oder hat eine falsche Länge.")
        End If

        Dim flags As CacheFlags = CacheFlags.None
        If meta.HasHeight Then flags = flags Or CacheFlags.HasHeight
        If meta.HasTid Then flags = flags Or CacheFlags.HasTid
        If meta.HasLandMask Then flags = flags Or CacheFlags.HasLandMask

        Directory.CreateDirectory(Path.GetDirectoryName(binPath))

        progress?.Report(New ProgressInfo("Cache speichern: Vorbereitung...", 0))
        ct.ThrowIfCancellationRequested()

        'Atomisch schreiben: temp -> replace
        Dim tmp As String = binPath & ".tmp"

        'Throttling: ca. 200 Updates max (mind. alle 4096)
        Dim totalValues As Integer = 0
        If meta.HasHeight Then totalValues += cache.HeightM.Length
        If meta.HasTid Then totalValues += cache.Tid.Length
        If meta.HasLandMask Then totalValues += cache.LandMask.Length

        Dim reportEvery As Integer = Math.Max(4096, totalValues \ 200)
        Dim processed As Integer = 0

        Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize:=1024 * 1024, useAsync:=False)
            Using bw As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=False)

                'Header
                progress?.Report(New ProgressInfo("Cache speichern: Header...", 1))
                ct.ThrowIfCancellationRequested()

                bw.Write(Encoding.ASCII.GetBytes(Magic))    '4 bytes
                bw.Write(CurrentVersion)                    'Int32
                bw.Write(meta.LatCount)                     'Int32
                bw.Write(meta.LonCount)                     'Int32
                bw.Write(meta.CellSizeDeg)                  'Double
                bw.Write(CInt(flags))                       'Int32
                bw.Write(0)                                 'Reserved Int32

                'Payload
                If meta.HasHeight Then
                    progress?.Report(New ProgressInfo("Cache speicher: Höhenfeld...", 2))

                    For i As Integer = 0 To cache.HeightM.Length - 1
                        ct.ThrowIfCancellationRequested()
                        bw.Write(cache.HeightM(i))          'Single
                        processed += 1

                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache speichern: Höhenfeld... ({i + 1:N0}/{cache.HeightM.Length:N0})", pct))
                        End If
                    Next
                End If

                If meta.HasTid Then
                    progress?.Report(New ProgressInfo("Cache speichern: TID-Feld...", 75))

                    For i As Integer = 0 To cache.Tid.Length - 1
                        ct.ThrowIfCancellationRequested()
                        bw.Write(cache.Tid(i))              'Single
                        processed += 1

                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache speichern: TID-Feld... ({i + 1:N0}/{cache.Tid.Length:N0})", pct))
                        End If
                    Next
                End If

                If meta.HasLandMask Then
                    progress?.Report(New ProgressInfo("Cache speichern: LandMask-Feld...", 85))

                    For i As Integer = 0 To cache.LandMask.Length - 1
                        ct.ThrowIfCancellationRequested()
                        bw.Write(cache.LandMask(i))         'Byte
                        processed += 1

                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache speichern: LandMask-Feld... ({i + 1:N0}/{cache.LandMask.Length:N0})", pct))
                        End If
                    Next
                End If
            End Using
        End Using

        ct.ThrowIfCancellationRequested()

        If File.Exists(binPath) Then
            File.Replace(tmp, binPath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, binPath)
        End If

        progress?.Report(New ProgressInfo("Cache gespeichert.", 100))
    End Sub

    Public Shared Function ReadCache(binPath As String, Optional progress As IProgress(Of ProgressInfo) = Nothing, Optional ct As CancellationToken = Nothing) As _
                                    (latCount As Integer, lonCount As Integer, cellSizeDeg As Double, flags As CacheFlags, height As Single(), tid As Single(), landMask As Byte())

        If Not File.Exists(binPath) Then
            Throw New FileNotFoundException("Cache-Datei nicht gefunden.", binPath)
        End If

        progress?.Report(New ProgressInfo("Cache laden: Öffne Datei...", 0))
        ct.ThrowIfCancellationRequested()

        Using fs As New FileStream(binPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize:=1024 * 1024, useAsync:=False)
            Using br As New BinaryReader(fs, Encoding.UTF8, leaveOpen:=False)

                progress?.Report(New ProgressInfo("Cache laden: Header...", 1))
                ct.ThrowIfCancellationRequested()

                Dim magicBytes() As Byte = br.ReadBytes(4)
                Dim magicStr As String = Encoding.ASCII.GetString(magicBytes)
                If magicStr <> Magic Then
                    Throw New InvalidDataException($"Ungültiges Cache-Format (Magic='{magicStr}')")
                End If

                Dim version As Integer = br.ReadInt32()
                If version <> CurrentVersion Then
                    Throw New InvalidDataException($"Nicht unterstützte Cache-Version: {version}. Erwartet: {CurrentVersion}")
                End If

                Dim latCount As Integer = br.ReadInt32()
                Dim lonCount As Integer = br.ReadInt32()
                Dim cellSize As Double = br.ReadDouble()
                Dim flags As CacheFlags = CType(br.ReadInt32(), CacheFlags)
                br.ReadInt32() 'Reserved-Int

                If latCount <= 0 OrElse lonCount <= 0 Then
                    Throw New InvalidDataException("Ungültige Rasterdimensionen im Cache.")
                End If

                Dim n As Integer = latCount * lonCount

                Dim height As Single() = Nothing
                Dim tid As Single() = Nothing
                Dim landMask As Byte() = Nothing

                Dim totalValues As Integer = 0
                If flags.HasFlag(CacheFlags.HasHeight) Then totalValues += n
                If flags.HasFlag(CacheFlags.HasTid) Then totalValues += n
                If flags.HasFlag(CacheFlags.HasLandMask) Then totalValues += n

                Dim reportEvery As Integer = Math.Max(4096, totalValues \ 200)
                Dim processed As Integer = 0

                If flags.HasFlag(CacheFlags.HasHeight) Then
                    progress?.Report(New ProgressInfo("Cache laden: Höhenfeld...", 2))

                    height = New Single(n - 1) {}

                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()

                        height(i) = br.ReadSingle()

                        processed += 1
                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache laden: Höhenfeld... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                If flags.HasFlag(CacheFlags.HasTid) Then
                    progress?.Report(New ProgressInfo("Cache laden: TID-Feld...", 75))

                    tid = New Single(n - 1) {}

                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()

                        tid(i) = br.ReadSingle()
                        processed += 1

                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache laden: TID-Feld... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                If flags.HasFlag(CacheFlags.HasLandMask) Then
                    progress?.Report(New ProgressInfo("Cache laden: LandMask-Feld...", 85))

                    landMask = New Byte(n - 1) {}
                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()
                        landMask(i) = br.ReadByte()
                        processed += 1

                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"Cache laden: LandMask-Feld... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                progress?.Report(New ProgressInfo("Cache geladen.", 100))
                Return (latCount, lonCount, cellSize, flags, height, tid, landMask)
            End Using
        End Using
    End Function

End Class
