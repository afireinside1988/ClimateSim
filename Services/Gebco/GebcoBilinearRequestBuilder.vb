
''' <summary>
''' Erstellt pro GEBCO-Tile eine Request-Map für bilineares Resampling
''' 
''' Grundidee:
''' - Für jedes Target-Zellzentrum (latCenter, lonCenter)  berechnen wir die fractional Source-Koordinate (fy/fx)
''' - Daraus folgen:
'''     y0 = floor(fy), y1 = y0 + 1, wy = fy - y0
'''     x0 = floor(fx), x1 = x0 + 1, wx = fx - x0
''' - Für bilinear benötigen wir 4 Samples (y0/x0, y0/x1, y1/x0, y1/x1).
''' - Da wir ASCII row-by-row streamen, erzeugen wir pro Target-Zelle 2 Requests:
'''     * Request für Row y0 (Part=0)
'''     * Request für Row y1 (Part=1)
'''     
''' Hinweis zur Robustheit:
''' - Wir halten Bilinear strikt "tile-lokal": x1/y1 werden auf Tilegrenzen geclamped.
''' - Damit vermeiden wir Cross-Tile-Zugriffe (viel simpler und stabiler).
''' </summary>
Public NotInheritable Class GebcoBilinearRequestBuilder

    Private Sub New()

    End Sub

    ''' <summary>
    ''' Kennzeichnet, ob der Request zur "ersten" (y0) oder "zweiten" (y1) Source-Row gehört.
    ''' </summary>
    Public Enum RowPart As Byte
        Row0 = 0        'y0
        Row1 = 1        'y1
    End Enum

    ''' <summary>
    ''' Ein Request für eine bestimmte Row im Tile.
    ''' Er enthält die beiden benötigten Spalten (Col0/Col1) und die Bilinear-Gewichte.
    ''' </summary>
    Public Structure BilinearRequest
        Public TargetIndex As Integer
        Public Part As RowPart

        'Spalten innerhalb der akteullen Tile-Row, die gelesen werden müssen
        Public Col0 As Integer
        Public Col1 As Integer

        'Bilinear-Gewichte in X/Y (0..1)
        Public Wx As Single
        Public Wy As Single
    End Structure

    Public Class TileRowRequest
        'RowInTile -> Request (sortiert nach Col0, damit Streamen effizient ist
        Public ReadOnly Rows As New Dictionary(Of Integer, List(Of BilinearRequest))()
    End Class

    'GEBCO 15 arcsec (pixel-centered registered)
    Private Const SrcCellDeg As Double = 15.0 / 3600.0
    Private Shared ReadOnly SrcLat0 As Double = -90.0 + SrcCellDeg / 2.0
    Private Shared ReadOnly SrcLon0 As Double = -180.0 + SrcCellDeg / 2.0

    Private Const SrcLatCount As Integer = 43200
    Private Const SrcLonCount As Integer = 86400

    'GEBCO Tiles: 90x90 Grad bei 15" -> 21600 Zellen
    Private Const TileSize As Integer = 21600

    ''' <summary>
    ''' Baut pro Tile eine Map: RowInTile -> List(Of BilinearRequest).
    ''' Tile-Reihenfolge muss deterministisch sein (GebcoZipCatalog sortiert bereits).
    ''' </summary>
    Public Shared Function BuildRequests(tiles As List(Of GebcoTileInfo),
                                        targetCellDeg As Double,
                                        targetLatCount As Integer,
                                        targetLonCount As Integer) As List(Of TileRowRequest)

        If tiles Is Nothing OrElse tiles.Count = 0 Then Throw New ArgumentException("Es wurden keine GEBCO-Tiles übergeben (tiles ist leer).")
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetCellDeg)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetLatCount)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetLonCount)

        Dim result As New List(Of TileRowRequest)(tiles.Count)
        For i As Integer = 0 To tiles.Count - 1
            result.Add(New TileRowRequest())
        Next

        For latIdx As Integer = 0 To targetLatCount - 1

            'Zellzentrum: 90°..-90° (nördlich nach südlich)
            Dim latCenter As Double = (90.0 - targetCellDeg / 2.0) - latIdx * targetCellDeg

            For lonIdx As Integer = 0 To targetLonCount - 1

                'Zellzentrum: -180°...+180° (westlich nach östlich)
                Dim lonCenter As Double = (-180.0 + targetCellDeg / 2.0) + lonIdx * targetCellDeg

                '----------------------------------------
                '1) Fractional Source-Koordinate (global)
                '----------------------------------------

                Dim fy As Double = (latCenter - SrcLat0) / SrcCellDeg
                Dim fx As Double = (lonCenter - SrcLon0) / SrcCellDeg

                'floor statt CInt/Math.Round -> Bilinear braucht die "untere linke" Zelle
                Dim y0 As Integer = CInt(Math.Floor(fy))
                Dim x0 As Integer = CInt(Math.Floor(fx))

                Dim wy As Single = CSng(fy - y0)
                Dim wx As Single = CSng(fx - x0)

                Dim y1 As Integer = y0 + 1
                Dim x1 As Integer = x0 + 1

                '---------------------------------
                '2) Clamp auf globales Source-Grid
                '---------------------------------

                ClampIndexPair(y0, y1, SrcLatCount, wy)
                ClampIndexPair(x0, x1, SrcLonCount, wx)

                '----------------------------------------
                '3) Tile finden (nach Target-Zellzentrum)
                '----------------------------------------

                Dim tileindex As Integer = FindTileIndex(tiles, latCenter, lonCenter)
                If tileindex < 0 Then
                    Throw New InvalidOperationException($"Kein GEBCO-Tile für lat={latCenter:0.####}°, lon={lonCenter:0.####}° gefunden. (Bilinear-RequestBuilder)")
                End If

                Dim tile As GebcoTileInfo = tiles(tileindex)

                '-------------------------------------------------------------------------------------
                '4) Map global src (y/x) -> row/col im Tile (Row 0 im ASCII = nördlichste Row im Tile)
                '-------------------------------------------------------------------------------------

                Dim srcYNorthMost As Integer = LatToSrcY(tile.North - SrcCellDeg / 2.0)
                Dim srcXWestMost As Integer = LonToSrcX(tile.West + SrcCellDeg / 2.0)

                Dim row0InTile As Integer = srcYNorthMost - y0
                Dim row1InTile As Integer = srcYNorthMost - y1

                Dim col0InTile As Integer = x0 - srcXWestMost
                Dim col1InTile As Integer = x1 - srcXWestMost

                'Tile-lokales Clamp (verhindert Cross-Tile-Bilinear)
                Clamp0ToTile(row0InTile)
                Clamp0ToTile(row1InTile)
                Clamp0ToTile(col0InTile)
                Clamp0ToTile(col1InTile)

                Dim targetIndex As Integer = latIdx * targetLonCount + lonIdx

                '-------------------------------------
                '5) Zwei Requests erzeugen (Row0/Row1)
                '-------------------------------------

                AddRequest(result(tileindex), row0InTile,
                            New BilinearRequest With {
                                .TargetIndex = targetIndex,
                                .Part = RowPart.Row0,
                                .Col0 = col0InTile,
                                .Col1 = col1InTile,
                                .Wx = wx,
                                .Wy = wy
                            })

                AddRequest(result(tileindex), row1InTile,
                            New BilinearRequest With {
                                .TargetIndex = targetIndex,
                                .Part = RowPart.Row1,
                                .Col0 = col0InTile,
                                .Col1 = col1InTile,
                                .Wx = wx,
                                .Wy = wy
                            })
            Next
        Next

        '---------------------
        'Sortierung und Guards
        '---------------------

        Dim totalReq As Long = 0
        Dim tilesWithReq As Integer = 0

        For Each trr In result

            Dim tileHasAny As Boolean = False

            For Each kvp In trr.Rows

                Dim list As List(Of BilinearRequest) = kvp.Value
                If list IsNot Nothing AndAlso list.Count > 0 Then

                    tileHasAny = True
                    totalReq += list.Count

                    list.Sort(
                        Function(a, b)

                            Dim c As Integer = a.Col0.CompareTo(b.Col0)

                            If c <> 0 Then Return c
                            c = a.Col1.CompareTo(b.Col1)

                            If c <> 0 Then Return c
                            'stabiler Tiebreak: TargetIndex
                            Return a.TargetIndex.CompareTo(b.TargetIndex)

                        End Function)
                End If

            Next

            If tileHasAny Then tilesWithReq += 1

        Next

        If totalReq = 0 OrElse tilesWithReq = 0 Then
            Throw New InvalidOperationException($"Bilinear-RequestBuilder: Es wurden keine Requests erzeugt. tiles={tiles.Count}, target={targetLatCount}x{targetLonCount}, cell={targetCellDeg:0.####}°")
        End If

        Dim expectedMin As Long = CLng(targetLatCount) * CLng(targetLonCount) * 2L
        If totalReq < expectedMin Then
            Throw New InvalidOperationException($"Bilinear-RequestBuilder: Unplausible Request-Anzahl ({totalReq:N0}). Erwartet mindestens {expectedMin:N0} (=2 pro Target-Zelle). tiles={tiles.Count}, target={targetLatCount}x{targetLonCount}, cell={targetCellDeg:0.####}°")
        End If


        Return result
    End Function

#Region "Helpers"

    Private Shared Sub AddRequest(trr As TileRowRequest, rowInTile As Integer, req As BilinearRequest)

        Dim list As List(Of BilinearRequest) = Nothing

        If Not trr.Rows.TryGetValue(rowInTile, list) Then
            list = New List(Of BilinearRequest)()
            trr.Rows(rowInTile) = list
        End If

        list.Add(req)
    End Sub

    ''' <summary>
    ''' Clamp von (i0, i1) auf [0...count-1].
    ''' Wenn i0/i1 kollabieren (am Rand), setzen wir w=0 (damit es sauber degeneriert)
    ''' </summary>
    Private Shared Sub ClampIndexPair(ByRef i0 As Integer, ByRef i1 As Integer, ByRef count As Integer, ByRef w As Single)

        If count <= 1 Then
            i0 = 0 : i1 = 0 : w = 0
            Return
        End If

        If i0 < 0 Then
            i0 = 0 : i1 = 0 : w = 0
            Return
        End If

        If i0 >= count - 1 Then
            i0 = count - 1
            i1 = count - 1
            w = 0
            Return
        End If

        'Normalfall: i0 ist im gültigen Bereich [0...count-2], i1=i0+1 passt
        If i1 >= count Then
            i1 = count - 1
            w = 0
        End If
    End Sub

    ''' <summary>
    ''' Clampt auf die Tile-Grenzen
    ''' </summary>
    Private Shared Sub Clamp0ToTile(ByRef idx As Integer)
        If idx < 0 Then idx = 0
        If idx > TileSize - 1 Then idx = TileSize - 1
    End Sub

    Private Shared Function FindTileIndex(tiles As List(Of GebcoTileInfo), latCenter As Double, lonCenter As Double) As Integer

        Const eps As Double = 0.000000001

        For i As Integer = 0 To tiles.Count - 1
            Dim t As GebcoTileInfo = tiles(i)

            'Grenzen als Ecken interpretieren: South <= lat < North, West <= lon < East
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

#End Region

End Class
