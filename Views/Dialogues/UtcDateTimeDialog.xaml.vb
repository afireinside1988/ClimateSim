Public Class UtcDateTimeDialog
    Inherits Window

    Public Sub New(initialUtc As DateTime, title As String)

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        DataContext = New UtcDateTimeDialogViewModel(Me, initialUtc, title)
    End Sub

    Public ReadOnly Property SelectedUtc As DateTime?
        Get
            Dim vm = TryCast(DataContext, UtcDateTimeDialogViewModel)
            Return vm?.ResultUtc
        End Get
    End Property

    Public Shared Function ShowDialogUtc(owner As Window, initialUtc As DateTime, title As String) As DateTime?
        Dim dlg As New UtcDateTimeDialog(initialUtc, title) With {
            .Owner = owner
        }

        Dim ok? As Boolean = (dlg.ShowDialog() = True)
        If Not ok Then Return Nothing
        Return dlg.SelectedUtc
    End Function
End Class
