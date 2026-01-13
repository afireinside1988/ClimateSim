Imports System.Globalization

Public Class StringNotEmptyToVisibilityConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        Dim s = TryCast(value, String)
        Return If(String.IsNullOrEmpty(s), Visibility.Collapsed, Visibility.Visible)

    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function
End Class
