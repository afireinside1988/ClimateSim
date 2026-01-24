Imports System.IO
Public Module EarthSurfacePaths
    Public ReadOnly Property RootDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClimateSim", "Scenarios", "EarthSurface")
    Public ReadOnly Property RawDirectory As String = Path.Combine(RootDirectory, "Raw")
    Public ReadOnly Property CacheDirectory As String = Path.Combine(RootDirectory, "Cache")

End Module
