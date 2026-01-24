Public Class LandCoverSmokeTestWindow

    Public Sub New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        DataContext = New LandCoverImporterSmokeTestVM(Me)
    End Sub
End Class
