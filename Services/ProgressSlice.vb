Public NotInheritable Class ProgressSlice

    Implements IProgress(Of ProgressInfo)

    Private ReadOnly _outer As IProgress(Of ProgressInfo)
    Private ReadOnly _startPct As Integer
    Private ReadOnly _endPct As Integer
    Private ReadOnly _prefix As String

    Public Sub New(outer As IProgress(Of ProgressInfo), startPct As Integer, endPct As Integer, Optional prefix As String = Nothing)
        _outer = outer
        _startPct = Math.Max(0, Math.Min(100, startPct))
        _endPct = Math.Max(0, Math.Min(100, endPct))
        _prefix = prefix

    End Sub

    Public Sub Report(value As ProgressInfo) Implements IProgress(Of ProgressInfo).Report

        If _outer Is Nothing OrElse value Is Nothing Then Return

        Dim msg As String = value.Message
        If Not String.IsNullOrWhiteSpace(_prefix) Then
            msg = $"{_prefix}{msg}"
        End If

        If value.Percent < 0 Then
            _outer.Report(New ProgressInfo(msg, ProgressInfo.Indeterminate))
            Return
        End If

        Dim local As Integer = Math.Max(0, Math.Min(100, value.Percent))
        Dim mapped As Integer = _startPct + CInt((local / 100.0) * (_endPct - _startPct))
        _outer.Report(New ProgressInfo(msg, mapped))
    End Sub
End Class
