Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Xml

Public Class LandCoverCacheStore

    Public Shared ReadOnly Property CacheDir As String = EarthSurfacePaths.CacheDirectory

    ''' <summary>
    ''' Erzeugt den standardisierten Cache-Dateinamen (ohne Extension)
    ''' Beispiel: "COPERNICUS_LC100_2019_1.0deg"
    ''' </summary>
    Public Shared Function BuildCacheBaseName(source As String, epochYear As Integer, cellSizeDeg As Double) As String
        Dim cs As String = CellSizeFileTokenFromDeg(cellSizeDeg)
        Return $"{source}_{epochYear}_{cs}deg".Replace(" ", "")
    End Function

    Public Shared Function GetCachePaths(source As String, epochYear As Integer, cellSizeDeg As Double) As (binPath As String, metaPath As String)
        Directory.CreateDirectory(CacheDir)

        Dim baseName As String = BuildCacheBaseName(source, epochYear, cellSizeDeg)
        Dim binPath As String = Path.Combine(CacheDir, baseName & ".lccf")
        Dim metaPath As String = Path.Combine(CacheDir, baseName & ".meta.json")
        Return (binPath, metaPath)
    End Function

    Public Shared Function TryOpenCacheFromFiles(metaPath As String,
                                                 ByRef cache As LandCoverCache,
                                                 ByRef errorKind As CacheOpenErrorKind,
                                                 ByRef errorMessage As String,
                                                 Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                                 Optional ct As CancellationToken = Nothing) As Boolean

        cache = Nothing
        errorKind = CacheOpenErrorKind.None
        errorMessage = Nothing

        If String.IsNullOrWhiteSpace(metaPath) OrElse Not File.Exists(metaPath) Then
            errorKind = CacheOpenErrorKind.NotFound
            errorMessage = "Meta-Datei fehlt."
            Return False
        End If

        Dim binPath As String = Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), "lccf")
        If Not File.Exists(binPath) Then
            errorKind = CacheOpenErrorKind.NotFound
            errorMessage = "Cache-Datei fehlt."
            Return False
        End If

        '1) Meta lesen
        Dim meta As LandCoverCacheMeta

        Try
            progress?.Report(New ProgressInfo("Cache öffnen: Meta prüfen...", 0))
            ct.ThrowIfCancellationRequested()

            Dim metaJson As String = File.ReadAllText(metaPath, Encoding.UTF8)
            meta = JsonSerializer.Deserialize(Of LandCoverCacheMeta)(metaJson, ConfigStore.JsonOptions)
        Catch ex As Exception
            errorKind = CacheOpenErrorKind.MetaJsonInvalid
            errorMessage = $"Meta-Datei ist beschädigt oder kein gültiges JSON: {ex.Message}"
            Return False
        End Try

        If meta Is Nothing Then
            errorKind = CacheOpenErrorKind.MetaJsonInvalid
            errorMessage = "Meta-Datei konnte nicht interpretiert werden."
            Return False
        End If

        '2) Version & Rasterdimensionen prüfen
        If meta.CacheVersion <> LandCoverCacheFormat.CurrentVersion Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Inkompatible Cache-Version: {meta.CacheVersion} (erwartet: {LandCoverCacheFormat.CurrentVersion})."
            Return False
        End If
        If meta.CellSizeDeg <= 0 Then
            errorKind = CacheOpenErrorKind.MetaJsonInvalid
            errorMessage = "Meta-Datei enthält ungültige Rasterauflösung."
            Return False
        End If
        Dim latCount As Integer = CInt(Math.Round(180.0 / meta.CellSizeDeg))
        Dim lonCount As Integer = CInt(Math.Round(360.0 / meta.CellSizeDeg))

        If meta.LatCount <> latCount OrElse meta.LonCount <> lonCount Then
            errorKind = CacheOpenErrorKind.MetaJsonInvalid
            errorMessage = "Meta-Datei enthält ungültige Rasterdimensionen."
            Return False
        End If

        '3) Binär lesen & Cross-Checks
        Try
            Dim r = LandCoverCacheFormat.ReadCache(binPath, progress, ct)

            If r.latCount <> meta.LatCount OrElse r.lonCount <> meta.LonCount Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Dimensionen stimmen nicht)."
                Return False
            End If

            If Math.Abs(r.cellSizeDeg - meta.CellSizeDeg) > 0.0000001 Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Rasterauflösung stimmt nicht)."
                Return False
            End If

            Dim hasClassBin As Boolean = r.flags.HasFlag(LandCoverCacheFormat.LandCoverCacheFlags.HasLandCoverClass)
            Dim hasConfBin As Boolean = r.flags.HasFlag(LandCoverCacheFormat.LandCoverCacheFlags.HasConfidence)
            Dim hasIceBin As Boolean = r.flags.HasFlag(LandCoverCacheFormat.LandCoverCacheFlags.HasLandIceThickness)

            If Not hasClassBin Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei enthält kein Klassenfeld."
                Return False
            End If

            If hasConfBin <> meta.HasConfidence OrElse hasIceBin <> meta.HasLandIceThickness Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Layer-Flags stimmen nicht)."
                Return False
            End If

            cache = New LandCoverCache With {
                .Meta = meta,
                .LandCoverClass = r.classes,
                .Confidence = r.confidence,
                .LandIceThicknessM = r.landIceThickness
            }
            Return True

        Catch ex As FileNotFoundException
            errorKind = CacheOpenErrorKind.NotFound
            errorMessage = ex.Message
            Return False

        Catch ex As InvalidDataException
            errorKind = CacheOpenErrorKind.BinaryInvalid
            errorMessage = $"Binärdatei ist beschädigt oder inkompatibel: {ex.Message}"
            Return False

        Catch ex As Exception
            errorKind = CacheOpenErrorKind.BinaryInvalid
            errorMessage = $"Fehler beim Lesen der Binärdatei: {ex.Message}"
            Return False

        End Try
    End Function

    Public Shared Sub SaveCacheToFiles(binPath As String, metaPath As String, cache As LandCoverCache,
                                            Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                            Optional ct As CancellationToken = Nothing)

        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))
        If String.IsNullOrWhiteSpace(binPath) Then Throw New ArgumentException("binPath fehlt.")
        If String.IsNullOrWhiteSpace(metaPath) Then Throw New ArgumentException("metaPath fehlt.")

        '1) Binär
        progress?.Report(New ProgressInfo("Cache speichern: Binärdaten...", 0))
        ct.ThrowIfCancellationRequested()
        LandCoverCacheFormat.WriteCache(binPath, cache, progress, ct)

        '2) Meta
        progress?.Report(New ProgressInfo("Cache speichern: Meta...", 98))
        ct.ThrowIfCancellationRequested()
        Dim metaJson As String = JsonSerializer.Serialize(cache.Meta, ConfigStore.JsonOptions)
        IOHelpers.WriteTextAtomic(metaPath, metaJson, ct)

        progress?.Report(New ProgressInfo("Cache gespeichert.", 100))
    End Sub
End Class
