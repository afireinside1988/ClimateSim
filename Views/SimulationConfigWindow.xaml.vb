Public Class SimulationConfigWindow

    Private _viewModel As SimulationConfigViewModel

    Private Sub SimulationConfigWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        _viewModel = TryCast(Me.DataContext, SimulationConfigViewModel)

        If _viewModel IsNot Nothing Then
            AddHandler _viewModel.OkRequested, AddressOf OnOkRequested
            AddHandler _viewModel.CancelRequested, AddressOf OnCancelRequested
        End If
    End Sub
    Private Sub SimulationConfigWindow_Unloaded(sender As Object, e As EventArgs) Handles Me.Closed
        If _viewModel IsNot Nothing Then
            RemoveHandler _viewModel.OkRequested, AddressOf OnOkRequested
            RemoveHandler _viewModel.CancelRequested, AddressOf OnCancelRequested
        End If
    End Sub

    Private Sub OnOkRequested(sender As Object, e As EventArgs)
        Me.DialogResult = True
        Me.Close()
    End Sub

    Private Sub OnCancelRequested(sender As Object, e As EventArgs)
        Me.DialogResult = False
        Me.Close()
    End Sub

End Class
