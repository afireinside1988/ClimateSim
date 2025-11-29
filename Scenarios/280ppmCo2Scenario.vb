Public Class _280ppmCo2Scenario
    Implements ICo2Scenario

    Public Function GetCO2ForYear(year As Double) As Double Implements ICo2Scenario.GetCO2ForYear
        Return 280.0
    End Function
End Class
