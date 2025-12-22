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

        PrepareBusy(vm, title, canCancel, showOverlay, cts)

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
            CleanupBusy(vm)
        End Try
    End Function

    Public Async Function RunAsync(Of T)(vm As ViewModelBase, title As String,
                                         work As Func(Of IProgress(Of ProgressInfo), CancellationToken, Task(Of T)),
                                         Optional canCancel As Boolean = True,
                                         Optional showOverlay As Boolean = True,
                                         Optional runInBackground As Boolean = True) As Task(Of T)

        ArgumentNullException.ThrowIfNull(vm)
        ArgumentNullException.ThrowIfNull(work)

        Dim cts As New CancellationTokenSource()

        PrepareBusy(vm, title, canCancel, showOverlay, cts)

        Dim prog As IProgress(Of ProgressInfo) = CreateUiProgress(vm)

        Try
            If runInBackground Then
                'Für CPU-lastige async work: komplett in Task.Run kapseln
                Return Await Task.Run(Async Function()
                                          Return Await work(prog, cts.Token)
                                      End Function, cts.Token)
            Else
                'Für echtes async I/O: ohne Task.Run (besser für Skalierung)
                Return Await work(prog, cts.Token)
            End If
        Finally
            CleanupBusy(vm)
        End Try
    End Function

    Public Async Function RunAsync(Of T)(
    vm As ViewModelBase,
    title As String,
    work As Func(Of IProgress(Of ProgressInfo), CancellationToken, T),
    Optional canCancel As Boolean = True,
    Optional showOverlay As Boolean = True
) As Task(Of T)

        ArgumentNullException.ThrowIfNull(vm)
        ArgumentNullException.ThrowIfNull(work)

        Dim cts As New CancellationTokenSource()
        PrepareBusy(vm, title, canCancel, showOverlay, cts)
        Dim prog As IProgress(Of ProgressInfo) = CreateUiProgress(vm)

        Try
            Return Await Task.Run(Function() work(prog, cts.Token), cts.Token)
        Finally
            CleanupBusy(vm)
        End Try
    End Function

#Region "Helper"

    Private Sub PrepareBusy(vm As ViewModelBase, title As String, canCancel As Boolean, showOverlay As Boolean, cts As CancellationTokenSource)
        vm.BusyTitle = title
        vm.BusyMessage = ""
        vm.BusyPercent = 0
        vm.BusyIsIndeterminate = True
        vm.BusyCanCancel = canCancel
        vm.BusyCancelAction = If(canCancel, Sub() cts.Cancel(), Nothing)
        vm.BusyShowOverlay = showOverlay
        vm.IsBusy = True
    End Sub

    Private Function CreateUiProgress(vm As ViewModelBase) As IProgress(Of ProgressInfo)
        'Wichtig: Progress wird im UI-Thread erzeugt -> Updates landen automatisch auf UI
        Return New Progress(Of ProgressInfo)(
            Sub(p)
                vm.BusyMessage = p.Message

                If p.Percent < 0 Then
                    vm.BusyIsIndeterminate = True
                Else
                    vm.BusyIsIndeterminate = False
                    vm.BusyPercent = p.Percent
                End If
            End Sub)
    End Function

    Private Sub CleanupBusy(vm As ViewModelBase)
        vm.IsBusy = False
        vm.BusyCancelAction = Nothing
        vm.BusyCanCancel = False
        vm.BusyIsIndeterminate = False
        vm.BusyShowOverlay = False
    End Sub

#End Region
End Module
