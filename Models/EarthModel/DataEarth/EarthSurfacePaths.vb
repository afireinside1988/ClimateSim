Imports System.IO
Public Module DataEarthPaths
    Public ReadOnly Property RootDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClimateSim", "Scenarios", "DataEarth")
    Public ReadOnly Property RawDirectory As String = Path.Combine(RootDirectory, "Raw")
    Public ReadOnly Property CacheDirectory As String = Path.Combine(RootDirectory, "Cache")

End Module
