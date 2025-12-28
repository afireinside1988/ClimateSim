Imports System.Diagnostics

Public NotInheritable Class RenderTiming
    Public Property Name As String
    Public Property Ms As Long
    Public Property Extra As String

    Public Sub New(name As String, ms As Long, Optional extra As String = Nothing)
        Me.Name = name
        Me.Ms = ms
        Me.Extra = extra
    End Sub

End Class


Public Module RenderTimingHelpers

    Public Function WithTiming(Of T)(name As String, timings As List(Of RenderTiming), work As Func(Of T), Optional extra As String = Nothing) As T

        Dim sw As Stopwatch = Stopwatch.StartNew()
        Dim result As T = work()
        sw.Stop()

        timings.Add(New RenderTiming(name, sw.ElapsedMilliseconds, extra))
        Return result
    End Function

    Public Function FormatTimings(timings As IEnumerable(Of RenderTiming)) As String
        Dim sb As New Text.StringBuilder()
        sb.AppendLine("=== Render Timings ===")

        Dim total As Long = 0
        For Each t In timings
            total += t.Ms
        Next

        For Each t In timings
            Dim extra As String = If(String.IsNullOrWhiteSpace(t.Extra), "", $"({t.Extra})")
            sb.AppendLine($"  {t.Name,-18} {t.Ms,6} ms{extra}")
        Next

        sb.AppendLine($"  {"TOTAL",-18} {total,6} ms")
        Return sb.ToString()
    End Function
End Module
