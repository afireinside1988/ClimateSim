Public Class UtcDateTimeDialogViewModel
    Inherits ViewModelBase

    Private ReadOnly _owner As Window

    Private _selectedDate As DateTime?
    Public Property SelectedDate As DateTime?
        Get
            Return _selectedDate
        End Get
        Set(value As DateTime?)
            SetProperty(_selectedDate, value)
        End Set
    End Property

    Private _hourText As String
    Public Property HourText As String
        Get
            Return _hourText
        End Get
        Set(value As String)
            SetProperty(_hourText, value)
        End Set
    End Property

    Private _minuteText As String
    Public Property MinuteText As String
        Get
            Return _minuteText
        End Get
        Set(value As String)
            SetProperty(_minuteText, value)
        End Set
    End Property

    Private _secondText As String
    Public Property SecondText As String
        Get
            Return _secondText
        End Get
        Set(value As String)
            SetProperty(_secondText, value)
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

    Public Sub New(owner As Window, initialUtc As DateTime)

        _owner = owner

        If initialUtc.Kind <> DateTimeKind.Utc Then
            initialUtc = DateTime.SpecifyKind(initialUtc, DateTimeKind.Utc)
        End If

        SelectedDate = initialUtc.Date
        HourText = initialUtc.Hour.ToString("00")
        MinuteText = initialUtc.Minute.ToString("00")
        SecondText = initialUtc.Second.ToString("00")

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

        Dim hh As Integer, mm As Integer, ss As Integer

        If Not Integer.TryParse(HourText, hh) OrElse hh < 0 OrElse hh > 23 Then
            ValidationMessage = "Stunde muss zwischen 00 und 23 sein."
            Return
        End If
        If Not Integer.TryParse(MinuteText, mm) OrElse mm < 0 OrElse mm > 59 Then
            ValidationMessage = "Minute muss zwischen 00 und 59 sein."
            Return
        End If
        If Not Integer.TryParse(SecondText, ss) OrElse ss < 0 OrElse ss > 59 Then
            ValidationMessage = "Sekunde muss zwischen 00 und 59 sein."
            Return
        End If

        Dim d As Date = SelectedDate.Value.Date
        Dim utc As DateTime = New DateTime(d.Year, d.Month, d.Day, hh, mm, ss, DateTimeKind.Utc)

        ResultUtc = utc
        _owner.DialogResult = True
        _owner.Close()
    End Sub
End Class
