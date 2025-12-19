
Imports System.IO
Imports System.Text
Imports System.Threading

Public NotInheritable Class GebcoTileProcessor

    Private Sub New()

    End Sub


    Public Shared Sub ProcessHeightTile(zipPath As String,
                                        tile As GebcoTileInfo,
                                        requests As GebcoNearestRequestBuilder.TileRowRequests,
                                        heightOut As Single(),
                                        progress As IProgress(Of ProgressInfo),
                                        ct As CancellationToken,
                                        Optional progressPrefix As String = "HEIGHT")

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

                        'Progress-Throttling und Report (alle 256 Reihen Progress reporten)
                        If (row Mod 256) = 0 Then
                            Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1)) * 100.0)
                            progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Row {row:N0}/{nRows:N0}", pct))
                        End If

                    Next
                End Using
            End Using
        End Using
    End Sub

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

                        'Progress-Throttling und reporten (alle 256 Reihen reporten)
                        If (row Mod 256) = 0 Then
                            Dim pct As Integer = CInt((row / Math.Max(1.0, nRows - 1.0)) * 100.0)
                            progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(tile.EntryName)}{Environment.NewLine}{Environment.NewLine}Row {row:N0}/{nRows:N0}", pct))

                        End If
                    Next
                End Using
            End Using
        End Using
    End Sub
End Class
