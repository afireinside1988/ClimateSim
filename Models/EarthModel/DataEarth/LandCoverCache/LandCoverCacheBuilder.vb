
Imports System.IO
Imports System.Text
Imports System.Threading

Public NotInheritable Class LandCoverCacheBuilder

    Public Class BuildOptions

        Public Property SourceName As String
        Public Property EpochYear As Integer
        Public Property ClassTifPath As String
        Public Property ProbaTifPath As String
        Public Property TargetCellSizeDeg As Double

        'Rastervertrag
        Public Property LonMinCenter As Double = -179.5
        Public Property LatMaxCenter As Double = 89.5  'N->S

        Public Property IncludeConfidence As Boolean
        Public Property NoDataClass As Byte = CByte(LandCoverClass.NoData)
        Public Property OpenWaterClass As Byte = CByte(LandCoverClass.OpenWater)

        Public Property LC11ClassCount As Integer = 12
    End Class


    Public Shared Function BuildAndSave(opts As BuildOptions,
                                        progress As IProgress(Of ProgressInfo),
                                        ct As CancellationToken) As String

        '0) Validierung
        ArgumentNullException.ThrowIfNull(opts)

        If String.IsNullOrWhiteSpace(opts.ClassTifPath) OrElse Not File.Exists(opts.ClassTifPath) Then
            Throw New FileNotFoundException("Class GeoTIFF wurde nicht gefunden.", opts.ClassTifPath)
        End If

        If opts.TargetCellSizeDeg <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(opts))
        If String.IsNullOrWhiteSpace(opts.SourceName) Then Throw New ArgumentException("SourceName fehlt.")
        If opts.EpochYear <= 0 Then Throw New ArgumentOutOfRangeException(NameOf(opts))


        Dim includeConf As Boolean =
            opts.IncludeConfidence AndAlso
            Not String.IsNullOrWhiteSpace(opts.ProbaTifPath) AndAlso File.Exists(opts.ProbaTifPath)

        '1) Zielraster ableiten
        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))

        ct.ThrowIfCancellationRequested()

        '2) Processing

        progress?.Report(New ProgressInfo("LandCover: Starte Verarbeitung...", 0))

        'DEBUG: Später hier entfernen und bei App-Start aufrufen
        CopernicusLc100Processor.InitGdal()

        Dim result As CopernicusLc100Processor.ProcessResult =
            CopernicusLc100Processor.ProcessCopernicusLc100(opts, progress, ct, "COPERNICUS (LC100)")

        ct.ThrowIfCancellationRequested()

        '3) Meta & Cache bauen
        progress?.Report(New ProgressInfo("LandCover: Meta & Cache...", 98))

        Dim meta As New LandCoverCacheMeta With {
            .CacheVersion = LandCoverCacheFormat.CurrentVersion,
            .Source = opts.SourceName,
            .EpochYear = opts.EpochYear,
            .CellSizeDeg = opts.TargetCellSizeDeg,
            .LatCount = latCount,
            .LonCount = lonCount,
            .HasConfidence = includeConf,
            .RawClassFile = opts.ClassTifPath,
            .RawProbaFile = If(includeConf, opts.ProbaTifPath, Nothing),
            .ImportNotes = "Copernicus LC100 Discrete-classification",
            .CreateUtc = DateTime.UtcNow
        }

        Dim cache As New LandCoverCache With {
            .Meta = meta,
            .LandCoverClass = result.Classes,
            .Confidence = If(includeConf, result.Confidence, Nothing),
            .LandIceThicknessM = Nothing                                    'NOCH NICHT IMPLEMENTIERT
        }

        '4) Save (bin & meta)
        progress?.Report(New ProgressInfo("LandCover: Speichere Cache...", 99))
        Dim paths As (binPath As String, metaPath As String) = LandCoverCacheStore.GetCachePaths(meta.Source, meta.EpochYear, meta.CellSizeDeg)
        LandCoverCacheStore.SaveCacheToFiles(paths.binPath, paths.metaPath, cache, progress, ct)

        '5) Report
        Dim sb As New StringBuilder()
        sb.AppendLine("LandCover Cache Build")
        sb.AppendLine($"Source: {meta.Source}  Epoch: {meta.EpochYear}")
        sb.AppendLine($"CellSize: {meta.CellSizeDeg}°  LatCount={meta.LatCount}  LonCount={meta.LonCount}")
        sb.AppendLine($"ClassTIF: {opts.ClassTifPath}")
        sb.AppendLine($"ProbaTIF: {(If(includeConf, opts.ProbaTifPath, "(none)"))}")
        sb.AppendLine($"Cache: {paths.binPath}")
        sb.AppendLine($"Meta : {paths.metaPath}")
        sb.AppendLine()
        sb.AppendLine("---- Processor Report ----")
        sb.AppendLine(result.Report)

        Return sb.ToString()
    End Function
End Class
