Imports System.Globalization
Imports System.IO

Public Class EsriAsciiHeaderReader

    ''' <summary>
    ''' Liest den Header aus einer ESRI ASCII-Datei
    ''' </summary>
    Public Shared Function ReadHeader(sr As StreamReader, ByRef firstDataLine As String) As AsciiGridHeader

        Dim h As New AsciiGridHeader()
        Dim ci As CultureInfo = CultureInfo.InvariantCulture

        'Wir lesen solange, bis wir alle Pflichtfelder haben
        Dim gotNcols, gotNrows, gotCellSize As Boolean

        'Header lesen (flexibel; Reihenfolge ist egal)
        For i As Integer = 0 To 200      'etwas großzügiger
            Dim line = sr.ReadLine()
            If line Is Nothing Then Exit For

            line = line.Trim()
            If line.Length = 0 Then Continue For

            Dim parts As String() = line.Split({" "c, vbTab}, StringSplitOptions.RemoveEmptyEntries)
            If parts.Length < 2 Then Continue For

            Dim key As String = parts(0).ToLowerInvariant()
            Dim val As String = parts(1)

            Select Case key
                Case "ncols"
                    h.NCols = Integer.Parse(val, ci) : gotNcols = True
                Case "nrows"
                    h.NRows = Integer.Parse(val, ci) : gotNrows = True
                Case "xllcorner"
                    h.XllCorner = Double.Parse(val, ci)
                Case "yllcorner"
                    h.YllCorner = Double.Parse(val, ci)
                Case "xllcenter"
                    h.XllCenter = Double.Parse(val, ci)
                Case "yllcenter"
                    h.YllCenter = Double.Parse(val, ci)
                Case "cellsize"
                    h.CellSize = Double.Parse(val, ci) : gotCellSize = True
                Case "nodata_value"
                    h.NoDataValue = Double.Parse(val, ci)
                Case Else
                    'wenn Header fertig ist, kann das schon Daten sein
                    If gotNcols AndAlso gotNrows AndAlso gotCellSize Then
                        If StartsWithNumber(line) Then
                            firstDataLine = line
                            Exit For
                        End If
                    End If
            End Select

        Next

        If h.NCols <= 0 OrElse h.NRows <= 0 OrElse h.CellSize <= 0 Then
            Throw New InvalidDataException("ESRI ASCII Header unvollständig oder ungültig (ncols/nrows/cellsize).")
        End If

        'Falls Header exakt endete und nächste ReadLine Daten ist
        If firstDataLine Is Nothing Then
            While Not sr.EndOfStream

                Dim line = sr.ReadLine()
                If line Is Nothing Then Exit While
                line = line.Trim()
                If line.Length = 0 Then Continue While

                If StartsWithNumber(line) Then
                    firstDataLine = line
                    Exit While
                End If

            End While
        End If

        If firstDataLine Is Nothing Then
            Throw New InvalidDataException("Keine Datenzeile im ASCII-Grid gefunden.")
        End If

        Return h
    End Function

    Private Shared Function StartsWithNumber(line As String) As Boolean
        Dim c As Char = line(0)
        Return Char.IsDigit(c) OrElse c = "-"c OrElse c = "+"c
    End Function
End Class
