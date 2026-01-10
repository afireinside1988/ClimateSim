Imports System.Globalization

Public Class GlobeMeshResolutionTickToStringConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim tick As Integer
        If value Is Nothing OrElse Not Integer.TryParse(value.ToString(), tick) Then tick = 0

        Select Case tick
            Case 0 : Return "128 x 64"
            Case 1 : Return "256 x 128"
            Case 2 : Return "512 x 256"
            Case 3 : Return "1024 x 512"
            Case 4 : Return "2048 x 1024"
            Case Else : Return "n/a"
        End Select
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return Binding.DoNothing
    End Function
End Class
