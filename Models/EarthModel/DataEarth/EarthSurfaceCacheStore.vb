Imports System.Drawing.Imaging
Imports System.IO
Imports System.Linq.Expressions
Imports System.Net.Http.Headers
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports System.Windows.Media.Converters

Public Enum CacheOpenErrorKind
    None = 0
    NotFound = 1
    MetaJsonInvalid = 2
    BinaryInvalid = 3
    IncompatibleSchema = 4
End Enum

Public Class EarthSurfaceCacheStore


    Private Sub New()

    End Sub

    Public Shared ReadOnly Property CacheDir As String = EarthSurfacePaths.CacheDirectory

    ''' <summary>
    ''' Erzeugt den standardisierten Cache-Dateinamen (ohne Extension)
    ''' Beispiel: "GEBCO_2025_1.0deg_nearest"
    ''' </summary>
    ''' <param name="source"></param>
    ''' <param name="cellSizeDeg"></param>
    ''' <param name="resampling"></param>
    ''' <returns></returns>
    Public Shared Function BuildCacheBaseName(source As String, cellSizeDeg As Double, resampling As String,
                                              Optional landMaskVariant As String = Nothing) As String
        Dim cs As String = cellSizeDeg.ToString("0.##", Globalization.CultureInfo.InvariantCulture)

        Dim lm As String = NormalizeNamePart(landMaskVariant)
        Dim lmPart As String = If(String.IsNullOrWhiteSpace(lm), "", "_" & lm)

        Return $"{source}_{cs}deg_{resampling}{lmPart}".Replace(" ", "")
    End Function

    Public Shared Function GetCachePaths(source As String, cellSizeDeg As Double, resampling As String,
                                         Optional landMaskVariant As String = Nothing) As (binPath As String, metaPath As String)

        Directory.CreateDirectory(CacheDir)

        Dim baseName As String = BuildCacheBaseName(source, cellSizeDeg, resampling, landMaskVariant)
        Dim binPath As String = Path.Combine(CacheDir, baseName & ".bin")
        Dim metaPath As String = Path.Combine(CacheDir, baseName & ".meta.json")
        Return (binPath, metaPath)
    End Function

    Public Shared Function TryOpenCacheFromSourceName(source As String, cellSizeDeg As Double, resampling As String,
                                        ByRef cache As EarthSurfaceCache,
                                        ByRef errorKind As CacheOpenErrorKind,
                                        ByRef errorMessage As String,
                                        Optional landMaskVariant As String = Nothing,
                                        Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                        Optional ct As CancellationToken = Nothing) As Boolean

        cache = Nothing
        errorKind = CacheOpenErrorKind.None
        errorMessage = Nothing

        Dim paths As (binPath As String, metaPath As String) = GetCachePaths(source, cellSizeDeg, resampling, landMaskVariant)
        Dim binPath As String = paths.binPath
        Dim metaPath As String = paths.metaPath

        If Not File.Exists(binPath) OrElse Not File.Exists(metaPath) Then
            errorKind = CacheOpenErrorKind.NotFound
            errorMessage = "Cache-Datei oder Meta-Datei fehlt."
            Return False
        End If

        Dim meta As EarthSurfaceCacheMeta

        progress?.Report(New ProgressInfo("Cache öffnen: Meta prüfen...", 0))
        ct.ThrowIfCancellationRequested()

        '1) Meta laden
        Try
            Dim metaJson = File.ReadAllText(metaPath, Encoding.UTF8)
            meta = JsonSerializer.Deserialize(Of EarthSurfaceCacheMeta)(metaJson, ConfigStore.JsonOptions)
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

        '2) Schema/Kompatibilität prüfen
        If meta.CacheVersion <> EarthSurfaceCacheFormat.CurrentVersion Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Inkompatible Cache-Version: {meta.CacheVersion} (erwartet: {EarthSurfaceCacheFormat.CurrentVersion})."
            Return False
        End If

        If Not String.Equals(meta.Source, source, StringComparison.OrdinalIgnoreCase) Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Cache-Quelle passt nicht. Meta={meta.Source}, erwartet='{source}'."
            Return False
        End If

        If Math.Abs(meta.CellSizeDeg - cellSizeDeg) > 0.000001 Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Cache-Auflösung passt nicht. Meta={meta.CellSizeDeg}, erwartet={cellSizeDeg}."
            Return False
        End If

        If Not String.Equals(meta.Resampling, resampling, StringComparison.OrdinalIgnoreCase) Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Resamling passt nicht. Meta='{meta.Resampling}', erwartet='{resampling}'."
            Return False
        End If

        '3) Binär lesen
        Try
            Dim r = EarthSurfaceCacheFormat.ReadCache(binPath, progress, ct)

            'Cross-Check: Dimensions
            If r.latCount <> meta.LatCount OrElse r.lonCount <> meta.LonCount Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Dimensionen stimmen nicht überein)."
                Return False
            End If

            'Cross-Check: Flags
            Dim hasHeightBin As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasHeight)
            Dim hasTidBin As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasTid)
            Dim hasLandMaskBin As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasLandMask)

            If hasHeightBin <> meta.HasHeight OrElse
               hasTidBin <> meta.HasTid OrElse
               hasLandMaskBin <> meta.HasLandMask Then

                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Layer-Flags stimmen nicht)."
                Return False
            End If

            cache = New EarthSurfaceCache(meta, r.height, r.tid, r.landMask)
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

    Public Shared Function TryOpenCacheFromFiles(metaPath As String,
                                                 ByRef cache As EarthSurfaceCache,
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

        Dim binPath As String = Path.ChangeExtension(Path.ChangeExtension(metaPath, Nothing), "bin")
        If Not File.Exists(binPath) Then
            errorKind = CacheOpenErrorKind.NotFound
            errorMessage = "Cache-Datei fehlt."
            Return False
        End If

        '1) Meta lesen
        Dim meta As EarthSurfaceCacheMeta

        Try

            progress?.Report(New ProgressInfo("Cache öffnen: Meta prüfen...", 0))
            ct.ThrowIfCancellationRequested()

            Dim metaJson = File.ReadAllText(metaPath, Encoding.UTF8)
            meta = JsonSerializer.Deserialize(Of EarthSurfaceCacheMeta)(metaJson, ConfigStore.JsonOptions)

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

        '2) Version prüfen
        If meta.CacheVersion <> EarthSurfaceCacheFormat.CurrentVersion Then
            errorKind = CacheOpenErrorKind.IncompatibleSchema
            errorMessage = $"Inkompatible Cache-Version: {meta.CacheVersion} (erwartet: {EarthSurfaceCacheFormat.CurrentVersion})."
            Return False
        End If

        '3) Binär lesen + Cross-Checks
        Try

            Dim r = EarthSurfaceCacheFormat.ReadCache(binPath, progress, ct)

            If r.latCount <> meta.LatCount OrElse r.lonCount <> meta.LonCount Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Dimensionen stimmen nicht)."
                Return False
            End If

            Dim hasHeightBin As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasHeight)
            Dim hasTidBin As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasTid)
            Dim hasLandMask As Boolean = r.flags.HasFlag(EarthSurfaceCacheFormat.CacheFlags.HasLandMask)

            If hasHeightBin <> meta.HasHeight OrElse hasTidBin <> meta.HasTid OrElse hasLandMask <> meta.HasLandMask Then
                errorKind = CacheOpenErrorKind.BinaryInvalid
                errorMessage = "Binärdatei passt nicht zu Meta (Layer-Flags stimmen nicht)."
                Return False
            End If

            cache = New EarthSurfaceCache(meta, r.height, r.tid, r.landMask)
            Return True

        Catch ex As Exception

            errorKind = CacheOpenErrorKind.BinaryInvalid
            errorMessage = $"Fehler beim Lesen der Binärdatei: {ex.Message}"
            Return False

        End Try

    End Function

    Public Shared Sub SaveCache(source As String, cellSizeDeg As Double, resampling As String, cache As EarthSurfaceCache,
                                Optional landMaskVariant As String = Nothing,
                                Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                Optional ct As CancellationToken = Nothing)

        If cache Is Nothing OrElse cache.Meta Is Nothing Then Throw New ArgumentNullException(NameOf(cache))

        Dim paths As (binPath As String, metaPath As String) = GetCachePaths(source, cellSizeDeg, resampling, landMaskVariant)

        '1) Binär
        progress?.Report(New ProgressInfo("Cache speichern: Binärdaten...", 0))
        ct.ThrowIfCancellationRequested()
        EarthSurfaceCacheFormat.WriteCache(paths.binPath, cache, progress, ct)

        '2) Meta
        progress?.Report(New ProgressInfo("Cache speichern: Meta...", 98))
        ct.ThrowIfCancellationRequested()
        Dim metaJson As String = JsonSerializer.Serialize(cache.Meta, ConfigStore.JsonOptions)
        WriteTextAtomic(paths.metaPath, metaJson, ct)

        progress?.Report(New ProgressInfo("Cache gespeichert.", 100))
    End Sub


#Region "Helper"

    Private Shared Sub WriteTextAtomic(savePath As String, content As String, Optional ct As CancellationToken = Nothing)

        ct.ThrowIfCancellationRequested()

        Directory.CreateDirectory(Path.GetDirectoryName(savePath))
        Dim tmp As String = savePath & ".tmp"
        File.WriteAllText(tmp, content, Encoding.UTF8)

        ct.ThrowIfCancellationRequested()

        If File.Exists(savePath) Then
            File.Replace(tmp, savePath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, savePath)
        End If
    End Sub

    Private Shared Function NormalizeNamePart(part As String) As String

        If String.IsNullOrWhiteSpace(part) Then Return Nothing

        Dim s As String = part.Trim()

        'Erlaubt: a-zA-Z0-9 _ - .
        For Each c In Path.GetInvalidFileNameChars()
            s = s.Replace(c, "_"c)
        Next

        s = s.Replace(" ", "_")

        Return s
    End Function

#End Region
End Class
