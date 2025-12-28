Imports System.IO

Public Module CellSizeHelper
    Public Enum CellSizePreset
        Deg1
        Deg0_5
        Deg0_25
        Deg0_125
        Deg0_0625
    End Enum

    Public Function CellSizePresetFromDeg(cellSizeDeg As Double) As CellSizePreset
        Const eps As Double = 0.0000001

        If Math.Abs(cellSizeDeg - 1.0) < eps Then
            Return CellSizePreset.Deg1
        ElseIf Math.Abs(cellSizeDeg - 0.5) < eps Then
            Return CellSizePreset.Deg0_5
        ElseIf Math.Abs(cellSizeDeg - 0.25) < eps Then
            Return CellSizePreset.Deg0_25
        ElseIf Math.Abs(cellSizeDeg - 0.125) < eps Then
            Return CellSizePreset.Deg0_125
        ElseIf Math.Abs(cellSizeDeg - 0.0625) < eps Then
            Return CellSizePreset.Deg0_0625
        End If

        Throw New InvalidDataException($"Unbekannte CellSizeDeg in Meta: {cellSizeDeg}. Erwarten: 1.0, 0.5, 0.25, 0.125 oder 0.0625.")
    End Function

    Public Function CellSizeDegFromPreset(cellSizePreset As CellSizePreset) As Double

        Select Case cellSizePreset
            Case CellSizePreset.Deg1 : Return 1.0
            Case CellSizePreset.Deg0_5 : Return 0.5
            Case CellSizePreset.Deg0_25 : Return 0.25
            Case CellSizePreset.Deg0_125 : Return 0.125
            Case CellSizePreset.Deg0_0625 : Return 0.0625
            Case Else : Return 1.0
        End Select

    End Function

    Public Function CellSizeFileTokenFromPreset(p As CellSizePreset) As String
        Select Case p
            Case CellSizePreset.Deg1 : Return "1.0"
            Case CellSizePreset.Deg0_5 : Return "0.5"
            Case CellSizePreset.Deg0_25 : Return "0.25"
            Case CellSizePreset.Deg0_125 : Return "0.125"
            Case CellSizePreset.Deg0_0625 : Return "0.0625"
            Case Else
                Throw New InvalidDataException($"Unbekanntes CellSizePreset: {p}")
        End Select
    End Function

    Public Function CellSizeFileTokenFromDeg(cellSizeDeg As Double) As String
        Dim p = CellSizePresetFromDeg(cellSizeDeg)
        Return CellSizeFileTokenFromPreset(p)
    End Function

End Module
