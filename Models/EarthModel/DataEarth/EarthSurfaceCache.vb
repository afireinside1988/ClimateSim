''' <summary>
''' In-Memory Repräsentation eines gecachten EarthSurface-Rasters
''' Indexierung: row-major (latIndex * LonCount + lonIndex)
''' </summary>
Public Class EarthSurfaceCache

    Public ReadOnly Property Meta As EarthSurfaceCacheMeta

    'Layer: Höhe (Meter, negativ = Bathymetrie)
    Public ReadOnly Property HeightM As Single()

    'Layer: TID (Qualitäts-/Data-ID-Grid; Bedeutung definieren wir später genauer)
    Public ReadOnly Property Tid As Single()

    'Layer: landMask (0=Ocean / 1=Land) - Seen in v0.3 als Land
    Public ReadOnly Property LandMask As Byte()

    Public Sub New(meta As EarthSurfaceCacheMeta, heightM As Single(), tid As Single(), Optional landMask As Byte() = Nothing)
        Me.Meta = meta
        Me.HeightM = heightM
        Me.Tid = tid
        Me.LandMask = landMask
    End Sub

    Public Function IndexOf(latIndex As Integer, lonIndex As Integer) As Integer
        Return latIndex * Meta.LonCount + lonIndex
    End Function
End Class
