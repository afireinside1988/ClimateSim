Imports System.ComponentModel.Design

Public Class ClimateGrid
    Public ReadOnly Property Width As Integer
    Public ReadOnly Property Height As Integer

    '2D-Array der Zellen: (latIndex, lonIndex)
    Private ReadOnly _cells(,) As ClimateCell

    Public Sub New(width As Integer, height As Integer)
        If width <= 0 OrElse height <= 0 Then
            Throw New ArgumentException("Width und Height müssen > 0 sein.")
        End If

        Me.Width = width
        Me.Height = height
        ReDim _cells(height - 1, width - 1)

        InitializeCells()

    End Sub

    Private Sub InitializeCells()

        'Wir gehen davon aus, dass:
        'latIndex = 0 -> Nordpol (+90°)
        'latIndex = Height-1 -> Südpol (-90°)

        For latIndex As Integer = 0 To Height - 1
            Dim latDeg As Double = 90.0 - 180.0 * (latIndex / CDbl(Height - 1))

            For lonIndex As Integer = 0 To Width - 1
                Dim lonDeg As Double = -180.0 + 360.0 * (lonIndex / CDbl(Width - 1))

                _cells(latIndex, lonIndex) = New ClimateCell() With {
                    .LatitudeDeg = latDeg,
                    .LongitudeDeg = lonDeg,
                    .TemperatureK = 0.0, 'wird später ersetzt
                    .IsOcean = True 'vorerst alles Ozean, später differenzieren
                }
            Next
        Next
    End Sub

    Public Function GetCell(latIndex As Integer, lonIndex As Integer) As ClimateCell
        If latIndex < 0 OrElse latIndex >= Height Then
            Throw New ArgumentOutOfRangeException(NameOf(latIndex), "latIndex außerhalb des gültigen Bereichs.")
        End If
        If lonIndex < 0 OrElse lonIndex >= Width Then
            Throw New ArgumentOutOfRangeException(NameOf(lonIndex), "lonIndex außerhalb des gültigen Bereichs.")
        End If
        Return _cells(latIndex, lonIndex)
    End Function

    Public Function GetCells() As ClimateCell(,)
        Return _cells
    End Function

    Public Function ComputeGlobalMeanTemperatureC() As Double
        Dim sum As Double = 0.0
        Dim count As Integer = 0

        For lat As Integer = 0 To Height - 1
            For lon As Integer = 0 To Width - 1
                Dim cell As ClimateCell = _cells(lat, lon)
                sum += (cell.TemperatureK - 273.15) 'Kelvin -> Celsius
                count += 1
            Next
        Next

        If count = 0 Then Return 0.0
        Return sum / count
    End Function

End Class
