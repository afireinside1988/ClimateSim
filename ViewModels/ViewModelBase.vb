Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Collections.Generic

Public MustInherit Class ViewModelBase
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler _
        Implements INotifyPropertyChanged.PropertyChanged

    Protected Overridable Sub OnPropertyChanged(
        <CallerMemberName> Optional propertyName As String = Nothing)

        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

    Protected Function SetProperty(Of T)(
        ByRef storage As T,
        value As T,
        <CallerMemberName> Optional propertyName As String = Nothing) As Boolean

        If EqualityComparer(Of T).Default.Equals(storage, value) Then
            Return False
        End If

        storage = value
        OnPropertyChanged(propertyName)
        Return True
    End Function

End Class
