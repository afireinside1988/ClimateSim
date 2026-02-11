Imports System.IO
Imports System.IO.Compression

Public NotInheritable Class GebcoZipCatalog

    Public Shared Function ListAscTiles(zipPath As String) As List(Of GebcoTileInfo)

        If String.IsNullOrWhiteSpace(zipPath) OrElse Not File.Exists(zipPath) Then
            Throw New FileNotFoundException("ZIP nicht gefunden.", zipPath)
        End If

        Dim tiles As New List(Of GebcoTileInfo)

        Using fs As New FileStream(zipPath, FileMode.Open, FileAccess.Read, FileShare.Read)
            Using za As New ZipArchive(fs, ZipArchiveMode.Read, leaveOpen:=False)

                For Each entry In za.Entries

                    Dim name As String = entry.FullName

                    If Not name.EndsWith(".asc", StringComparison.OrdinalIgnoreCase) Then Continue For
                    'Sicherheit: .asc.aux raus
                    If name.EndsWith(".asc.aux", StringComparison.OrdinalIgnoreCase) Then Continue For

                    Dim n, s, w, e As Double

                    If Not GebcoTileNameParser.TryParseBounds(Path.GetFileName(name), n, s, w, e) Then
                        'Falls GEBCO mal anders benennt: wir skippen erstmal
                        Continue For
                    End If

                    tiles.Add(New GebcoTileInfo With {
                            .EntryName = name,
                            .North = n,
                            .South = s,
                            .West = w,
                            .East = e
                        })

                Next

            End Using
        End Using

        'Sortierung: Nord->Süd, dann West->Ost
        Return tiles.
            OrderByDescending(Function(t) t.North).
            ThenBy(Function(t) t.West).
            ToList()
    End Function

End Class
