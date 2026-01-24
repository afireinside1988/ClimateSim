Imports System

''' <summary>
''' Erzeugt LandMask (Byte pro Zelle) für EarthSurfaceCache
''' v0.3: 0 = Ocean, 1 = Land
''' 
''' Vorgehen:
''' 1) Default: height >= 0 => Land, sonst Ocean
''' 2) Korrekturpass: 3x3 Majority-Filter mit Hysterese (damit kleine Löcher im Land verschwinden,
'''     aber Küsten nicht unnatürlich wandern)
''' </summary>
Public NotInheritable Class LandMaskBuilder

#Region "Öffentliche Hauptfunktion"
    ''' <summary>
    ''' Erzeugt LandMask aus HeightM + optionalem Majority-Filter.
    ''' </summary>
    ''' <param name="heightM">Height-Layer, Länge = latCount*lonCount</param>
    ''' <param name="latCount">Anzahl Breitenzellen</param>
    ''' <param name="lonCount">Anzahl Längenzellen</param>
    ''' <param name="applyMajorityFilter">Wenn True, wird der Korrekturpass angewendet.</param>
    ''' <param name="iterations">Anzahl Filter-Iterationen (empfohlen 1..2)</param>
    ''' <param name="landThreshold">Land-Hysterese: wenn in 3x3 mindestens dieser Wert Land ist, wird die Zelle Land. Typisch: 6 (von 9)</param>
    ''' <param name="oceanThreshold">Ocean-Hysterese: wenn in 3x3 höchstens dieser Wert Land ist, wird die Zelle Ocean. Typisch: 3 (von 9)</param>
    Public Shared Function BuildLandMaskFromHeight(heightM As Single(), latCount As Integer, lonCount As Integer,
                                                   Optional applyMajorityFilter As Boolean = False,
                                                   Optional iterations As Integer = 1,
                                                   Optional landThreshold As Integer = 6,
                                                   Optional oceanThreshold As Integer = 3) As Byte()

        '-------------------
        '0) Basic Validation
        '-------------------

        ArgumentNullException.ThrowIfNull(heightM)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(latCount)
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lonCount)

        Dim expectedLen As Integer = latCount * lonCount
        If heightM.Length <> expectedLen Then
            Throw New ArgumentException($"heightM-Länge passt nicht: ist={heightM.Length} (erwartet: {expectedLen}).")
        End If

        ArgumentOutOfRangeException.ThrowIfNegative(iterations)

        'landThreshold/oceanThreshold müssen sinnvoll sein
        'In einer 3x3 Nachbarschaft gibt es 9 Zellen
        If landThreshold < 0 OrElse landThreshold > 9 Then Throw New ArgumentOutOfRangeException(NameOf(landThreshold))
        If oceanThreshold < 0 OrElse oceanThreshold > 9 Then Throw New ArgumentOutOfRangeException(NameOf(oceanThreshold))

        'Hysterese solll "Lücke" lassen; oceanThreshold < landThreshold
        If oceanThreshold >= landThreshold Then
            Throw New ArgumentException("oceanThreshold muss kleiner sein als landThreshold damit die Hysterese funktioniert.")
        End If

        '--------------------------------
        '1) Default: heigthM >= 0 -> Land
        '--------------------------------

        Dim mask As Byte() = New Byte(expectedLen - 1) {}

        For i As Integer = 0 To expectedLen - 1
            'v0.3 Definition:
            'height >= 0 -> Land (1)
            'height < 0  -> Ocean (0)
            mask(i) = If(heightM(i) >= 0.0F, CByte(1), CByte(0))
        Next

        '---------------------------
        '2) Korrekturpass (optional)
        '---------------------------

        If applyMajorityFilter AndAlso iterations > 0 Then
            mask = ApplyMajorityFilterHysteresis(mask, latCount, lonCount, iterations, landThreshold, oceanThreshold)
        End If

        Return mask
    End Function

#End Region

    ''' <summary>
    ''' Führt ein 3x3 Majority-Filter mit Hysterese aus.
    ''' 
    ''' Regeln pro Zelle:
    ''' - Zähle in der 3x3-Nachbarschaft die Anzahl Land-Zellen (0..9)
    ''' - Wenn landCount >= landThreshold -> setze Zielzelle auf Land
    ''' - Wenn landCount <= oceanThreshold -> setze Zielzelle auf Ocean
    ''' - Sonst: Zielzelle bleibt wie vorher (Hysterese)
    ''' 
    ''' Randbehandlung:
    ''' - Longitude wrappt (globales Raster)
    ''' - Latitude clamped (am Rand keine Nachbarn "jenseits" des Rasters)
    ''' </summary>
    Private Shared Function ApplyMajorityFilterHysteresis(srcMask As Byte(), latCount As Integer, lonCount As Integer, iterations As Integer, landThreshold As Integer, oceanThreshold As Integer) As Byte()

        'Wir arbeiten iterativ: src -> dst -> src -> ...
        Dim src As Byte() = srcMask
        Dim dst As Byte() = New Byte(srcMask.Length - 1) {}

        For i As Integer = 1 To iterations

            'Für jede Zelle (lat,lon) berechnen wir die Entscheidung
            For lat As Integer = 0 To latCount - 1
                For lon As Integer = 0 To lonCount - 1

                    Dim idx As Integer = lat * lonCount + lon

                    '1) Land-Zellen in 3x3 zählen
                    Dim landCount As Integer = CountLandIn3x3(src, lat, lon, latCount, lonCount)

                    '2) Hysterese-Entscheidung
                    If landCount >= landThreshold Then
                        dst(idx) = CByte(1)         'Land
                    ElseIf landCount <= oceanThreshold Then
                        dst(idx) = CByte(0)         'Ocean
                    Else
                        'in der "Grauzone" bleibt der alte Wert
                        dst(idx) = src(idx)
                    End If
                Next
            Next

            '3) nächste Iteration: dst wird neue Quelle
            'Wir müssen Arrays nicht neu allokieren: wir tauschen Referenzen
            Dim tmp As Byte() = src
            src = dst
            dst = tmp
        Next

        'Wichtig: Nach gerader Anzahl Iterationen ist src das Original-Array,
        'nach ungerade Anzahl Iterationen ist src das zuletzt berechnete Array.
        'Wir geben "src" zurück, das immer die letzte Version ist
        Return src
    End Function

    ''' <summary>
    ''' Zählt Land-Zellen (Wert=1) in der 3x3-Nachbarschaft um (lat,lon) inkl. Zentrum
    ''' </summary>
    Private Shared Function CountLandIn3x3(mask As Byte(), centerLat As Integer, centerLon As Integer, latCount As Integer, lonCount As Integer) As Integer

        Dim landCount As Integer = 0

        'latOffset = -1, 0, +1
        For dLat As Integer = -1 To 1

            'Randbehandlung Latitude:
            'Wir clampen auf den gültigen Bereich [0..latCount-1]
            Dim lat As Integer = Clamp(centerLat + dLat, 0, latCount - 1)

            'lonOffset = -1, 0, +1
            For dLon As Integer = -1 To 1

                'Randbehandlung Longitude:
                'globales Raster -> wrap (z.B. lon=-1 wird lonCount-1)
                Dim lon As Integer = WrapLonIndex(centerLon + dLon, lonCount)

                Dim idx As Integer = lat * lonCount + lon
                If mask(idx) <> 0 Then landCount += 1
            Next
        Next

        Return landCount
    End Function

    ''' <summary>
    ''' Wrappt einen Longitude-Index in den Bereich (0...lonCount-1)+
    ''' </summary>
    Private Shared Function WrapLonIndex(lonIndex As Integer, lonCount As Integer) As Integer
        'VB Mod kann negative Werte liefern, daher robust:
        Dim x As Integer = lonIndex Mod lonCount
        If x < 0 Then x += lonCount
        Return x
    End Function

End Class
