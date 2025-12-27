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

    Public Const ReliefGammaLandBase As Double = 0.7
    Public Const ReliefGammaOceanBase As Double = 1.3

    'Optional: Deadzone in normalisiertem t (0..1)
    Public Const ReliefDeadzoneLandT As Double = 0.03
    Public Const ReliefDeadzoneOceanT As Double = 0.02

    '------------------------------------------------------
    ' Relief: Stärke
    ' neutral ist 128 (mid-grey), delta addiert/subtrahiert
    '------------------------------------------------------

    'Alpha-Extremwerte
    Public Const ReliefAlphaLandMin As Byte = 20
    Public Const ReliefAlphaLandMax As Byte = 200
    Public Const ReliefAlphaOceanMin As Byte = 60
    Public Const ReliefAlphaOceanMax As Byte = 200

    'Alpha-Kurve: >1 = Fokus auf Extreme, <1 => flächiger
    Public Const ReliefAlphaLandPower As Double = 1.4
    Public Const ReliefAlphaOceanPower As Double = 1.4

    Public Const ReliefNeutral As Byte = 128
    Public Const ReliefBaseGrayLand As Byte = 200
    Public Const ReliefBaseGryOcean As Byte = 80

    'Maximale Aufhellung Land / Abdunklung Ozean ist in Graustufen-Delta (0..127 sinnvoll)
    Public Const ReliefDeltaLandMax As Double = 100.0
    Public Const ReliefDeltaOceanMax As Double = 100.0

    'Tail-Verhalten (für Hochgebirge/Tiefsee)
    Public Const ReliefTailPower As Double = 0.9         'Tail-Krümmung: >1 = flacher Start, stärkerer Fokus auf echte Hochgebirge/Tiefsee
    Public Const ReliefTailWeight As Double = 0.45        'wie viel "extra Raum" bekommt der Tail

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
