Imports System.IO
Imports System.Text
Imports System.Threading
Imports OSGeo.OGR

Public NotInheritable Class RgiRegionProcessor

    Private Const ProgressThrottleRowInterval As Integer = 2

    Public Shared Function Process(opts As LandCoverCacheBuilder.BuildOptions,
                                   progress As IProgress(Of ProgressInfo),
                                   ct As CancellationToken,
                                   Optional progressPrefix As String = "RGI Regionen") As RgiRegionProcessResult

        ArgumentNullException.ThrowIfNull(opts, NameOf(opts))

        Dim zipPath As String = opts.RawRgiRegionsZipPath
        ArgumentNullException.ThrowIfNullOrWhiteSpace(zipPath, NameOf(zipPath))
        If Not File.Exists(zipPath) Then Throw New FileNotFoundException("RGI Regions ZIP wurde nicht gefunden")

        progress?.Report(New ProgressInfo($"{progressPrefix}: Initialisere Region-Mapping...", 0))
        ct.ThrowIfCancellationRequested()

        '1) o1regions.shp im Zip finden
        Dim shpEntry As String = ZipHelpers.FindZipEntryEndingWith(zipPath, "o1regions.shp")
        If shpEntry Is Nothing Then Throw New FileNotFoundException("In der RGI Region ZIP wurde keine '*-01regions.shp' gefunden.")

        '2) GDAL VFS PFad bauen
        Dim vfsZip As String = GdalHelpers.ToVsiZipPath(zipPath)
        Dim vfsShp As String = $"{vfsZip}/{shpEntry.Replace("\", "/")}"

        '3) Dataset öffnen
        Using ds As DataSource = Ogr.Open(vfsShp, 0)

            If ds Is Nothing Then Throw New InvalidDataException($"OGR konnte Shapefile nicht öffnen: {vfsShp}")

            Dim layer As Layer = ds.GetLayerByIndex(0)
            If layer Is Nothing Then Throw New InvalidDataException($"OGR Layer fehlt in: {vfsShp}")

            '4) FieldIndex für "o1region" bestimmen
            Dim defn As FeatureDefn = layer.GetLayerDefn()
            If defn Is Nothing Then Throw New InvalidDataException("LayerDefn fehlt (unerwartet).")

            Dim o1Idx As Integer = -1
            For i As Integer = 0 To defn.GetFieldCount() - 1
                Dim name As String = defn.GetFieldDefn(i).GetName()
                If String.Equals(name, "o1region", StringComparison.OrdinalIgnoreCase) Then
                    o1Idx = i
                    Exit For
                End If
            Next
            If o1Idx < 0 Then Throw New InvalidDataException("Feld 'o1region' wurde im o1regions-Layer nicht gefunden.")

            '5) Region-Geometrien einsammeln: RGIRegion -> Geometry (Union, falls mehrfach)
            progress?.Report(New ProgressInfo($"{progressPrefix}: Lade Regions-Geometrien...", 1))
            ct.ThrowIfCancellationRequested()

            Dim regionGeoms As New Dictionary(Of RGIRegion, Geometry)()
            layer.ResetReading()

            Dim feat As Feature = Nothing

            Do

                ct.ThrowIfCancellationRequested()
                feat = layer.GetNextFeature()
                If feat Is Nothing Then Exit Do

                Using feat

                    Dim geomRef As Geometry = feat.GetGeometryRef()
                    If geomRef Is Nothing Then Continue Do

                    Dim o1Str As String = feat.GetFieldAsString(o1Idx)
                    If String.IsNullOrWhiteSpace(o1Str) Then Continue Do

                    o1Str = o1Str.Trim()

                    Dim regionCode As Integer
                    If Not Integer.TryParse(o1Str, regionCode) Then Continue Do

                    Dim region As RGIRegion = CType(regionCode, RGIRegion)
                    If Not [Enum].IsDefined(region) Then Continue Do

                    Dim geomClone As Geometry = geomRef.Clone()

                    If regionGeoms.ContainsKey(region) Then
                        Dim u As Geometry = regionGeoms(region).Union(geomClone)
                        regionGeoms(region).Dispose()
                        geomClone.Dispose()
                        regionGeoms(region) = u
                    Else
                        regionGeoms(region) = geomClone
                    End If

                End Using

            Loop

            '6) Envelopes pro Region für Quick-Reject
            Dim envByRegion As New Dictionary(Of RGIRegion, Envelope)()
            For Each kv In regionGeoms

                Dim env As New Envelope()
                kv.Value.GetEnvelope(env)
                envByRegion(kv.Key) = env

            Next

            '7) RegionMask bauen (0.5°)
            Dim cellSize As Double = 0.5
            Dim lonCount As Integer = CInt(360.0 / cellSize)
            Dim latCount As Integer = CInt(180.0 / cellSize)

            Dim regionsArr(lonCount * latCount - 1) As Byte

            'Stats
            Dim cellCounts As New Dictionary(Of RGIRegion, Integer)
            Dim minLon As New Dictionary(Of RGIRegion, Double)
            Dim maxLon As New Dictionary(Of RGIRegion, Double)
            Dim minLat As New Dictionary(Of RGIRegion, Double)
            Dim maxLat As New Dictionary(Of RGIRegion, Double)

            For Each r As RGIRegion In [Enum].GetValues(Of RGIRegion)()
                cellCounts(r) = 0
                minLon(r) = Double.PositiveInfinity
                maxLon(r) = Double.NegativeInfinity
                minLat(r) = Double.PositiveInfinity
                maxLat(r) = Double.NegativeInfinity
            Next

            progress?.Report(New ProgressInfo($"{progressPrefix}: Rasterisiere RegionMask...", 2))

            For row As Integer = 0 To latCount - 1
                ct.ThrowIfCancellationRequested()

                Dim latCenter As Double = -90.0 + (row + 0.5) * cellSize

                For col As Integer = 0 To lonCount - 1

                    Dim lonCenter As Double = -180.0 + (col + 0.5) * cellSize

                    Dim region As RGIRegion = RGIRegion.Global_Region

                    Using pt As New Geometry(wkbGeometryType.wkbPoint)
                        pt.AddPoint_2D(lonCenter, latCenter)

                        'Reihenfolge: 1..19 (Global_Region = 0 wird nicht getestet)
                        For code As Integer = 1 To 20
                            Dim r As RGIRegion = CType(code, RGIRegion)
                            If Not regionGeoms.ContainsKey(r) Then Continue For

                            Dim env As Envelope = envByRegion(r)
                            If lonCenter < env.MinX OrElse lonCenter > env.MaxX OrElse latCenter < env.MinY OrElse latCenter > env.MaxY Then
                                Continue For
                            End If

                            Dim g As Geometry = regionGeoms(r)
                            If g.Intersects(pt) Then
                                region = r
                                Exit For
                            End If
                        Next
                    End Using

                    Dim idx As Integer = row * lonCount + col
                    regionsArr(idx) = CByte(region)

                    'Stats
                    cellCounts(region) += 1
                    If lonCenter < minLon(region) Then minLon(region) = lonCenter
                    If lonCenter > maxLon(region) Then maxLon(region) = lonCenter
                    If latCenter < minLat(region) Then minLat(region) = latCenter
                    If latCenter > maxLat(region) Then maxLat(region) = latCenter

                Next

                If row Mod ProgressThrottleRowInterval = 0 Then
                    Dim pct = 2 + CInt((row / CDbl(latCount - 1)) * 96.0)         '2..98
                    progress?.Report(New ProgressInfo($"{progressPrefix}: Rasterisiere RegionMask...{Environment.NewLine}{Environment.NewLine}(Zeile {row}/{latCount}", pct))
                End If
            Next

            Dim sb As New StringBuilder()
            sb.AppendLine($"==== {progressPrefix} RegionMask Report =====")
            sb.AppendLine($"Source ZIP: {zipPath}")
            sb.AppendLine($"SHP Entry:  {shpEntry}")
            sb.AppendLine($"CellSize:   {cellSize:0.###}°")
            sb.AppendLine($"Grid:       {lonCount} x {latCount} (Lon x Lat)")
            sb.AppendLine($"LonRange:   [-180, 180)  LatRange: [-90, 90]")
            sb.AppendLine()

            For Each r As RGIRegion In [Enum].GetValues(GetType(RGIRegion))
                Dim n = cellCounts(r)
                If n = 0 Then
                    sb.AppendLine($"{CInt(r):00} {r}: Cells=0")
                Else
                    sb.AppendLine($"{CInt(r):00} {r}: Cells={n}, Lon=[{minLon(r):0.##}..{maxLon(r):0.##}], Lat=[{minLat(r):0.##}..{maxLat(r):0.##}]")
                End If
            Next

            Dim report = sb.ToString()

            ' 9) Ergebnis
            progress?.Report(New ProgressInfo($"{progressPrefix}: Fertig.", 100))

            Return New RgiRegionProcessResult With {
                .RegionMask = New RgiRegionMask(regionsArr),
                .Report = report
            }
        End Using


    End Function


End Class
