Public Class GlobePreviewWindow

    Public Sub New(payload As GlobePreviewPayload)

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        Me.DataContext = New GlobePreviewViewModel(payload)
    End Sub

End Class
