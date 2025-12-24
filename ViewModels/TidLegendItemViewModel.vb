Public NotInheritable Class TidLegendItemViewModel
    Inherits ViewModelBase

    Private _tidCode As Integer
    Public Property TidCode As Integer
        Get
            Return _tidCode
        End Get
        Set(value As Integer)
            SetProperty(_tidCode, value)
        End Set
    End Property

    Private _tidText As String
    Public Property TidText As String
        Get
            Return _tidText
        End Get
        Set(value As String)
            SetProperty(_tidText, value)
        End Set
    End Property

    Private _tidTitle As String
    Public Property TidTitle As String
        Get
            Return _tidTitle
        End Get
        Set(value As String)
            SetProperty(_tidTitle, value)
        End Set
    End Property

    Private _tidDescription As String
    Public Property TidDescription As String
        Get
            Return _tidDescription
        End Get
        Set(value As String)
            SetProperty(_tidDescription, value)
        End Set
    End Property

    Private _tidGroup As String
    Public Property TidGroup As String
        Get
            Return _tidGroup
        End Get
        Set(value As String)
            SetProperty(_tidGroup, value)
        End Set
    End Property

    Private _tidColor As Color
    Public Property TidColor As Color
        Get
            Return _tidColor
        End Get
        Set(value As Color)
            SetProperty(_tidColor, value)
        End Set
    End Property


    Public ReadOnly Property Brush As SolidColorBrush
        Get
            Dim b As New SolidColorBrush(TidColor)
            b.Freeze()
            Return b
        End Get
    End Property

    Private Shared ReadOnly separator As Char() = New Char() {":"c}

    Public Sub New()

    End Sub

    Public Sub New(tidCode As Integer, tidText As String, tidGroup As String, tidColor As Color)
        Me.TidCode = tidCode
        Me.TidText = tidText
        Me.TidGroup = tidGroup
        Me.TidColor = tidColor

        'Erwartet: "TID 10: Einzelstrahl-Echolot..."
        Dim parts As String() = tidText.Split(separator, 2)

        If parts.Length = 2 Then
            TidTitle = parts(0).Trim()
            TidDescription = parts(1).Trim()
        Else
            TidTitle = tidText
            TidDescription = String.Empty
        End If
    End Sub
End Class
