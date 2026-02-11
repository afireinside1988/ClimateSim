Imports System.IO
Imports MaxRev.Gdal.Core
Imports OSGeo.GDAL
Imports OSGeo.OGR

Module GdalHelpers

    Private Const GdalMaxCacheMB As String = "256"

    Public Sub InitGdal()



        Dim baseDir As String = AppDomain.CurrentDomain.BaseDirectory

        Dim projDir As String = Path.Combine(baseDir, "runtimes", "win-x64", "native", "maxrev.gdal.core.libshared")
        GdalBase.ConfigureAll()
        Gdal.SetConfigOption("GDAL_CACHEMAX", GdalMaxCacheMB)
        Gdal.SetConfigOption("GDAL_DISABLE_READDIR_ON_OPEN", "YES")
        Gdal.SetConfigOption("PROJ_LIB", projDir)
        Gdal.SetConfigOption("GDAL_DATA", projDir)

        Gdal.AllRegister()

        Ogr.RegisterAll()
    End Sub

    Public Function OpenDataset(path As String, purpose As String) As Dataset

        Try
            Dim ds As Dataset = Gdal.Open(path, Access.GA_ReadOnly)

            If ds Is Nothing Then
                Throw New InvalidDataException($"{purpose}: Datei konnte nicht geöffnet werden (GDAL.Open schlug fehl.).")
            End If

            Return ds
        Catch ex As Exception
            Throw New InvalidDataException($"{purpose}: Datei konnte nicht gelesen werden: '{path}'.{Environment.NewLine}{ex.Message}", ex)
        End Try

    End Function

    Public Function TryGetMetaValue(band As Band, key As String, ByRef value As String) As Boolean

        value = Nothing

        Dim md() As String = band.GetMetadata("")
        If md Is Nothing Then Return False
        For Each kv In md
            Dim p As Integer = kv.IndexOf("="c)
            If p > 0 Then
                Dim k As String = kv.Substring(0, p).Trim()
                If String.Equals(k, key, StringComparison.OrdinalIgnoreCase) Then
                    value = kv.Substring(p + 1).Trim()
                    Return True
                End If
            End If
        Next

        Return False
    End Function

    Public Function GetMissingValueByte(band As Band, defaultValue As Byte) As Byte

        Dim hasNoData As Integer = 0
        Dim noData As Double
        band.GetNoDataValue(noData, hasNoData)
        If hasNoData <> 0 Then
            Dim v As Integer = CInt(Math.Round(noData))
            If v >= 0 AndAlso v <= 255 Then Return CByte(v)
        End If

        Dim mv As String = Nothing
        If TryGetMetaValue(band, "missing_value", mv) Then
            Dim v As Integer
            If Integer.TryParse(mv, v) AndAlso v >= 0 AndAlso v <= 255 Then Return CByte(v)
        End If

        Return defaultValue
    End Function

    Public Function ToVsiZipPath(zipPath As String) As String

        Dim p As String = Path.GetFullPath(zipPath).Replace("\", "/")
        Return $"/vsizip/{p}"

    End Function

End Module
