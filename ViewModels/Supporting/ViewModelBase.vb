Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Collections.Generic

Public MustInherit Class ViewModelBase
    Implements INotifyPropertyChanged

#Region "Implementation von INotifyPropertyChanged"

    Public Event PropertyChanged As PropertyChangedEventHandler _
        Implements INotifyPropertyChanged.PropertyChanged

    Protected Overridable Sub OnPropertyChanged(
        <CallerMemberName> Optional propertyName As String = Nothing)

        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Protected Function SetProperty(Of T)(
        ByRef storage As T,
        value As T,
        <CallerMemberName> Optional propertyName As String = Nothing) As Boolean

        If EqualityComparer(Of T).Default.Equals(storage, value) Then
            Return False
        End If

        storage = value
        OnPropertyChanged(propertyName)
        Return True
    End Function

#End Region

#Region "Progress-Verhalten"

    Private _isBusy As Boolean
    Private _busyTitle As String = "Bitte warten..."
    Private _busyMessage As String = ""
    Private _busyPercent As Integer = 0
    Private _busyIsIndeterminate As Boolean = False
    Private _busyCanCancel As Boolean = False

    Private _cancelBusyCommand As ICommand

    'wird vom BusyRunner gesetzt
    Friend Property BusyCancelAction As Action

    Public Property IsBusy As Boolean
        Get
            Return _isBusy
        End Get
        Set(value As Boolean)
            SetProperty(_isBusy, value)
        End Set
    End Property
    Public Property BusyTitle As String
        Get
            Return _busyTitle
        End Get
        Set(value As String)
            SetProperty(_busyTitle, value)
        End Set
    End Property
    Public Property BusyMessage As String
        Get
            Return _busyMessage
        End Get
        Set(value As String)
            SetProperty(_busyMessage, value)
        End Set
    End Property
    Public Property BusyPercent As Integer
        Get
            Return _busyPercent
        End Get
        Set(value As Integer)
            SetProperty(_busyPercent, value)
        End Set
    End Property
    Public Property BusyIsIndeterminate As Boolean
        Get
            Return _busyIsIndeterminate
        End Get
        Set(value As Boolean)
            SetProperty(_busyIsIndeterminate, value)
        End Set
    End Property
    Public Property BusyCanCancel As Boolean
        Get
            Return _busyCanCancel
        End Get
        Set(value As Boolean)
            SetProperty(_busyCanCancel, value)
        End Set
    End Property

    Public ReadOnly Property CancelBusyCommand As ICommand
        Get
            If _cancelBusyCommand Is Nothing Then
                _cancelBusyCommand = New RelayCommand(Of Object)(
                    Sub(o)
                        BusyCancelAction?.Invoke()
                    End Sub,
                    Function(o) BusyCanCancel
                    )
            End If
            Return _cancelBusyCommand
        End Get
    End Property
#End Region

End Class
