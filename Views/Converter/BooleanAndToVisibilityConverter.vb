Imports System.Globalization
Imports System.Windows
Imports System.Windows.Data

Public Class BooleanAndToVisibilityConverter

    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert
#Disable Warning IDE0075 ' Bedingten Ausdruck vereinfachen
        Dim a = If(values IsNot Nothing AndAlso values.Length > 0 AndAlso TypeOf values(0) Is Boolean, CBool(values(0)), False)
        Dim b = If(values IsNot Nothing AndAlso values.Length > 1 AndAlso TypeOf values(1) Is Boolean, CBool(values(1)), False)
#Enable Warning IDE0075 ' Bedingten Ausdruck vereinfachen

        Return If(a AndAlso b, Visibility.Visible, Visibility.Collapsed)
    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Throw New NotSupportedException()
    End Function
End Class
