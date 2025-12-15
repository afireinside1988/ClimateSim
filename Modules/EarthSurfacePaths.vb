Imports System.IO
Public Module EarthSurfacePaths
    Public ReadOnly Property RootDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClimateSim", "Sceneries", "EarthSurface")
    Public ReadOnly Property RawDirectory As String = Path.Combine(RootDirectory, "Raw")
    Public ReadOnly Property CacheDirectory As String = Path.Combine(RootDirectory, "Cache")

    'Raw-Dateien (aktuelle Namen)
    Public ReadOnly Property RawTidPath As String = Path.Combine(RawDirectory, "GEBCO_2025_TID.nc")
    Public ReadOnly Property RawSubIceTopoPath As String = Path.Combine(RawDirectory, "GEBCO_2025_sub_ice_topo.nc")

End Module
