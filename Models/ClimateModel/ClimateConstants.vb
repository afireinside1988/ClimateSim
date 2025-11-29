Module ClimateConstants

    'Minimaler Flächenfaktor für Zellen nahe der Pole, um numerische Probleme zu lösen
    Public Const MinAreaWeight As Double = 0.0001

    'Temperatur-Höhen-Koeffizient (Kelvin/m), typischer mittlerer Wert
    Public Const ElevationLapseRateKPerM As Double = 0.0065

End Module
