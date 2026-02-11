Imports System.IO
Imports System.Text
Imports System.Threading
Imports OSGeo.GDAL
Imports OSGeo.OSR

Public NotInheritable Class BedMachineGreenlandProcessor

    Public Shared Function Process(opts As LandCoverCacheBuilder.BuildOptions, progress As IProgress(Of ProgressInfo), ct As CancellationToken, Optional progressPrefix As String = "BedMachine Greenland") As BedMachineProcessResult

        ArgumentNullException.ThrowIfNullOrEmpty(NameOf(opts))
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.TargetCellSizeDeg, NameOf(opts))

        Dim ncPath As String = opts.RawBedMachineGreenlandNcPath

        If String.IsNullOrWhiteSpace(ncPath) OrElse Not File.Exists(ncPath) Then Throw New FileNotFoundException("BedMachine Greenland netCDF nicht gefunden.", ncPath)

        progress?.Report(New ProgressInfo($"{progressPrefix}: Öffne netCDF...", -1))
        ct.ThrowIfCancellationRequested()

        Dim sb As New StringBuilder()

        '--- globales Zielraster ---
        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))
        Dim nCells As Integer = latCount * lonCount

        Dim outIce As Single() = New Single(nCells - 1) {}
        For i As Integer = 0 To outIce.Length - 1
            outIce(i) = Single.NaN
        Next

        Using dsRoot As Dataset = Gdal.Open(ncPath, Access.GA_ReadOnly)

            If dsRoot Is Nothing Then Throw New InvalidDataException("netCDF konnte nicht geöffnet werden.")

            '--- Subdataset finden ---
            Dim thicknessSub As String = Nothing
            For Each kv In dsRoot.GetMetadata("SUBDATASETS")
                If kv.Contains(":thickness") Then
                    thicknessSub = kv.Split("="c)(1)
                    Exit For
                End If
            Next

            If thicknessSub Is Nothing Then Throw New InvalidDataException("Subdataset ':thickness' nicht gefunden.")

            sb.AppendLine("=== BedMachine Greenland ===")
            sb.AppendLine($"Subdataset: {thicknessSub}")
            sb.AppendLine()

            Using ds As Dataset = Gdal.Open(thicknessSub, Access.GA_ReadOnly)

                If ds.RasterCount <> 1 Then Throw New InvalidDataException($"Thickness-Subdataset hat {ds.RasterCount} Rasterbänder (erwartet: 1 Rasterband).")

                Dim band As Band = ds.GetRasterBand(1)

                '--- GeoTransform ---
                Dim gt(5) As Double
                ds.GetGeoTransform(gt)

                sb.AppendLine("== GeoTransform ==")
                sb.AppendLine($"OriginX: {gt(0)}")
                sb.AppendLine($"PixelSizeX: {gt(1)}")
                sb.AppendLine($"OriginY: {gt(3)}")
                sb.AppendLine($"PixelSizeY: {gt(5)}")
                sb.AppendLine()

                'Validierung auf Greenland V6
                If Math.Abs(gt(1) - 150) > 1 Then Throw New InvalidDataException($"PixelSizeX ist {gt(1)}m (erwartet 150m).")
                If Math.Abs(gt(5) + 150) > 1 Then Throw New InvalidDataException($"PixelSizeY ist {gt(5)}m (erwartet -150m).")

                '--- Projection ---
                Dim wkt As String = ds.GetProjection()
                If String.IsNullOrWhiteSpace(wkt) Then Throw New InvalidDataException("Projection fehlt.")
                If Not wkt.Contains("Polar_Stereographic", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("Projection ist nicht Polar Sterepgraphic.")
                If Not wkt.Contains("latitude_of_origin", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("latitude_of_origin fehlt (keine BedMachine Greenland v6).")

                sb.AppendLine("== Projection (WKT) ==")
                sb.AppendLine(wkt)
                sb.AppendLine()

                '--- SRS ---
                Dim srcSrs As New SpatialReference(wkt)
                Dim tgtSrs As New SpatialReference(Nothing)

                If tgtSrs.ImportFromEPSG(4326) <> 0 Then Throw New InvalidDataException("Konnte Ziel-SRS EPSG:4326 nicht initialisieren.")

                'Achsreihenfolge fixieren
                srcSrs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER)
                tgtSrs.SetAxisMappingStrategy(AxisMappingStrategy.OAMS_TRADITIONAL_GIS_ORDER)

                Dim ctGeo As New CoordinateTransformation(srcSrs, tgtSrs)

                '--- noData ---
                Dim noData As Double
                Dim hasNoData As Integer
                band.GetNoDataValue(noData, hasNoData)

                '--- Scanline ---
                Dim width As Integer = ds.RasterXSize
                Dim height As Integer = ds.RasterYSize
                Dim scan(width - 1) As Single

                progress?.Report(New ProgressInfo($"{progressPrefix}: Import läuft...", 0))
                ct.ThrowIfCancellationRequested()

                '--- Loop ---
                For y As Integer = 0 To height - 1

                    ct.ThrowIfCancellationRequested()

                    band.ReadRaster(0, y, width, 1, scan, width, 1, 0, 0)

                    Dim yMap As Double = gt(3) + (y + 0.5) * gt(5)

                    For x As Integer = 0 To width - 1

                        Dim t As Single = BedMachineCommon.ReadThickness(scan(x), noData, hasNoData)
                        If Single.IsNaN(t) Then Continue For

                        Dim xMap As Double = gt(0) + (x + 0.5) * gt(1)

                        Dim p As Double() = New Double(1) {}
                        ctGeo.TransformPoint(p, xMap, yMap, 0)

                        Dim lon As Double = p(0)
                        Dim lat As Double = p(1)

                        Dim row As Integer = CInt(Math.Floor((90.0 - lat) / opts.TargetCellSizeDeg))
                        Dim col As Integer = CInt(Math.Floor((lon + 180.0) / opts.TargetCellSizeDeg))

                        If row < 0 OrElse row >= latCount OrElse col < 0 OrElse col >= lonCount Then Continue For

                        Dim idx As Integer = row * lonCount + col

                        If Single.IsNaN(outIce(idx)) OrElse t > outIce(idx) Then
                            outIce(idx) = t
                        End If

                    Next

                    If (y Mod BedMachineCommon.ProgressThrottleRowInterval) = 0 Then
                        Dim pct As Integer = CInt((y / Math.Max(1.0, height - 1)) * 100)
                        progress?.Report(New ProgressInfo($"{progressPrefix}: {ncPath}{Environment.NewLine}{Environment.NewLine}Zeilen verarbeitet: {y:N0}/{height:N0}", pct))
                    End If

                Next

            End Using
        End Using

        '--- DEBUG-Statistik ---
        Dim filledCount As Integer = 0
        Dim minT As Single = Single.PositiveInfinity
        Dim maxT As Single = Single.NegativeInfinity
        Dim sumT As Double = 0

        Dim minRow As Integer = Integer.MaxValue
        Dim maxRow As Integer = Integer.MinValue
        Dim minCol As Integer = Integer.MaxValue
        Dim maxCol As Integer = Integer.MinValue

        For i As Integer = 0 To outIce.Length - 1

            Dim v As Single = outIce(i)
            If Single.IsNaN(v) Then Continue For

            filledCount += 1
            sumT += v

            minT = Math.Min(minT, v)
            maxT = Math.Max(maxT, v)

            Dim row As Integer = i \ lonCount
            Dim col As Integer = i Mod lonCount

            minRow = Math.Min(minRow, row)
            maxRow = Math.Max(maxRow, row)
            minCol = Math.Min(minCol, col)
            maxCol = Math.Max(maxCol, col)

        Next

        sb.AppendLine()
        sb.AppendLine("=== Aggregation Statistik ===")
        sb.AppendLine($"Target Grid: {latCount} x {lonCount} ({nCells:N0} Zellen)")
        sb.AppendLine($"Gefüllte Zellen: {filledCount:N0}")
        sb.AppendLine($"NaN-Zellen: {(nCells - filledCount):N0}")

        If filledCount > 0 Then
            sb.AppendLine($"Thickness Min: {minT:0.##} m")
            sb.AppendLine($"Thickness Max: {maxT:0.##} m")
            sb.AppendLine($"Thickness Mean: {(sumT / filledCount):0.##} m")
            sb.AppendLine($"Row-Range: {minRow} .. {maxRow}")
            sb.AppendLine($"Col-Range: {minCol} .. {maxCol}")
        Else
            sb.AppendLine("WARNUNG: Keine einzige Zielzelle wurde befüllt!")
        End If

        Return New BedMachineProcessResult With {
            .IceThicknessM = outIce,
            .Report = sb.ToString()
        }

    End Function

End Class