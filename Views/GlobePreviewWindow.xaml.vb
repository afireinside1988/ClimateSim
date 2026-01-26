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

    Private Sub GlobePreviewWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim vm = TryCast(Me.DataContext, GlobePreviewViewModel)
        If vm Is Nothing Then Return

        AddHandler vm.RequestSetUtc, Sub()

                                         Dim initialUtc = If(vm.SimulationUtc = DateTime.MinValue, DateTime.UtcNow, vm.SimulationUtc)
                                         Dim picked As Date? = UtcDateTimeDialog.ShowDialogUtc(Me, initialUtc, "Simulationszeit (UTC)")
                                         If picked.HasValue Then
                                             vm.SetSimulationUtc(picked.Value)
                                         End If
                                     End Sub
    End Sub
End Class
