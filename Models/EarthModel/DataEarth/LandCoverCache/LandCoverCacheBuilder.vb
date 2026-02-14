
Imports System.IO
Imports System.Text
Imports System.Threading
Imports System.Diagnostics

Public NotInheritable Class LandCoverCacheBuilder

    Public Class BuildOptions

        Public Property SourceName As String
        Public Property EpochYear As Integer
        Public Property TargetCellSizeDeg As Double

        'Input
        Public Property RawCopernicusLc100ClassTifPath As String
        Public Property RawCopernicusLc100ProbaTifPath As String
        Public Property RawBedMachineGreenlandNcPath As String
        Public Property RawBedMachineAntarcticaNcPath As String
        Public Property RawRgiGlobalGlacierZipPath As String
        Public Property RawRgiRegionsZipPath As String
        Public Property RawGlaThiDaZipPath As String



        'EarthSurface-Reference
        Public Property EarthSurfaceReference As String
        Public Property EarthSurfaceCreateUTC As DateTime

        'Copernicus-Parameter
        Public Property NoDataClass As Byte = CByte(LandCoverClass.NoData)
        Public Property OpenWaterClass As Byte = CByte(LandCoverClass.OpenWater)
        Public Property LC11ClassCount As Integer = 12

    End Class


    Public Shared Function BuildAndSave(opts As BuildOptions,
                                        progress As IProgress(Of ProgressInfo),
                                        ct As CancellationToken) As String

        '----------------
        '0) Validierung
        '----------------

        ArgumentNullException.ThrowIfNullOrEmpty(NameOf(opts))
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.TargetCellSizeDeg, NameOf(opts))
        If String.IsNullOrWhiteSpace(opts.SourceName) Then Throw New ArgumentException("SourceName fehlt.", NameOf(opts))
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(opts.EpochYear, NameOf(opts))
        ArgumentOutOfRangeException.ThrowIfGreaterThan(opts.EpochYear, DateTime.UtcNow.Year + 1, NameOf(opts))
        If String.IsNullOrWhiteSpace(opts.RawCopernicusLc100ClassTifPath) OrElse Not File.Exists(opts.RawCopernicusLc100ClassTifPath) Then
            Throw New FileNotFoundException("Copernicus Class GeoTIFF wurde nicht gefunden.", opts.RawCopernicusLc100ClassTifPath)
        End If

        Dim includeConf As Boolean = Not String.IsNullOrWhiteSpace(opts.RawCopernicusLc100ProbaTifPath) AndAlso File.Exists(opts.RawCopernicusLc100ProbaTifPath)
        Dim includeBMGrn As Boolean = Not String.IsNullOrWhiteSpace(opts.RawBedMachineGreenlandNcPath) AndAlso File.Exists(opts.RawBedMachineGreenlandNcPath)
        Dim includeBMAnt As Boolean = Not String.IsNullOrWhiteSpace(opts.RawBedMachineAntarcticaNcPath) AndAlso File.Exists(opts.RawBedMachineAntarcticaNcPath)

        'Wenn Pfad angegeben aber Datei fehlt -> Fehler
        If Not String.IsNullOrWhiteSpace(opts.RawCopernicusLc100ProbaTifPath) AndAlso Not includeConf Then
            Throw New FileNotFoundException("Copernicus Proba GeoTIFF angegeben, aber Datei nicht gefunden.", opts.RawCopernicusLc100ProbaTifPath)
        End If
        If Not String.IsNullOrWhiteSpace(opts.RawBedMachineGreenlandNcPath) AndAlso Not includeBMGrn Then
            Throw New FileNotFoundException("BedMachine Greenland netCDF angegeben, aber Datei nicht gefunden.", opts.RawBedMachineGreenlandNcPath)
        End If
        If Not String.IsNullOrWhiteSpace(opts.RawBedMachineAntarcticaNcPath) AndAlso Not includeBMAnt Then
            Throw New FileNotFoundException("BedMachine Antarctica netCDF angegeben, aber Datei nicht gefunden.", opts.RawBedMachineAntarcticaNcPath)
        End If

        '------------------------
        '1) Zielraster ableiten
        '------------------------

        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))
        If latCount <= 0 OrElse lonCount <= 0 Then Throw New InvalidDataException("Ungültige Zielraster-Dimension")
        Dim nCells As Integer
        Try
            nCells = latCount * lonCount
        Catch ex As OverflowException
            Throw New InvalidDataException("Zielraster ist zu groß (Overflow).", ex)
        End Try

        ct.ThrowIfCancellationRequested()

        '--------------------
        '2) Progress planen
        '--------------------

        Dim steps As New List(Of (name As String, weight As Integer)) From {
            ("Copernicus", 60)
        }

        If includeBMAnt Then steps.Add(("BedMachineAntarctica", 25))
        If includeBMGrn Then steps.Add(("BedMachineGreenland", 10))

        Dim bmCount As Integer = 0
        If includeBMAnt Then bmCount += 1
        If includeBMGrn Then bmCount += 1

        If bmCount > 1 Then steps.Add(("Merge", 3))

        steps.Add(("BuildMeta", 1))
        steps.Add(("Save", 1))

        Dim totalW As Integer = steps.Sum(Function(s) s.weight)
        If totalW <= 0 Then totalW = 1

        Dim sliceMap As New Dictionary(Of String, (StartPct As Integer, EndPct As Integer))(StringComparer.OrdinalIgnoreCase)
        Dim cur As Integer = 0
        For i As Integer = 0 To steps.Count - 1
            Dim w As Integer = steps(i).weight
            Dim span As Integer = CInt(Math.Round((w / CDbl(totalW)) * 100))

            'Sicherstellen, dass am Ende genau 100 rauskommt
            Dim startPct As Integer = cur
            Dim endPct As Integer = If(
                                    i = steps.Count - 1,
                                    100,
                                    Math.Min(100, cur + Math.Max(1, span)))

            sliceMap(steps(i).name) = (startPct, endPct)
            cur = endPct
        Next

        '----------------------------
        '3) Timings & Report-Buffer
        '----------------------------

        Dim timings As New Dictionary(Of String, TimeSpan)(StringComparer.OrdinalIgnoreCase)
        Dim sb As New StringBuilder()
        Dim swTotal As Stopwatch = Stopwatch.StartNew()

        progress?.Report(New ProgressInfo("LandCoverCache: Initialisierung...", 0))
        ct.ThrowIfCancellationRequested()

        '--------------------------
        '4) Copernicus Processing
        '--------------------------

        Dim copResult As CopernicusLc100ProcessResult
        Dim sw As Stopwatch = Stopwatch.StartNew()
        Dim copSlice = sliceMap("Copernicus")
        Dim copProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, copSlice.StartPct, copSlice.EndPct, "LandCoverCache: ")

        copResult = CopernicusLc100Processor.Process(opts, copProg, ct, "COPERNICUS (LC100)")

        sw.Stop()
        timings("Copernicus") = sw.Elapsed

        ct.ThrowIfCancellationRequested()

        '--------------------------------------------
        '5) BedMachine optional: globales Ice-Array
        '--------------------------------------------

        Dim globalIce As Single() = Nothing
        If includeBMAnt OrElse includeBMGrn Then
            globalIce = New Single(nCells - 1) {}
            For i As Integer = 0 To globalIce.Length - 1
                globalIce(i) = Single.NaN
            Next
        End If

        '--------------------------
        '6) BedMachine Antarctica
        '--------------------------

        Dim bmAntRes As BedMachineProcessResult = Nothing
        If includeBMAnt Then
            ct.ThrowIfCancellationRequested()

            sw = Stopwatch.StartNew()
            Dim s = sliceMap("BedMachineAntarctica")
            Dim bmProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, s.StartPct, s.EndPct, "LandCoverCache: ")

            bmAntRes = BedMachineAntarcticaProcessor.Process(opts, bmProg, ct, "BedMachine Antarctica")

            sw.Stop()
            timings("BedMachineAntarctica") = sw.Elapsed
        End If

        '-------------------------
        '7) BedMachine Greenland
        '-------------------------

        Dim bmGrnRes As BedMachineProcessResult = Nothing
        If includeBMGrn Then
            ct.ThrowIfCancellationRequested()

            sw = Stopwatch.StartNew()
            Dim s = sliceMap("BedMachineGreenland")
            Dim bmProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, s.StartPct, s.EndPct, "LandCoverCache: ")

            bmGrnRes = BedMachineGreenlandProcessor.Process(opts, bmProg, ct, "BedMachine Greenland")

            sw.Stop()
            timings("BedMachineGreenland") = sw.Elapsed
        End If

        '-----------------------------------------------
        '8) Merge/Direktübergabe der BedMachine-Arrays
        '-----------------------------------------------

        If globalIce IsNot Nothing Then

            sw = Stopwatch.StartNew()

            If bmCount = 1 Then         'Nur ein BM-Processor aktiv -> direkt übernehmen (kein Merge nötig)
                Dim src As Single() = If(includeBMAnt, bmAntRes?.IceThicknessM, bmGrnRes?.IceThicknessM)

                If src Is Nothing OrElse src.Length <> nCells Then
                    Throw New InvalidDataException("BedMachine-Prozessor hat kein gültiges globales Ice-Array geliefert.")
                End If

                'Kopieren
                Array.Copy(src, globalIce, nCells)

            ElseIf bmCount > 1 Then     'Mehr als ein BM-Processor aktiv -> Max-Aggregation, aber nur wenn src nicht NaN ist

                Dim mergeSlice = sliceMap("Merge")
                Dim mergeProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, mergeSlice.StartPct, mergeSlice.EndPct, "BedMachine aggregieren: ")

                Dim SubMerge = Sub(src As Single(), name As String)

                                   If src Is Nothing Then Throw New InvalidDataException($"BedMachine '{name}' lieferte kein Ice-Array.")

                                   If src.Length <> nCells Then Throw New InvalidDataException($"BedMachine '{name}' Ice-Array hat ungültige Länge ({src.Length:N0} - erwartet: {nCells}).")

                                   Const mergeThrottle As Integer = 1000

                                   For i As Integer = 0 To nCells - 1
                                       ct.ThrowIfCancellationRequested()

                                       Dim v As Single = src(i)
                                       If Single.IsNaN(v) Then Continue For

                                       Dim curV As Single = globalIce(i)
                                       If Single.IsNaN(curV) OrElse v > curV Then
                                           globalIce(i) = v
                                       End If

                                       If (i Mod mergeThrottle) = 0 AndAlso i > 0 Then
                                           Dim pct As Integer = CInt((i / Math.Max(1.0, nCells - 1)) * 100)
                                           mergeProg.Report(New ProgressInfo($"Zellen gemerged: {i:N0}/{nCells:N0}", pct))
                                       End If
                                   Next

                               End Sub

                If includeBMAnt Then SubMerge(bmAntRes.IceThicknessM, "Antarctica")
                If includeBMGrn Then SubMerge(bmGrnRes.IceThicknessM, "Greenland")

                mergeProg.Report(New ProgressInfo("Merge fertig.", 100))

            End If

            sw.Stop()
            timings("Merge") = sw.Elapsed

        End If

        ct.ThrowIfCancellationRequested()

        '-------------------------------------------------------
        '8.1) SnowIce-Override auf Basis von LandIceThicknessM
        '-------------------------------------------------------
        Dim overridden As Integer = 0
        Dim skippedWater As Integer = 0

        If globalIce IsNot Nothing Then

            sw = Stopwatch.StartNew()

            Dim snowIceCls As Byte = CByte(LandCoverClass.SnowIce)
            Dim openWaterCls As Byte = CByte(LandCoverClass.OpenWater)

            'Tuning-Parameter
            Const iceEps As Single = 0.5F                   'unter 0.5m ignorieren (nummerisches Rauschen)
            Const waterOverrideMin As Single = 50.0F        'Wenn Copernicus Wasser sagt, erst ab 50m überschreiben

            Dim lc As Byte() = copResult.Classes
            If lc Is Nothing OrElse lc.Length <> nCells Then
                Throw New InvalidDataException("LandCoverClass-Array ungültig oder falsche Länge.")
            End If


            For i As Integer = 0 To nCells - 1
                ct.ThrowIfCancellationRequested()

                Dim ice As Single = globalIce(i)
                If Single.IsNaN(ice) OrElse ice <= iceEps Then Continue For

                Dim curCls As Byte = lc(i)

                'Guard: Küsten/Wasser nur bei massivem Eis überschreiben
                If curCls = openWaterCls AndAlso ice < waterOverrideMin Then
                    skippedWater += 1
                    Continue For
                End If

                If curCls <> snowIceCls Then
                    lc(i) = snowIceCls
                    overridden += 1
                End If
            Next

            'zurück ins Copernicus-Array kopieren
            Array.Copy(lc, copResult.Classes, nCells)

            sw.Stop()
            timings("SnowIceOverride") = sw.Elapsed
        End If

        '-----------------------
        '9) Meta & Cache bauen
        '-----------------------

        sw = Stopwatch.StartNew()
        Dim metaSlice = sliceMap("BuildMeta")
        Dim metaProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, metaSlice.StartPct, metaSlice.EndPct, "Cache generieren: ")

        metaProg.Report(New ProgressInfo("Erzeuge Cache & Meta", 0))

        Dim meta As New LandCoverCacheMeta With {
            .CacheVersion = LandCoverCacheFormat.CurrentVersion,
            .Source = opts.SourceName,
            .EpochYear = opts.EpochYear,
            .CellSizeDeg = opts.TargetCellSizeDeg,
            .LatCount = latCount,
            .LonCount = lonCount,
            .HasConfidence = includeConf,
            .HasLandIceThickness = (globalIce IsNot Nothing),
            .EarthSurfaceRef = opts.EarthSurfaceReference,
            .EarthSurfaceCreateUtc = opts.EarthSurfaceCreateUTC,
            .RawCopernicusLC100ClassFile = opts.RawCopernicusLc100ClassTifPath,
            .RawCopernicusLC100ProbaFile = If(includeConf, opts.RawCopernicusLc100ProbaTifPath, Nothing),
            .RawBedMachineGreenlandFile = If(includeBMGrn, opts.RawBedMachineGreenlandNcPath, Nothing),
            .RawBedMachineAntarcticaFile = If(includeBMAnt, opts.RawBedMachineAntarcticaNcPath, Nothing),
            .ImportNotes = "Copernicus LC100 Discrete-classification" &
                            If(globalIce IsNot Nothing, " + BedMachine LandIceThickness (max-aggregation)", ""),
            .CreateUtc = DateTime.UtcNow
        }

        Dim cache As New LandCoverCache With {
            .Meta = meta,
            .LandCoverClass = copResult.Classes,
            .Confidence = If(includeConf, copResult.Confidence, Nothing),
            .LandIceThicknessM = globalIce
        }

        metaProg.Report(New ProgressInfo("Cache & Meta fertig.", 100))
        sw.Stop()
        timings("BuildMeta") = sw.Elapsed

        ct.ThrowIfCancellationRequested()

        '----------
        '10) Save
        '----------

        sw = Stopwatch.StartNew()
        Dim saveSlice = sliceMap("Save")
        Dim saveProg As IProgress(Of ProgressInfo) = New ProgressSlice(progress, saveSlice.StartPct, saveSlice.EndPct, "Speichern: ")

        saveProg.Report(New ProgressInfo("Speichere Cache...", 0))
        Dim paths As (binPath As String, metaPath As String) = LandCoverCacheStore.GetCachePaths(meta.Source, meta.EpochYear, meta.CellSizeDeg)

        LandCoverCacheStore.SaveCacheToFiles(paths.binPath, paths.metaPath, cache, saveProg, ct)
        saveProg.Report(New ProgressInfo("Speichern fertig.", 100))

        sw.Stop()
        timings("Save") = sw.Elapsed

        swTotal.Stop()

        '------------
        '11) Report
        '------------

        sb.AppendLine("==== LandCover Cache Build Report =====")
        sb.AppendLine($"Source: {meta.Source}")
        sb.AppendLine($"Epoche: {meta.EpochYear}")
        sb.AppendLine($"CellSize: {meta.CellSizeDeg}°")
        sb.AppendLine($"Grid-Size: {meta.LatCount} x {meta.LonCount}")
        sb.AppendLine()
        sb.AppendLine("--- Input ---")
        sb.AppendLine($"Copernicus Class (GeoTIFF): {opts.RawCopernicusLc100ClassTifPath}")
        sb.AppendLine($"Copernicus Proba (GeoTIFF): {(If(includeConf, opts.RawCopernicusLc100ProbaTifPath, "(none)"))}")
        sb.AppendLine($"BedMachine Greenland (netCDF): {(If(includeBMGrn, opts.RawBedMachineGreenlandNcPath, "(none)"))}")
        sb.AppendLine($"BedMachine Antarctica (netCDF): {(If(includeBMAnt, opts.RawBedMachineAntarcticaNcPath, "(none)"))}")
        sb.AppendLine()
        sb.AppendLine("--- Output ---")
        sb.AppendLine($"Cache: {paths.binPath}")
        sb.AppendLine($"Meta: {paths.metaPath}")
        sb.AppendLine()

        sb.AppendLine("--- Processor Report: Copernicus ---")
        sb.AppendLine(copResult.Report)

        If includeBMAnt AndAlso bmAntRes IsNot Nothing Then
            sb.AppendLine()
            sb.AppendLine("--- Processor Report: BedMachine Antarctica ---")
            sb.AppendLine(bmAntRes.Report)
        End If

        If includeBMGrn AndAlso bmGrnRes IsNot Nothing Then
            sb.AppendLine()
            sb.AppendLine("--- Processor Report: BedMachine Greenland ---")
            sb.AppendLine(bmGrnRes.Report)
        End If

        If globalIce IsNot Nothing Then
            sb.AppendLine()
            sb.AppendLine("--- SnowIce Override ---")
            sb.AppendLine($"Zellen überschrieben: {overridden:N0}")
            sb.AppendLine($"Wasser-Zellen übersprungen (Guard): {skippedWater:N0}")
        End If

        sb.AppendLine()
        sb.AppendLine("==== Timing ====")
        For Each kv In timings
            sb.AppendLine($"  {kv.Key,-22}: {kv.Value.TotalSeconds:0.00}s")
        Next
        sb.AppendLine($"  {"Total",-22}: {swTotal.Elapsed.TotalSeconds:0.00}s")

        progress?.Report(New ProgressInfo("Fertig", 100))

        Return sb.ToString()

    End Function

End Class
