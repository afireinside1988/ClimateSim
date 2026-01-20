Imports System.Windows.Media.Media3D

''' <summary>
''' Astronomie-Hilfsfunktionen für SCOACH
''' </summary>
Module Astronomics

    ''' <summary>
    ''' Sekunden des mittleren Sonnentages.
    ''' </summary>
    Private Const SecondsPerDay As Double = 86400.0

    ''' <summary>
    ''' Bündelt mehrfach benötigte Hilfsgrößen, die aus einem julianischem Datum abgeleitet werden.
    ''' </summary>
    Public Structure SolarContext

        ''' <summary>
        ''' Julianisches Datum (UTC-basiert)
        ''' </summary>
        Public Jd As Double
        ''' <summary>
        ''' Julianische Jahrhunderte seit J2000.0
        ''' </summary>
        Public T As Double

        ''' <summary>
        ''' Mittlere ekliptikale Länge der Sonne [Grad]
        ''' </summary>
        Public L0Deg As Double

        ''' <summary>
        ''' Mittlere Anomalie [Grad]
        ''' </summary>
        Public MDeg As Double
        ''' <summary>
        ''' Mittlere Anomalie [Radient]
        ''' </summary>
        Public MRad As Double

        ''' <summary>
        ''' Exzentrizität der Erbahn e
        ''' </summary>
        Public Ecc As Double
        ''' <summary>
        ''' Neigung der Ekliptik ε [Grad]
        ''' </summary>
        Public EpsDeg As Double
        ''' <summary>
        ''' Neigung der Ekliptik ε [Radient]
        ''' </summary>
        Public EpsRad As Double

        ''' <summary>
        ''' Ergebnis der Mittelpunktgleichung C [Grad]
        ''' korrigiert die mittlere Anomalie, um die elliptische Bahn zu berücksichtigen
        ''' </summary>
        Public CDeg As Double

        ''' <summary>
        ''' Wahre ekliptische Länge λ = L0 + C [Grad]
        ''' </summary>
        Public LambdaDeg As Double
        ''' <summary>
        ''' Wahre ekliptische Länge λ = L0 + C [Radient]
        ''' </summary>
        Public LambdaRad As Double

        ''' <summary>
        ''' Wahre Anomalie v (näherungsweise ν ≈ M + C) [Radient]
        ''' </summary>
        Public NuRad As Double
        ''' <summary>
        ''' Erde-Sonne-Distanz r in Astronomischen Einheiten (AU)
        ''' </summary>
        Public R_AU As Double
        ''' <summary>
        ''' Distanzfaktor Erde<->Sonne 1/r²
        ''' Skaliert die Solarkonstante, um Perihel/Aphel zu berücksichtigen
        ''' </summary>
        Public DistFactor As Double     '1/r^2

        ''' <summary>
        ''' tan(ε/2)^2 (für Equation of Time)
        ''' </summary>
        Public Y As Double

    End Structure

    ''' <summary>
    ''' Errechnet aus der UTC das julianische Datum;
    ''' Standard-Algorithmus (gregorianischer Kalender) mit Tagesbruchteil
    ''' </summary>
    Public Function JulianDateUtc(t As DateTime) As Double

        Dim u As DateTime = DateTime.SpecifyKind(t, DateTimeKind.Utc)

        Dim y As Integer = u.Year
        Dim m As Integer = u.Month
        Dim d As Double = u.Day + (u.Hour + (u.Minute + u.Second / 60.0) / 60.0) / 24.0

        If m <= 2 Then
            y -= 1
            m += 12
        End If

        Dim A As Integer = y \ 100
        Dim B As Integer = 2 - A + (A \ 4)

        Dim jd As Double =
            Math.Floor(365.25 * (y + 4716)) +
            Math.Floor(30.6001 * (m + 1)) +
            d + B - 1524.5

        Return jd

    End Function

    ''' <summary>
    ''' Errechnet aus dem julianischen Datum Sonnen-Hilfsgrößen;
    ''' Formeln sind gängige Näherungen nachn Meeus/NOAA
    ''' </summary>
    Public Function BuildSolarContext(jd As Double) As SolarContext

        Dim ctx As New SolarContext()
        ctx.Jd = jd

        ctx.T = (jd - 2451545.0) / 36525.0

        'Mittlere exkliptikale Länge L0 und mittlere Anomalie M
        Dim L0 As Double = 280.46646 + 36000.76983 * ctx.T + 0.0003032 * ctx.T * ctx.T
        Dim M As Double = 357.52911 + 35999.05029 * ctx.T - 0.0001537 * ctx.T * ctx.T

        ctx.L0Deg = Mathematics.Wrap360(L0)
        ctx.MDeg = Mathematics.Wrap360(M)
        ctx.MRad = Mathematics.DegToRad(ctx.MDeg)

        'Exzentrizität und Neigung der Ekliptik
        ctx.Ecc = 0.016708634 - 0.000042037 * ctx.T - 0.0000001267 * ctx.T * ctx.T
        ctx.EpsDeg = 23.439291 - 0.0130042 * ctx.T
        ctx.EpsRad = Mathematics.DegToRad(ctx.EpsDeg)

        'Mittelpunktgleichung C
        ctx.CDeg =
            (1.914602 - 0.004817 * ctx.T - 0.000014 * ctx.T * ctx.T) * Math.Sin(ctx.MRad) +
            (0.019993 - 0.000101 * ctx.T) * Math.Sin(2 * ctx.MRad) +
            0.000289 * Math.Sin(3 * ctx.MRad)

        'Wahre ekliptikale Länge
        ctx.LambdaDeg = Mathematics.Wrap360(ctx.L0Deg + ctx.CDeg)
        ctx.LambdaRad = Mathematics.DegToRad(ctx.LambdaDeg)

        'y = tan(eps/2)^2  (Equation of Time)
        Dim t2 As Double = Math.Tan(ctx.EpsRad / 2.0)
        ctx.Y = t2 * t2

        'Wahre Anomalie v
        ctx.NuRad = Mathematics.DegToRad(ctx.MDeg + ctx.CDeg)

        'Erde-Sonne-Distanz in Astronomischen Einheiten (Keppler-Geometrie)
        ctx.R_AU = (1.0 - ctx.Ecc * ctx.Ecc) / (1.0 + ctx.Ecc * Math.Cos(ctx.NuRad))
        If ctx.R_AU <= 0.0 Then ctx.R_AU = 1.0

        'Distanzfaktor für Strahlungsfluss
        ctx.DistFactor = 1.0 / (ctx.R_AU * ctx.R_AU)

        Return ctx

    End Function

    ''' <summary>
    ''' Errechnet die Rektaszension und Deklination der Sonne
    ''' </summary>
    Public Function SunRaDecDeg(ctx As SolarContext, ByRef raDeg As Double, ByRef decDeg As Double) As Boolean

        Dim sinLam As Double = Math.Sin(ctx.LambdaRad)
        Dim cosLam As Double = Math.Cos(ctx.LambdaRad)

        Dim x As Double = cosLam
        Dim y As Double = Math.Cos(ctx.EpsRad) * sinLam
        Dim z As Double = Math.Sin(ctx.EpsRad) * sinLam

        Dim ra As Double = Math.Atan2(y, x)
        Dim dec As Double = Math.Asin(Mathematics.Clamp(z, -1.0, 1.0))

        raDeg = Mathematics.Wrap360(Mathematics.RadToDeg(ra))
        decDeg = Mathematics.RadToDeg(dec)
        Return True

    End Function

    ''' <summary>
    ''' Errechnet die GMST (Greenwich Mean Sidereal Time) in Grad aus der julianischen Zeit
    ''' </summary>
    Public Function GmstDeg(jd As Double) As Double

        Dim T As Double = (jd - 2451545.0) / 36525.0

        'GMST in Grad (klassische Näherung)
        Dim gmst As Double =
             280.46061837 +
             360.98564736629 * (jd - 2451545.0) +
             0.000387933 * T * T -
             (T * T * T) / 38710000.0

        Return Mathematics.Wrap360(gmst)
    End Function

    ''' <summary>
    ''' Zeitabweichung EoT in Minuten zwischen wahrer Sonnenzeit und mittlerer UTC-Uhrzeit
    ''' </summary>
    Public Function EquationOfTimeMinutes(ctx As SolarContext) As Double

        Dim L0r As Double = Mathematics.DegToRad(ctx.L0Deg)
        Dim Mr As Double = ctx.MRad
        Dim e As Double = ctx.Ecc
        Dim y As Double = ctx.Y

        Dim eE As Double =
            y * Math.Sin(2 * L0r) -
            2 * e * Math.Sin(Mr) +
            4 * e * y * Math.Sin(Mr) * Math.Cos(2 * L0r) -
            0.5 * y * y * Math.Sin(4 * L0r) -
            1.25 * e * e * Math.Sin(2 * Mr)

        'E in RAD -> Minuten
        Return Mathematics.RadToDeg(eE) * 4.0

    End Function

    Public Function BodyVectorFromLatLon(latDeg As Double, lonDeg As Double) As Vector3D

        Dim latRad As Double = DegToRad(latDeg)
        Dim lonRad As Double = DegToRad(lonDeg)

        Dim clat As Double = Math.Cos(latRad)

        Dim x As Double = clat * Math.Cos(lonRad)
        Dim y As Double = Math.Sin(latRad)
        Dim z As Double = -clat * Math.Sin(lonRad)

        Dim v As New Vector3D(x, y, z)
        v.Normalize()
        Return v
    End Function

    ''' <summary>
    ''' Errechnet die Sonnenhöhe und das Azimut für einen Punkt der Erdoberfläche
    ''' </summary>
    Public Sub ComputeSunHeightAzimuthAtPoint(latDeg As Double, lonDeg As Double, subsolarLatDeg As Double, subsolarLonDeg As Double, ByRef sunHeightDeg As Double, ByRef sunAzimuthDeg As Double)

        'Sonnenrichtung im Body-Space (Erde->Sonne) aus dem Subsolar-Punkt
        Dim s As Vector3D = BodyVectorFromLatLon(subsolarLatDeg, subsolarLonDeg)
        s.Normalize()

        'Oberflächen-Normale (Up) im Body-Space
        Dim up As Vector3D = BodyVectorFromLatLon(latDeg, lonDeg)
        up.Normalize()

        'Lokale Ost/Nord im Body-Space, konsistent mit unserem Mapping:
        'x = cos(lat)cos(lon)
        'y = sin(lat)
        'z = -cos(lat)sin(lon)
        Dim latRad As Double = DegToRad(latDeg)
        Dim lonRad As Double = DegToRad(lonDeg)

        'Osten zeigt nach ansteigender Lon (Ost-positiv)
        Dim east As New Vector3D(-Math.Sin(lonRad), 0.0, -Math.Cos(lonRad))
        If east.LengthSquared < 0.000000000001 Then
            sunHeightDeg = 0
            sunAzimuthDeg = 0
            Return
        End If
        east.Normalize()

        'Norden zeigt nach ansteigender Lat
        Dim north As New Vector3D(-Math.Sin(latRad) * Math.Cos(lonRad),
                                  Math.Cos(latRad),
                                  Math.Sin(latRad) * Math.Sin(lonRad))

        If north.LengthSquared < 0.000000000001 Then
            sunHeightDeg = 0
            sunAzimuthDeg = 0
            Return
        End If
        north.Normalize()

        'Sonnenvektor auf lokale Achsen projezieren
        Dim u As Double = Vector3D.DotProduct(s, up)        'Up-Komponente
        Dim e As Double = Vector3D.DotProduct(s, east)      'Ost-Komponente
        Dim n As Double = Vector3D.DotProduct(s, north)      'Nord-Komponente

        u = Clamp(u, -1.0, 1.0)

        'Sonnenhöhe über dem Horizont:
        Dim hRad As Double = Math.Asin(u)
        sunHeightDeg = RadToDeg(hRad)

        'Azimuth: von Norden im Uhrzeigersinn (0=N; 90=E; 180=S; 270=W)
        'atan2(Ost,Nord)
        Dim azRad As Double = Math.Atan2(e, n)
        Dim azDeg As Double = Wrap360(RadToDeg(azRad))
        sunAzimuthDeg = azDeg

    End Sub

    ''' <summary>
    ''' Errechnet die Tages- und Nachtlänge für einen Punkt der Erdoberfläche
    ''' </summary>
    Public Sub ComputeDayNightLength(latDeg As Double, declDeg As Double, ByRef dayMinutes As Integer, ByRef nightMinutes As Integer)

        Dim latRad = DegToRad(latDeg)
        Dim declRad = DegToRad(declDeg)

        Dim cosH0 As Double = -Math.Tan(latRad) * Math.Tan(declRad)

        'Polartag/Polarnacht
        If cosH0 <= -1.0 Then
            dayMinutes = 1440
            nightMinutes = 0
            Return
        ElseIf cosH0 >= 1.0 Then
            dayMinutes = 0
            nightMinutes = 1440
            Return
        End If

        Dim H0 = Math.Acos(Clamp(cosH0, -1.0, 1.0))
        Dim dayHours As Double = (2.0 * H0) * 24.0 / (2.0 * Math.PI)

        dayMinutes = CInt(Math.Round(dayHours * 60.0))
        nightMinutes = 1440 - dayMinutes
    End Sub

    ''' <summary>
    ''' Erde-Sonne Distanz-Faktor in Astronomischen Einheiten  f=1AE/r^2
    ''' </summary>
    Public Function EarthSunDistanceFactor(jd As Double) As Double

        Dim ctx As SolarContext = BuildSolarContext(jd)
        Return ctx.DistFactor

    End Function

    ''' <summary>
    ''' Tagesgemittelte solare Einstrahlung am TOA (W/m²) für gegebene Breite und solare Deklination
    ''' </summary>
    Public Function ComputeDailyMeanInsolationTOA(latDeg As Double, declDeg As Double, Optional distanceFactor As Double = 1.0) As Double

        Dim meanWm2 As Double, energyJm2 As Double
        ComputeDailyInsolationTOA(latDeg, declDeg, distanceFactor, meanWm2, energyJm2)
        Return meanWm2
    End Function

    ''' <summary>
    ''' Tägliche Insolationsenergie am TOA (J/m²/Tag) für gegebene Breite und solare Deklination
    ''' </summary>
    Public Function ComputeDailyEnergyInsolationTOA(latDeg As Double, declDeg As Double, Optional distanceFactor As Double = 1.0) As Double

        Dim meanWm2 As Double, energyJm2 As Double
        ComputeDailyInsolationTOA(latDeg, declDeg, distanceFactor, meanWm2, energyJm2)
        Return energyJm2
    End Function

    ''' <summary>
    ''' Liefert Tagesmittel (W/m²) und Tagesenergie (J/m²/Tag) am TOA.
    ''' </summary>
    Public Sub ComputeDailyInsolationTOA(latDeg As Double, declDeg As Double, distanceFactor As Double,
                                         ByRef dailyMeanWm2 As Double,
                                         ByRef dailyEnergyJm2 As Double)

        Dim phi As Double = DegToRad(latDeg)
        Dim delta As Double = DegToRad(declDeg)

        Dim cosH0 As Double = -Math.Tan(phi) * Math.Tan(delta)

        Dim H0 As Double
        If cosH0 <= -1.0 Then       'Polartag
            H0 = Math.PI
        ElseIf cosH0 >= 1.0 Then    'Polarnacht
            dailyMeanWm2 = 0.0
            dailyEnergyJm2 = 0.0
            Return
        Else
            H0 = Math.Acos(Clamp(cosH0, -1.0, 1.0))
        End If

        Dim sinPhi As Double = Math.Sin(phi)
        Dim cosPhi As Double = Math.Cos(phi)
        Dim sinDel As Double = Math.Sin(delta)
        Dim cosDel As Double = Math.Cos(delta)

        Dim Q As Double =
            (ClimateConstants.SolarConstantWm2 * distanceFactor / Math.PI) *
            (H0 * sinPhi * sinDel + cosPhi * cosDel * Math.Sin(H0))

        If Q < 0.0 Then Q = 0.0

        dailyMeanWm2 = Q
        dailyEnergyJm2 = Q * SecondsPerDay

    End Sub

End Module
