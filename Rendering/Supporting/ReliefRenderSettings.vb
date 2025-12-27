Public Module ReliefRenderSettings

    '------------------
    ' Perzentile (0..1)
    '------------------

    Public Const LandPlo As Double = 0.02
    Public Const LandPhi As Double = 0.98

    Public Const OceanPlo As Double = 0.02
    Public Const OceanPhi As Double = 0.98


    '---------------------------------------------------------
    ' Relief: Kurvenform
    ' gamma > 1: kleine Werte gedämpft, Berge bleiben sichtbar
    '---------------------------------------------------------

    Public Const ReliefGammaLand As Double = 1.5
    Public Const ReliefGammaOcean As Double = 1.3

    'Optional: Deadzone in normalisiertem t (0..1)
    Public Const ReliefDeadzoneLandT As Double = 0.03
    Public Const ReliefDeadzoneOceanT As Double = 0.02

    '------------------------------------------------------
    ' Relief: Stärke
    ' neutral ist 128 (mid-grey), delta addiert/subtrahiert
    '------------------------------------------------------

    Public Const ReliefAlpha As Byte = 105
    Public Const ReliefNeutral As Byte = 105
    Public Const ReliefBaseGrayLand As Byte = 80
    Public Const ReliefBaseGryOcean As Byte = 90

    'Maximale Aufhellung Land / Abdunklung Ozean ist in Graustufen-Delta (0..127 sinnvoll)
    Public Const ReliefDeltaLandMax As Double = 100.0
    Public Const ReliefDeltaOceanMax As Double = 75.0

    '-----------------
    ' HillShade: Sonne
    '-----------------

    Public Const SunAzimutDeg As Double = 45.0      '0=North, 90=East
    Public Const SunElevationDeg As Double = 45.0   '0=Horizont, 90=Zenit

    'HillShade wird als mid-grey +/- amplitude gemappt
    Public Const HillShadeNeutral As Byte = 90
    Public Const HillShadeAmplitude As Double = 21.0

    'Ozean gedämpft, Beziehung Land/Ozean bleibt erhalten
    Public Const HillShadeOceanFactor As Double = 0.55      'bei 0 deaktiviert

    Public Const HillShadeAlphaOcean As Byte = 5            'statisches Alpha für Ozeane
    Public Const HillShadeAlphaLandMax As Byte = 40         'dynamisches Alpha
    Public Const HillShadeAlphaLandPower As Double = 1.3      ' >1 = stärkerer Fokus auf echte Kanten, <1 = flächiger


    '-----------
    ' Histogramm
    '-----------

    Public Const HistogramBins As Integer = 4096

End Module
