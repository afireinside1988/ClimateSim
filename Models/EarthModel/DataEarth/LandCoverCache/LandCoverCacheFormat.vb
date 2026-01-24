Imports System.IO
Imports System.Reflection.PortableExecutable
Imports System.Text
Imports System.Threading
Imports System.Windows.Ink

''' <summary>
''' Binärformat für LandCover-Cache
''' 
''' Datei-Aufbau (v1, little-endian):
''' 
''' [HEADER] (32 Bytes)
'''     0..3    : Magic "LCCF" (Land Cover Cache File)
'''     4..7    : Int32 Version (=1)
'''     8..11   : Int32 LatCount
'''    12..15   : Int32 LonCount
'''    16..23   : Double CellSizeDeg
'''    24..27   : Int32 Flags (Bitfeld)
'''               Bit0 = HasLandCoverClass (immer gesetzt)
'''               Bit1 = HasConfidence
'''               Bit2 = HasLandIceThickness
'''    28..31   : Inr32 Reserved (0)
'''    
''' [PAYLOAD]
'''     Wenn HasLandCoverClass  :   LandCoverClass      byte[LatCount * LonCount]
'''     Wenn HasConfidence      :   Confidence          byte[LatCount * LonCount]
'''     Wenn HasLandIceThickness:   LandIceThicknessM   Single[LatCount * LonCount]
''' </summary>
Public NotInheritable Class LandCoverCacheFormat

    <Flags>
    Public Enum LandCoverCacheFlags As Integer
        None = 0
        HasLandCoverClass = 1
        HasConfidence = 2
        HasLandIceThickness = 4
    End Enum

    Public Const CurrentVersion As Integer = 1
    Public Const Magic As String = "LCCF"

    Public Shared Sub WriteCache(lccfPath As String, cache As LandCoverCache,
                                 Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                 Optional ct As CancellationToken = Nothing)

        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim meta As LandCoverCacheMeta = cache.Meta

        Dim n As Integer = meta.LatCount * meta.LonCount
        If n <= 0 Then Throw New InvalidOperationException("Ungültige Rasterdimension (n<=0).")

        'Pflicht: LandCoverClass
        If cache.LandCoverClass Is Nothing OrElse cache.LandCoverClass.Length <> n Then
            Throw New InvalidDataException("LandCoverClass fehlt oder hat eine falsche Länge.")
        End If

        'Optional: Confidence
        If meta.HasConfidence Then
            If cache.Confidence Is Nothing OrElse cache.Confidence.Length <> n Then
                Throw New InvalidDataException("Confidence ist laut Meta enthalten, Array fehlt oder hat falsche Länge.")
            End If
        Else
            'Wenn Meta sagt, dass kein Confidence enthalten ist, ignorieren wir ein eventuelles Array
        End If

        'Optional: LandIceThickness
        If meta.HasLandIceThickness Then
            If cache.LandIceThicknessM Is Nothing OrElse cache.LandIceThicknessM.Length <> n Then
                Throw New InvalidDataException("LandIceThickness ist laut Meta enthalten, Array fehlt oder hat falsche Länge.")
            End If
        Else
            'wenn Meta sagt, dass kein LandIceThickness enthalten ist, ignorieren wir ein eventuelles Array
        End If

        'Flags aus Meta ableiten
        Dim flags As LandCoverCacheFlags = LandCoverCacheFlags.HasLandCoverClass
        If meta.HasConfidence Then flags = flags Or LandCoverCacheFlags.HasConfidence
        If meta.HasLandIceThickness Then flags = flags Or LandCoverCacheFlags.HasLandIceThickness

        Dim dir As String = Path.GetDirectoryName(lccfPath)
        If Not String.IsNullOrWhiteSpace(dir) Then Directory.CreateDirectory(dir)

        progress?.Report(New ProgressInfo("LandCoverCache speichern: Vorbereitung...", 0))
        ct.ThrowIfCancellationRequested()

        Dim tmp As String = lccfPath & ".tmp"

        'Throttling: ca. 200 Updates max (mind. alle 4096)
        Dim totalValues As Integer = n  'LandCoverClass
        If meta.HasConfidence Then totalValues += n
        If meta.HasLandIceThickness Then totalValues += n

        Dim reportEvery As Integer = Math.Max(4096, totalValues \ 200)
        Dim processed As Integer = 0

        Using fs As New FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize:=1024 * 1024, useAsync:=False)
            Using bw As New BinaryWriter(fs, Encoding.UTF8, leaveOpen:=False)

                progress?.Report(New ProgressInfo("LandCoverCache speichern: Header...", 1))
                ct.ThrowIfCancellationRequested()

                bw.Write(Encoding.ASCII.GetBytes(Magic))    '4 bytes
                bw.Write(CurrentVersion)                    'Int32
                bw.Write(meta.LatCount)                     'Int32
                bw.Write(meta.LonCount)                     'Int32
                bw.Write(meta.CellSizeDeg)                  'Double
                bw.Write(CInt(flags))                       'Int32
                bw.Write(0)                                 'Reserved Int32

                'Payload: LandCoverClass
                progress?.Report(New ProgressInfo("LandCoverCache speichern: Klassenfeld...", 2))
                For i As Integer = 0 To n - 1
                    ct.ThrowIfCancellationRequested()
                    bw.Write(cache.LandCoverClass(i))           'byte
                    processed += 1
                    If (processed Mod reportEvery) = 0 Then
                        Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                        progress?.Report(New ProgressInfo($"LandCoverCache speichern: Klassenfeld... ({i + 1:N0}/{n:N0})", pct))
                    End If
                Next

                'Payload: Confidence (optional)
                If meta.HasConfidence Then
                    progress?.Report(New ProgressInfo("LandCoverCache speichern: Confidence...", 40))
                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()
                        bw.Write(cache.Confidence(i))           'Byte
                        processed += 1
                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"LandCoverCache speichern: Confidence... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                'Payload: LandIceThickness (optional)
                If meta.HasLandIceThickness Then
                    progress?.Report(New ProgressInfo("LandCoverCache speichern: LandIceThickness...", 70))
                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()
                        bw.Write(cache.LandIceThicknessM(i))    'Single
                        processed += 1
                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"LandCoverCache speichern: LandIceThickness... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

            End Using
        End Using

        ct.ThrowIfCancellationRequested()

        If File.Exists(lccfPath) Then
            File.Replace(tmp, lccfPath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, lccfPath)
        End If

        progress?.Report(New ProgressInfo("LandCoverCache gespeichert.", 100))

    End Sub

    Public Shared Function ReadCache(lccfPath As String,
                                     Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                     Optional ct As CancellationToken = Nothing) As _
                                     (latCount As Integer, lonCount As Integer, cellSizeDeg As Double, flags As LandCoverCacheFlags, classes As Byte(), confidence As Byte(), landIceThickness As Single())

        If Not File.Exists(lccfPath) Then
            Throw New FileNotFoundException("LandCoverCache-Datei nicht gefunden.", lccfPath)
        End If

        progress?.Report(New ProgressInfo("LandCoverCache laden: Öffne Datei...", 0))
        ct.ThrowIfCancellationRequested()

        Using fs As New FileStream(lccfPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize:=1024 * 1024, useAsync:=False)
            Using br As New BinaryReader(fs, Encoding.UTF8, leaveOpen:=False)

                progress?.Report(New ProgressInfo("LandCoverCache laden: Header...", 1))
                ct.ThrowIfCancellationRequested()

                Dim magicBytes() As Byte = br.ReadBytes(4)
                Dim magicStr As String = Encoding.ASCII.GetString(magicBytes)
                If magicStr <> Magic Then
                    Throw New InvalidDataException($"Ungültiges LandCoverCache-Format (Magic='{magicStr}')")
                End If

                Dim version As Integer = br.ReadInt32()
                If version <> CurrentVersion Then
                    Throw New InvalidDataException($"Nicht unterstützte LandCoverCache-Version: {version}. Erwartet: {CurrentVersion}")
                End If

                Dim latCount As Integer = br.ReadInt32()
                Dim lonCount As Integer = br.ReadInt32()
                Dim cellSize As Double = br.ReadDouble()
                Dim flags As LandCoverCacheFlags = CType(br.ReadInt32, LandCoverCacheFlags)
                br.ReadInt32()  'Reserved

                If latCount <= 0 OrElse lonCount <= 0 Then
                    Throw New InvalidDataException("Ungültige Rasterdimension im LandCoverCache.")
                End If

                Dim n As Integer = latCount * lonCount

                'LandCoverClass ist Pflicht
                If Not flags.HasFlag(LandCoverCacheFlags.HasLandCoverClass) Then
                    Throw New InvalidDataException("LandCoverCache enthält kein Klassenfeld (HasLandCoverClass fehlt).")
                End If

                Dim classes As Byte() = New Byte(n - 1) {}
                Dim confidence As Byte() = Nothing
                Dim landIceThickness As Single() = Nothing

                Dim totalValues As Integer = n
                If flags.HasFlag(LandCoverCacheFlags.HasConfidence) Then totalValues += n
                If flags.HasFlag(LandCoverCacheFlags.HasLandIceThickness) Then totalValues += n

                Dim reportEvery As Integer = Math.Max(4096, totalValues \ 200)
                Dim processed As Integer = 0

                'Klassen lesen
                progress?.Report(New ProgressInfo("LandCoverCache laden: Klassenfeld...", 2))
                For i As Integer = 0 To n - 1
                    ct.ThrowIfCancellationRequested()
                    classes(i) = br.ReadByte()
                    processed += 1
                    If (processed Mod reportEvery) = 0 Then
                        Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                        progress?.Report(New ProgressInfo($"LandCoverCache laden: Klassenfeld... ({i + 1:N0}/{n:N0})", pct))
                    End If
                Next

                'Confidence lesen (optional)
                If flags.HasFlag(LandCoverCacheFlags.HasConfidence) Then
                    confidence = New Byte(n - 1) {}
                    progress?.Report(New ProgressInfo("LandCoverCache laden: Confidence...", 40))
                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()
                        confidence(i) = br.ReadByte()
                        processed += 1
                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"LandCoverCache laden: Confidence... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                'LandIceThickness lesen (optional)
                If flags.HasFlag(LandCoverCacheFlags.HasLandIceThickness) Then
                    landIceThickness = New Single(n - 1) {}
                    progress?.Report(New ProgressInfo("LandCoverCache laden: LandIceThickness...", 70))
                    For i As Integer = 0 To n - 1
                        ct.ThrowIfCancellationRequested()
                        landIceThickness(i) = br.ReadSingle()
                        processed += 1
                        If (processed Mod reportEvery) = 0 Then
                            Dim pct As Integer = 2 + CInt((processed / Math.Max(1, totalValues)) * 96)
                            progress?.Report(New ProgressInfo($"LandCoverCache laden: LandIceThickness... ({i + 1:N0}/{n:N0})", pct))
                        End If
                    Next
                End If

                progress?.Report(New ProgressInfo("LandCoverCache geladen.", 100))
                Return (latCount, lonCount, cellSize, flags, classes, confidence, landIceThickness)
            End Using
        End Using

    End Function
End Class
