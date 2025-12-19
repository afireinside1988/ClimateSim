Public NotInheritable Class GebcoNearestRequestBuilder

    Private Sub New()

    End Sub


    Public Structure NearestRequest
        Public TargetIndex As Integer
        Public CollInTile As Integer
    End Structure

    Public Class TileRowRequests
        'RowInTile -> Requests nach ColInTile sortieren
        Public ReadOnly Rows As New Dictionary(Of Integer, List(Of NearestRequest))()
    End Class

    'GEBCO 15arc
    Private Const SrcCellDeg As Double = 15.0 / 3600.0      '0.00416666666...
    Private Shared ReadOnly SrcLat0 As Double = -90.0 + SrcCellDeg / 2.0
    Private Shared ReadOnly SrcLon0 As Double = -180.0 + SrcCellDeg / 2.0

    Private Const SrcLatCount As Integer = 43200
    Private Const SrcLonCount As Integer = 86400

    ''' <summary>
    ''' Baut pro Tile eine Map: RowInTile -> List(Of (ColInTile, TargetIndex)) für Nearest-Resampling
    ''' Tile-Reihenfolge muss deterministisch sein (GebcoZipCatalog sortiert bereits)
    ''' </summary>
    Public Shared Function BuildRequests(tiles As List(Of GebcoTileInfo),
                                         targetCellDeg As Double,
                                         targetLatCount As Integer,
                                         targetLonCount As Integer) As List(Of TileRowRequests)

        If tiles Is Nothing OrElse tiles.Count = 0 Then Throw New ArgumentException("tiles fehlt.")
        Dim result As New List(Of TileRowRequests)
        For i As Integer = 0 To tiles.Count - 1
            result.Add(New TileRowRequests())
        Next

        For latIdx As Integer = 0 To targetLatCount - 1
            Dim latCenter As Double = (90.0 - targetCellDeg / 2.0) - latIdx * targetCellDeg

            For lonIdx As Integer = 0 To targetLonCount - 1
                Dim lonCenter As Double = (-180.0 + targetCellDeg / 2.0) + lonIdx * targetCellDeg

                'Nearest global source index (pixel-centre registered)
                Dim srcY As Integer = CInt(Math.Round((latCenter - SrcLat0) / SrcCellDeg))
                Dim srcX As Integer = CInt(Math.Round((lonCenter - SrcLon0) / SrcCellDeg))

                'Auf globales Grid clampen
                If srcY < 0 Then srcY = 0
                If srcY > SrcLatCount - 1 Then srcY = SrcLatCount - 1
                If srcX < 0 Then srcX = 0
                If srcX > SrcLonCount - 1 Then srcX = SrcLonCount - 1

                Dim tileIndex As Integer = FindTileIndex(tiles, latCenter, lonCenter)
                If tileIndex < 0 Then
                    'Sollte bei GEBCO-Abdeckung nicht passieren - aber robust bleiben
                    Throw New Exception($"No tile for latCenter={latCenter}, lonCenter={lonCenter}")
                    'Continue For
                End If

                Dim tile As GebcoTileInfo = tiles(tileIndex)

                'Tile row/col:
                'Row 0 in ASCII = nördlichste Row im Tile
                Dim srcYNorthMost As Integer = LatToSrcY(tile.North - SrcCellDeg / 2.0)
                Dim srcXWestMost As Integer = LonToSrcX(tile.West + SrcCellDeg / 2.0)

                Dim rowInTile As Integer = srcYNorthMost - srcY
                Dim colInTile As Integer = srcX - srcXWestMost

                'Auf Tile-Grenzen clampen um sicher zu bleiben
                Const TileSize As Integer = 21600
                If rowInTile < 0 Then rowInTile = 0
                If rowInTile > TileSize - 1 Then rowInTile = TileSize - 1
                If colInTile < 0 Then colInTile = 0
                If colInTile > TileSize - 1 Then colInTile = TileSize - 1

                'Tile-Größe sollte 21600x21600 sein, aber wir lassen es generisch (Clamp während der Laufzeit auf die header-Werte)
                Dim targetIndex As Integer = latIdx * targetLonCount + lonIdx

                Dim trr As TileRowRequests = result(tileIndex)
                Dim list As List(Of NearestRequest) = Nothing
                If Not trr.Rows.TryGetValue(rowInTile, list) Then
                    list = New List(Of NearestRequest)()
                    trr.Rows(rowInTile) = list
                End If

                list.Add(New NearestRequest With {.TargetIndex = targetIndex, .CollInTile = colInTile})
            Next
        Next

        'Jede Reihe nach Spalten sortieren, damit die CPU einen Pointer Scan nutzen kann
        For Each trr In result
            For Each kvp In trr.Rows
                kvp.Value.Sort(Function(a, b) a.CollInTile.CompareTo(b.CollInTile))
            Next
        Next

        Return result
    End Function

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

        'Wenn Ecke: bei Zellzentren sollte lon nicht exakt 180 sein, aber wir bleiben robust
        For i As Integer = 0 To tiles.Count - 1
            Dim t As GebcoTileInfo = tiles(i)

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
