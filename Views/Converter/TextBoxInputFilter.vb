Imports System.Text.RegularExpressions
Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input

Public Class TextBoxInputFilter
    Inherits DependencyObject

    '--- Attached Property: Integer-Input ---
    Public Shared ReadOnly IsIntegerInputProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
            "IsIntegerInput",
            GetType(Boolean),
            GetType(TextBoxInputFilter),
            New PropertyMetadata(False, AddressOf OnIsIntegerInputChanged))

    Public Shared Sub SetIsIntegerInput(element As DependencyObject, value As Boolean)
        element.SetValue(IsIntegerInputProperty, value)
    End Sub

    Public Shared Function GetIsIntegerInput(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(IsIntegerInputProperty))
    End Function

    Private Shared Sub OnIsIntegerInputChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim tb As TextBox = TryCast(d, TextBox)
        If tb Is Nothing Then Return

        Dim isEnabled As Boolean = CBool(e.NewValue)

        If isEnabled Then
            AddHandler tb.PreviewTextInput, AddressOf OnIntegerPreviewTextInput
            DataObject.AddPastingHandler(tb, AddressOf OnIntegerPaste)
        Else
            RemoveHandler tb.PreviewTextInput, AddressOf OnIntegerPreviewTextInput
            DataObject.RemovePastingHandler(tb, AddressOf OnIntegerPaste)
        End If
    End Sub

    '--- Attached Property: NegativeInteger-Input ---
    Public Shared ReadOnly IsNegativeIntegerInputProperty As DependencyProperty =
    DependencyProperty.RegisterAttached(
        "IsNegativeIntegerInput",
        GetType(Boolean),
        GetType(TextBoxInputFilter),
        New PropertyMetadata(False, AddressOf OnIsNegativeIntegerInputChanged))

    Public Shared Sub SetIsNegativeIntegerInput(element As DependencyObject, value As Boolean)
        element.SetValue(IsNegativeIntegerInputProperty, value)
    End Sub

    Public Shared Function GetIsNegativeIntegerInput(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(IsNegativeIntegerInputProperty))
    End Function

    Private Shared Sub OnIsNegativeIntegerInputChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim tb As TextBox = TryCast(d, TextBox)
        If tb Is Nothing Then Return

        Dim isEnabled As Boolean = CBool(e.NewValue)

        If isEnabled Then
            AddHandler tb.PreviewTextInput, AddressOf OnNegativeIntegerPreviewTextInput
            DataObject.AddPastingHandler(tb, AddressOf OnNegativeIntegerPaste)
        Else
            RemoveHandler tb.PreviewTextInput, AddressOf OnNegativeIntegerPreviewTextInput
            DataObject.RemovePastingHandler(tb, AddressOf OnNegativeIntegerPaste)
        End If
    End Sub

    '--- Attached Property: Double-Input
    Public Shared ReadOnly IsDoubleInputProperty As DependencyProperty =
        DependencyProperty.RegisterAttached(
        "IsDoubleInput",
        GetType(Boolean),
        GetType(TextBoxInputFilter),
        New PropertyMetadata(False, AddressOf OnIsDoubleInputChanged))

    Public Shared Sub SetIsDoubleInput(element As DependencyObject, value As Boolean)
        element.SetValue(IsDoubleInputProperty, value)
    End Sub

    Public Shared Function GetIsDoubleInput(element As DependencyObject) As Boolean
        Return CBool(element.GetValue(IsDoubleInputProperty))
    End Function

    Private Shared Sub OnIsDoubleInputChanged(d As DependencyObject, e As DependencyPropertyChangedEventArgs)
        Dim tb As TextBox = TryCast(d, TextBox)
        If tb Is Nothing Then Return

        Dim isEnabled As Boolean = CBool(e.NewValue)

        If isEnabled Then
            AddHandler tb.PreviewTextInput, AddressOf OnDoublePreviewTextInput
            DataObject.AddPastingHandler(tb, AddressOf OnDoublePaste)
        Else
            RemoveHandler tb.PreviewTextInput, AddressOf OnDoublePreviewTextInput
            DataObject.RemovePastingHandler(tb, AddressOf OnDoublePaste)
        End If
    End Sub

    '--- Regexes
    Private Shared ReadOnly IntRegex As New Regex("^[0-9]*$", RegexOptions.Compiled)
    Private Shared ReadOnly DoubleRegex As New Regex("^[0-9]*([.,][0-9]*)?$", RegexOptions.Compiled)
    Private Shared ReadOnly NegIntRegex As New Regex("^-?[0-9]*$", RegexOptions.Compiled)

    '--- Integer: Tipp-Eingabe ---
    Private Shared Sub OnIntegerPreviewTextInput(sender As Object, e As TextCompositionEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        Dim proposed As String = GetProposedText(tb, e.Text)

        If Not IntRegex.IsMatch(proposed) Then
            e.Handled = True 'ungültiges Zeichen unterdrücken
        End If
    End Sub

    Private Shared Sub OnIntegerPaste(sender As Object, e As DataObjectPastingEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        If e.DataObject.GetDataPresent(DataFormats.Text) Then
            Dim text As String = CStr(e.DataObject.GetData(DataFormats.Text))
            If Not IntRegex.IsMatch(text) Then
                e.CancelCommand()
            End If
        Else
            e.CancelCommand()
        End If
    End Sub

    '--- NegativeInteger: Tipp-Eingabe ---
    Private Shared Sub OnNegativeIntegerPreviewTextInput(sender As Object, e As TextCompositionEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        Dim proposed As String = GetProposedText(tb, e.Text)

        If Not NegIntRegex.IsMatch(proposed) Then
            e.Handled = True 'ungültiges Zeichen unterdrücken
        End If
    End Sub

    Private Shared Sub OnNegativeIntegerPaste(sender As Object, e As DataObjectPastingEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        If e.DataObject.GetDataPresent(DataFormats.Text) Then
            Dim text As String = CStr(e.DataObject.GetData(DataFormats.Text))
            If Not NegIntRegex.IsMatch(text) Then
                e.CancelCommand()
            End If
        Else
            e.CancelCommand()
        End If
    End Sub

    '--- Double: Tipp-Eingabe
    Private Shared Sub OnDoublePreviewTextInput(sender As Object, e As TextCompositionEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        Dim proposed As String = GetProposedText(tb, e.Text)

        If Not DoubleRegex.IsMatch(proposed) Then
            e.Handled = True 'ungültiges Zeichen unterdrücken
        End If
    End Sub

    Private Shared Sub OnDoublePaste(sender As Object, e As DataObjectPastingEventArgs)
        Dim tb As TextBox = TryCast(sender, TextBox)
        If tb Is Nothing Then Return

        If e.DataObject.GetDataPresent(DataFormats.Text) Then
            Dim text As String = CStr(e.DataObject.GetData(DataFormats.Text))
            If Not DoubleRegex.IsMatch(text) Then
                e.CancelCommand()
            End If
        Else
            e.CancelCommand()
        End If
    End Sub

    '--- Helfer: simulierten Text nach der Eingabe berechnen ---
    Private Shared Function GetProposedText(tb As TextBox, newText As String) As String
        Dim text As String = tb.Text
        Dim selStart = tb.SelectionStart
        Dim selLength = tb.SelectionLength

        Return String.Concat(text.AsSpan(0, selStart), newText, text.AsSpan(selStart + selLength))
    End Function
End Class
