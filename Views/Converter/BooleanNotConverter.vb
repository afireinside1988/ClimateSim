Imports System.Globalization
Imports System.Windows.Data

Public Class BooleanNotConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is Boolean Then
            Return Not CBool(value)
        End If

        Return True
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        If TypeOf value Is Boolean Then
            Return Not CBool(value)
        End If

        Return True
    End Function
End Class
