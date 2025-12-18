Imports System.Globalization
Imports System.Text.RegularExpressions

Module GebcoTileNameParser

    'Beispiel: gebco_2025_sub_ice_n90.0_s0.0_w-180.0_e-90.0.asc
    Private ReadOnly Rx As New Regex("_n(?<n>-?\d+(\.\d+)?)_s(?<s>-?\d+(\.\d+)?)_w(?<w>-?\d+(\.\d+)?)_e(?<e>-?\d+(\.\d+)?)\.asc$",
                                        RegexOptions.IgnoreCase Or RegexOptions.Compiled)

    Public Function TryParseBounds(fileName As String, ByRef n As Double, ByRef s As Double, ByRef w As Double, ByRef e As Double) As Boolean
        Dim m As Match = Rx.Match(fileName)
        If Not m.Success Then Return False

        Dim ci As CultureInfo = CultureInfo.InvariantCulture

        If Not Double.TryParse(m.Groups("n").Value, NumberStyles.Float, ci, n) Then Return False
        If Not Double.TryParse(m.Groups("s").Value, NumberStyles.Float, ci, s) Then Return False
        If Not Double.TryParse(m.Groups("w").Value, NumberStyles.Float, ci, w) Then Return False
        If Not Double.TryParse(m.Groups("e").Value, NumberStyles.Float, ci, e) Then Return False

        Return True
    End Function

End Module
