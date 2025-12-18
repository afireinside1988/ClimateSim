
Imports System.Globalization
Imports System.IO
Imports System.Reflection.Metadata

''' <summary>
''' Streaming-Tokenizer für ESRI-ASCII Grid-Daten.
''' - arbeitet nur vorwärts (kein Seek nötig)
''' - kann mit einer gepufferten firstDataLine starten
''' - liefert Integer-Werte
''' -akzeptiert Tokens wie "11.0" / "11.000" als Integer 11
''' </summary>
Public NotInheritable Class AsciiIntTokenizer

    Private ReadOnly _sr As StreamReader

    Private _line As String
    Private _pos As Integer

    Private ReadOnly _ci As CultureInfo = CultureInfo.InvariantCulture

    Public Sub New(sr As StreamReader, Optional firstDataLine As String = Nothing)

        _sr = sr

        _line = firstDataLine
        _pos = 0

    End Sub

    ''' <summary>
    ''' Liest den nächsten Integer-Wert. Gibt False zurück bei EOF (keine weiteren Tokens).
    ''' Wirft InvalidDataException bei nicht unterstützten Tokens (z.B. "11.5")
    ''' </summary>
    Public Function TryReadInt(ByRef value As Integer) As Boolean
        value = 0

        Dim token As String = Nothing
        If Not TryReadToken(token) Then
            Return False
        End If

        value = ParseIntToken(token)

        Return True

    End Function

    ''' <summary>
    ''' Optional: für TID praktisch. Liest Integer und validiert Byte-Range
    ''' </summary>
    Public Function TryReadByte(ByRef value As Byte) As Boolean

        value = 0
        Dim i As Integer
        If Not TryReadInt(i) Then Return False

        If i < Byte.MinValue OrElse i > Byte.MaxValue Then
            Throw New InvalidDataException($"TID außerhalb Byte-Bereich: {i}")
        End If

        value = CByte(i)

        Return True

    End Function

#Region "Token reading"

    Private Function TryReadToken(ByRef token As String) As Boolean

        token = Nothing

        While True

            If _line Is Nothing Then
                If _sr.EndOfStream Then Return False
                _line = _sr.ReadLine()
                _pos = 0
                If _line Is Nothing Then Return False
            End If

            'Skip whitepsace
            While _pos < _line.Length AndAlso Char.IsWhiteSpace(_line(_pos))
                _pos += 1
            End While

            'Wenn Zeile leer/zu Ende -> nächste Zeile
            If _pos >= _line.Length Then
                _line = Nothing
                Continue While
            End If

            'Token bis zum nächsten Whitespace
            Dim start As Integer = _pos
            While _pos < _line.Length AndAlso Not Char.IsWhiteSpace(_line(_pos))
                _pos += 1
            End While

            token = _line.Substring(start, _pos - start)
            Return True
        End While

        Return False
    End Function

#End Region

#Region "Parsing"

    Private Function ParseIntToken(token As String) As Integer

        If String.IsNullOrWhiteSpace(token) Then
            Throw New InvalidDataException("Leeres Token im ASCII Grid")
        End If

        token = token.Trim()

        'Exponent? -> nicht erwartet im ESRI ASCII (und wir wollen das nicht stillschweigend unterstützen)
        If token.Contains("e"c) OrElse token.Contains("E"c) Then
            Throw New InvalidDataException($"Nicht unterstütztes Zahlenformat (Exponent): '{token}'")
        End If

        Dim dotIdx As Integer = token.IndexOf("."c)
        If dotIdx < 0 Then
            'reines Integer
            Dim i As Integer
            If Not Integer.TryParse(token, NumberStyles.Integer, _ci, i) Then
                Throw New InvalidDataException($"Ungültiger Integer im ASCII Grid: '{token}'")
            End If

            Return i

        End If

        'hat Dezimalpunkt -> nur zulassen, wenn danach ausschließlich Nullen kommen
        Dim intPart As String = token.Substring(0, dotIdx)
        Dim fracPart As String = token.Substring(dotIdx + 1)

        If fracPart.Length = 0 Then
            '"11." -> behandlen wie 11
            fracPart = "0"
        End If

        For Each ch As Char In fracPart
            If ch <> "0"c Then
                Throw New InvalidDataException($"Nicht-integer Dezimalwert im ASCII Grid: '{token}'")
            End If
        Next

        Dim baseInt As Integer
        If Not Integer.TryParse(intPart, NumberStyles.Integer, _ci, baseInt) Then
            Throw New InvalidDataException($"Ungültiger Integer-Anteil im ASCII Grid: '{token}'")
        End If

        Return baseInt
    End Function

#End Region
End Class
