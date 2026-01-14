Public Class UtcDateTimeDialog
    Inherits Window

    Public Sub New(initialUtc As DateTime)

        ' Dieser Aufruf ist für den Designer erforderlich.
        InitializeComponent()

        ' Fügen Sie Initialisierungen nach dem InitializeComponent()-Aufruf hinzu.
        DataContext = New UtcDateTimeDialogViewModel(Me, initialUtc)
    End Sub

    Public ReadOnly Property SelectedUtc As DateTime?
        Get
            Dim vm = TryCast(DataContext, UtcDateTimeDialogViewModel)
            Return vm?.ResultUtc
        End Get
    End Property

    Public Shared Function ShowDialogUtc(owner As Window, initialUtc As DateTime) As DateTime?
        Dim dlg As New UtcDateTimeDialog(initialUtc) With {
            .Owner = owner
        }

        Dim ok? As Boolean = (dlg.ShowDialog() = True)
        If Not ok Then Return Nothing
        Return dlg.SelectedUtc
    End Function
End Class
