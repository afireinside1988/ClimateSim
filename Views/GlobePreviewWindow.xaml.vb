Public Class GlobePreviewWindow

    Public Sub New(payload As GlobePreviewPayload)

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        Me.DataContext = New GlobePreviewViewModel(payload)

        Dim vm = CType(DataContext, GlobePreviewViewModel)
        vm.AttachViewport(Vp)
    End Sub

    Private Sub GlobePreviewWindow_Closed(sender As Object, e As EventArgs) Handles Me.Closed
        Dim vm = TryCast(Me.DataContext, GlobePreviewViewModel)
        vm?.DisposeAnimation()
    End Sub
End Class
