Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports Microsoft.Xaml.Behaviors

#Disable Warning CA1416 'Plattformkompatibilität überprüfen

Public Class NumericTimeTextBehavior
    Inherits Behavior(Of TextBox)

    Public Enum NumericTimeMode
        Year
        Hour
        MinuteSecond
    End Enum
#Region "Mode"
    Public Property Mode As NumericTimeMode
        Get
            Return CType(GetValue(ModeProperty), NumericTimeMode)
        End Get
        Set(value As NumericTimeMode)
            SetValue(ModeProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ModeProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(Mode),
            GetType(NumericTimeMode),
            GetType(NumericTimeTextBehavior),
            New PropertyMetadata(NumericTimeMode.MinuteSecond))

#End Region

#Region "Value (Integer?)"

    Public Property Value As Nullable(Of Integer)
        Get
            Return CType(GetValue(ValueProperty), Nullable(Of Integer))
        End Get
        Set(value As Nullable(Of Integer))
            SetValue(ValueProperty, value)
        End Set
    End Property
    Public Shared ReadOnly ValueProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(Value),
            GetType(Nullable(Of Integer)),
            GetType(NumericTimeTextBehavior),
            New FrameworkPropertyMetadata(
                Nothing,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                AddressOf OnValueChanged,
                AddressOf CoerceValueCallback))

    Private Shared Sub OnValueChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)

        Dim b As NumericTimeTextBehavior = TryCast(d, NumericTimeTextBehavior)
        If b Is Nothing Then Return
        b.SyncTextFromValue()

    End Sub

    Private Shared Function CoerceValueCallback(d As DependencyObject, baseValue As Object) As Object

        Dim b As NumericTimeTextBehavior = TryCast(d, NumericTimeTextBehavior)
        If b Is Nothing Then Return baseValue

        Dim v As Nullable(Of Integer) = Nothing
        If TypeOf baseValue Is Nullable(Of Integer) Then
            v = CType(baseValue, Nullable(Of Integer))
        ElseIf TypeOf baseValue Is Integer Then
            v = CType(baseValue, Integer)
        End If

        If Not v.HasValue Then Return Nothing

        If Not b.IsWithinRange(v.Value) Then
            'Wenn von außen ein invalides Value gesetzt wird, setzen wir es auf Nothing
            Return Nothing
        End If

        Return v
    End Function
#End Region

#Region "Options"

    ''' <summary>
    ''' Gibt an, ob beim LostFocus-Event die Ziffern aufgefüllt werden sollen.
    ''' </summary>
    Public Property PadOnLostFocus As Boolean
        Get
            Return CBool(GetValue(PadOnLostFocusProperty))
        End Get
        Set(value As Boolean)
            SetValue(PadOnLostFocusProperty, value)
        End Set
    End Property
    Public Shared ReadOnly PadOnLostFocusProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(PadOnLostFocus),
            GetType(Boolean),
            GetType(NumericTimeTextBehavior),
            New PropertyMetadata(False))

    ''' <summary>
    ''' Gibt an, ob eine leere Eingabe akzeptiert wird
    ''' </summary>
    Public Property AllowEmpty As Boolean
        Get
            Return CBool(GetValue(AllowEmptyProperty))
        End Get
        Set(value As Boolean)
            SetValue(AllowEmptyProperty, value)
        End Set
    End Property
    Public Shared ReadOnly AllowEmptyProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(AllowEmpty),
            GetType(Boolean),
            GetType(NumericTimeTextBehavior),
            New PropertyMetadata(False))

    ''' <summary>
    ''' Gibt an, ob der gesamte Inhalt markiert wird, wenn die Textbox den Fokus erhält
    ''' </summary>
    Public Property SelectAllOnFocus As Boolean
        Get
            Return CBool(GetValue(SelectAllOnFocusProperty))
        End Get
        Set(value As Boolean)
            SetValue(SelectAllOnFocusProperty, value)
        End Set
    End Property
    Public Shared ReadOnly SelectAllOnFocusProperty As DependencyProperty =
        DependencyProperty.Register(
            NameOf(SelectAllOnFocus),
            GetType(Boolean),
            GetType(NumericTimeTextBehavior),
            New PropertyMetadata(True))

#End Region

    Private _isInternalUpdate As Boolean = False

    Protected Overrides Sub OnAttached()
        MyBase.OnAttached()

        AddHandler AssociatedObject.PreviewTextInput, AddressOf OnPreviewTextInput
        AddHandler AssociatedObject.PreviewKeyDown, AddressOf OnPreviewKeyDown
        DataObject.AddPastingHandler(AssociatedObject, New DataObjectPastingEventHandler(AddressOf OnPaste))
        AddHandler AssociatedObject.LostFocus, AddressOf OnLostFocus

        'Text -> Value sync
        AddHandler AssociatedObject.TextChanged, AddressOf OnTextChanged

        'SelectAll on Focus
        AddHandler AssociatedObject.GotKeyboardFocus, AddressOf OnGotKeyboardFocus
        AddHandler AssociatedObject.PreviewMouseLeftButtonDown, AddressOf OnPreviewMouseLeftButtonDown

        'IME ausschalten
        InputMethod.SetIsInputMethodEnabled(AssociatedObject, False)

        'Initial: falls Value gesetzt ist, Text setzen; sonst optional Text -> Value
        SyncValueFromText()
        SyncTextFromValue()
    End Sub

    Protected Overrides Sub OnDetaching()
        RemoveHandler AssociatedObject.PreviewTextInput, AddressOf OnPreviewTextInput
        RemoveHandler AssociatedObject.PreviewKeyDown, AddressOf OnPreviewKeyDown
        DataObject.RemovePastingHandler(AssociatedObject, New DataObjectPastingEventHandler(AddressOf OnPaste))
        RemoveHandler AssociatedObject.LostFocus, AddressOf OnLostFocus

        RemoveHandler AssociatedObject.TextChanged, AddressOf OnTextChanged
        RemoveHandler AssociatedObject.GotKeyboardFocus, AddressOf OnGotKeyboardFocus
        RemoveHandler AssociatedObject.PreviewMouseLeftButtonDown, AddressOf OnPreviewMouseLeftButtonDown

        MyBase.OnDetaching()
    End Sub

#Region "Select All"

    Private Sub OnGotKeyboardFocus(sender As Object, e As KeyboardFocusChangedEventArgs)

        If Not SelectAllOnFocus Then Return
        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        tb.SelectAll()

    End Sub

    Private Sub OnPreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)

        If Not SelectAllOnFocus Then Return
        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        'Wenn noch kein Fokus: Fokus setzen und Klick "schlucken", damit nicht der Caret irgendwohin springt
        If Not tb.IsKeyboardFocusWithin Then
            e.Handled = True
            tb.Focus()
            tb.SelectAll()
        End If

    End Sub

#End Region

#Region "Input-Filtering"

    Private Sub OnPreviewKeyDown(sender As Object, e As KeyEventArgs)
        'Blockiere Space
        If e.Key = Key.Space Then e.Handled = True
    End Sub

    Private Sub OnPreviewTextInput(sender As Object, e As TextCompositionEventArgs)
        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        'Nur Ziffern erlauben
        If Not Regex.IsMatch(e.Text, "^\d+$") Then
            e.Handled = True
            Return
        End If

        Dim candidate = BuildCandidateText(tb, e.Text)
        If Not IsValidCandidate(candidate) Then
            e.Handled = True
        End If
    End Sub

    Private Sub OnPaste(sneder As Object, e As DataObjectPastingEventArgs)
        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        If Not e.SourceDataObject.GetDataPresent(DataFormats.Text, True) Then
            e.CancelCommand()
            Return
        End If

        Dim pasteText As String = TryCast(e.SourceDataObject.GetData(DataFormats.Text, True), String)
        If pasteText Is Nothing Then
            e.CancelCommand()
            Return
        End If

        pasteText = pasteText.Trim()

        'Nur Ziffern
        If Not Regex.IsMatch(pasteText, "^\d*$") Then
            e.CancelCommand()
            Return
        End If

        Dim candidate = BuildCandidateText(tb, pasteText)
        If Not IsValidCandidate(candidate) Then
            e.CancelCommand()
        End If
    End Sub

#End Region

#Region "LostFocus Padding"

    Private Sub OnLostFocus(sender As Object, e As RoutedEventArgs)
        If Not PadOnLostFocus Then Return

        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        Dim t As String = If(tb.Text, "").Trim()

        If t.Length = 0 Then
            If Not AllowEmpty Then
                tb.Text = DefaultTextForMode()
            End If
            Return
        End If

        'Validieren und ggf. pad
        If Not IsValidCandidate(t) Then
            'Wenn der Inhalt invalide ist (z.b. per Binding gekommen), setze auf Default
            tb.Text = DefaultTextForMode()
            Return
        End If

        Select Case Mode
            Case NumericTimeMode.Year
                'Kein Padding mehr, sieht unschön aus
                'tb.Text = t.PadLeft(4, "0"c)
            Case NumericTimeMode.Hour, NumericTimeMode.MinuteSecond
                tb.Text = t.PadLeft(2, "0"c)
        End Select
    End Sub

#End Region

#Region "Text <-> Value Sync"

    Private Sub OnTextChanged(sender As Object, e As TextChangedEventArgs)

        If _isInternalUpdate Then Return
        SyncValueFromText()

    End Sub

    Private Sub SyncValueFromText()

        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return

        Dim t As String = (If(tb.Text, "")).Trim()

        If t.Length = 0 Then
            If AllowEmpty Then
                setValueSafely(Nothing)
            Else
                SetValueSafely(ParseOrNothing(DefaultTextForMode()))
            End If
        End If

        If Not IsValidCandidate(t) Then
            SetValueSafely(Nothing)
            Return
        End If

        Dim v = ParseOrNothing(t)
        If v.HasValue AndAlso IsWithinRange(v.Value) Then
            SetValueSafely(v)
        Else
            SetValueSafely(Nothing)
        End If
    End Sub

    Private Sub SyncTextFromValue()

        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return
        If _isInternalUpdate Then Return

        Dim v As Integer? = Value
        If Not v.HasValue Then
            If AllowEmpty Then
                SetTextSafely("")
            Else
                SetTextSafely(DefaultTextForMode())
            End If
            Return
        End If

        If Not IsWithinRange(v.Value) Then
            If AllowEmpty Then
                SetTextSafely("")
            Else
                SetTextSafely(DefaultTextForMode())
            End If
            Return
        End If

        Dim formatted As String
        Select Case Mode
            Case NumericTimeMode.Year
                formatted = v.Value.ToString()

            Case NumericTimeMode.Hour, NumericTimeMode.MinuteSecond
                formatted = v.Value.ToString("00")

            Case Else
                formatted = v.Value.ToString()
        End Select

        SetTextSafely(formatted)
    End Sub

    Private Sub SetValueSafely(v As Nullable(Of Integer))
        Try
            _isInternalUpdate = True
            SetCurrentValue(ValueProperty, v)
        Finally
            _isInternalUpdate = False
        End Try
    End Sub

    Private Sub SetTextSafely(text As String)

        Dim tb As TextBox = AssociatedObject
        If tb Is Nothing Then Return
        Try
            _isInternalUpdate = True
            tb.Text = text
        Finally
            _isInternalUpdate = False
        End Try
    End Sub

#End Region

#Region "Validation Helper"

    Private Function DefaultTextForMode() As String
        Select Case Mode
            Case NumericTimeMode.Year
                Return "0000"
            Case Else
                Return "00"
        End Select
    End Function

    Private Shared Function ParseOrNothing(s As String) As Nullable(Of Integer)

        Dim val As Integer
        If Integer.TryParse(s, val) Then Return val
        Return Nothing

    End Function

    Private Shared Function BuildCandidateText(tb As TextBox, insertText As String) As String

        Dim text As String = If(tb.Text, "")
        Dim selStart As Integer = tb.SelectionStart
        Dim selLen As Integer = tb.SelectionLength

        If selLen > 0 Then
            text = text.Remove(selStart, selLen)
        End If

        If selStart < 0 OrElse selStart > text.Length Then
            selStart = text.Length
        End If

        text = text.Insert(selStart, insertText)
        Return text

    End Function

    Private Function IsValidCandidate(candidate As String) As Boolean

        candidate = If(candidate, "").Trim()

        If candidate.Length = 0 Then
            Return AllowEmpty
        End If

        'Nur Ziffern
        If Not Regex.IsMatch(candidate, "^\d+$") Then Return False

        Select Case Mode
            Case NumericTimeMode.Year
                '0000..9999, max 4 Ziffern (partial erlaubt: 1..4 Ziffern)
                If candidate.Length > 4 Then Return False
                'Zahlbereich ist durch 0..9999 + maxLen implizit ok
                Return True

            Case NumericTimeMode.Hour
                '00..23, partial erlaubt
                If candidate.Length > 2 Then Return False

                If candidate.Length = 1 Then
                    'erste Ziffer 0..2
                    Dim d0 = AscW(candidate(0)) - AscW("0"c)
                    Return d0 >= 0 AndAlso d0 <= 2
                Else
                    Dim val As Integer
                    If Not Integer.TryParse(candidate, val) Then Return False
                    Return val >= 0 AndAlso val <= 23
                End If

            Case NumericTimeMode.MinuteSecond
                '0..59, partial erlaubt
                If candidate.Length > 2 Then Return False

                If candidate.Length = 1 Then
                    'erste Ziffer 0..5
                    Dim d0 = AscW(candidate(0)) - AscW("0"c)
                    Return d0 >= 0 AndAlso d0 <= 5
                Else
                    Dim val As Integer
                    If Not Integer.TryParse(candidate, val) Then Return False
                    Return val >= 0 AndAlso val <= 59
                End If

            Case Else
                Return False

        End Select
    End Function

    Private Function IsWithinRange(v As Integer) As Boolean

        Select Case Mode
            Case NumericTimeMode.Year
                Return v >= 0 AndAlso v <= 9999

            Case NumericTimeMode.Hour
                Return v >= 0 AndAlso v <= 23

            Case NumericTimeMode.MinuteSecond
                Return v >= 0 AndAlso v <= 59

            Case Else
                Return False
        End Select

    End Function

#End Region

End Class

#Enable Warning CA1416 'Plattformkompatibilität überprüfen