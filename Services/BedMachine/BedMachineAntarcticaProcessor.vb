Imports System.IO
Imports System.Text
Imports System.Threading
Imports OSGeo.GDAL
Imports OSGeo.OSR

Public NotInheritable Class BedMachineAntarcticaProcessor


    Private Const PolarFillLatThreshold As Double = -85.0       'Lat-Grenze für Inverse-Fill
    Private Const InverseSampleRadius As Integer = 1            'Fenstergröße in Source-Pixeln um den invers gemappten Center-Pixel. (0 => 1x1; 1 => 3x3; 2 => 5x5)

    Public Shared Function Process(opts As LandCoverCacheBuilder.BuildOptions, progress As IProgress(Of ProgressInfo), ct As CancellationToken, Optional progressPrefix As String = "BedMachine Antarctica") As BedMachineProcessResult

        ArgumentNullException.ThrowIfNullOrEmpty(NameOf(opts))
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.TargetCellSizeDeg, NameOf(opts))


        Dim ncPath As String = opts.RawBedMachineAntarcticaNcPath

        If String.IsNullOrWhiteSpace(ncPath) OrElse Not File.Exists(ncPath) Then Throw New FileNotFoundException("BedMachine Antarctica netCDF nicht gefunden.", ncPath)

        progress?.Report(New ProgressInfo($"{progressPrefix}: Öffne netCDF...", -1))
        ct.ThrowIfCancellationRequested()

        Dim sb As New StringBuilder()

        'globales Zielraster festlegen
        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))
        Dim nCells As Integer = latCount * lonCount

        'outIce-Array initialiseren und mit NaN füllen
        Dim outIce As Single() = New Single(nCells - 1) {}
        For i As Integer = 0 To outIce.Length - 1
            outIce(i) = Single.NaN
        Next


        Using dsRoot As Dataset = Gdal.Open(ncPath, Access.GA_ReadOnly)

            If dsRoot Is Nothing Then Throw New InvalidDataException("netCDF konnte nicht geöffnet werden.")

            '--- Subdataset ":thickness" finden ---
            Dim thicknessSub As String = Nothing
            For Each kv In dsRoot.GetMetadata("SUBDATASETS")
                If kv.Contains(":thickness") Then
                    thicknessSub = kv.Split("="c)(1)
                    Exit For
                End If
            Next

            If thicknessSub Is Nothing Then Throw New InvalidDataException("Subdataset ':thickness' nicht gefunden.")

            sb.AppendLine("=== BedMachine Antarctica ===")
            sb.AppendLine($"Subdataset: {thicknessSub}")
            sb.AppendLine()

            Using ds As Dataset = Gdal.Open(thicknessSub, Access.GA_ReadOnly)

                If ds.RasterCount <> 1 Then Throw New InvalidDataException($"Thickness-Subdataset hat {ds.RasterCount} Rasterbänder (erwartet: 1)")

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

                'Validierung auf Antarctica V3
                If Math.Abs(gt(1) - 500) > 1 Then Throw New InvalidDataException($"PixelSizeX ist {gt(1)}m (erwartet exakt 500m")
                If Math.Abs(gt(5) + 500) > 1 Then Throw New InvalidDataException($"PixelSizeY ist {gt(5)}m (erwartet exakt -500m")

                '--- Projection ---
                Dim wkt As String = ds.GetProjection()
                If String.IsNullOrWhiteSpace(wkt) Then Throw New InvalidDataException("Projection fehlt.")

                If Not wkt.Contains("Polar_Stereographic", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("Projection ist nicht Polar Stereographic.")
                If Not wkt.Contains("PARAMETER[""latitude_of_origin"",-71]", StringComparison.OrdinalIgnoreCase) Then Throw New InvalidDataException("latitude_of_origin ist nicht -71 (keine BedMachine Antarctica v3).")

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

                'Forward: src -> geo
                Dim ctFwd As New CoordinateTransformation(srcSrs, tgtSrs)
                'Inverse: geo -> src
                Dim ctInv As New CoordinateTransformation(tgtSrs, srcSrs)

                '--- NoData ---
                Dim noData As Double
                Dim hasNoData As Integer
                band.GetNoDataValue(noData, hasNoData)

                '--- Scanline ---
                Dim width As Integer = ds.RasterXSize
                Dim height As Integer = ds.RasterYSize


                '--- PASS 1: Forward Mapping ---
                Dim scan(width - 1) As Single

                progress?.Report(New ProgressInfo($"{progressPrefix}: Import (Forward) läuft...", 0))
                ct.ThrowIfCancellationRequested()

                For y As Integer = 0 To height - 1

                    ct.ThrowIfCancellationRequested()

                    band.ReadRaster(0, y, width, 1, scan, width, 1, 0, 0)

                    Dim yMap As Double = gt(3) + (y + 0.5) * gt(5)

                    For x As Integer = 0 To width - 1

                        Dim t As Single = BedMachineCommon.ReadThickness(scan(x), noData, hasNoData)
                        If Single.IsNaN(t) Then Continue For

                        Dim xMap As Double = gt(0) + (x + 0.5) * gt(1)

                        Dim p As Double() = New Double(1) {}
                        ctFwd.TransformPoint(p, xMap, yMap, 0)

                        Dim lon As Double = p(0)
                        Dim lat As Double = p(1)

                        Dim row As Integer = CInt(Math.Floor((90.0 - lat) / opts.TargetCellSizeDeg))
                        Dim col As Integer = CInt(Math.Floor((lon + 180.0) / opts.TargetCellSizeDeg))

                        If row < 0 OrElse row >= latCount OrElse col < 0 OrElse col >= lonCount Then Continue For

                        Dim idx As Integer = row * lonCount + col

                        'Maximum-Aggregation
                        If Single.IsNaN(outIce(idx)) OrElse t > outIce(idx) Then
                            outIce(idx) = t
                        End If
                    Next

                    If (y Mod BedMachineCommon.ProgressThrottleRowInterval) = 0 Then
                        Dim pct As Integer = CInt((y / Math.Max(1.0, height - 1)) * 100)
                        progress?.Report(New ProgressInfo($"{progressPrefix}: {ncPath}{Environment.NewLine}{Environment.NewLine}Zeilen verarbeitet: {y:N0}/{height:N0}", pct))
                    End If

                Next

                '--- PASS 2: Inverse Fill für Polnähe ---
                progress?.Report(New ProgressInfo($"{progressPrefix}: Pol-Fill (Inverse Mapping) läuft...", 86))
                ct.ThrowIfCancellationRequested()

                Dim invTouched As Integer = 0
                Dim invFilled As Integer = 0

                'Reusable Buffer für kleine Fenster
                Dim winSize As Integer = (InverseSampleRadius * 2) + 1
                Dim winBuf As Single() = New Single(winSize * winSize - 1) {}

                For row As Integer = 0 To latCount - 1

                    ct.ThrowIfCancellationRequested()

                    Dim latCenter As Double = 90.0 - (row + 0.5) * opts.TargetCellSizeDeg
                    If latCenter > PolarFillLatThreshold Then Continue For

                    For col As Integer = 0 To lonCount - 1

                        Dim idx As Integer = row * lonCount + col
                        If Not Single.IsNaN(outIce(idx)) Then Continue For      'nur Löcher füllen

                        'Zielzellen-Center
                        Dim lonCenter As Double = -180.0 + (col + 0.5) * opts.TargetCellSizeDeg

                        'geo -> src (x,y)
                        Dim q As Double() = New Double(1) {}
                        ctInv.TransformPoint(q, lonCenter, latCenter, 0)

                        Dim xMap As Double = q(0)
                        Dim yMap As Double = q(1)

                        'x,y -> Pixel (center)
                        Dim pxD As Double = ((xMap - gt(0)) / gt(1)) - 0.5
                        Dim pyD As Double = ((yMap - gt(3)) / gt(5)) - 0.5

                        Dim px0 As Integer = CInt(Math.Floor(pxD))
                        Dim py0 As Integer = CInt(Math.Floor(pyD))

                        invTouched += 1

                        'Fenster clampen
                        Dim xOff As Integer = px0 - InverseSampleRadius
                        Dim yOff As Integer = py0 - InverseSampleRadius
                        Dim xSize As Integer = winSize
                        Dim ySize As Integer = winSize

                        If xOff < 0 Then
                            xSize -= (0 - xOff)
                            xOff = 0
                        End If
                        If yOff < 0 Then
                            ySize -= (0 - yOff)
                            yOff = 0
                        End If
                        If xOff + xSize > width Then xSize = width - xOff
                        If yOff + ySize > height Then ySize = height - yOff

                        If xSize <= 0 OrElse ySize <= 0 Then Continue For

                        'Read window
                        band.ReadRaster(xOff, yOff, xSize, ySize, winBuf, xSize, ySize, 0, 0)

                        'Max über gültige Samples
                        Dim best As Single = Single.NaN
                        Dim n As Integer = xSize * ySize
                        For k As Integer = 0 To n - 1

                            Dim t As Single = BedMachineCommon.ReadThickness(winBuf(k), noData, hasNoData)
                            If Single.IsNaN(t) Then Continue For
                            If Single.IsNaN(best) OrElse t > best Then best = t

                        Next

                        If Not Single.IsNaN(best) Then
                            outIce(idx) = best
                            invFilled += 1
                        End If

                    Next

                    If (row Mod 8) = 0 Then
                        Dim pct As Integer = 86 + CInt((row / Math.Max(1.0, latCount - 1)) * 14)     '86...100
                        progress?.Report(New ProgressInfo($"{progressPrefix}: Pol-Fill Zeile {row}/{latCount} (gefüllt: {invFilled:N0})", pct))
                    End If

                Next

                sb.AppendLine()
                sb.AppendLine("=== Inverse Pol-Fill ===")
                sb.AppendLine($"LatThreshold: {PolarFillLatThreshold:0.##}°")
                sb.AppendLine($"Window: {winSize}x{winSize}")
                sb.AppendLine($"Touched cells (NaN in Polzone): {invTouched:N0}")
                sb.AppendLine($"Filled cells: {invFilled:N0}")

            End Using
        End Using

        'DEBUG-Statistik
        Dim filledCount As Integer = 0
        Dim minT As Single = Single.PositiveInfinity
        Dim maxT As Single = Single.NegativeInfinity
        Dim sumT As Double = 0.0

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

        Dim meanT As Double = If(filledCount > 0, sumT / filledCount, Double.NaN)

        sb.AppendLine()
        sb.AppendLine("=== Aggregation Statistik ===")
        sb.AppendLine($"Target Grid: {latCount} x {lonCount} ({nCells:N0} Zellen)")
        sb.AppendLine($"Gefüllte Zellen: {filledCount:N0}")
        sb.AppendLine($"NaN-Zellen: {(nCells - filledCount):N0}")

        If filledCount > 0 Then
            sb.AppendLine($"Thickness Min: {minT:0.##} m")
            sb.AppendLine($"Thickness Max: {maxT:0.##} m")
            sb.AppendLine($"Thickness Mean: {meanT:0.##} m")
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
