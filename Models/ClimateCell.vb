Public Class ClimateCell
    'Geografische Position (optional für spätere Erweiterungen)
    Public Property LatitudeDeg As Double
    Public Property LongitudeDeg As Double
    Public Property AreaWeight As Double    'Flächengewichtung (proportional zur Zellfläche (cos(Breite)), immer > 0

    Public Property TemperatureK As Double 'Oberflächentemperatur in Kelvin

    Public Property Surface As SurfaceType 'Oberflächentyp
    Public Property HeightM As Double '+Höhe über Meeresspiegel, -Tiefe unter Meer
    Public Property HeatCapacityFactor As Double 'Effektive Wärmekapazität (relativ, z.B. Land=1,Ozean=6)
    Public Property Albedo As Double 'Reflexionsgrad (0..1)

End Class
