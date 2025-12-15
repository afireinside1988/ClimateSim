Imports System.Collections
Imports System.Globalization
Imports System.Text
Imports System.Windows.Controls
Imports System.Windows.Data

Public Class AllValidationErrorsConverter
    Implements IValueConverter
    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim list As IList = TryCast(value, IList)
        If list Is Nothing OrElse list.Count = 0 Then Return Nothing

        Dim sb As New StringBuilder()

        For i As Integer = 0 To list.Count - 1
            Dim ve As ValidationError = TryCast(list(i), ValidationError)
            If ve Is Nothing Then Continue For

            Dim msg As String = TryCast(ve.ErrorContent, String)
            If String.IsNullOrWhiteSpace(msg) Then
                msg = ve.ErrorContent?.ToString()
            End If
            If String.IsNullOrWhiteSpace(msg) Then Continue For

            If sb.Length > 0 Then sb.AppendLine()
            sb.Append(msg.Trim())
        Next

        Dim result As String = sb.ToString()
        If result.Length = 0 Then Return Nothing
        Return result
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotImplementedException()
    End Function
End Class
