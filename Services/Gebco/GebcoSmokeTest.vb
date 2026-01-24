Imports System.IO
Imports System.Text

Public Module GebcoSmokeTest

    Public Function RunZipSmokeTest(zipPath As String,
                                    label As String,
                                    progress As IProgress(Of ProgressInfo),
                                    ct As Threading.CancellationToken) As String

        If String.IsNullOrWhiteSpace(zipPath) OrElse Not File.Exists(zipPath) Then
            Throw New FileNotFoundException($"{label}-ZIP nicht gefunden.", zipPath)
        End If

        progress?.Report(New ProgressInfo($"{label}: Scan ZIP nach .asc Tiles...", ProgressInfo.Indeterminate))
        ct.ThrowIfCancellationRequested()

        Dim tiles As List(Of GebcoTileInfo) = GebcoZipCatalog.ListAscTiles(zipPath)
        If tiles.Count = 0 Then
            Throw New InvalidDataException($"{label}: Keine .asc Tiles im ZIP gefunden.")
        End If

        Dim first As GebcoTileInfo = tiles(0)

        progress?.Report(New ProgressInfo($"{label}: Tiles={tiles.Count:N0}. Öffne erstes Tile...", 10))
        ct.ThrowIfCancellationRequested()

        Using s As Stream = ZipHelpers.OpenZipEntryStream(zipPath, first.EntryName)
            Using bs As New BufferedStream(s, 1024 * 1024)
                Using sr As New StreamReader(bs, Encoding.ASCII, detectEncodingFromByteOrderMarks:=False, bufferSize:=1024 * 1024, leaveOpen:=False)

                    progress?.Report(New ProgressInfo($"{label}: Lese Header...", 20))
                    ct.ThrowIfCancellationRequested()

                    Dim header As AsciiGridHeader
                    Dim firstLine As String = Nothing

                    header = EsriAsciiHeaderReader.ReadHeader(sr, firstLine)

                    progress?.Report(New ProgressInfo($"{label}: Header OK ({header.NRows}x{header.NCols}, cell={header.CellSize})", 35))
                    ct.ThrowIfCancellationRequested()

                    Dim tok As New AsciiIntTokenizer(sr, firstLine)

                    progress?.Report(New ProgressInfo($"{label}: Tokenizer - lese erste Werte...", 50))
                    ct.ThrowIfCancellationRequested()

                    Dim sb As New StringBuilder()
                    sb.AppendLine($"[{label}] ZIP: {zipPath}")
                    sb.AppendLine($"Tiles: {tiles.Count:N0}")
                    sb.AppendLine($"FirstTile: {first.EntryName}")
                    sb.AppendLine($"Bounds: N={first.North}, S={first.South}, W={first.West}, E={first.East}")
                    sb.AppendLine($"Header: nrows={header.NRows}, ncols={header.NCols}, cellsize={header.CellSize}")
                    sb.Append("FirstValues: ")

                    Dim v As Integer
                    For i As Integer = 1 To 50
                        ct.ThrowIfCancellationRequested()
                        If Not tok.TryReadInt(v) Then
                            Throw New EndOfStreamException($"{label}: Unerwartetes EOF beim Tokenisieren.")
                        End If
                        sb.Append(v)
                        If i < 50 Then sb.Append(", ")
                    Next

                    progress?.Report(New ProgressInfo($"{label}: Smoke-Test erfolgreich.", 100))
                    Return sb.ToString()
                End Using
            End Using
        End Using
    End Function

End Module
