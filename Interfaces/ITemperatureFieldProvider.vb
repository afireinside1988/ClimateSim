Public Interface ITemperatureFieldProvider

    '''<summary>
    '''Gibt eine Starttemperatur (z.B. Klimamittel) für die gegebene Position und das angegebene Startjahr zurück (in Kelvin)
    ''' </summary>
    ''' 
    Function GetInitialTemperatureK(latitudeDeg As Double, longitudeDeg As Double, year As Double) As Double

End Interface
