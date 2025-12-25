Imports System.Text.Json.Serialization

''' <summary>
''' Meta-Daten für eine Cache-Datei.
''' Wird als JSON gespeichert und beim Laden als "Kompatibilitätsvertrag" verwendet
''' </summary>
Public Class EarthSurfaceCacheMeta
    Public Property CacheVersion As Integer = 2         'Version 2 für: TID von Single auf Byte umgestellt

    'Identifiziert die Datenquellen und Layer (für Menschen + spätere Auswahl in UI)
    Public Property Source As String = "GEBCO_2025"
    Public Property LandMaskSource As String = "FromHeight"
    Public Property LandMaskNotes As String = "v0.3: height>=0 then 3x3 hysteresis majority (6/3), 1 iter"
    Public Property UseHysteresis As Boolean = True
    Public Property HysteresisIterations As Integer = 1
    Public Property LandThreshold As Integer = 6
    Public Property OceanThreshold As Integer = 3
    'Raster-Definition
    Public Property CellSizeDeg As Double       '1.0 / 0.5 / 0.25 / 0.125
    Public Property LatCount As Integer          'z.B. 180 bei 1° (wenn -90...90 exkl. Pol-Kanten)
    Public Property LonCount As Integer          'z.B. 360 bei 1°

    'Bounds (wir speichern hier die theoretischen Grenzen; Mapping/Wrap macht später der Provider)
    Public Property LatMin As Double = -89.5
    Public Property LatMax As Double = 89.5
    Public Property LonMin As Double = -179.5
    Public Property LonMax As Double = 179.5

    'Welche Interpolation wurde beim Resampling benutzt (wichtig für Reproduzierbarkeit)
    Public Property Resampling As String = "nearest"        'nearest|bilinear

    'Welche Layer sind enthalten
    Public Property HasHeight As Boolean = True
    Public Property HasTid As Boolean = True
    Public Property HasLandMask As Boolean = False

    'Zeitstempel
    Public Property CreateUtc As DateTime = DateTime.UtcNow

    'Optional: Infos zu Rohdaten (für spätere Validierung / UI)
    Public Property RawTidFile As String
    Public Property RawHeightFile As String

End Class
