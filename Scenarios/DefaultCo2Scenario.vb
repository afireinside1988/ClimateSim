Public Class DefaultCo2Scenario

    Implements ICo2Scenario


    Public Function GetCO2ForYear(year As Double) As Double Implements ICo2Scenario.GetCO2ForYear
        If year <= 1850 Then
            Return 280.0
        End If

        If year <= 1950 Then
            Dim t As Double = (year - 1850.0) / (1950.0 - 1850.0)
            Return 280.0 + t * (310.0 - 280.0)
        End If

        If year <= 2000 Then
            Dim t As Double = (year - 1950.0) / (2000.0 - 1950.0)
            Return 310.0 + t * (370.0 - 310.0)
        End If

        If year <= 2020 Then
            Dim t As Double = (year - 2000.0) / (2020.0 - 2000.0)
            Return 370.0 + t * (415.0 - 370.0)
        End If

        If year <= 2100 Then
            Dim t As Double = (year - 2020.0) / (2100.0 - 2020.0)
            Return 415.0 + t * (700.0 - 415.0)
        End If

        Return 700.0
    End Function

End Class
