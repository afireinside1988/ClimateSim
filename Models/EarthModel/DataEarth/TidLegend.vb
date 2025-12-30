Imports System.Windows.Media

Public NotInheritable Class TidLegend

    Private Sub New()

    End Sub


    Public Shared ReadOnly TidColor(255) As Color
    Public Shared ReadOnly TidText(255) As String
    Public Shared ReadOnly TidGroup(255) As String

    Shared Sub New()

        'Default setzen (neutrales Grau)
        For i As Integer = 0 To 255
            TidColor(i) = Colors.DimGray
            TidText(i) = $"TID {i}"
        Next

        '--- Unknown / NoData ---
        SetTid(255, "Unbekannt / keine TID-Information", "Unbekannt", Colors.Magenta)

        '--- Land ---
        SetTid(0, "Land (terrestrische Rasterzelle)", "Land", Color.FromRgb(&H3B, &H8F, &H3B)) 'grün

        '--- Manuell gesetzt ---
        SetTid(254, "Manuell editierte Zelle", "Manuell", Colors.Red)

        '=========================
        ' Direktmessungen (10–17)
        '=========================
        'Blautöne (dunkel -> hell), innerhalb der Gruppe konsistent
        SetTid(10, "Einzelstrahl-Echolot (Singlebeam): Tiefenwert aus Einzelstrahlmessung", "Direktmessung", Color.FromRgb(&H10, &H3A, &H6F))
        SetTid(11, "Mehrstrahl-Echolot (Multibeam): Tiefenwert aus Mehrstrahlmessung", "Direktmessung", Color.FromRgb(&H1A, &H52, &H8A))
        SetTid(12, "Seismische Verfahren: Tiefenwert aus seismischen Messmethoden", "Direktmessung", Color.FromRgb(&H24, &H6A, &HA5))
        SetTid(13, "Isolierte Lotung: Einzelwert außerhalb regulärer Vermessungsprofile", "Direktmessung", Color.FromRgb(&H2F, &H82, &HC0))
        SetTid(14, "ENC-Lotung: Tiefenwert aus Elektronischer Seekarte (ENC) extrahiert", "Direktmessung", Color.FromRgb(&H3A, &H9A, &HDB))
        SetTid(15, "Bathymetrisches LiDAR: Tiefe aus bathymetrischem LiDAR abgeleitet", "Direktmessung", Color.FromRgb(&H52, &HB2, &HF0))
        SetTid(16, "Optischer Lichtsensor: Tiefe aus optischer Messung abgeleitet", "Direktmessung", Color.FromRgb(&H6A, &HC8, &HFF))
        SetTid(17, "Kombination direkter Messmethoden (gemischte Direktdaten)", "Direktmessung", Color.FromRgb(&H3B, &H6D, &HB5))

        '=========================
        ' Indirekte Ableitungen (40–47)
        '=========================
        'Orange/Braun-Töne: Modell/Interpolation/Gravimetrie
        SetTid(40, "Vorhersage aus satellitengestützter Gravimetrie: interpoliert, gravimetrisch geführt", "Indirekt", Color.FromRgb(&H8C, &H4B, &H0))
        SetTid(41, "Algorithmische Interpolation (z. B. Generic Mapping Tools): berechneter Interpolationswert", "Indirekt", Color.FromRgb(&HA3, &H5A, &H0))
        SetTid(42, "Digitale bathymetrische Isobathen aus Karten: Tiefenwert aus Konturdatensatz", "Indirekt", Color.FromRgb(&HBA, &H69, &H0))
        SetTid(43, "Digitale bathymetrische Isobathen aus ENCs: Tiefenwert aus ENC-Konturen", "Indirekt", Color.FromRgb(&HD1, &H78, &H0))
        SetTid(44, "Mehrquellen-Griddatensatz (gemessen/abgeleitet): Interpolation gravimetrisch (Satellit) geführt", "Indirekt", Color.FromRgb(&HE8, &H87, &H0))
        SetTid(45, "Vorhersage aus luft-/helikoptergestützter Gravimetrie", "Indirekt", Color.FromRgb(&HFF, &H96, &H1A))
        SetTid(46, "Abschätzung aus aufgelaufenem Eisberg: Tiefe aus Freibord (Satellit) und Auftrieb/Draft berechnet", "Indirekt", Color.FromRgb(&HFF, &HA9, &H4D))
        SetTid(47, "Ableitung aus aufgelaufenen Argo-Floats (Grounded Argo): bathymetrische Information aus Float-Grundkontakt", "Indirekt", Color.FromRgb(&HFF, &HBC, &H80))

        '=========================
        ' Unbekannt / gemischt (70–72)
        '=========================
        'Violett/Grau (klar getrennt von Direkt/Indirekt)
        SetTid(70, "Vorgefertigtes Grid (gemischte Quelltypen): Wert aus bereits erzeugtem Raster mit Mischdaten", "Unbekannt/Gemischt", Color.FromRgb(&H60, &H4A, &H7A))
        SetTid(71, "Unbekannte Quelle: Tiefenwert aus nicht spezifizierter Datenquelle", "Unbekannt/Gemischt", Color.FromRgb(&H7A, &H6A, &H8F))
        SetTid(72, "Steuer-/Constraint-Punkte: zur Stabilisierung des Grids in Bereichen geringer Datenabdeckung", "Unbekannt/Gemischt", Color.FromRgb(&H92, &H84, &HA6))

    End Sub

    Private Shared Sub SetTid(code As Integer, text As String, group As String, color As Color)
        If code < 0 OrElse code > 255 Then Throw New ArgumentOutOfRangeException(NameOf(code))

        TidText(code) = $"TID {code}: {text}"
        TidGroup(code) = group
        TidColor(code) = color
    End Sub

    Public Shared Function BuildDefaultItems() As IEnumerable(Of TidLegendItemViewModel)
        Dim order As Integer() = New Integer() {0,
                                                 10, 11, 12, 13, 14, 15, 16, 17,
                                                 40, 41, 42, 43, 44, 45, 46, 47,
                                                 70, 71, 72, 254, 255}

        Dim list As New List(Of TidLegendItemViewModel)(order.Length)

        For Each c As Integer In order
            list.Add(New TidLegendItemViewModel(
                     tidCode:=c,
                     tidText:=TidText(c),
                     tidGroup:=TidGroup(c),
                     tidColor:=TidColor(c)))
        Next

        Return list
    End Function

End Class
