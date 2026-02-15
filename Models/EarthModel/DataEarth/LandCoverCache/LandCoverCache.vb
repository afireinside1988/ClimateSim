Public Class LandCoverCache

    Public Property Meta As LandCoverCacheMeta

    Public Property LandCoverClass As Byte()        'Pflicht (n = LatCount * LonCount)

    'Optional
    Public Property Confidence As Byte()            '0..255 (nuur wenn importiert)
    Public Property LandIceThicknessM As Single()   'nur wenn BedMachine importiert
    Public Property GlacierFraction As Single()
End Class
