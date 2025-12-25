Imports System.IO
Imports System.Text
Imports System.Threading

Public NotInheritable Class EarthSurfaceCacheBuilder

    Private Sub New()

    End Sub

    Public Class BuildOptions
        Public Property SourceName As String
        Public Property HeightZipPath As String
        Public Property TidZipPath As String
        Public Property CellSizeDeg As Double
        Public Property Resampling As String = "nearest"        ' "nearest" oder "bilinear"
        Public Property LandMaskMode As LandMaskMode

        Public Property UseHysteresis As Boolean
        Public Property HysteresisIterations As Integer = 1

        'Hysterese-Defaults
        Public Property LandThreshold As Integer = 6
        Public Property OceanThreshold As Integer = 3

        'LandMask Variant Tag für Dateinamen (z.B. "lm_fromHeight_hyst1_it01" oder "lm_fromTid0")
        Public Property LandMaskVariant As String

    End Class

    ''' <summary>
    ''' Baut und speichert den EarthSurface-Cache.
    ''' Height-Tiles: resamplet nearest oder bilinear (je nach BuildOption)
    ''' TID:          wird immer nearest resampled (falls TID vorhanden ist)
    ''' </summary>
    Public Shared Function BuildAndSave(opts As BuildOptions, progress As IProgress(Of ProgressInfo), ct As CancellationToken) As String

        '-------------
        '0) Validation
        '-------------

        ArgumentNullException.ThrowIfNull(opts)

        If String.IsNullOrWhiteSpace(opts.SourceName) Then Throw New ArgumentException("SourceName fehlt.")
        If String.IsNullOrWhiteSpace(opts.HeightZipPath) OrElse Not File.Exists(opts.HeightZipPath) Then
            Throw New FileNotFoundException("Height-ZIP nicht gefunden.", opts.HeightZipPath)
        End If
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.CellSizeDeg)

        Dim resampling As String = If(opts.Resampling, "nearest").Trim().ToLowerInvariant()
        If resampling <> "nearest" AndAlso resampling <> "bilinear" Then
            Throw New ArgumentException($"Unbekanntes Resampling '{opts.Resampling}'. Erlaubt sind 'nearest' und 'bilinear'.")
        End If

        ct.ThrowIfCancellationRequested()

        Dim sb As New StringBuilder()

        '---------------------------------------------
        'A) Tiles listen und deterministisch sortieren
        '---------------------------------------------

        progress?.Report(New ProgressInfo("Scanne Height-ZIP nach Tiles...", ProgressInfo.Indeterminate))
        Dim heightTiles As List(Of GebcoTileInfo) = GebcoZipCatalog.ListAscTiles(opts.HeightZipPath)
        If heightTiles Is Nothing OrElse heightTiles.Count = 0 Then Throw New InvalidDataException("Keine .asc Tiles im Height-ZIP gefunden.")

        Dim hasTid As Boolean = (Not String.IsNullOrWhiteSpace(opts.TidZipPath) AndAlso File.Exists(opts.TidZipPath))
        Dim tidTiles As List(Of GebcoTileInfo) = Nothing

        If hasTid Then
            progress?.Report(New ProgressInfo("Scanne TID-ZIP nach Tiles...", ProgressInfo.Indeterminate))
            tidTiles = GebcoZipCatalog.ListAscTiles(opts.TidZipPath)
            If tidTiles Is Nothing OrElse tidTiles.Count = 0 Then hasTid = False
        End If

        'Sanity: TID-Files müssen "gleich geschnitten" und gleich sortiert sein
        If hasTid Then
            If tidTiles.Count <> heightTiles.Count Then
                Throw New InvalidDataException("TID-Tileanzahl passt nicht zu Height-Tiles.")
            End If

            For i As Integer = 0 To heightTiles.Count - 1
                Dim a As GebcoTileInfo = heightTiles(i)
                Dim b As GebcoTileInfo = tidTiles(i)

                If a.North <> b.North OrElse a.South <> b.South OrElse a.West <> b.West OrElse a.East <> b.East Then
                    Throw New InvalidDataException("TID-Tiles sind anders sortiert/geschnitten als Height-Tiles.")
                End If
            Next
        End If

        '----------------------
        'B) Zielraster ableiten
        '----------------------

        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.CellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.CellSizeDeg))
        Dim n As Integer = latCount * lonCount

        '------------------------
        'C) Output-Arrays anlegen
        '------------------------

        Dim heightOut As Single() = New Single(n - 1) {}
        Array.Fill(heightOut, Single.NaN)

        Dim tidOut As Byte() = If(hasTid, New Byte(n - 1) {}, Nothing)
        If hasTid Then Array.Fill(tidOut, CByte(255))
        Dim landMaskOut As Byte() = New Byte(n - 1) {}

        '------------------------
        'D) Progress-Meilensteine
        '------------------------

        Dim mapStart As Integer = 0
        Dim mapEnd As Integer = 2

        Dim heightStart As Integer = mapEnd
        Dim heightEnd As Integer = If(hasTid, 48, 94)

        Dim tidStart As Integer = heightEnd
        Dim tidEnd As Integer = 94

        Dim lmStart As Integer = If(hasTid, tidEnd, heightEnd)
        Dim lmEnd As Integer = 96

        Dim metaStart As Integer = lmEnd
        Dim metaEnd As Integer = 98

        Dim saveStart As Integer = metaEnd
        Dim saveEnd As Integer = 100

        '---------------------
        'E) Requests erstellen
        '---------------------

        Dim pReq As New ProgressSlice(progress, mapStart, mapEnd, "GEBCO: ")

        'TID bleibt immer Nearest -> diese Request brauchen wir ggf. später
        Dim reqNearestByTile As List(Of GebcoNearestRequestBuilder.TileRowRequests) = Nothing

        If hasTid OrElse resampling = "nearest" Then
            pReq.Report(New ProgressInfo("Baue Nearest-Mapping...", 0))
            reqNearestByTile = GebcoNearestRequestBuilder.BuildRequests(heightTiles, opts.CellSizeDeg, latCount, lonCount)
        Else
            pReq.Report(New ProgressInfo("Baue Bilinear-Mapping...", 0))
        End If

        'Height-Requests je nach Resampling
        Dim reqBilinearByTile As List(Of GebcoBilinearRequestBuilder.TileRowRequest) = Nothing

        If resampling = "bilinear" Then
            reqBilinearByTile = GebcoBilinearRequestBuilder.BuildRequests(heightTiles, opts.CellSizeDeg, latCount, lonCount)
        End If

        ct.ThrowIfCancellationRequested()

        '------------------------
        'F) Height Tiles streamen
        '------------------------

        Dim tileCount As Integer = heightTiles.Count

        If resampling = "nearest" Then

            'NEAREST-Resampling

            If reqNearestByTile Is Nothing OrElse reqNearestByTile.Count <> tileCount Then Throw New InvalidOperationException("Interner Fehler: Nearest-Requests fehlen oder passen nicht zur Tile-Anzahl.")

            For t As Integer = 0 To tileCount - 1

                ct.ThrowIfCancellationRequested()

                Dim tile As GebcoTileInfo = heightTiles(t)

                Dim ts, te As Integer
                GetTileSlice(t, tileCount, heightStart, heightEnd, ts, te)

                Dim pTile As New ProgressSlice(progress, ts, te, "GEBCO: ")
                pTile.Report(New ProgressInfo($"HEIGHT: Tile {t + 1}/{tileCount}: {Path.GetFileName(tile.EntryName)}", 0))

                GebcoTileProcessor.ProcessHeightTileNearest(
                    opts.HeightZipPath, tile, reqNearestByTile(t), heightOut,
                    pTile, ct, progressPrefix:=$"HEIGHT {t + 1}/{tileCount}")

            Next

        Else
            'BILINEAR-Resampling

            If reqBilinearByTile Is Nothing OrElse reqBilinearByTile.Count <> tileCount Then Throw New InvalidOperationException("Interner Fehler: Bilinear-Requests fehlen oder passen nicht zur Tile-Anzahl.")

            'Scratch einmalig allokieren (wird pro Tile wiederverwendet)
            Dim row0Buf As Single() = New Single(n - 1) {}
            Dim row1Buf As Single() = New Single(n - 1) {}

            Dim row0Set As Byte() = New Byte(n - 1) {}
            Dim row1Set As Byte() = New Byte(n - 1) {}

            Dim touched As New List(Of Integer)(capacity:=Math.Min(n, 200000))
            For t As Integer = 0 To tileCount - 1

                ct.ThrowIfCancellationRequested()

                Dim tile As GebcoTileInfo = heightTiles(t)

                Dim ts, te As Integer
                GetTileSlice(t, tileCount, heightStart, heightEnd, ts, te)

                Dim pTile As New ProgressSlice(progress, ts, te, "GEBCO: ")
                pTile.Report(New ProgressInfo($"HEIGHT: Tile {t + 1}/{tileCount}: {Path.GetFileName(tile.EntryName)}", 0))

                GebcoTileProcessor.ProcessHeightTileBilinear(
                    opts.HeightZipPath, tile, reqBilinearByTile(t), heightOut,
                    row0Buf, row1Buf,
                    row0Set, row1Set,
                    touched,
                    pTile, ct, progressPrefix:=$"HEIGHT {t + 1}/{tileCount}")

            Next

        End If

        '-------------------------------------
        'G) TID Tiles streamen (immer Nearest)
        '-------------------------------------

        If hasTid Then

            If tidTiles Is Nothing OrElse tidTiles.Count = 0 Then Throw New InvalidDataException("TID-ZIP ist vorhanden, aber es wurden keine TID-Tiles gefunden.")
            If tidTiles.Count <> tileCount Then Throw New InvalidDataException($"Inkonsistente Tile-Anzahl: Height-Tiles={tileCount}, TID-Tiles={tidTiles.Count}.")

            If reqNearestByTile Is Nothing OrElse reqNearestByTile.Count <> tileCount Then
                'Falls Resampling=bilinear und wir oben aus irgendeinem Grund reqNearestByTile nicht gebaut hätten:
                ' -> aber wir bauen es bereits, sobald hasTid True ist.
                Throw New InvalidOperationException("Interner Fehler: Nearest-Requests für TID fehlen.")
            End If

            For t As Integer = 0 To tileCount - 1

                ct.ThrowIfCancellationRequested()

                Dim tile As GebcoTileInfo = tidTiles(t)

                Dim ts, te As Integer
                GetTileSlice(t, tileCount, tidStart, tidEnd, ts, te)

                Dim pTile As New ProgressSlice(progress, ts, te, "GEBCO: ")
                pTile.Report(New ProgressInfo($"TID Tile: {t + 1}/{tileCount}: {Path.GetFileName(tile.EntryName)}", 0))

                GebcoTileProcessor.ProcessTidTile(
                    opts.TidZipPath, tile, reqNearestByTile(t), tidOut,
                    pTile, ct, progressPrefix:=$"TID: {t + 1}/{tileCount}")
            Next

            If tidOut Is Nothing OrElse tidOut.Length <> n Then Throw New InvalidOperationException($"Interner Fehler: tidOut ist Nothing oder hat eine falsche Länge. Erwartet={n}, ist={(If(tidOut Is Nothing, 0, tidOut.Length))}.")

        End If

        ct.ThrowIfCancellationRequested()

        '-----------------
        'H) LandMask bauen
        '-----------------

        Dim plm As New ProgressSlice(progress, lmStart, lmEnd, "GEBCO: ")
        plm.Report(New ProgressInfo("Erzeuge LandMask...", 0))

        Select Case opts.LandMaskMode

            Case LandMaskMode.FromHeight
                landMaskOut = LandMaskBuilder.BuildLandMaskFromHeight(
                    heightOut, latCount, lonCount,
                    applyMajorityFilter:=opts.UseHysteresis,
                    iterations:=opts.HysteresisIterations,
                    landThreshold:=opts.LandThreshold,
                    oceanThreshold:=opts.OceanThreshold)

            Case LandMaskMode.FromTid0
                If Not hasTid OrElse tidOut Is Nothing Then Throw New InvalidOperationException("LandMaskMode=FromTid0, aber kein TID verarbeitet.")

                For i As Integer = 0 To n - 1

                    Dim tidVal As Integer = tidOut(i)

                    If tidVal = 255 Then
                        landMaskOut(i) = 2          'Unknown
                    ElseIf tidVal = 0 Then
                        landMaskOut(i) = 1          'Land
                    ElseIf Not Single.IsNaN(heightOut(i)) AndAlso heightOut(i) >= 0.0F Then
                        landMaskOut(i) = 1          'Fallback: Land aus Höhe >= 0
                    Else
                        landMaskOut(i) = 0          'Ocean
                    End If
                Next

            Case Else
                Throw New NotSupportedException("ExternalSource ist noch nicht implementiert.")

        End Select

        plm.Report(New ProgressInfo("LandMask OK.", 100))

        ct.ThrowIfCancellationRequested()

        '----------------------
        'I) Meta + Cache-Objekt
        '----------------------

        Dim pMeta As New ProgressSlice(progress, metaStart, metaEnd, "GEBCO: ")
        pMeta.Report(New ProgressInfo("Erzeuge Cache-Meta...", 0))

        Dim meta As New EarthSurfaceCacheMeta With {
            .CacheVersion = EarthSurfaceCacheFormat.CurrentVersion,
            .Source = opts.SourceName,
            .CellSizeDeg = opts.CellSizeDeg,
            .LatCount = latCount,
            .LonCount = lonCount,
            .Resampling = resampling,
            .HasHeight = True,
            .HasTid = hasTid,
            .HasLandMask = True,
            .CreateUtc = DateTime.UtcNow,
            .RawHeightFile = opts.HeightZipPath,
            .RawTidFile = If(hasTid, opts.TidZipPath, Nothing)
        }

        Select Case opts.LandMaskMode

            Case LandMaskMode.FromHeight
                meta.LandMaskSource = LandMaskMode.FromHeight.ToString()
                meta.LandMaskNotes = $"height>=0 -> Land; NaN(Void) -> Ocean; hysteresis={opts.UseHysteresis}; it={opts.HysteresisIterations}; landThr={opts.LandThreshold}; oceanThr={opts.OceanThreshold}"

            Case LandMaskMode.FromTid0
                meta.LandMaskSource = LandMaskMode.FromTid0.ToString()
                meta.LandMaskNotes = $"tid==0 -> Land; tid<>>0 => FromHeight (height>=0 -> Land)"

            Case Else
                meta.LandMaskSource = LandMaskMode.ExternalSource.ToString()
                meta.LandMaskNotes = "not implemented"
        End Select

        Dim cache As New EarthSurfaceCache(meta, heightOut, tidOut, landMaskOut)

        pMeta.Report(New ProgressInfo("Meta OK.", 100))

        ct.ThrowIfCancellationRequested()

        '-------------
        'J) Save Cache
        '-------------

        Dim pSave As New ProgressSlice(progress, saveStart, saveEnd, "GEBCO: ")

        EarthSurfaceCacheStore.SaveCache(
            opts.SourceName,
            opts.CellSizeDeg,
            resampling,
            cache,
            landMaskVariant:=opts.LandMaskVariant,
            progress:=pSave,
            ct:=ct)

        '---------
        'K) Report
        '---------

        sb.AppendLine($"ErathSurface Cache Build ({resampling})")
        sb.AppendLine($"Source: {opts.SourceName}")
        sb.AppendLine($"CellSize: {opts.CellSizeDeg}°  LatCount={latCount}  LonCount={lonCount}")
        sb.AppendLine($"Height-ZIP: {opts.HeightZipPath}")
        sb.AppendLine($"TID-ZIP: {(If(hasTid, opts.TidZipPath, "(none)"))}")
        sb.AppendLine($"LandMaskMode: {opts.LandMaskMode}")
        sb.AppendLine($"LandMaskVariant: {opts.LandMaskVariant}")
        sb.AppendLine($"Cache-Verzeichnis: {EarthSurfaceCacheStore.CacheDir}")

        Return sb.ToString()

    End Function

    Private Shared Sub GetTileSlice(t As Integer, tileCount As Integer, blockStart As Integer, blockEnd As Integer,
                                    ByRef tileStart As Integer, ByRef tileEnd As Integer)

        Dim span As Integer = blockEnd - blockStart

        'Floor (Int) statt CInt, sonst "Banker's Rounding"-Artefakte

        tileStart = blockStart + CInt(Int((t / CDbl(tileCount)) * span))

        If t = tileCount - 1 Then
            tileEnd = blockEnd          'letztes Tile endet immer exakt auf blockEnd
        Else
            tileEnd = blockStart + CInt(Int(((t + 1) / CDbl(tileCount)) * span))
        End If

        'Sicherheitsnetz (nie rückwärts / nie out of range)
        If tileEnd < tileStart Then tileEnd = tileStart
        If tileStart < blockStart Then tileStart = blockStart
        If tileEnd > blockEnd Then tileEnd = blockEnd

    End Sub

End Class
