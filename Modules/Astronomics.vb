Module Astronomics

    ''' <summary>
    ''' Errechnet aus der UTC das julianische Datum
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
    ''' Errechnet aus dem julianischen Datum die Rektaszension und Deklination der Sonne
    ''' </summary>
    Public Function SunRaDecDeg(jd As Double, ByRef raDeg As Double, ByRef decDeg As Double) As Boolean
        'Sehr gängige Näherung: mittlere Länge + Gleichung des Zentrums (genug für Tag/Nacht)
        Dim T As Double = (jd - 2451545.0) / 36525.0

        'Durchschnittliche Longitude L0 und Anomalie M
        Dim L0 As Double = 280.46646 + 36000.76983 * T + 0.0003032 * T * T
        Dim M As Double = 357.52911 + 35999.05029 * T - 0.0001537 * T * T

        L0 = Wrap360(L0)
        M = Wrap360(M)

        Dim Mrad = DegToRad(M)

        'Mittelpunktgleichung C
        Dim C As Double =
            (1.914602 - 0.004817 * T - 0.000014 * T * T) * Math.Sin(Mrad) +
            (0.019993 - 0.000101 * T) * Math.Sin(2 * Mrad) +
            0.000289 * Math.Sin(3 * Mrad)

        'Wahre ekliptische Longitude
        Dim lambda As Double = L0 + C
        lambda = Wrap360(lambda)

        'Durchschnittliche Neigung + Korrektur
        Dim eps0 As Double = 23.439291 - 0.0130042 * T

        Dim epsRad As Double = DegToRad(eps0)
        Dim lamRad As Double = DegToRad(lambda)

        'Ekliptik->Äquatorial
        Dim sinLam As Double = Math.Sin(lamRad)
        Dim cosLam As Double = Math.Cos(lamRad)

        Dim x As Double = cosLam
        Dim y As Double = Math.Cos(epsRad) * sinLam
        Dim z As Double = Math.Sin(epsRad) * sinLam

        'RA = ATan2(y, x); Dec = ASin(z)
        Dim ra As Double = Math.Atan2(y, x)
        Dim dec As Double = Math.Asin(Clamp(z, -1.0, 1.0))

        raDeg = Wrap360(RadToDeg(ra))
        decDeg = RadToDeg(dec)

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

        Return Wrap360(gmst)
    End Function

    Public Function EquationOfTimeMinutes(jd As Double) As Double

        Dim T As Double = (jd - 2451545.0) / 36525.0

        Dim L0 As Double = Wrap360(280.46646 + 36000.76983 * T + 0.0003032 * T * T)
        Dim M As Double = Wrap360(357.52911 + 35999.05029 * T - 0.0001537 * T * T)

        Dim e As Double = 0.016708634 - 0.000042037 * T - 0.0000001267 * T * T
        Dim eps As Double = DegToRad(23.439291 - 0.0130042 * T)

        Dim y As Double = Math.Tan(eps / 2.0)
        y *= y

        Dim L0r As Double = DegToRad(L0)
        Dim Mr As Double = DegToRad(M)

        Dim eE As Double =
        y * Math.Sin(2 * L0r) -
        2 * e * Math.Sin(Mr) +
        4 * e * y * Math.Sin(Mr) * Math.Cos(2 * L0r) -
        0.5 * y * y * Math.Sin(4 * L0r) -
        1.25 * e * e * Math.Sin(2 * Mr)

        'E in RAD -> Minuten
        Return RadToDeg(eE) * 4.0

    End Function
End Module
