Imports System
Imports System.CodeDom
Imports System.Threading

Public Class DataEarthSurfaceProvider
    Implements IEarthSurfaceProvider

    Private ReadOnly _source As String
    Private ReadOnly _cellSizeDeg As Double
    Private ReadOnly _resampling As String

    Private ReadOnly _cache As EarthSurfaceCache

    '--- einfache Regeln (später konfigurierbar machen) ---
    Private ReadOnly _seaIceNorthLatDeg As Double = 80.0
    Private ReadOnly _seaIceSouthLatDeg As Double = -70.0

    'LandIce grob (wie ToyEarth; später durch echte Eis-Modelle ersetzen)
    Private ReadOnly _antarcticaLatDeg As Double = -70.0

    'Grönland Bounding Box (grob, wie bisher)
    Private ReadOnly _greenlandLatMin As Double = 60.0
    Private ReadOnly _greenlandLatMax As Double = 85.0
    Private ReadOnly _greenlandLonMin As Double = -60.0
    Private ReadOnly _greenlandLonMax As Double = -40.0

    Private Sub New(source As String, cellSizeDeg As Double, resampling As String, cache As EarthSurfaceCache)
        _source = source
        _cellSizeDeg = cellSizeDeg
        _resampling = resampling
        _cache = cache

        If _cache Is Nothing OrElse _cache.Meta Is Nothing Then
            Throw New InvalidOperationException("Cache oder Cache.Meta fehlt.")
        End If
    End Sub

    Public Shared Function CreateAsync(source As String, cellSizeDeg As Double, resampling As String,
                                             Optional progress As IProgress(Of ProgressInfo) = Nothing,
                                             Optional ct As CancellationToken = Nothing) As Task(Of DataEarthSurfaceProvider)

        ct.ThrowIfCancellationRequested()

        Dim loaded As EarthSurfaceCache = Nothing
        Dim kind As CacheOpenErrorKind
        Dim msg As String = Nothing

        progress?.Report(New ProgressInfo("EarthSurface: Öffne Cache...", 0))

        If Not EarthSurfaceCacheStore.TryOpenCache(source, cellSizeDeg, resampling, loaded, kind, msg, progress, ct) Then
            Throw New InvalidOperationException($"EarthSurface-Cache konnte nicht geöffnet werden: {kind} - {msg}")
        End If

        progress?.Report(New ProgressInfo("EarthSurface: Cache bereit.", 100))

        Dim provider As DataEarthSurfaceProvider = New DataEarthSurfaceProvider(source, cellSizeDeg, resampling, loaded)
        Return Task.FromResult(provider)
    End Function

    Public Function GetSurfaceInfo(latitudeDeg As Double, longitudeDeg As Double) As SurfaceInfo Implements IEarthSurfaceProvider.GetSurfaceInfo
        Dim lat As Double = Clamp(latitudeDeg, -90.0, 90.0)
        Dim lon As Double = WrapLon180(longitudeDeg)

        'Index aus Zellzentren ableiten
        Dim latIdx As Integer = LatToIndex(lat, _cache.Meta.LatCount, _cache.Meta.CellSizeDeg)
        Dim lonIdx As Integer = LonToIndex(lon, _cache.Meta.LonCount, _cache.Meta.CellSizeDeg)

        Dim idx As Integer = latIdx * _cache.Meta.LonCount + lonIdx

        Dim landMaskAvailable As Boolean = (_cache.LandMask IsNot Nothing AndAlso _cache.LandMask.Length > idx)
        Dim isLand As Boolean = False

        If landMaskAvailable Then
            isLand = (_cache.LandMask(idx) <> 0)
        End If

        Dim h As Double = 0.0
        If _cache.HeightM IsNot Nothing AndAlso _cache.HeightM.Length > idx Then
            h = _cache.HeightM(idx)
        End If

        Dim surface As SurfaceType = SurfaceTypeFromMaskOrHeight(landMaskAvailable, isLand, h)

        '--- LandIce-Regeln (grob) ---
        If IsAntarctica(lat) Then
            surface = SurfaceType.LandIce
            'Höhe bei LandIce: wir nehmen erstmal eine pauschale Eisdicke/Topografie-Höhe
            h = Math.Max(h, 2500.0)
        ElseIf IsGreenland(lat, lon) Then
            surface = SurfaceType.LandIce
            h = Math.Max(h, 2500.0)
        End If

        '--- SeaIce-Regel (nur wenn "Ocean") ---
        If surface = SurfaceType.Ocean Then
            If lat >= _seaIceNorthLatDeg OrElse lat <= _seaIceSouthLatDeg Then
                surface = SurfaceType.SeaIce
                h = 0.0
            End If
        End If

        Dim info As New SurfaceInfo With {
            .Surface = surface,
            .HeightM = h,
            .Albedo = Nothing,
            .HeatCapacityFactor = Nothing
            }

        Return info

    End Function

#Region "Mapping und Helfer"

    Private Shared Function SurfaceTypeFromMaskOrHeight(isLandMaskAvailable As Boolean, isLand As Boolean, heightM As Double) As SurfaceType
        If isLandMaskAvailable Then
            If Not isLand Then
                Return SurfaceType.Ocean
            End If
            'Land: Mountain/Plain weiterhin heuristisch über Höhe
            If heightM >= 1500.0 Then Return SurfaceType.LandMountain
            Return SurfaceType.LandPlain
        End If

        'Fallback: alte Caches ohne LandMask
        Return SurfaceTypeFromHeight(heightM)
    End Function
    Private Shared Function SurfaceTypeFromHeight(heightM As Double) As SurfaceType
        If heightM < 0.0 Then
            Return SurfaceType.Ocean
        End If

        'Minimalheuristik (später: Biome/Mountain aus zusätzlichen Datensets)
        If heightM >= 1500.0 Then
            Return SurfaceType.LandMountain
        End If

        Return SurfaceType.LandPlain
    End Function

    ''' <summary>
    ''' Erwartet Zellzentren-Raster:
    ''' Lat-Zentren laufen von (-90 + cell/2) bis (+90 - cell/2).
    ''' </summary>
    Private Shared Function LatToIndex(latDeg As Double, latCount As Integer, cellSizeDeg As Double) As Integer
        Dim baseCenter As Double = -90.0 + (cellSizeDeg / 2.0)

        Dim x As Double = (latDeg - baseCenter) / cellSizeDeg
        Dim i As Integer = CInt(Math.Floor(x + 0.0000000001)) 'kleiner Epsilon gegen Rundungsgrenzen

        If i < 0 Then i = 0
        If i > latCount - 1 Then i = latCount - 1
        Return i
    End Function

    ''' <summary>
    ''' Erwartet Zellzentren-Raster:
    ''' Lon-Zentren laufen von (-180 + cell/2) bis (+180 - cell/2),
    ''' Lon ist vorher in [-180,+180] normalisiert. 
    ''' </summary>
    Private Shared Function LonToIndex(lonDeg As Double, lonCount As Integer, cellSizeDeg As Double) As Integer
        Dim baseCenter As Double = -180.0 + (cellSizeDeg / 2.0)

        Dim x As Double = (lonDeg - baseCenter) / cellSizeDeg
        Dim i As Integer = CInt(Math.Floor(x + 0.0000000001))

        If i < 0 Then i = 0
        If i > lonCount - 1 Then i = lonCount - 1
        Return i
    End Function

    ''' <summary>
    ''' Wrap auf [-180,+180]
    ''' </summary>
    Private Shared Function WrapLon180(lonDeg As Double) As Double
        Dim x As Double = lonDeg
        x = ((x + 180.0) Mod 360.0 + 360.0) Mod 360.0
        Return x - 180.0
    End Function

    Private Shared Function Clamp(x As Double, lo As Double, hi As Double) As Double
        If x < lo Then Return lo
        If x > hi Then Return hi
        Return x
    End Function

    Private Function IsAntarctica(latDeg As Double) As Boolean
        Return latDeg <= _antarcticaLatDeg
    End Function

    Private Function IsGreenland(latDeg As Double, lonDeg As Double) As Boolean
        Return (latDeg >= _greenlandLatMin AndAlso latDeg <= _greenlandLatMax AndAlso
                lonDeg >= _greenlandLonMin AndAlso lonDeg <= _greenlandLonMax)
    End Function



#End Region
End Class
