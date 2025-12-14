Imports System.Globalization
Imports System.Windows.Controls
Imports System.Media

Public Class SimulationConfigWindow

    Private _viewModel As SimulationConfigViewModel

#Region "Konstruktor"

    Public Sub New()

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

    End Sub

#End Region

#Region "Events"
    Private Sub SimulationConfigWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        _viewModel = TryCast(Me.DataContext, SimulationConfigViewModel)

        If _viewModel IsNot Nothing Then
            AddHandler _viewModel.OkRequested, AddressOf OnOkRequested
            AddHandler _viewModel.CancelRequested, AddressOf OnCancelRequested
            AddHandler _viewModel.FocusFirstErrorRequested, AddressOf OnFocusFirstErrorRequested
        End If

        Debug.WriteLine($"Mainkultur: {CultureInfo.CurrentCulture}")
        Debug.WriteLine($"UI-Kultur: {CultureInfo.CurrentUICulture}")
        Debug.WriteLine($"Language: {Language}")
    End Sub
    Private Sub SimulationConfigWindow_Unloaded(sender As Object, e As EventArgs) Handles Me.Closed
        If _viewModel IsNot Nothing Then
            RemoveHandler _viewModel.OkRequested, AddressOf OnOkRequested
            RemoveHandler _viewModel.CancelRequested, AddressOf OnCancelRequested
            RemoveHandler _viewModel.FocusFirstErrorRequested, AddressOf OnFocusFirstErrorRequested
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

    Private Sub OnFocusFirstErrorRequested(sender As Object, e As EventArgs)
        Dim first As TextBox = FindFirstInvalidTextBox(Me)
        If first IsNot Nothing Then
            first.Focus()
            first.SelectAll()
        End If
    End Sub

#End Region

    Private Shared Function FindFirstInvalidTextBox(root As DependencyObject) As TextBox
        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(root) - 1
            Dim child As DependencyObject = VisualTreeHelper.GetChild(root, i)

            Dim tb As TextBox = TryCast(child, TextBox)
            If tb IsNot Nothing AndAlso Validation.GetHasError(tb) Then
                Return tb
            End If

            Dim nested As TextBox = FindFirstInvalidTextBox(child)
            If nested IsNot Nothing Then Return nested
        Next
        Return Nothing
    End Function

End Class
