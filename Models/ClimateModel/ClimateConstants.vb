Module ClimateConstants

    Public Const MinAreaWeight As Double = 0.0001           'Minimaler Flächenfaktor für Zellen nahe der Pole, um numerische Probleme zu lösen
    Public Const ElevationLapseRateKPerM As Double = 0.005  'Temperatur-Höhen-Koeffizient (Kelvin/m), typischer mittlerer Wert
    Public Const SolarConstantWm2 As Double = 1361.0        'Solarkonstante [W/m²]
    Public Const EarthOliquityDeg As Double = 23.44         'Neigung der Erdachse in Grad
    Public ReadOnly Property EarthObliquityRad As Double    'Neigung der Erdachse als Rad
        Get
            Return EarthOliquityDeg * Math.PI / 180.0
        End Get
    End Property

    Public Const DaysPerYear As Double = 365.2422           'Exakte Tage pro Jahr
End Module
