Public Interface IEarthSurfaceProvider
    '''<summary>
    '''Liefert Oberflächeninformationen für eine gegebene Position (Breite/Länge in Grad)
    ''' </summary>
    Function GetSurfaceInfo(latitudeDeg As Double, longitudeDeg As Double) As SurfaceInfo

End Interface
