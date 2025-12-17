Imports System
Imports System.CodeDom

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

    Public Sub New(source As String, cellSizeDeg As Double, resampling As String)
        _source = source
        _cellSizeDeg = cellSizeDeg
        _resampling = resampling

        Dim loaded As EarthSurfaceCache = Nothing
        Dim kind As CacheOpenErrorKind
        Dim msg As String = Nothing

        If Not EarthSurfaceCacheStore.TryOpenCache(source, cellSizeDeg, resampling, loaded, kind, msg) Then
            Throw New InvalidOperationException($"EarthSurface-Cache konnte nicht geöffnet werden: {kind} - {msg}")
        End If

        _cache = loaded

        'Sicherheitscheck: Zellzentren-Raster erwartet
        'Für 1°: latCount = 180 lonCount = 360 (Zentren -89.5...+89.5 / -179.5...+179.5)
        If _cache.Meta Is Nothing Then
            Throw New InvalidOperationException("Cache.Meta fehlt.")
        End If
    End Sub

    Public Function GetSurfaceInfo(latitudeDeg As Double, longitudeDeg As Double) As SurfaceInfo Implements IEarthSurfaceProvider.GetSurfaceInfo
        Throw New NotImplementedException()
    End Function
End Class
