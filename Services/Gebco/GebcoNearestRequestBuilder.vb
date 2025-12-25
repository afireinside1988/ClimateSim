Public NotInheritable Class GebcoNearestRequestBuilder

    Private Sub New()

    End Sub

    'GEBCO 15arc
    Private Const SrcCellDeg As Double = 15.0 / 3600.0      '0.00416666666...
    Private Shared ReadOnly SrcLat0 As Double = -90.0 + SrcCellDeg / 2.0
    Private Shared ReadOnly SrcLon0 As Double = -180.0 + SrcCellDeg / 2.0

    Private Const SrcLatCount As Integer = 43200
    Private Const SrcLonCount As Integer = 86400

    Private Const TileSize As Integer = 21600

    Public Shared Function BuildRequests(tiles As List(Of GebcoTileInfo),
                                         targetCellDeg As Double,
                                         targetLatCount As Integer,
                                         targetLonCount As Integer) As List(Of TileRequests(Of NearestRequestPacked))

        If tiles Is Nothing OrElse tiles.Count = 0 Then Throw New ArgumentException("tiles fehlt/leer.")
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCellDeg)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetLatCount)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetLonCount)

        Dim tileCount As Integer = tiles.Count
        Dim result As New List(Of TileRequests(Of NearestRequestPacked))(tileCount)

        'RowCounts pro Tile (TileSize * tilesCount, aber als viele kleine Arrays)
        Dim rowCountsPerTile As Integer()() = New Integer(tileCount - 1)() {}
        For t As Integer = 0 To tileCount - 1
            rowCountsPerTile(t) = New Integer(TileSize - 1) {}
        Next

        '--------------------------
        'Pass A: nur zählen
        '--------------------------
        Dim totalReq As Long = 0

        For latIdx As Integer = 0 To targetLatCount - 1
            Dim latCenter As Double = (90.0 - targetCellDeg / 2.0) - latIdx * targetCellDeg

            For lonIdx As Integer = 0 To targetLonCount - 1
                Dim lonCenter As Double = (-180.0 + targetCellDeg / 2.0) + lonIdx * targetCellDeg

                'Nearest global source index
                Dim srcY As Integer = CInt(Math.Round((latCenter - SrcLat0) / SrcCellDeg))
                Dim srcX As Integer = CInt(Math.Round((lonCenter - SrcLon0) / SrcCellDeg))

                srcY = Clamp(srcY, 0, SrcLatCount - 1)
                srcX = Clamp(srcX, 0, SrcLonCount - 1)

                Dim tileIndex As Integer = FindTileIndex(tiles, latCenter, lonCenter)
                If tileIndex < 0 Then
                    Throw New InvalidOperationException($"No tile for latCenter={latCenter}, lonCenter={lonCenter}")
                End If

                Dim tile As GebcoTileInfo = tiles(tileIndex)

                Dim srcYNorthMost As Integer = LatToSrcY(tile.North - SrcCellDeg / 2.0)
                Dim srcXWestMost As Integer = LonToSrcX(tile.West + SrcCellDeg / 2.0)

                Dim rowInTile As Integer = srcYNorthMost - srcY
                Dim colInTile As Integer = srcX - srcXWestMost

                rowInTile = Clamp(rowInTile, 0, TileSize - 1)
                colInTile = Clamp(colInTile, 0, TileSize - 1)

                rowCountsPerTile(tileIndex)(rowInTile) += 1
                totalReq += 1
            Next
        Next

        If totalReq = 0 Then Throw New InvalidOperationException("Nearest-RequestBuilder: 0 Requests erzeugt (unerwartet).")

        '--------------------------
        'PrefixSum: RowStart + Total pro Tile
        '--------------------------
        Dim rowStartPerTile As Integer()() = New Integer(tileCount - 1)() {}
        Dim writePtrPerTile As Integer()() = New Integer(tileCount - 1)() {}
        Dim totalPerTile As Integer() = New Integer(tileCount - 1) {}

        For t As Integer = 0 To tileCount - 1
            Dim counts As Integer() = rowCountsPerTile(t)

            Dim starts As Integer() = New Integer(TileSize - 1) {}
            Dim wptr As Integer() = New Integer(TileSize - 1) {}

            For r As Integer = 0 To TileSize - 1
                starts(r) = -1
            Next

            Dim acc As Integer = 0
            For r As Integer = 0 To TileSize - 1
                Dim c As Integer = counts(r)
                If c > 0 Then
                    starts(r) = acc
                    wptr(r) = acc
                    acc += c
                End If
            Next

            totalPerTile(t) = acc
            rowStartPerTile(t) = starts
            writePtrPerTile(t) = wptr
        Next

        '--------------------------
        'Requests-Arrays allokieren + result befüllen
        '--------------------------
        Dim reqPerTile As NearestRequestPacked()() = New NearestRequestPacked(tileCount - 1)() {}

        For t As Integer = 0 To tileCount - 1
            Dim n As Integer = totalPerTile(t)
            reqPerTile(t) = If(n > 0, New NearestRequestPacked(n - 1) {}, Array.Empty(Of NearestRequestPacked)())

            Dim idx As New TileRowIndex With {
                .RowStart = rowStartPerTile(t),
                .RowCount = rowCountsPerTile(t)
            }

            result.Add(New TileRequests(Of NearestRequestPacked) With {
                .Index = idx,
                .Requests = reqPerTile(t)
            })
        Next

        '--------------------------
        'Pass B: Requests schreiben
        '--------------------------
        For latIdx As Integer = 0 To targetLatCount - 1
            Dim latCenter As Double = (90.0 - targetCellDeg / 2.0) - latIdx * targetCellDeg

            For lonIdx As Integer = 0 To targetLonCount - 1
                Dim lonCenter As Double = (-180.0 + targetCellDeg / 2.0) + lonIdx * targetCellDeg

                Dim srcY As Integer = CInt(Math.Round((latCenter - SrcLat0) / SrcCellDeg))
                Dim srcX As Integer = CInt(Math.Round((lonCenter - SrcLon0) / SrcCellDeg))

                srcY = Clamp(srcY, 0, SrcLatCount - 1)
                srcX = Clamp(srcX, 0, SrcLonCount - 1)

                Dim tileIndex As Integer = FindTileIndex(tiles, latCenter, lonCenter)
                If tileIndex < 0 Then Throw New InvalidOperationException($"No tile for latCenter={latCenter}, lonCenter={lonCenter}")

                Dim tile As GebcoTileInfo = tiles(tileIndex)

                Dim srcYNorthMost As Integer = LatToSrcY(tile.North - SrcCellDeg / 2.0)
                Dim srcXWestMost As Integer = LonToSrcX(tile.West + SrcCellDeg / 2.0)

                Dim rowInTile As Integer = srcYNorthMost - srcY
                Dim colInTile As Integer = srcX - srcXWestMost

                rowInTile = Clamp(rowInTile, 0, TileSize - 1)
                colInTile = Clamp(colInTile, 0, TileSize - 1)

                Dim ti As Integer = latIdx * targetLonCount + lonIdx

                Dim wp As Integer = writePtrPerTile(tileIndex)(rowInTile)
                reqPerTile(tileIndex)(wp) = New NearestRequestPacked With {
                    .TargetIndex = ti,
                    .Col = CUShort(colInTile)
                }
                writePtrPerTile(tileIndex)(rowInTile) = wp + 1
            Next
        Next

        '--------------------------
        'Sortierung je Row-Segment nach Col
        '--------------------------
        Dim cmp As New NearestPackedComparer()

        For t As Integer = 0 To tileCount - 1
            Dim idx = result(t).Index
            Dim reqs = result(t).Requests
            If reqs Is Nothing OrElse reqs.Length = 0 Then Continue For

            For r As Integer = 0 To TileSize - 1
                Dim c As Integer = idx.RowCount(r)
                If c > 1 Then
                    Dim s As Integer = idx.RowStart(r)
                    Array.Sort(reqs, s, c, cmp)
                End If
            Next
        Next

        Return result
    End Function

    Private NotInheritable Class NearestPackedComparer
        Implements IComparer(Of NearestRequestPacked)

        Public Function Compare(a As NearestRequestPacked, b As NearestRequestPacked) As Integer Implements IComparer(Of NearestRequestPacked).Compare
            Dim c As Integer = a.Col.CompareTo(b.Col)
            If c <> 0 Then Return c
            Return a.TargetIndex.CompareTo(b.TargetIndex)
        End Function
    End Class


    Private Shared Function FindTileIndex(tiles As List(Of GebcoTileInfo), latCenter As Double, lonCenter As Double) As Integer
        Const eps As Double = 0.000000001

        For i As Integer = 0 To tiles.Count - 1
            Dim t As GebcoTileInfo = tiles(i)

            'Wir interpretieren die Grenzen als Ecken: Süden <= lat < Norden, West <= lon < Osten
            If latCenter >= t.South - eps AndAlso latCenter < t.North + eps AndAlso
               lonCenter >= t.West - eps AndAlso lonCenter < t.East + eps Then

                Return i
            End If
        Next

        Return -1
    End Function

    Private Shared Function LatToSrcY(latCenter As Double) As Integer

        Dim y As Integer = CInt(Math.Round((latCenter - SrcLat0) / SrcCellDeg))
        If y < 0 Then y = 0
        If y > SrcLatCount - 1 Then y = SrcLatCount - 1

        Return y

    End Function

    Private Shared Function LonToSrcX(lonCenter As Double) As Integer

        Dim x As Integer = CInt(Math.Round((lonCenter - SrcLon0) / SrcCellDeg))
        If x < 0 Then x = 0
        If x > SrcLonCount - 1 Then x = SrcLonCount - 1

        Return x

    End Function
End Class
