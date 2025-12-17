Imports System.Threading
Imports System.Threading.Tasks

Public Module BusyRunner

    ''' <summary>
    ''' Führt Arbeit im Hintergrund aus und steuert Busy-Overlay via ViewModelBase.
    ''' </summary>
    ''' <param name="vm"></param>
    ''' <param name="title"></param>
    ''' <param name="work">wird in Task.Run ausgeführt; Progress läuft automatisch auf UI-Thread (wenn Progress hier im UI-Thread erstellt wird.</param>
    ''' <param name="canCancel"></param>
    ''' <returns></returns>
    Public Async Function RunAsync(vm As ViewModelBase, title As String,
                                   work As Action(Of IProgress(Of ProgressInfo), CancellationToken),
                                   Optional canCancel As Boolean = True,
                                   Optional showOverlay As Boolean = True) As Task

        ArgumentNullException.ThrowIfNull(vm)
        ArgumentNullException.ThrowIfNull(work)

        Dim cts As New CancellationTokenSource()

        vm.BusyTitle = title
        vm.BusyMessage = ""
        vm.BusyPercent = 0
        vm.BusyIsIndeterminate = True
        vm.BusyCanCancel = canCancel
        vm.BusyCancelAction = If(canCancel, Sub() cts.Cancel(), Nothing)
        vm.BusyShowOverlay = showOverlay
        vm.IsBusy = True

        Dim prog = New Progress(Of ProgressInfo)(
            Sub(p)
                vm.BusyMessage = p.Message

                If p.Percent < 0 Then
                    vm.BusyIsIndeterminate = True
                Else
                    vm.BusyIsIndeterminate = False
                    vm.BusyPercent = p.Percent
                End If

            End Sub)

        Try
            Await Task.Run(Sub() work(prog, cts.Token), cts.Token)
        Finally
            vm.IsBusy = False
            vm.BusyCancelAction = Nothing
            vm.BusyCanCancel = False
            vm.BusyIsIndeterminate = False
            vm.BusyShowOverlay = False
        End Try
    End Function

End Module
