Public Module ReliefRenderSettings


    '========================
    ' ReliefEnergy (Steilheit)
    '========================
    Public Const EnergyBins As Integer = 2048

    'Perzentile für Normalisierung der Energie
    Public Const EnergyLandPlo As Double = 0.02
    Public Const EnergyLandPhi As Double = 0.98
    Public Const EnergyOceanPlo As Double = 0.02
    Public Const EnergyOceanPhi As Double = 0.98

    'Energie->Alpha Kurve (größer = weniger Fläche, mehr Fokus auf echte Kanten)
    Public Const EnergyLandPower As Double = 1.2
    Public Const EnergyOceanPower As Double = 1.3

    'Energie-Deadzone (0..1 im normalisierten Energieraum)
    Public Const EnergyLandDeadzoneT As Double = 0.03
    Public Const EnergyOceanDeadzoneT As Double = 0.04

    'Wie stark die Energie den HillShade-Alpha dämpft (0..1)
    '0 = HillShade unverändert, 1 = HillShade vollständig über Energie "maskiert"
    Public Const EnergyMaskStrengthLand As Double = 0.35
    Public Const EnergyMaskStrengthOcean As Double = 0.35

    '=================
    ' HillShade
    '=================
    Public Const SunAzimuthDeg As Double = 45.0
    Public Const SunElevationDeg As Double = 45.0

    Public Const HillShadeNeutral As Byte = 90
    Public Const HillShadeAmplitude As Double = 21.0

    Public Const HillShadeOceanFactor As Double = 0.55

    Public Const HillShadeAlphaOcean As Byte = 5
    Public Const HillShadeAlphaLandMax As Byte = 60        'leicht reduziert (dezent!)
    Public Const HillShadeAlphaLandPower As Double = 1.3

End Module
