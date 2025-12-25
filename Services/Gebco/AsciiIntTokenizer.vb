Imports System.Buffers
Imports System.IO

Public NotInheritable Class AsciiIntTokenizer
    Implements IDisposable

    Private ReadOnly _sr As StreamReader
    Private ReadOnly _pool As ArrayPool(Of Char) = ArrayPool(Of Char).Shared

    'Buffer für StreamReader
    Private _buf As Char()
    Private _pos As Integer
    Private _len As Integer

    'Optional: firstDataLine, die schon als String existiert
    Private _first As String
    Private _firstPos As Integer
    Private _usingFirst As Boolean

    Private _disposed As Boolean
    Private Const DefaultBufferSize As Integer = 64 * 1024

    Public Sub New(sr As StreamReader, Optional firstDataLine As String = Nothing, Optional bufferSize As Integer = DefaultBufferSize)

        ArgumentNullException.ThrowIfNull(sr)
        If bufferSize < 4096 Then bufferSize = DefaultBufferSize

        _sr = sr
        _buf = _pool.Rent(bufferSize)

        If Not String.IsNullOrEmpty(firstDataLine) Then
            _first = firstDataLine
            If _first.Length = 0 OrElse (_first(_first.Length - 1) <> ControlChars.Lf) Then
                _first &= ControlChars.Lf
            End If

            _firstPos = 0
            _usingFirst = True
        End If
    End Sub


    Private Sub Dispose(disposing As Boolean)
        _disposed = disposing
        If _disposed Then Return
        _disposed = True

        Dim tmp = _buf
        _buf = Nothing
        If tmp IsNot Nothing Then
            _pool.Return(tmp, clearArray:=False)
        End If

        _first = Nothing
        _len = 0
        _pos = 0
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        ' Ändern Sie diesen Code nicht. Fügen Sie Bereinigungscode in der Methode "Dispose(disposing As Boolean)" ein.
        Dispose(disposing:=True)
        GC.SuppressFinalize(Me)
    End Sub



    Public Function TryReadInt(ByRef value As Integer) As Boolean
        value = 0

        If Not SkipWs() Then Return False

        Dim ch As Char

        Dim neg As Boolean = False
        Dim hasSign As Boolean = False

        Dim acc As Integer = 0
        Dim hasDigit As Boolean = False
        Dim seenDot As Boolean = False

        While True

            If Not PeekChar(ch) Then
                Exit While 'EOF beendet Token
            End If

            If IsWs(ch) Then
                Exit While 'Token endet an Whitespace
            End If

            'Exponent explizit verbieten
            If ch = "e"c OrElse ch = "E"c Then
                Throw New InvalidDataException("Nicht unterstütztes Zahlenformat (Exponent) im ASCII Grid.")
            End If

            'Vorzeichen nur am Anfang erlauben (bevor Digit oder Dot gelesen wurde)
            If (ch = "-"c OrElse ch = "+"c) AndAlso Not hasDigit AndAlso Not seenDot AndAlso Not hasSign Then
                hasSign = True
                If Not ReadChar(ch) Then Return False
                neg = (ch = "-"c)
                Continue While
            End If

            If ch = "."c Then
                If seenDot Then
                    Throw New InvalidDataException("Ungültiges Zahlenformat: mehrfacher Dezimalpunkt.")
                End If

                seenDot = True
                If Not ReadChar(ch) Then Return False
                Continue While
            End If

            Dim d As Integer = AscW(ch) - AscW("0"c)
            If d < 0 OrElse d > 9 Then
                Throw New InvalidDataException($"ungültiges Zeichen im Zahlen-Token: '{ch}'")
            End If

            'consume digit
            If Not ReadChar(ch) Then Return False
            hasDigit = True

            If Not seenDot Then
                'Overflow-robust
                If acc > (Integer.MaxValue - d) \ 10 Then
                    Throw New InvalidDataException("Integer overflow im ASCII Grid Token.")
                End If
                acc = acc * 10 + d
            Else
                'Nach Dezimalpunkt dürfen nur Nullen kommen
                If d <> 0 Then
                    Throw New InvalidDataException("Nicht-integer Dezimalwert im ASCII Grid (z.B. 11.5).")
                End If
            End If

        End While

        'Token ohne Ziffern ist ungültig (z.B. nur "+" oder "-" oder ".")
        If Not hasDigit Then
            Throw New InvalidDataException("Ungültiges Zahlen-Token (keine Ziffern).")
        End If

        value = If(neg, -acc, acc)
        Return True
    End Function

    Public Function TryReadByte(ByRef value As Byte) As Boolean

        Dim i As Integer

        If Not TryReadInt(i) Then
            value = 0
            Return False
        End If

        If i < Byte.MinValue OrElse i > Byte.MaxValue Then
            Throw New InvalidDataException($"TID außerhalb Byte-Bereich: {i}.")
        End If

        value = CByte(i)
        Return True
    End Function


#Region "Helper"

    Private Function Refill() As Boolean
        _pos = 0
        _len = _sr.Read(_buf, 0, _buf.Length)
        Return _len > 0
    End Function

    Private Function PeekChar(ByRef ch As Char) As Boolean

        If _usingFirst Then
            If _firstPos < _first.Length Then
                ch = _first(_firstPos)
                Return True
            Else
                _usingFirst = False
            End If
        End If

        If _pos >= _len Then
            If Not Refill() Then Return False
        End If

        ch = _buf(_pos)
        Return True
    End Function

    Private Function ReadChar(ByRef ch As Char) As Boolean

        If _usingFirst Then
            If _firstPos < _first.Length Then
                ch = _first(_firstPos)
                _firstPos += 1
                Return True
            Else
                _usingFirst = False
            End If
        End If

        If _pos >= _len Then
            If Not Refill() Then Return False
        End If

        ch = _buf(_pos)
        _pos += 1
        Return True
    End Function

    Private Shared Function IsWs(ch As Char) As Boolean
        Return Char.IsWhiteSpace(ch)
    End Function

    Private Function SkipWs() As Boolean
        Dim ch As Char
        While True
            If Not PeekChar(ch) Then Return False
            If Not IsWs(ch) Then Return True
            'consume
            If Not ReadChar(ch) Then
                Return False
            End If
        End While

        Return False
    End Function

#End Region

End Class
