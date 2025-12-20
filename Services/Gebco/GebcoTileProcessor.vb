
Imports System.CodeDom
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Windows.Media.Animation

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

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Tile-Processor für Height-Tiles (für Nearest-Resampling)
    Public Shared Sub ProcessHeightTileNearest(zipPath As String,
                                        tile As GebcoTileInfo,
                                        requests As GebcoNearestRequestBuilder.TileRowRequests,
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

                    Dim noData As Double? = header.NoDataValue
                    If Not noData.HasValue Then
                        'GEBCO hat es eigentlich immer. Wenn nicht: wir setzen "keins"
                        noData = Nothing
                    End If

                    Dim tok As New AsciiIntTokenizer(sr, firstLine)

                    Dim nRows As Integer = header.NRows
                    Dim nCols As Integer = header.NCols

                    'Row 0 = nördlichste Row im ASCII Grid
                    For row As Integer = 0 To nRows - 1

                        ct.ThrowIfCancellationRequested()

                        Dim want As List(Of GebcoNearestRequestBuilder.NearestRequest) = Nothing
                        requests.Rows.TryGetValue(row, want)

                        Dim wantPtr As Integer = 0
                        Dim wantCount As Integer = If(want Is Nothing, 0, want.Count)

                        For col As Integer = 0 To nCols - 1

                            Dim v As Integer
                            If Not tok.TryReadInt(v) Then
                                Throw New EndOfStreamException($"{progressPrefix}: EOF in {tile.EntryName} bei row={row}, col={col}")
                            End If

                            If wantCount > 0 AndAlso wantPtr < wantCount AndAlso col = want(wantPtr).CollInTile Then
                                Dim ti As Integer = want(wantPtr).TargetIndex

                                If noData.HasValue AndAlso v = CInt(noData.Value) Then
                                    heightOut(ti) = Single.NaN
                                Else
                                    heightOut(ti) = CSng(v)
                                End If

                                'Mehrere Requests können theoretisch dieselbe Spalte haben (sollte nicht passieren),
                                'wir gehen sicherheitshalber weiter
                                wantPtr += 1

                                While wantPtr < wantCount AndAlso want(wantPtr).CollInTile = col

                                    ti = want(wantPtr).TargetIndex
                                    If noData.HasValue AndAlso v = CInt(noData.Value) Then
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
                            progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Row {row:N0}/{nRows:N0}", pct))
                        End If

                    Next
                End Using
            End Using
        End Using
    End Sub

    ''' <summary>
    ''' Tile-Processor für Height-Tiles (für Bilinear-Resampling)
    ''' </summary>
    Public Shared Sub ProcessHeightTileBilinear(zipPath As String,
                                                tile As GebcoTileInfo,
                                                requests As GebcoBilinearRequestBuilder.TileRowRequest,
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
        ArgumentNullException.ThrowIfNull(requests)
        ArgumentNullException.ThrowIfNull(row0Buf)
        ArgumentNullException.ThrowIfNull(row1Buf)
        ArgumentNullException.ThrowIfNull(row0Set)
        ArgumentNullException.ThrowIfNull(row1Set)
        ArgumentNullException.ThrowIfNull(touched)

        If row0Buf.Length <> heightOut.Length OrElse row1Buf.Length <> heightOut.Length OrElse
           row0Set.Length <> heightOut.Length OrElse row1Set.Length <> heightOut.Length Then
            Throw New InvalidOperationException("Interner Fehler: Scratch-Arrays haben nicht die gleiche Länge wie heightOut")
        End If

        ct.ThrowIfCancellationRequested()

        Using s As Stream = ZipHelpers.OpenZipEntryStream(zipPath, tile.EntryName)
            Using bs As New BufferedStream(s, 1024 * 1024)
                Using sr As New StreamReader(bs, Encoding.ASCII, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024 * 1024, leaveOpen:=False)

                    Dim firstLine As String = Nothing
                    Dim header As AsciiGridHeader = EsriAsciiHeaderReader.ReadHeader(sr, firstLine)

                    Dim noData As Integer? = Nothing
                    If header.NoDataValue.HasValue Then noData = CInt(header.NoDataValue.Value)

                    Dim tok As New AsciiIntTokenizer(sr, firstLine)

                    Dim nRows As Integer = header.NRows
                    Dim nCols As Integer = header.NCols

                    'Row 0 = nördlichste Row im ASCII Grid
                    For row As Integer = 0 To nRows - 1

                        ct.ThrowIfCancellationRequested()

                        Dim want As List(Of GebcoBilinearRequestBuilder.BilinearRequest) = Nothing
                        requests.Rows.TryGetValue(row, want)

                        If want Is Nothing OrElse want.Count = 0 Then
                            'Row ist uninteressant -> trotzdem Tokens der Row komplett lesen!
                            'ABER: wir streamen ohnehin alle Spalten, daher kein Sonderfall nötig
                        End If

                        'Wenn wir Requests haben, bauen wir eine "NeedCol"-Liste, damit wir beim Col-Scan pointerbasiert bleiben.
                        Dim needs As List(Of ColNeed) = Nothing
                        Dim sample0 As Integer() = Nothing
                        Dim sample1 As Integer() = Nothing
                        Dim has0 As Byte() = Nothing
                        Dim has1 As Byte() = Nothing

                        Dim wantCount As Integer = If(want Is Nothing, 0, want.Count)

                        If wantCount > 0 Then

                            needs = New List(Of ColNeed)(wantCount * 2)
                            sample0 = New Integer(wantCount - 1) {}
                            sample1 = New Integer(wantCount - 1) {}
                            has0 = New Byte(wantCount - 1) {}
                            has1 = New Byte(wantCount - 1) {}

                            For i As Integer = 0 To wantCount - 1
                                Dim r As GebcoBilinearRequestBuilder.BilinearRequest = want(i)
                                needs.Add(New ColNeed(r.Col0, i, True))       'True => Col0
                                needs.Add(New ColNeed(r.Col1, i, False))      'False => Col1
                            Next

                            needs.Sort(Function(a, b) a.Col.CompareTo(b.Col))

                        End If

                        Dim needPtr As Integer = 0
                        Dim needCount As Integer = If(needs Is Nothing, 0, needs.Count)

                        For col As Integer = 0 To nCols - 1

                            Dim v As Integer
                            If Not tok.TryReadInt(v) Then Throw New EndOfStreamException($"{progressPrefix}: Unerwartetes Dateiende in {tile.EntryName} bei row={row}, col={col}.")

                            If needCount > 0 Then

                                'Alle NeedCol-Einträge für diese Spalte abarbeiten
                                While needPtr < needCount AndAlso needs(needPtr).Col = col

                                    Dim reqIndex As Integer = needs(needPtr).ReqIndex

                                    'NODATA -> ungültig
                                    If noData.HasValue AndAlso v = noData.Value Then
                                        'nicht setzen
                                    Else
                                        If needs(needPtr).IsCol0 Then
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
                        If wantCount > 0 Then

                            For i As Integer = 0 To wantCount - 1

                                Dim r As GebcoBilinearRequestBuilder.BilinearRequest = want(i)

                                Dim v0Valid As Boolean = (has0(i) <> 0)
                                Dim v1Valid As Boolean = (has1(i) <> 0)

                                'X-Interpolation (innerhalb der Row)
                                Dim rowLerp As Single = LerpRobust(
                                    If(v0Valid, CSng(sample0(i)), Single.NaN),
                                    If(v1Valid, CSng(sample1(i)), Single.NaN), r.Wx)

                                'Wenn beide ungültig -> kein Beitrag aus dieser Row
                                If Single.IsNaN(rowLerp) Then Continue For

                                Dim ti As Integer = r.TargetIndex

                                'Erstkontakt? -> merken, damit wir am Ende zurücksetzen können
                                'Wir fügen ti in touched ein, sobald wir irgendeinen Part setzen.
                                'Damit landet jeder ti zwar evtl. doppelt, aber wir vermeiden teure "Contains".
                                'Dopllete sind okay, Reset bleibt korrekt (setzt einfach zweimal)
                                If r.Part = GebcoBilinearRequestBuilder.RowPart.Row0 Then

                                    row0Buf(ti) = rowLerp
                                    If row0Set(ti) = 0 Then
                                        row0Set(ti) = 1
                                        touched.Add(ti)
                                    End If

                                Else    'Row1

                                    row1Buf(ti) = rowLerp
                                    If row1Set(ti) = 0 Then
                                        row1Set(ti) = 1
                                        touched.Add(ti)
                                    End If

                                End If

                                'Wenn wir beide Parts haben -> Y-Interpolation und final schreiben
                                If row0Set(ti) <> 0 AndAlso row1Set(ti) <> 0 Then

                                    Dim outVal As Single = LerpRobust(row0Buf(ti), row1Buf(ti), r.Wy)
                                    heightOut(ti) = outVal

                                    'Optional: direkt freigeben, damit ein späteres "zufälliges" Doppeltreffen nicht stört und um touched-Reset kleiner zu halten
                                    row0Set(ti) = 0
                                    row1Set(ti) = 0

                                End If

                            Next
                        End If

                        'Progress
                        If (row Mod ProgressThrottleRowInterval) = 0 Then
                            Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1)) * 100.0)
                            progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Row {row:N0}/{nRows:N0}", pct))
                        End If
                    Next

                End Using
            End Using
        End Using

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
                                     requests As GebcoNearestRequestBuilder.TileRowRequests,
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
                    Dim noDataBye As Byte? = Nothing
                    If header.NoDataValue.HasValue Then
                        Dim nd As Integer = CInt(header.NoDataValue.Value)

                        'nur übernehmen, wenn es wirklich in Byte-Range liegt
                        If nd >= Byte.MinValue AndAlso nd <= Byte.MaxValue Then
                            noDataBye = CByte(nd)
                        End If
                    End If

                    Dim tok As New AsciiIntTokenizer(sr, firstLine)

                    Dim nRows As Integer = header.NRows
                    Dim nCols As Integer = header.NCols

                    For row As Integer = 0 To nRows - 1

                        ct.ThrowIfCancellationRequested()

                        Dim want As List(Of GebcoNearestRequestBuilder.NearestRequest) = Nothing
                        requests.Rows.TryGetValue(row, want)

                        Dim wantPtr As Integer = 0
                        Dim wantCount As Integer = If(want Is Nothing, 0, want.Count)

                        For col As Integer = 0 To nCols - 1

                            Dim b As Byte
                            If Not tok.TryReadByte(b) Then
                                Throw New EndOfStreamException($"{progressPrefix}: EOF in {tile.EntryName} bei row={row}, col={col}")
                            End If

                            If wantCount > 0 AndAlso wantPtr < wantCount AndAlso col = want(wantPtr).CollInTile Then

                                'NODATA -> UnknownTid
                                Dim outVal As Byte = b
                                If noDataBye.HasValue AndAlso b = noDataBye.Value Then
                                    outVal = UnknownTid
                                End If

                                Dim ti As Integer = want(wantPtr).TargetIndex
                                tidOut(ti) = outVal

                                wantPtr += 1

                                While wantPtr < wantCount AndAlso want(wantPtr).CollInTile = col
                                    ti = want(wantPtr).TargetIndex
                                    tidOut(ti) = outVal

                                    wantPtr += 1
                                End While
                            End If
                        Next

                        'Progress-Throttling und reporten
                        If (row Mod ProgressThrottleRowInterval) = 0 Then
                            Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1.0)) * 100.0)
                            progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Row {row:N0}/{nRows:N0}", pct))

                        End If
                    Next

                    progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Done", 100))

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
