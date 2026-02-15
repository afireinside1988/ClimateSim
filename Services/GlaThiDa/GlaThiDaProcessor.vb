Imports System.Globalization
Imports System.IO
Imports System.IO.Compression
Imports System.Text
Imports System.Threading
Imports Microsoft.VisualBasic.FileIO

Public NotInheritable Class GlaThiDaProcessor

    Private Const ProgressThrottleRowInterval As Integer = 128

    Public Shared Function Process(opts As LandCoverCacheBuilder.BuildOptions,
                                   progress As IProgress(Of ProgressInfo),
                                   ct As CancellationToken,
                                   Optional regionMask As RgiRegionMask = Nothing,
                                   Optional progressPrefix As String = "GlaThiDa") As GlaThiDaProcessResult

        ArgumentNullException.ThrowIfNull(opts, NameOf(opts))

        Dim zipPath As String = opts.RawGlaThiDaZipPath
        ArgumentNullException.ThrowIfNullOrWhiteSpace(zipPath, NameOf(zipPath))
        If Not File.Exists(zipPath) Then Throw New FileNotFoundException("GlaThiDa-Zip nicht gefunden.", zipPath)

        progress?.Report(New ProgressInfo($"{progressPrefix}: Starte GlaThiDa-Verarbeitung...", 0))
        ct.ThrowIfCancellationRequested()

        Dim entryName As String = Nothing
        Dim csvBytes As Byte() = Nothing

        '1) ZIP öffnen, T.csv finden und in RAM laden
        Using fs As FileStream = File.OpenRead(zipPath)

            Using za As New ZipArchive(fs, ZipArchiveMode.Read, leaveOpen:=False)

                For Each e In za.Entries
                    If e.FullName.EndsWith("data/T.csv", StringComparison.OrdinalIgnoreCase) OrElse
                            e.FullName.EndsWith("/data/T.csv", StringComparison.OrdinalIgnoreCase) OrElse
                            e.FullName.EndsWith("\data\T.csv", StringComparison.OrdinalIgnoreCase) Then
                        entryName = e.FullName
                        Exit For
                    End If
                Next


                If String.IsNullOrWhiteSpace(entryName) Then Throw New InvalidDataException("In der GlaThiDa-Zip wurde keine T.csv gefunden.")

                Dim entry As ZipArchiveEntry = za.GetEntry(entryName)
                If entry Is Nothing Then Throw New InvalidDataException($"ZIP-Entry wurde nicht gefunden: {entryName}")

                progress?.Report(New ProgressInfo($"{progressPrefix}: Lade T.csv in Speicher...", 1))
                ct.ThrowIfCancellationRequested()

                Using es As Stream = entry.Open()
                    Using ms As New MemoryStream(CInt(Math.Max(0, entry.Length)))
                        es.CopyTo(ms)
                        csvBytes = ms.ToArray()
                    End Using
                End Using
            End Using

        End Using

        If csvBytes Is Nothing OrElse csvBytes.Length = 0 Then Throw New InvalidDataException("T.csv konnte nicht gelesen werden oder ist leer.")

        Dim inv As CultureInfo = CultureInfo.InvariantCulture

        '2) Zeilen zählen
        Dim totalRows As Integer = CountCsvRowsFromMemory(csvBytes, ct)
        If totalRows <= 0 Then totalRows = 1

        progress?.Report(New ProgressInfo($"{progressPrefix}: T.csv geladen, {totalRows} Datenzeilen gefunden. CSV wird geparsed...", 2))
        ct.ThrowIfCancellationRequested()

        '3) Sammel-Container pro Region (Log-Log Fit)
        Dim xsByRegion As New Dictionary(Of RGIRegion, List(Of Double))()
        Dim ysByRegion As New Dictionary(Of RGIRegion, List(Of Double))()

        For Each r As RGIRegion In [Enum].GetValues(Of RGIRegion)()
            xsByRegion(r) = New List(Of Double)(128)
            ysByRegion(r) = New List(Of Double)(128)
        Next

        'Stats
        Dim readRows As Integer = 0
        Dim usedRowsGlobal As Integer = 0
        Dim skippedMissing As Integer = 0
        Dim skippedInvalid As Integer = 0

        '4) CSV parsen
        Using ms As New MemoryStream(csvBytes, writable:=False)

            Using parser As New TextFieldParser(ms, Encoding.UTF8, detectEncoding:=True)

                'Parser-Einstellungen
                parser.TextFieldType = FieldType.Delimited
                parser.SetDelimiters(",")
                parser.HasFieldsEnclosedInQuotes = True
                parser.TrimWhiteSpace = True

                If parser.EndOfData Then Throw New InvalidDataException("T.csv ist leer.")

                Dim header As String() = parser.ReadFields()
                If header Is Nothing OrElse header.Length = 0 Then Throw New InvalidDataException("CSV-Header konnte nicht gelesen werden.")


                'Spalten finden und indexieren
                Dim idxLat As Integer = CSVHelpers.FindHeaderIndex(header, "LAT")
                Dim idxLon As Integer = CSVHelpers.FindHeaderIndex(header, "LON")
                Dim idxArea As Integer = CSVHelpers.FindHeaderIndex(header, "AREA")
                Dim idxMeanThickness As Integer = CSVHelpers.FindHeaderIndex(header, "MEAN_THICKNESS")

                If idxLat < 0 Then Throw New InvalidDataException("CSV enthält nicht die benötigte LAT-Spalte.")
                If idxLon < 0 Then Throw New InvalidDataException("CSV enthält nicht die benötigte LON-Spalte.")
                If idxArea < 0 Then Throw New InvalidDataException("CSV enthält nicht die benötigte AREA-Spalte.")
                If idxMeanThickness < 0 Then Throw New InvalidDataException("CSV enthält nicht die benötigte MEAN_THICKNESS-Spalte.")

                While Not parser.EndOfData

                    ct.ThrowIfCancellationRequested()

                    Dim fields As String() = parser.ReadFields()
                    If fields Is Nothing Then Exit While

                    readRows += 1
                    If readRows Mod ProgressThrottleRowInterval = 0 Then
                        Dim pct As Integer = 2 + CInt((readRows / CDbl(totalRows)) * 96.0)
                        pct = Math.Min(98, pct)
                        progress?.Report(New ProgressInfo($"{progressPrefix}: Parse CSV...{Environment.NewLine}{Environment.NewLine}Zeile: {readRows}/{totalRows}", pct))
                    End If

                    Dim sLat As String = GetFieldSafe(fields, idxLat)
                    Dim sLon As String = GetFieldSafe(fields, idxLon)
                    Dim sArea As String = GetFieldSafe(fields, idxArea)
                    Dim sMeanThickness As String = GetFieldSafe(fields, idxMeanThickness)

                    If String.IsNullOrWhiteSpace(sLat) OrElse
                            String.IsNullOrWhiteSpace(sLon) OrElse
                            String.IsNullOrWhiteSpace(sArea) OrElse
                            String.IsNullOrWhiteSpace(sMeanThickness) Then
                        skippedMissing += 1
                        Continue While
                    End If

                    Dim lat As Double
                    Dim lon As Double
                    Dim areaKm2 As Double
                    Dim meanThicknessM As Double

                    If Not Double.TryParse(sLat, NumberStyles.Float, inv, lat) Then skippedInvalid += 1 : Continue While
                    If Not Double.TryParse(sLon, NumberStyles.Float, inv, lon) Then skippedInvalid += 1 : Continue While
                    If Not Double.TryParse(sArea, NumberStyles.Float, inv, areaKm2) Then skippedInvalid += 1 : Continue While
                    If Not Double.TryParse(sMeanThickness, NumberStyles.Float, inv, meanThicknessM) Then skippedInvalid += 1 : Continue While

                    If areaKm2 <= 0 OrElse meanThicknessM <= 0 Then skippedInvalid += 1 : Continue While

                    'Volumen (km³): V = A(km²) * H(m) / 1000
                    Dim volumeKm3 As Double = (areaKm2 * meanThicknessM) / 1000.0
                    If volumeKm3 <= 0 Then skippedInvalid += 1 : Continue While

                    Dim x As Double = Math.Log(areaKm2)
                    Dim y As Double = Math.Log(volumeKm3)

                    'Global immer
                    xsByRegion(RGIRegion.Global_Region).Add(x)
                    ysByRegion(RGIRegion.Global_Region).Add(y)
                    usedRowsGlobal += 1

                    'Regional via RegionMask (wenn nicht global)
                    If regionMask IsNot Nothing Then

                        Dim region As RGIRegion = regionMask.GetRegion(lat, lon)
                        If region <> RGIRegion.Global_Region Then
                            xsByRegion(region).Add(x)
                            ysByRegion(region).Add(y)
                        End If

                    End If


                End While
            End Using

        End Using

        progress?.Report(New ProgressInfo($"{progressPrefix}: Fits berechnen...", 99))
        ct.ThrowIfCancellationRequested()

        Dim fits As New Dictionary(Of RGIRegion, GlacierVolumeAreaFit)()

        fits(RGIRegion.Global_Region) = FitPowerLaw(xsByRegion(RGIRegion.Global_Region), ysByRegion(RGIRegion.Global_Region))


        If regionMask IsNot Nothing Then

            For Each r As RGIRegion In [Enum].GetValues(Of RGIRegion)

                If r = RGIRegion.Global_Region Then Continue For
                If xsByRegion(r).Count >= 2 Then
                    fits(r) = FitPowerLaw(xsByRegion(r), ysByRegion(r))
                End If

            Next

        End If

        '6) Report
        Dim sb As New StringBuilder()
        sb.AppendLine($"==== {progressPrefix} Process Report =====")
        sb.AppendLine($"Source ZIP:   {zipPath}")
        sb.AppendLine($"T.csv Entry:  {entryName}")
        sb.AppendLine($"Bytes loaded: {csvBytes.Length}")
        sb.AppendLine($"Rows (data):  {totalRows}")
        sb.AppendLine($"Parsed:       {readRows}")
        sb.AppendLine($"Used global:  {usedRowsGlobal}")
        sb.AppendLine($"Skipped missing: {skippedMissing}")
        sb.AppendLine($"Skipped invalid: {skippedInvalid}")
        sb.AppendLine()
        sb.AppendLine("Fits (V = C * A^Gamma), A in km², V in km³")

        Dim ordered As List(Of RGIRegion) = fits.Keys.OrderBy(Function(k) CInt(k)).ToList()
        For Each r In ordered
            Dim f As GlacierVolumeAreaFit = fits(r)
            sb.AppendLine($"{CInt(r):00} {r}: N={f.N}, C={f.C.ToString("0.########", inv)}, Gamma={f.Gamma.ToString("0.####", inv)}")
        Next

        progress?.Report(New ProgressInfo($"{progressPrefix}: Fertig.", 100))

        Return New GlaThiDaProcessResult With {
            .FitsByRegion = fits,
            .Report = sb.ToString()
        }

    End Function


    Private Shared Function FitPowerLaw(xs As List(Of Double), ys As List(Of Double)) As GlacierVolumeAreaFit

        Dim n As Integer = xs.Count
        If n < 2 Then Return New GlacierVolumeAreaFit With {.C = Double.NaN, .Gamma = Double.NaN, .N = n}

        Dim sumX As Double = 0
        Dim sumY As Double = 0

        For i As Integer = 0 To n - 1
            sumX += xs(i)
            sumY += ys(i)
        Next

        Dim meanX As Double = sumX / n
        Dim meanY As Double = sumY / n

        Dim sxx As Double = 0
        Dim sxy As Double = 0

        For i As Integer = 0 To n - 1
            Dim dx As Double = xs(i) - meanX
            sxx += dx * dx
            sxy += dx * (ys(i) - meanY)
        Next

        If sxx <= 0 Then Return New GlacierVolumeAreaFit With {.C = Double.NaN, .Gamma = Double.NaN, .N = n}

        Dim gamma As Double = sxy / sxx
        Dim a As Double = meanY - gamma * meanX
        Dim c As Double = Math.Exp(a)

        Return New GlacierVolumeAreaFit With {.C = c, .Gamma = gamma, .N = n}

    End Function
End Class
