Module TimeFormatting
    Public Function FormatYearWithStepMode(currentYear As Double, mode As TimeStepMode) As String
        Dim yearInt As Integer = CInt(Math.Floor(currentYear))
        Dim frac As Double = currentYear - yearInt

        Select Case mode
            Case TimeStepMode.Month
                Dim monthIndex As Integer = CInt(Math.Floor(frac * 12.0))
                If monthIndex < 0 Then monthIndex = 0
                If monthIndex > 11 Then monthIndex = 11

                Dim monthNames As String() = {"Jan", "Feb", "Mär", "Apr", "Mai", "Jun", "Jul", "Aug", "Sep", "Okt", "Nov", "Dez"}
                Return $"{yearInt} {monthNames(monthIndex)}"
            Case TimeStepMode.Quarter
                Dim qIndex As Integer = CInt(Math.Floor(frac * 4.0)) + 1
                If qIndex < 1 Then qIndex = 1
                If qIndex > 4 Then qIndex = 4
                Return $"{yearInt} Q{qIndex}"
            Case TimeStepMode.Decade
                Return $"{yearInt}"
            Case Else 'Jahr
                Return $"{yearInt}"
        End Select
    End Function

End Module
