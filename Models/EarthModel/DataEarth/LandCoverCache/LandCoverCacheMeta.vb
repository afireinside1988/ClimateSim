
Public Class LandCoverCacheMeta

    'Meta-Schema-Version (unabhängig von Binär-Version)
    Public Property MetaSchemaVersion As Integer = 1

    'Binärformat-Version
    Public Property CacheVersion As Integer = 2

    Public Property CacheType As CacheType = CacheType.LandCover

    'Datenquelle
    Public Property Source As String = "COPERNICUS_LC100_v.3.0.1_2019"
    Public Property EpochYear As Integer = 2019

    'Zielraster
    Public Property CellSizeDeg As Double
    Public Property LatCount As Integer
    Public Property LonCount As Integer

    'Rastervertrag
    Public Property GridConvention As String = "CellCentered"
    Public Property RowOrder As String = "NorthToSouth"
    Public Property ColOrder As String = "WestToEast"

    'Verweis auf EarthSurfaceCache
    Public Property EarthSurfaceRef As String
    Public Property EarthSurfaceCreateUtc As DateTime?

    'Mapping-Vertrag
    Public Property MappingName As String = LandCoverSchema.SchemaName
    Public Property NoDataValue As Integer = CInt(LandCoverSchema.NoDataValue)

    'Optionale Felder
    Public Property HasConfidence As Boolean = False
    Public Property HasLandIceThickness As Boolean = False
    Public Property HasGlacierFraction As Boolean = False

    'Importer-Infos
    Public Property RawCopernicusLC100ClassFile As String
    Public Property RawCopernicusLC100ProbaFile As String
    Public Property RawBedMachineGreenlandFile As String
    Public Property RawBedMachineAntarcticaFile As String
    Public Property ImportNotes As String

    'Zeitstempel
    Public Property CreateUtc As DateTime = DateTime.UtcNow

End Class
