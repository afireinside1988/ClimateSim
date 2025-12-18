Imports System.IO
Public Module EarthSurfacePaths
    Public ReadOnly Property RootDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClimateSim", "Scenarios", "EarthSurface")
    Public ReadOnly Property RawDirectory As String = Path.Combine(RootDirectory, "Raw")
    Public ReadOnly Property CacheDirectory As String = Path.Combine(RootDirectory, "Cache")

    'Raw-Dateien (aktuelle Namen)
    Public ReadOnly Property RawTidPath As String = Path.Combine(RawDirectory, "gebco_2025_tid_ascii.zip")
    Public ReadOnly Property RawSubIceTopoPath As String = Path.Combine(RawDirectory, "gebco_2025_sub_ice_ascii.zip")

End Module
