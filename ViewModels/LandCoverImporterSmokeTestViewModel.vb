Imports Microsoft.Win32
Imports System.IO
Imports System.Threading
Imports System.Threading.Tasks

Public Class LandCoverImporterSmokeTestVM
    Inherits ViewModelBase

    Private _classTifPath As String
    Public Property ClassTifPath As String
        Get
            Return _classTifPath
        End Get
        Set(value As String)
            _classTifPath = value
            OnPropertyChanged()
            OnPropertyChanged(NameOf(CanRunImport))
        End Set
    End Property

    Private _probaTifPath As String
    Public Property ProbaTifPath As String
        Get
            Return _probaTifPath
        End Get
        Set(value As String)
            _probaTifPath = value
            OnPropertyChanged()
        End Set
    End Property

    Private _reportText As String
    Public Property ReportText As String
        Get
            Return _reportText
        End Get
        Set(value As String)
            _reportText = value
            OnPropertyChanged()
        End Set
    End Property

    Public ReadOnly Property CanRunImport As Boolean
        Get
            Return Not String.IsNullOrWhiteSpace(ClassTifPath) AndAlso File.Exists(ClassTifPath)
        End Get
    End Property

    Public ReadOnly Property BrowseClassCommand As ICommand
    Public ReadOnly Property BrowseProbaCommand As ICommand
    Public ReadOnly Property RunImportCommand As ICommand

    Private ReadOnly _ownerWindow As Window

    Public Sub New(owner As Window)
        _ownerWindow = owner

        BrowseClassCommand = New RelayCommand(Of Object)(Sub(o) BrowseClass())
        BrowseProbaCommand = New RelayCommand(Of Object)(Sub(o) BrowseProba())
        RunImportCommand = New AsyncRelayCommand(Of Object)(Async Function(o)
                                                                Await GenerateCacheAsync()
                                                            End Function, Function(o) CanRunImport AndAlso Not IsBusy)
    End Sub

    Private Sub BrowseClass()
        Dim ofd As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|Alle Dateien (*.*)|*.*",
            .Title = "Copernicus Class-map GeoTIFF auswählen"
        }
        If ofd.ShowDialog(_ownerWindow) = True Then
            ClassTifPath = ofd.FileName
        End If
    End Sub

    Private Sub BrowseProba()
        Dim ofd As New OpenFileDialog With {
            .Filter = "GeoTIFF (*.tif;*.tiff)|*.tif;*.tiff|Alle Dateien (*.*)|*.*",
            .Title = "Copernicus Proba GeoTIFF auswählen (optional)"
        }
        If ofd.ShowDialog(_ownerWindow) = True Then
            ProbaTifPath = ofd.FileName
        End If
    End Sub

    Private Async Function RunImportAsync() As Task
        Try
            ReportText = ""

            ' GDAL init (einmalig; hier im SmokeTest ok)
            CopernicusLc100Processor.InitGdal()

            Dim resultTuple =
                Await BusyRunner.RunAsync(Of Tuple(Of CopernicusLc100Processor.ProcessResult, String))(
                    Me,
                    "LandCover: Cache SmokeTest",
                    Function(progress, ct)

                        ct.ThrowIfCancellationRequested()

                        Dim opts As New LandCoverCacheBuilder.BuildOptions With {
                            .ClassTifPath = ClassTifPath,
                            .ProbaTifPath = ProbaTifPath,
                            .TargetCellSizeDeg = 0.0625,
                            .IncludeConfidence = True
                        }

                        progress?.Report(New ProgressInfo("Starte Import...", 0))

                        Dim r = CopernicusLc100Processor.ProcessCopernicusLc100(
                            opts:=opts,
                            progress:=progress,
                            ct:=ct,
                            progressPrefix:="COPERNICUS (LC100)")

                        ct.ThrowIfCancellationRequested()

                        ' Minimaler Smoke-Check
                        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
                        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))

                        Dim nExpected As Integer = latCount * lonCount
                        If r.Classes Is Nothing OrElse r.Classes.Length <> nExpected Then
                            Throw New InvalidDataException($"Import-Result hat falsche Array-Länge (Classes). Erwartet {nExpected}.")
                        End If
                        If r.Confidence IsNot Nothing AndAlso r.Confidence.Length <> nExpected Then
                            Throw New InvalidDataException($"Import-Result hat falsche Array-Länge (Confidence). Erwartet {nExpected}.")
                        End If

                        Dim report As String = r.Report
                        progress?.Report(New ProgressInfo("Fertig.", 100))

                        Return Tuple.Create(r, report)
                    End Function,
                    canCancel:=True,
                    showOverlay:=True)

            ReportText = resultTuple.Item2

        Catch ex As OperationCanceledException
            ReportText = "Abgebrochen."
        Catch ex As Exception
            ReportText = "Fehler: " & Environment.NewLine & FlattenException(ex, showStackTrace:=False)
        End Try
    End Function

    Private Async Function GenerateCacheAsync() As Task
        Try
            ReportText = ""

            ' GDAL init (einmalig; hier im SmokeTest ok)
            CopernicusLc100Processor.InitGdal()

            Dim report =
                Await BusyRunner.RunAsync(Of String)(
                    Me,
                    "LandCover: Cache SmokeTest",
                    Function(progress, ct)

                        ct.ThrowIfCancellationRequested()

                        Dim opts As New LandCoverCacheBuilder.BuildOptions With {
                            .SourceName = "COPERNICUS_LC100_v3.0.1",
                            .EpochYear = 2019,
                            .ClassTifPath = ClassTifPath,
                            .ProbaTifPath = ProbaTifPath,
                            .TargetCellSizeDeg = 0.0625,
                            .IncludeConfidence = True
                        }

                        progress?.Report(New ProgressInfo("Starte Cache-Build...", 0))

                        Dim buildReport As String =
                            LandCoverCacheBuilder.BuildAndSave(opts, progress, ct)

                        ct.ThrowIfCancellationRequested()

                        'Optional: Smoke-Check: cache direkt wieder öffnen und Dimensionen prüfen
                        Dim paths = LandCoverCacheStore.GetCachePaths(opts.SourceName, opts.EpochYear, opts.TargetCellSizeDeg)

                        Dim cache As LandCoverCache = Nothing
                        Dim kind As CacheOpenErrorKind = CacheOpenErrorKind.None
                        Dim msg As String = Nothing

                        If Not LandCoverCacheStore.TryOpenCacheFromFiles(paths.metaPath, cache, kind, msg, progress, ct) Then
                            Throw New InvalidDataException($"Cache wurde gespeichert, kann aber nicht wieder geöffnet werden: {kind} - {msg}")
                        End If

                        'Minimal: Dimensionscheck
                        Dim latCount As Integer = CInt(Math.Round(180.0 / opts.TargetCellSizeDeg))
                        Dim lonCount As Integer = CInt(Math.Round(360.0 / opts.TargetCellSizeDeg))
                        Dim nExpected As Integer = latCount * lonCount

                        If cache Is Nothing OrElse cache.Meta Is Nothing Then
                            Throw New InvalidDataException("Cache-Reload: cache/meta ist Nothing.")
                        End If

                        If cache.Meta.LatCount <> latCount OrElse cache.Meta.LonCount <> lonCount Then
                            Throw New InvalidDataException("Cache-Reload: Meta-Dimensionen stimmen nicht.")
                        End If

                        If cache.LandCoverClass Is Nothing OrElse cache.LandCoverClass.Length <> nExpected Then
                            Throw New InvalidDataException($"Cache-Reload: LandCoverClass Länge falsch. Erwartet {nExpected}.")
                        End If

                        If cache.Meta.HasConfidence Then
                            If cache.Confidence Is Nothing OrElse cache.Confidence.Length <> nExpected Then
                                Throw New InvalidDataException($"Cache-Reload: Confidence Länge falsch. Erwartet {nExpected}.")
                            End If
                        End If

                        progress?.Report(New ProgressInfo("Fertig.", 100))
                        Return buildReport
                    End Function,
                    canCancel:=True,
                    showOverlay:=True)

            ReportText = report

        Catch ex As OperationCanceledException
            ReportText = "Abgebrochen."
        Catch ex As Exception
            ReportText = "Fehler:" & Environment.NewLine & FlattenException(ex, showStackTrace:=False)
        End Try
    End Function

End Class
