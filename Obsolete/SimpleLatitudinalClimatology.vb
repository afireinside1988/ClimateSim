Public Class SimpleLatitudinalClimatology
    Implements ITemperatureFieldProvider

    'Referenzwerte für vorindustriell (ca. 1850)
    Public Property EquatorTempK As Double = 303.0 '30°C
    Public Property PoleTempK As Double = 258.0 '-15°C
    Public Property ReferenceYear As Double = 1850.0
    Public Function GetInitialTemperatureK(latitudeDeg As Double, longitudeDeg As Double, year As Double) As Double Implements ITemperatureFieldProvider.GetInitialTemperatureK

        'Aktuell ignorieren wir "year" und liefern ein konstantes Klimamittel basierend auf einem cos²-Breitenprofil
        Dim latRad As Double = latitudeDeg * Math.PI / 180
        Dim weight As Double = Math.Pow(Math.Cos(latRad), 2)

        Dim Tref As Double = PoleTempK + (EquatorTempK - PoleTempK) * weight

        Return Tref

    End Function
End Class
