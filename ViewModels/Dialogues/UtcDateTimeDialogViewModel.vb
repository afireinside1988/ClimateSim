Imports System.CodeDom

Public Class UtcDateTimeDialogViewModel
    Inherits ViewModelBase

    Private ReadOnly _owner As Window
    Private ReadOnly _title As String
    Public Property Title As String
        Get
            Return _title
        End Get
        Set(value As String)
            SetProperty(_title, value)
        End Set
    End Property

    Private _selectedDate As DateTime?
    Public Property SelectedDate As DateTime?
        Get
            Return _selectedDate
        End Get
        Set(value As DateTime?)
            SetProperty(_selectedDate, value)
        End Set
    End Property

    Private _hours As Nullable(Of Integer)
    Public Property Hours As Nullable(Of Integer)
        Get
            Return _hours
        End Get
        Set(value As Nullable(Of Integer))
            SetProperty(_hours, value)
        End Set
    End Property

    Private _minutes As Nullable(Of Integer)
    Public Property Minutes As Nullable(Of Integer)
        Get
            Return _minutes
        End Get
        Set(value As Nullable(Of Integer))
            SetProperty(_minutes, value)
        End Set
    End Property

    Private _seconds As Nullable(Of Integer)
    Public Property Seconds As Nullable(Of Integer)
        Get
            Return _seconds
        End Get
        Set(value As Nullable(Of Integer))
            SetProperty(_seconds, value)
        End Set
    End Property

    Private _validationMessage As String
    Public Property ValidationMessage As String
        Get
            Return _validationMessage
        End Get
        Set(value As String)
            SetProperty(_validationMessage, value)
        End Set
    End Property

    Public Property ResultUtc As DateTime?

    Public ReadOnly Property OkCommand As ICommand
    Public ReadOnly Property CancelCommand As ICommand

    Public Sub New(owner As Window, initialUtc As DateTime, title As String)

        _owner = owner
        _title = title

        If initialUtc.Kind <> DateTimeKind.Utc Then
            initialUtc = DateTime.SpecifyKind(initialUtc, DateTimeKind.Utc)
        End If

        SelectedDate = initialUtc.Date
        Hours = initialUtc.Hour
        Minutes = initialUtc.Minute
        Seconds = initialUtc.Second

        OkCommand = New RelayCommand(Of Object)(Sub(o) OnOk())
        CancelCommand = New RelayCommand(Of Object)(Sub(o) OnCancel())
    End Sub

    Private Sub OnCancel()
        ResultUtc = Nothing
        _owner.DialogResult = False
        _owner.Close()
    End Sub

    Private Sub OnOk()
        ValidationMessage = ""

        If Not SelectedDate.HasValue Then
            ValidationMessage = "Bitte ein Datum auswählen"
        End If

        If Hours < 0 OrElse Hours > 23 OrElse Hours Is Nothing Then
            ValidationMessage = "Die Stunden müssen zwischen 00 und 23 liegen."
            Return
        End If
        If Minutes < 0 OrElse Minutes > 59 OrElse Minutes Is Nothing Then
            ValidationMessage = "Die Minuten müssen zwischen 00 und 59 liegen."
            Return
        End If
        If Seconds < 0 OrElse Seconds > 59 OrElse Seconds Is Nothing Then
            ValidationMessage = "Die Sekunden müssen zwischen 00 und 59 liegen."
            Return
        End If


        Dim d As Date = SelectedDate.Value.Date
        Dim utc As DateTime = New DateTime(d.Year, d.Month, d.Day, CInt(Hours), CInt(Minutes), CInt(Seconds), DateTimeKind.Utc)

        ResultUtc = utc
        _owner.DialogResult = True
        _owner.Close()
    End Sub
End Class
