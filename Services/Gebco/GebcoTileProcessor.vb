Imports System.Buffers
Imports System.IO
Imports System.Text
Imports System.Threading

Public NotInheritable Class GebcoTileProcessor

    Private Const ProgressThrottleRowInterval As Integer = 128 'Legt fest, nach wie vielen Rows jeweils der Progress reportet werden soll (kleiner=flüssiger)

    ''' <summary>
    ''' interne Hilfsstruktur für pointer-scan
    ''' </summary>
    Public Structure ColNeed
        Public ReadOnly Col As Integer
        Public ReadOnly ReqIndex As Integer
        Public ReadOnly IsCol0 As Boolean

        Public Sub New(col As Integer, reqIndex As Integer, isCol0 As Boolean)
            Me.Col = col
            Me.ReqIndex = reqIndex
            Me.IsCol0 = isCol0
        End Sub
    End Structure

    Private NotInheritable Class ColNeedComparer
        Implements IComparer(Of ColNeed)

        Public Shared ReadOnly Instance As New ColNeedComparer()

        Public Function Compare(x As ColNeed, y As ColNeed) As Integer Implements IComparer(Of ColNeed).Compare
            Return x.Col.CompareTo(y.Col)
        End Function
    End Class

    ''' <summary>
    ''' Tile-Processor für Height-Tiles (für Nearest-Resampling)
    Public Shared Sub ProcessHeightTileNearest(zipPath As String,
                                        tile As GebcoTileInfo,
                                        requests As TileRequests(Of NearestRequestPacked),
                                        heightOut As Single(),
                                        progress As IProgress(Of ProgressInfo),
                                        ct As CancellationToken,
                                        Optional progressPrefix As String = "HEIGHT(NEAREST)")

        ct.ThrowIfCancellationRequested()

        Using s As Stream = ZipHelpers.OpenZipEntryStream(zipPath, tile.EntryName)
            Using bs As New BufferedStream(s, 1024 * 1024)
                Using sr As New StreamReader(bs, Encoding.ASCII, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024 * 1024, leaveOpen:=False)

                    Dim firstLine As String = Nothing
                    Dim header As AsciiGridHeader = EsriAsciiHeaderReader.ReadHeader(sr, firstLine)

                    Dim noData As Integer? = Nothing
                    If header.NoDataValue.HasValue Then noData = CInt(header.NoDataValue.Value)

                    Using tok As New AsciiIntTokenizer(sr, firstLine)

                        Dim nRows As Integer = header.NRows
                        Dim nCols As Integer = header.NCols

                        Dim idx As TileRowIndex = requests.Index
                        Dim reqs As NearestRequestPacked() = requests.Requests

                        'Row 0 = nördlichste Row im ASCII Grid
                        For row As Integer = 0 To nRows - 1

                            ct.ThrowIfCancellationRequested()

                            Dim start As Integer = -1
                            Dim count As Integer = 0

                            If idx.RowStart IsNot Nothing AndAlso row < idx.RowStart.Length Then
                                start = idx.RowStart(row)
                                count = idx.RowCount(row)
                            End If

                            Dim wantPtr As Integer = 0

                            For col As Integer = 0 To nCols - 1

                                Dim v As Integer
                                If Not tok.TryReadInt(v) Then
                                    Throw New EndOfStreamException($"{progressPrefix}: EOF in {tile.EntryName} bei Zeile={row}, Spalte={col}")
                                End If

                                If count > 0 Then

                                    'Pointer-Scan innerhalb des RowSegments (Requests sind nach Col sortiert)
                                    While wantPtr < count AndAlso CInt(reqs(start + wantPtr).Col) = col

                                        Dim ti As Integer = reqs(start + wantPtr).TargetIndex
                                        If noData.HasValue AndAlso v = noData.Value Then
                                            heightOut(ti) = Single.NaN
                                        Else
                                            heightOut(ti) = CSng(v)
                                        End If

                                        wantPtr += 1
                                    End While

                                End If
                            Next

                            'Progress-Throttling und Report
                            If (row Mod ProgressThrottleRowInterval) = 0 Then
                                Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1)) * 100.0)
                                progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Zeilen verarbeitet: {row:N0}/{nRows:N0}", pct))
                            End If

                        Next
                    End Using
                End Using
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Tile-Processor für Height-Tiles (für Bilinear-Resampling)
    ''' </summary>
    Public Shared Sub ProcessHeightTileBilinear(zipPath As String,
                                                tile As GebcoTileInfo,
                                                requests As TileRequests(Of BilinearRequestPacked),
                                                heightOut As Single(),
                                                row0Buf As Single(),
                                                row1Buf As Single(),
                                                row0Set As Byte(),
                                                row1Set As Byte(),
                                                touched As List(Of Integer),
                                                progress As IProgress(Of ProgressInfo),
                                                ct As CancellationToken,
                                                Optional progressPrefix As String = "HEIGHT(BILINEAR)")

        ArgumentNullException.ThrowIfNull(heightOut)
        ArgumentNullException.ThrowIfNull(row0Buf)
        ArgumentNullException.ThrowIfNull(row1Buf)
        ArgumentNullException.ThrowIfNull(row0Set)
        ArgumentNullException.ThrowIfNull(row1Set)
        ArgumentNullException.ThrowIfNull(touched)


        Dim n As Integer = heightOut.Length
        If row0Buf.Length < n OrElse row1Buf.Length < n OrElse row0Set.Length < n OrElse row1Set.Length < n Then
            Throw New InvalidOperationException("Interner Fehler: Scratch-Arrays sind zu klein")
        End If

        ct.ThrowIfCancellationRequested()

        Dim idx As TileRowIndex = requests.Index
        Dim reqs As BilinearRequestPacked() = requests.Requests

        Dim poolNeed As ArrayPool(Of ColNeed) = ArrayPool(Of ColNeed).Shared
        Dim poolI As ArrayPool(Of Integer) = ArrayPool(Of Integer).Shared
        Dim poolB As ArrayPool(Of Byte) = ArrayPool(Of Byte).Shared

        Dim needsArr As ColNeed() = Nothing
        Dim needsCap As Integer = 0

        Dim sample0 As Integer() = Nothing
        Dim sample1 As Integer() = Nothing
        Dim has0 As Byte() = Nothing
        Dim has1 As Byte() = Nothing
        Dim sampleCap As Integer = 0

        Try
            Using s As Stream = ZipHelpers.OpenZipEntryStream(zipPath, tile.EntryName)
                Using bs As New BufferedStream(s, 1024 * 1024)
                    Using sr As New StreamReader(bs, Encoding.ASCII, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024 * 1024, leaveOpen:=False)

                        Dim firstLine As String = Nothing
                        Dim header As AsciiGridHeader = EsriAsciiHeaderReader.ReadHeader(sr, firstLine)

                        Dim noData As Integer? = Nothing
                        If header.NoDataValue.HasValue Then noData = CInt(header.NoDataValue.Value)

                        Using tok As New AsciiIntTokenizer(sr, firstLine)

                            Dim nRows As Integer = header.NRows
                            Dim nCols As Integer = header.NCols

                            'Row 0 = nördlichste Row im ASCII Grid
                            For row As Integer = 0 To nRows - 1

                                ct.ThrowIfCancellationRequested()

                                'Row-Segment holen
                                Dim start As Integer = -1
                                Dim count As Integer = 0
                                If idx.RowStart IsNot Nothing AndAlso row < idx.RowStart.Length Then
                                    start = idx.RowStart(row)
                                    count = idx.RowCount(row)
                                End If

                                Dim needPtr As Integer = 0
                                Dim needCount As Integer = 0

                                If count > 0 Then

                                    '--- Needs: 2 pro Request ---
                                    Dim needReq As Integer = count * 2
                                    If needsArr Is Nothing OrElse needsCap < needReq Then
                                        If needsArr IsNot Nothing Then poolNeed.Return(needsArr, clearArray:=False)
                                        needsArr = poolNeed.Rent(needReq)
                                        needsCap = needsArr.Length

                                    End If

                                    '--- Samples/Flags: 1 pro Request (index = i im RowSegment) ---
                                    If sample0 Is Nothing OrElse sampleCap < count Then
                                        If sample0 IsNot Nothing Then poolI.Return(sample0, clearArray:=False)
                                        If sample1 IsNot Nothing Then poolI.Return(sample1, clearArray:=False)
                                        If has0 IsNot Nothing Then poolB.Return(has0, clearArray:=True)
                                        If has1 IsNot Nothing Then poolB.Return(has1, clearArray:=True)

                                        sample0 = poolI.Rent(count)
                                        sample1 = poolI.Rent(count)
                                        has0 = poolB.Rent(count)
                                        has1 = poolB.Rent(count)
                                        sampleCap = count
                                    End If

                                    'Flags zurücksetzen (Samples brauchen wir nicht clearen)
                                    Array.Clear(has0, 0, count)
                                    Array.Clear(has1, 0, count)

                                    'Needs füllen aus reqs(start..start+count)
                                    For i As Integer = 0 To count - 1
                                        Dim r As BilinearRequestPacked = reqs(start + i)
                                        needsArr(needCount) = New ColNeed(r.Col0, i, True) : needCount += 1
                                        needsArr(needCount) = New ColNeed(r.Col1, i, False) : needCount += 1
                                    Next

                                    Array.Sort(needsArr, 0, needCount, ColNeedComparer.Instance)

                                End If

                                For col As Integer = 0 To nCols - 1

                                    Dim v As Integer
                                    If Not tok.TryReadInt(v) Then Throw New EndOfStreamException($"{progressPrefix}: Unerwartetes Dateiende in {tile.EntryName} bei Zeile={row}, Spalte={col}.")

                                    If needCount > 0 Then

                                        'Alle NeedCol-Einträge für diese Spalte abarbeiten
                                        While needPtr < needCount AndAlso needsArr(needPtr).Col = col

                                            Dim reqIndex As Integer = needsArr(needPtr).ReqIndex

                                            'NODATA -> ungültig
                                            If Not (noData.HasValue AndAlso v = noData.Value) Then
                                                If needsArr(needPtr).IsCol0 Then
                                                    sample0(reqIndex) = v
                                                    has0(reqIndex) = 1
                                                Else
                                                    sample1(reqIndex) = v
                                                    has1(reqIndex) = 1
                                                End If
                                            End If

                                            needPtr += 1
                                        End While
                                    End If
                                Next

                                'Nach Col-Scan: Requests finalisieren, bei denen wir mindestens 1 Sample haben
                                If count > 0 Then

                                    For i As Integer = 0 To count - 1

                                        Dim r As BilinearRequestPacked = reqs(start + i)

                                        Dim v0Valid As Boolean = (has0(i) <> 0)
                                        Dim v1Valid As Boolean = (has1(i) <> 0)

                                        'X-Interpolation (innerhalb der Row)
                                        Dim rowLerp As Single = LerpRobust(
                                        If(v0Valid, CSng(sample0(i)), Single.NaN),
                                        If(v1Valid, CSng(sample1(i)), Single.NaN),
                                        r.Wx)

                                        'Wenn beide ungültig -> kein Beitrag aus dieser Row
                                        If Single.IsNaN(rowLerp) Then Continue For

                                        Dim ti As Integer = r.TargetIndex

                                        'Erstkontakt? -> merken, damit wir am Ende zurücksetzen können
                                        'Wir fügen ti in touched ein, sobald wir irgendeinen Part setzen.
                                        'Damit landet jeder ti zwar evtl. doppelt, aber wir vermeiden teure "Contains".
                                        'Dopllete sind okay, Reset bleibt korrekt (setzt einfach zweimal)

                                        If r.Part = 0 Then              'Row0
                                            row0Buf(ti) = rowLerp
                                            If row0Set(ti) = 0 Then
                                                row0Set(ti) = 1
                                                touched.Add(ti)
                                            End If
                                        Else                            'Row1
                                            row1Buf(ti) = rowLerp
                                            If row1Set(ti) = 0 Then
                                                row1Set(ti) = 1
                                                touched.Add(ti)
                                            End If

                                        End If

                                        'Wenn wir beide Parts haben -> Y-Interpolation und final schreiben
                                        If row0Set(ti) <> 0 AndAlso row1Set(ti) <> 0 Then

                                            heightOut(ti) = LerpRobust(row0Buf(ti), row1Buf(ti), r.Wy)

                                            'Optional: direkt freigeben, damit ein späteres "zufälliges" Doppeltreffen nicht stört und um touched-Reset kleiner zu halten
                                            row0Set(ti) = 0
                                            row1Set(ti) = 0

                                        End If

                                    Next
                                End If

                                'Progress
                                If (row Mod ProgressThrottleRowInterval) = 0 Then
                                    Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1)) * 100.0)
                                    progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Zeilen verarbeitet: {row:N0}/{nRows:N0}", pct))
                                End If
                            Next

                        End Using
                    End Using
                End Using
            End Using

        Finally
            If needsArr IsNot Nothing Then poolNeed.Return(needsArr, clearArray:=False)
            If sample0 IsNot Nothing Then poolI.Return(sample0, clearArray:=False)
            If sample1 IsNot Nothing Then poolI.Return(sample1, clearArray:=False)
            If has0 IsNot Nothing Then poolB.Return(has0, clearArray:=True)
            If has1 IsNot Nothing Then poolB.Return(has1, clearArray:=True)
        End Try

        'Am Ende: alles aufräumen, was noch halb-fertig ist
        'Falls ein TargetIndex nur Row0 ODER nur Row1 bekommen hat, bleibt er sonst "gesetzt" und könnte im nächsten Tile fälschlich finalisieren
        If touched.Count > 0 Then
            For Each ti In touched
                row0Set(ti) = 0
                row1Set(ti) = 0
                row0Buf(ti) = 0.0F
                row1Buf(ti) = 0.0F
            Next
            touched.Clear()
        End If
    End Sub

    ''' <summary>
    ''' Tile-Processor für TID-Tiles (immer Nearest-Resampling)
    ''' </summary>
    Public Shared Sub ProcessTidTile(zipPath As String,
                                     tile As GebcoTileInfo,
                                     requests As TileRequests(Of NearestRequestPacked),
                                     tidOut As Byte(),
                                     progress As IProgress(Of ProgressInfo),
                                     ct As CancellationToken,
                                     Optional progressPrefix As String = "TID")

        ct.ThrowIfCancellationRequested()

        'Wir nutzen 255 als "Unknown" im Cache (kommt in GEBCO TID-Code nicht vor)
        Const UnknownTid As Byte = 255

        Using s As Stream = ZipHelpers.OpenZipEntryStream(zipPath, tile.EntryName)
            Using bs As New BufferedStream(s, 1024 * 1024)
                Using sr As New StreamReader(bs, Encoding.ASCII, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024 * 1024, leaveOpen:=False)

                    Dim firstLine As String = Nothing
                    Dim header As AsciiGridHeader = EsriAsciiHeaderReader.ReadHeader(sr, firstLine)

                    'NODATA aus Header lesen (bei GEBCO TID typischerweise 127)
                    Dim noDataByte As Byte? = Nothing
                    If header.NoDataValue.HasValue Then
                        Dim nd As Integer = CInt(header.NoDataValue.Value)

                        'nur übernehmen, wenn es wirklich in Byte-Range liegt
                        If nd >= Byte.MinValue AndAlso nd <= Byte.MaxValue Then
                            noDataByte = CByte(nd)
                        End If
                    End If

                    Using tok As New AsciiIntTokenizer(sr, firstLine)

                        Dim nRows As Integer = header.NRows
                        Dim nCols As Integer = header.NCols

                        Dim idx As TileRowIndex = requests.Index
                        Dim reqs As NearestRequestPacked() = requests.Requests

                        For row As Integer = 0 To nRows - 1

                            ct.ThrowIfCancellationRequested()

                            Dim start As Integer = -1
                            Dim count As Integer = 0

                            If idx.RowStart IsNot Nothing AndAlso row < idx.RowStart.Length Then
                                start = idx.RowStart(row)
                                count = idx.RowCount(row)
                            End If

                            Dim wantPtr As Integer = 0

                            For col As Integer = 0 To nCols - 1

                                Dim b As Byte
                                If Not tok.TryReadByte(b) Then
                                    Throw New EndOfStreamException($"{progressPrefix}: EOF in {tile.EntryName} bei Zeile={row}, Spalte={col}")
                                End If

                                If count > 0 Then
                                    While wantPtr < count AndAlso CInt(reqs(start + wantPtr).Col) = col

                                        Dim outVal As Byte = b
                                        If noDataByte.HasValue AndAlso b = noDataByte.Value Then outVal = UnknownTid

                                        Dim ti As Integer = reqs(start + wantPtr).TargetIndex
                                        tidOut(ti) = outVal

                                        wantPtr += 1
                                    End While
                                End If
                            Next

                            'Progress-Throttling und reporten
                            If (row Mod ProgressThrottleRowInterval) = 0 Then
                                Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1.0)) * 100.0)
                                progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Zeilen verarbeitet: {row:N0}/{nRows:N0}", pct))

                            End If
                        Next

                        progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Fertig.", 100))
                    End Using
                End Using
            End Using
        End Using
    End Sub

#Region "Helper"

    ''' <summary>
    ''' Robust Lerp:
    ''' - Wenn beide Werte gültig: normal Lerp
    ''' - Wenn nur einer gültig:   nimm den gültigen
    ''' - Wenn beide ungültig:     NaN
    ''' </summary>
    Private Shared Function LerpRobust(a As Single, b As Single, w As Single) As Single

        Dim aOk As Boolean = Not Single.IsNaN(a)
        Dim bOk As Boolean = Not Single.IsNaN(b)

        If aOk AndAlso bOk Then
            'normaler Lerp
            Return a + (b - a) * w
        End If

        If aOk Then Return a
        If bOk Then Return b

        Return Single.NaN

    End Function

#End Region
End Class
