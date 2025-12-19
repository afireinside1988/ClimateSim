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
        Public Property Resampling As String = "nearest"
        Public Property LandMaskMode As LandMaskMode

        Public Property UseHysteresis As Boolean
        Public Property HysteresisIterations As Integer = 1

        'Hysterese-Defaults
        Public Property LandThreshold As Integer = 6
        Public Property OceanThreshold As Integer = 3

        'LandMask Variant Tag für Dateinamen (z.B. "lm_fromHeight_hyst1_it01" oder "lm_fromTid0")
        Public Property LandMaskVariant As String

    End Class

    Public Shared Function BuildAndSaveNearest(opts As BuildOptions, progress As IProgress(Of ProgressInfo), ct As CancellationToken) As String

        ArgumentNullException.ThrowIfNull(opts)
        If String.IsNullOrWhiteSpace(opts.SourceName) Then Throw New ArgumentException("SourceName fehlt")
        If String.IsNullOrWhiteSpace(opts.HeightZipPath) OrElse Not File.Exists(opts.HeightZipPath) Then
            Throw New FileNotFoundException("Height-Zip nicht gefunden.", opts.HeightZipPath)
        End If
        If opts.CellSizeDeg <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(opts.CellSizeDeg))

        ct.ThrowIfCancellationRequested()

        Dim sb As New StringBuilder()

        '---------------------------------------------
        'A) Tiles listen und deterministisch sortieren
        '---------------------------------------------

        progress?.Report(New ProgressInfo("Scanne Height-Zip nach Tiles...", ProgressInfo.Indeterminate))
        Dim heightTiles = GebcoZipCatalog.ListAscTiles(opts.HeightZipPath)
        If heightTiles.Count = 0 Then Throw New InvalidDataException("Keine .asc Tiles im Height-ZIP gefunden.")

        Dim hasTid As Boolean = (Not String.IsNullOrWhiteSpace(opts.TidZipPath) AndAlso File.Exists(opts.TidZipPath))
        Dim tidTiles As List(Of GebcoTileInfo) = Nothing

        If hasTid Then
            progress?.Report(New ProgressInfo("Scanne TID-ZIP nach Tiles...", ProgressInfo.Indeterminate))
            tidTiles = GebcoZipCatalog.ListAscTiles(opts.TidZipPath)
            If tidTiles.Count = 0 Then hasTid = False
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
        For i As Integer = 0 To n - 1
            heightOut(i) = Single.NaN       'sicherer Default, falls irgendwas "ungefüllt" bleibt
        Next

        Dim tidOut As Single() = If(hasTid, New Single(n - 1) {}, Nothing)
        Dim landMaskOut As Byte() = New Byte(n - 1) {}

        '------------------------------
        'D) Nearest-Request vorbereiten
        '------------------------------
        Dim pReq As New ProgressSlice(progress, 0, 5, "GEBCO: ")
        pReq.Report(New ProgressInfo("Baue Nearest-Mapping...", 0))

        Dim reqByTile = GebcoNearestRequestBuilder.BuildRequests(heightTiles, opts.CellSizeDeg, latCount, lonCount)

        ct.ThrowIfCancellationRequested()

        '------------------------
        'E) HEIGHT Tiles streamen (5..55)
        '------------------------
        Dim pHeight As New ProgressSlice(progress, 5, 55, "GEBCO: ")
        Dim tileCount As Integer = heightTiles.Count

        For t As Integer = 0 To tileCount - 1
            ct.ThrowIfCancellationRequested()

            Dim tile As GebcoTileInfo = heightTiles(t)
            pHeight.Report(New ProgressInfo($"HEIGHT Tile {t + 1}/{tileCount}: {Path.GetFileName(tile.EntryName)}", CInt((t / Math.Max(1.0, tileCount)) * 100.0)))

            'TileProcessor schreibt direkt ins Zielarray (NaN für NODATA)
            GebcoTileProcessor.ProcessHeightTile(opts.HeightZipPath, tile, reqByTile(t), heightOut, pHeight, ct, progressPrefix:=$"HEIGHT {t + 1}/{tileCount}")

        Next

        '------------------------------
        'F) TID Tiles streamen (55..75)
        '------------------------------

        If hasTid Then

            Dim pTid As New ProgressSlice(progress, 55, 75, "GEBCO: ")

            For t As Integer = 0 To tileCount - 1

                ct.ThrowIfCancellationRequested()

                Dim tile As GebcoTileInfo = tidTiles(t)
                pTid.Report(New ProgressInfo($"TID Tile {t + 1}/{tileCount}: {Path.GetFileName(tile.EntryName)}", CInt((t / Math.Max(1.0, tileCount)) * 100.0)))

                'Wir lesen Byte-weise, aber speichern in Single() (Cache-Format erwartet Single für TID
                '=> kleiner Adapter: temporär Byte[] wäre möglich, aber wir sparen uns den extra RAM und konvertieren on the fly.
                Dim tmpByte As Byte() = New Byte(n - 1) {} 'falls du 0 extra RAM willst: siehe Hinweis unten
                GebcoTileProcessor.ProcessTidTile(opts.TidZipPath, tile, reqByTile(t), tmpByte, pTid, ct, progressPrefix:=$"TID: {t + 1}/{tileCount}")

                'Konvertiere nur die tatsächlich gesetzten Werte? -> schwierig ohne extra Tracking
                'Wir nehmen hier die pragmatische Lösung: 1x Konvert am Ende ODER tileweise wie hier:
                For i As Integer = 0 To n - 1
                    tidOut(i) = CSng(tmpByte(i))
                Next

            Next
        End If

        ct.ThrowIfCancellationRequested()

        '--------------------------
        'G) LandMask bauen (75..88)
        '--------------------------

        Dim plm As New ProgressSlice(progress, 75, 88, "GEBCO: ")
        plm.Report(New ProgressInfo("Erzeuge LandMask...", 0))

        Select Case opts.LandMaskMode
            Case LandMaskMode.FromHeight
                'WICHTIG: LandMaskBuilder kennt NaN nicht explizit -> NaN>=0 ist False => Ocean (0)
                landMaskOut = LandMaskBuilder.BuildLandMaskFromHeight(
                    heightOut, latCount, lonCount,
                    applyMajorityFilter:=opts.UseHysteresis,
                    iterations:=opts.HysteresisIterations,
                    landThreshold:=opts.LandThreshold,
                    oceanThreshold:=opts.OceanThreshold)

            Case LandMaskMode.FromTid0

                If Not hasTid OrElse tidOut Is Nothing Then
                    Throw New InvalidOperationException("LandMaskMode=FromTido, aber kein TID-ZIP geladen.")
                End If

                For i As Integer = 0 To n - 1
                    landMaskOut(i) = If(CInt(tidOut(i)) = 0, CByte(1), CByte(0))
                Next
            Case Else
                Throw New NotSupportedException("ExternalSource ist noch nicht implementiert.")
        End Select

        plm.Report(New ProgressInfo("LandMask OK.", 100))

        ct.ThrowIfCancellationRequested()

        '-------------------------------
        'H) Meta + Cache-Objekt (88..92)
        '-------------------------------

        Dim pMeta As New ProgressSlice(progress, 88, 92, "GEBCO: ")
        pMeta.Report(New ProgressInfo("Erzeuge Cache-Meta...", 0))

        Dim meta As New EarthSurfaceCacheMeta With {
            .CacheVersion = EarthSurfaceCacheFormat.CurrentVersion,
            .Source = opts.SourceName,
            .CellSizeDeg = opts.CellSizeDeg,
            .LatCount = latCount,
            .LonCount = lonCount,
            .Resampling = "nearest",
            .HasHeight = True,
            .HasTid = hasTid,
            .HasLandMask = True,
            .CreateUtc = DateTime.UtcNow,
            .RawSubIceTopoFile = opts.HeightZipPath,
            .RawTidFile = If(hasTid, opts.TidZipPath, Nothing)
        }

        Select Case opts.LandMaskMode
            Case LandMaskMode.FromHeight
                meta.LandMaskSource = "FromHeight"
                meta.LandMaskNotes = $"height>=0 => land; NaN(Void)=>ocean; hysteresis={opts.UseHysteresis}; it={opts.HysteresisIterations}; landThr={opts.LandThreshold}; oceanThr={opts.OceanThreshold}"
            Case LandMaskMode.FromTid0
                meta.LandMaskSource = "FromTid0"
                meta.LandMaskNotes = "tid==0 => land"
            Case Else
                meta.LandMaskSource = "ExternalSource"
                meta.LandMaskNotes = "not implemented"
        End Select

        Dim cache As New EarthSurfaceCache(meta, heightOut, tidOut, landMaskOut)

        pMeta.Report(New ProgressInfo("Meta OK,", 100))

        ct.ThrowIfCancellationRequested()

        '------------------------------------------------------------------------
        'I) Speichern (92..109) - SaveCache hat eigenen Progress 0..100 -> slice!
        '------------------------------------------------------------------------

        Dim pSave As New ProgressSlice(progress, 92, 100, "GEBCO: ")

        EarthSurfaceCacheStore.SaveCache(
            opts.SourceName,
            opts.CellSizeDeg,
            "nearest",
            cache,
            landMaskVariant:=opts.LandMaskVariant,
            progress:=pSave,
            ct:=ct)

        '---------
        'J) Report
        '---------

        sb.AppendLine("EarthSurface Cache Build (Nearest)")
        sb.AppendLine($"Source: {opts.SourceName}")
        sb.AppendLine($"CellSize: {opts.CellSizeDeg}°  LatCount={latCount}  LonCount={lonCount}")
        sb.AppendLine($"Height-ZIP: {opts.HeightZipPath}")
        sb.AppendLine($"TID-ZIP: {(If(hasTid, opts.TidZipPath, "(none)"))}")
        sb.AppendLine($"LandMaskMode: {opts.LandMaskMode}")
        sb.AppendLine($"LandMaskVariant: {opts.LandMaskVariant}")
        sb.AppendLine($"Cache-Verzeichnis: {EarthSurfaceCacheStore.CacheDir}")

        Return sb.ToString()
    End Function
End Class
