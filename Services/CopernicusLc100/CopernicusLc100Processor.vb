Imports OSGeo.GDAL
Imports MaxRev.Gdal.Core

Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.ComponentModel.Design

Public Class CopernicusLc100Processor

    Private Const ProgressThrottleRowInterval As Integer = 128
    Private Const GdalMaxCacheMB As String = "256"

    Public Class ProcessResult
        Public Property Classes As Byte()
        Public Property Confidence As Byte()
        Public Property Report As String
    End Class

    Public Shared Sub InitGdal()
        'Einmal vor dem Import Initialisieren
        GdalBase.ConfigureAll()
        Gdal.SetConfigOption("GDAL_CACHEMAX", GdalMaxCacheMB)
        Gdal.SetConfigOption("GDAL_DISABLE_READDIR_ON_OPEN", "YES")
        Gdal.AllRegister()
    End Sub

    Public Shared Function ProcessCopernicusLc100(opts As LandCoverCacheBuilder.BuildOptions,
                                                 progress As IProgress(Of ProgressInfo), ct As CancellationToken,
                                                 Optional progressPrefix As String = "COPERNICUS (LC100)") As ProcessResult

        If String.IsNullOrWhiteSpace(opts.ClassTifPath) OrElse Not File.Exists(opts.ClassTifPath) Then
            Throw New FileNotFoundException("Class GeoTIFF nicht gefunden.", opts.ClassTifPath)
        End If

        Dim includeConf As Boolean = opts.IncludeConfidence AndAlso Not String.IsNullOrWhiteSpace(opts.ProbaTifPath) AndAlso File.Exists(opts.ProbaTifPath)

        'Zielraster ableiten
        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))

        Dim swTotal As Stopwatch = Stopwatch.StartNew()
        Dim swImport As Stopwatch = Nothing
        Dim swFinalize As Stopwatch = Nothing

        progress?.Report(New ProgressInfo($"{progressPrefix}: Öffne GeoTIFF...", 0))
        ct.ThrowIfCancellationRequested()

        Using dsClass As Dataset = GdalHelpers.OpenDataset(opts.ClassTifPath, "Class-TIF")

            Dim bandClass As Band = dsClass.GetRasterBand(1)

            If bandClass Is Nothing Then Throw New InvalidDataException("Fehler beim Öffnen der Class-TIF: Kein Rasterband gefunden.")

            'Validierung Class-TIF
            Dim shortClass As String = Nothing
            If Not GdalHelpers.TryGetMetaValue(bandClass, "short_name", shortClass) Then
                Throw New InvalidDataException("Class-TIF: Band-Metadata 'short_name' fehlt (Validierung nicht möglich).")
            End If
            If Not shortClass.Contains("Discrete-Classification-map", StringComparison.OrdinalIgnoreCase) Then
                Throw New InvalidDataException($"Die Datei '{Path.GetFileName(opts.ClassTifPath)}' ist keine Discrete-Classification-map (short_name='{shortClass}').")
            End If

            'Missing-Value ermitteln (API bevorzugt, fallback Metadata, fallback 255)
            Dim classMissing As Byte = GdalHelpers.GetMissingValueByte(bandClass, 255)

            Dim width As Integer = dsClass.RasterXSize
            Dim height As Integer = dsClass.RasterYSize

            'GeoTransform: gt(0)=originX, gt(3)=originY, gt(1)=pixelW, gt(5)=pixelH (meist negativ)
            Dim gt(5) As Double
            dsClass.GetGeoTransform(gt)

            Dim srcLonMin As Double = gt(0)
            Dim srcLonMax As Double = gt(0) + width * gt(1)
            Dim srcLatMax As Double = gt(3)
            Dim srcLatMin As Double = gt(3) + height * gt(5)

            'Für EPSG:4326 (Lon/lat in Grad) sollte gelten: gt2=0, gt4=0, north-up
            'Wir prüfen nur grob, wenn nicht, brechen wir lieber früh ab.
            If Math.Abs(gt(2)) > 0.00000001 OrElse Math.Abs(gt(4)) > 0.0000001 Then
                Throw New InvalidDataException("GeoTIFF ist rotiert/sheared (gt2/gt4 != 0). Dieses Format ist nicht unterstützt.")
            End If

            For i As Integer = 0 To gt.Length - 1
                Debug.WriteLine($"GT({i}): {gt(i)}")
            Next

            Dim pixelW As Double = gt(1)
            Dim pixelH As Double = gt(5)

            If pixelW <= 0 OrElse pixelH >= 0 Then
                Throw New InvalidDataException("Unerwartete GeoTransform-Skalierung (pixelW <= 0 oder pixelH >= 0).")
            End If

            progress?.Report(New ProgressInfo($"{progressPrefix}: Mapping vorbereiten...", 0))
            ct.ThrowIfCancellationRequested()

            'Precompute: SourceX -> TargetCol, SourceY -> TargetRow
            Dim colMap(width - 1) As Integer
            For x As Integer = 0 To width - 1
                Dim lonCenter As Double = gt(0) + (x + 0.5) * pixelW
                Dim col As Integer = CInt(Math.Floor((lonCenter - opts.LonMinCenter) / opts.TargetCellSizeDeg))
                If col < 0 OrElse col >= lonCount Then col = -1
                colMap(x) = col
            Next

            Dim rowMap(height - 1) As Integer
            For y As Integer = 0 To height - 1
                Dim latCenter As Double = gt(3) + (y + 0.5) * pixelH    'pixelH ist negativ -> lat nimmt ab
                Dim row As Integer = CInt(Math.Floor((opts.LatMaxCenter - latCenter) / opts.TargetCellSizeDeg))
                If row < 0 OrElse row >= latCount Then row = -1
                rowMap(y) = row
            Next

            Dim nCells As Integer = latCount * lonCount

            'Histogramm: nCells * classCount (UInt32 reicht)
            Dim hist As UInteger() = New UInteger(nCells * opts.LC11ClassCount - 1) {}

            'Confidence Aggregation (optional)
            Dim confSum As ULong() = Nothing
            Dim confCnt As UInteger() = Nothing
            Dim dsProba As Dataset = Nothing
            Dim bandProba As Band = Nothing
            Dim probaMetaDataDump As String = Nothing
            Dim probaMissing As Byte = 255

            Try

                If includeConf Then
                    dsProba = GdalHelpers.OpenDataset(opts.ProbaTifPath, "Proba-TIF")
                    If dsProba.RasterXSize <> width OrElse dsProba.RasterYSize <> height Then
                        Throw New InvalidDataException("Proba-TIF Dimensionen passen nicht zur Class-TIF.")
                    End If
                    bandProba = dsProba.GetRasterBand(1)

                    If bandProba Is Nothing Then Throw New InvalidDataException("Fehler beim Öffnen der Proba-TIF: Kein Rasterband gefunden.")

                    'Validierung Proba-TIF:
                    Dim shortProba As String = Nothing
                    If Not GdalHelpers.TryGetMetaValue(bandProba, "short_name", shortProba) Then
                        Throw New InvalidDataException("Proba-TIF: Band-Metadata 'short_name' fehlt (Validierung nicht möglich).")
                    End If
                    If Not shortProba.Contains("Discrete-Classification-proba", StringComparison.OrdinalIgnoreCase) Then
                        Throw New InvalidDataException($"Die Datei '{Path.GetFileName(opts.ProbaTifPath)}' ist keine Discrete-Classification-proba (short_name='{shortProba}').")
                    End If

                    'Missing-Value ermitteln (API bevorzugt, fallback Metadata, fallback 255)
                    probaMissing = GdalHelpers.GetMissingValueByte(bandProba, 255)

                    'DEBUG
                    Dim tmp As New StringBuilder()
                    AppendGdalMetadata(tmp, "Proba-TIF Metadata:", dsProba, bandProba)
                    probaMetaDataDump = tmp.ToString()

                    confSum = New ULong(nCells - 1) {}
                    confCnt = New UInteger(nCells - 1) {}
                End If

                progress?.Report(New ProgressInfo($"{progressPrefix}: Import läuft...", 0))
                ct.ThrowIfCancellationRequested()

                'Lesen zeilenweise (Scanline)
                Dim scanClass As Byte() = New Byte(width - 1) {}
                Dim scanProba As Byte() = If(includeConf, New Byte(width - 1) {}, Nothing)

                swImport = Stopwatch.StartNew()

                For y As Integer = 0 To height - 1

                    ct.ThrowIfCancellationRequested()

                    Dim tRow As Integer = rowMap(y)
                    If tRow = -1 Then Continue For

                    bandClass.ReadRaster(0, y, width, 1, scanClass, width, 1, 0, 0)

                    If includeConf Then
                        bandProba.ReadRaster(0, y, width, 1, scanProba, width, 1, 0, 0)
                    End If

                    Dim rowBase As Integer = tRow * lonCount

                    For x As Integer = 0 To width - 1
                        Dim tCol As Integer = colMap(x)
                        If tCol = -1 Then Continue For

                        Dim cellIndex As Integer = rowBase + tCol

                        Dim lc As Byte

                        If scanClass(x) = classMissing Then
                            lc = opts.NoDataClass
                        Else
                            Dim copCode As Integer = CInt(scanClass(x))
                            lc = CByte(CopernicusLc100Mapping.MapCopernicusToLc11(copCode))
                        End If

                        Dim histIndex As Integer = cellIndex * opts.LC11ClassCount + lc
                        hist(histIndex) += 1UI

                        If includeConf Then
                            Dim p As Byte = scanProba(x)
                            'Proba ist Byte ohne Skalierung; Missing = 255
                            If p <> probaMissing Then
                                confSum(cellIndex) += p
                                confCnt(cellIndex) += 1UI
                            End If
                        End If
                    Next

                    'Progress-Throttling
                    If (y Mod ProgressThrottleRowInterval) = 0 Then
                        Dim pct As Integer = CInt((y / Math.Max(1.0, height - 1)) * 98)   '95% für Import, Rest finalize
                        progress?.Report(New ProgressInfo($"{progressPrefix}: {Path.GetFileName(opts.ClassTifPath)}{Environment.NewLine}{Environment.NewLine}Row {y:N0}/{height:N0}", pct))
                    End If
                Next

            Finally
                If swImport IsNot Nothing AndAlso swImport.IsRunning Then swImport.Stop()

                'Proba-DataSet schließen
                If dsProba IsNot Nothing Then dsProba.Dispose()
            End Try


            swFinalize = Stopwatch.StartNew()

            progress?.Report(New ProgressInfo($"{progressPrefix}: Finalisiere Zielraster...", 98))
            ct.ThrowIfCancellationRequested()

            'Finalize per Cell: argmax histogramm
            Dim outClass As Byte() = New Byte(nCells - 1) {}
            Dim outConf As Byte() = If(includeConf, New Byte(nCells - 1) {}, Nothing)

            Dim classCounts(opts.LC11ClassCount - 1) As UInteger

            Dim emptyCells As Integer = 0           'bestCount = 0 (kein einziger Pixel gemappt)
            Dim nodataDominant As Integer = 0       'bestClass = NoData, aber bestCount > 0

            Dim stats As New Dictionary(Of Byte, ULong)()
            For i As Integer = 0 To nCells - 1

                ct.ThrowIfCancellationRequested()

                'argmax
                Dim bestClass As Integer = opts.NoDataClass
                Dim bestCount As UInteger = 0UI

                Dim baseIdx As Integer = i * opts.LC11ClassCount
                For c As Integer = 0 To opts.LC11ClassCount - 1
                    Dim cnt As UInteger = hist(baseIdx + c)
                    If cnt > bestCount Then
                        bestCount = cnt
                        bestClass = c
                    End If
                Next

                'Diagnose
                If bestCount = 0UI Then
                    emptyCells += 1
                ElseIf bestClass = opts.NoDataClass Then
                    nodataDominant += 1
                End If

                outClass(i) = CByte(bestClass)

                'Confidence avg
                If includeConf Then
                    Dim cnt As UInteger = confCnt(i)
                    If cnt = 0UI Then
                        outConf(i) = probaMissing
                    Else
                        Dim avg As UInteger = CUInt(confSum(i) \ cnt)
                        If avg > 255UI Then avg = 255UI
                        outConf(i) = CByte(avg)
                    End If
                End If

                'Stats
                If Not stats.ContainsKey(outClass(i)) Then stats(outClass(i)) = 0UL
                stats(outClass(i)) += 1UI

                If (i Mod 50000) = 0 AndAlso i > 0 Then
                    progress?.Report(New ProgressInfo($"{progressPrefix}: Finalisiere...({i:N0}/{nCells:N0})", 97))
                End If
            Next

            '=== DEBUG ===
            swFinalize.Stop()
            swTotal.Stop()

            Dim sb As New StringBuilder()
            sb.AppendLine("Copernicus LC100 Import Report")
            sb.AppendLine($"Source: {Path.GetFileName(opts.ClassTifPath)}")
            sb.AppendLine($"Target: {latCount}x{lonCount} @ {opts.TargetCellSizeDeg}°")
            sb.AppendLine($"Confidence: {includeConf}")
            sb.AppendLine()
            sb.AppendLine($"Missing (Class): {classMissing}")
            If includeConf Then sb.AppendLine($"Missing (Proba): {probaMissing}")
            sb.AppendLine()
            AppendGdalMetadata(sb, "Class-TIF Metadata:", dsClass, bandClass)
            sb.AppendLine()
            If includeConf AndAlso probaMetaDataDump IsNot Nothing Then
                sb.Append(probaMetaDataDump)
            End If
            sb.AppendLine()
            sb.AppendLine("Class histogramm (LC11 Code -> Cell Count):")
            For Each kvp In stats.OrderBy(Function(k) k.Key)
                sb.AppendLine($"  {kvp.Key}: {kvp.Value:N0}")
            Next
            sb.AppendLine()
            sb.AppendLine("NoData-Diagnose")
            sb.AppendLine($"  EmptyCells (kein Mapping-Treffer): {emptyCells:N0}")
            sb.AppendLine($"  NoData-dominant (NoData mit Treffern): {nodataDominant:N0}")
            sb.AppendLine()
            sb.AppendLine("Timing:")
            If swImport IsNot Nothing Then
                sb.AppendLine($"  Import: (Read + Mapping): {swImport.Elapsed.TotalSeconds:0.00}s")
            End If
            If swFinalize IsNot Nothing Then
                sb.AppendLine($"  Finalize (ArgMax + Confidence): {swFinalize.Elapsed.TotalSeconds:0.00}s")
            End If
            sb.AppendLine($"  Gesamt: {swTotal.Elapsed.TotalSeconds:0.00}s")
            sb.AppendLine("Source coverage (aus GeoTransform):")
            sb.AppendLine($"  Lon: {srcLonMin:0.00} .. {srcLonMax:0.00}")
            sb.AppendLine($"  Lat: {srcLatMax:0.00} .. {srcLatMin:0.00}")
            sb.AppendLine($"  PixelSize: dLon={gt(1)} dLat={gt(5)}")

            '=== END DEBUG ===

            progress?.Report(New ProgressInfo($"{progressPrefix}: Fertig.", 100))

            Return New ProcessResult With {
                .Classes = outClass,
                .Confidence = outConf,
                .Report = sb.ToString()
            }
        End Using

    End Function

    ''' <summary>
    ''' DEBUG
    ''' </summary>
    Private Shared Sub AppendGdalMetadata(sb As StringBuilder, title As String, ds As Dataset, band As Band)
        sb.AppendLine(title)

        'Dataset metadata
        Dim dsMd() As String = ds.GetMetadata("")
        If dsMd IsNot Nothing AndAlso dsMd.Length > 0 Then
            sb.AppendLine("  Dataset Metadata:")
            For Each kv In dsMd
                sb.AppendLine("    " & kv)
            Next
        Else
            sb.AppendLine("  Dataset Metadata: (leer)")
        End If

        'Band Metadata
        Dim bMd() As String = band.GetMetadata("")
        If bMd IsNot Nothing AndAlso bMd.Length > 0 Then
            sb.AppendLine("  Band Metadata:")
            For Each kv In bMd
                sb.AppendLine("    " & kv)
            Next
        Else
            sb.AppendLine("  Band Metadata: (leer)")
        End If

        'NoData aus GDAL-API (unabhängig von Metadata)
        Dim noData As Double = 0.0
        Dim hasNoData As Integer = 0
        band.GetNoDataValue(noData, hasNoData)
        If hasNoData <> 0 Then
            sb.AppendLine($"  Band NoDataValue (API): {noData}")
        Else
            sb.AppendLine("  Band NoDataValue (API): (nicht gesetzt)")
        End If

        sb.AppendLine()
    End Sub

End Class
