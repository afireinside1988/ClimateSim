Imports System
Imports System.Globalization
Imports System.Windows.Data

Public Class EnumToBooleanConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert

        If value Is Nothing OrElse parameter Is Nothing Then Return Nothing

        Return String.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack

        If parameter Is Nothing Then Return Binding.DoNothing

        If TypeOf value Is Boolean AndAlso CBool(value) Then
            Return [Enum].Parse(targetType, parameter.ToString())
        End If

        Return Binding.DoNothing
    End Function
End Class
