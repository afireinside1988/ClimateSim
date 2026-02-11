Public Structure GlacierVolumeAreaFit
    Public C As Double
    Public Gamma As Double
    Public N As Integer         'Anzahl der Trainingsdatensätze
End Structure

Public Class GlaThiDaProcessResult
    Public Property FitsByRegion As Dictionary(Of RGIRegion, GlacierVolumeAreaFit)
    Public Property Report As String
End Class
