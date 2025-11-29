Imports System.Net.NetworkInformation

Public Class ToyEarthSurfaceProvider
    Implements IEarthSurfaceProvider

    'Basis-Landflächen (Kontinente)
    Private Shared ReadOnly BaseLandPatches As List(Of SurfacePatch) = CreateBaseLandPatches()

    'Gebirge, die Land überschreiben
    Private Shared ReadOnly MountainPatches As List(Of SurfacePatch) = CreateMountainPatches()

    'Biome
    Private Shared ReadOnly BiomePatches As List(Of SurfacePatch) = CreateBiomePatches()

    Public Function GetSurfaceInfo(latitudeDeg As Double, longitudeDeg As Double) As SurfaceInfo Implements IEarthSurfaceProvider.GetSurfaceInfo

        Dim lat As Double = latitudeDeg
        Dim lon As Double = longitudeDeg


        '1) Basis: Ozean-Tiefsee
        Dim info As New SurfaceInfo With {
            .Surface = SurfaceType.Ocean,
            .HeightM = -3000.0
            }

        '2) Kontinente & Grundhöhe
        ApplyPatches(info, lat, lon, BaseLandPatches)

        '3) Gebirge
        ApplyPatches(info, lat, lon, MountainPatches)

        '3) Biome (Wälder, Wüsten)
        ApplyBiomes(info, lat, lon, BiomePatches)

        '4) Eis (Meereis, Inlandeis)
        ApplyIce(info, lat, lon)

        Return info

    End Function

    Private Sub ApplyPatches(info As SurfaceInfo,
                              lat As Double, lon As Double,
                              patches As List(Of SurfacePatch))

        For Each p As SurfacePatch In patches
            If p.Contains(lat, lon) Then
                info.Surface = p.Surface
                info.HeightM = p.HeightM
                'Wir brechen hier NICHT ab, später definierte Patches können überlagern, wenn die die Liste entsprechend sortiert ist
            End If
        Next
    End Sub
    Private Sub ApplyBiomes(info As SurfaceInfo,
                             lat As Double, lon As Double,
                             patches As List(Of SurfacePatch))

        'Nur echte Landflächen biom-fähig machen
        If info.Surface = SurfaceType.Ocean OrElse
            info.Surface = SurfaceType.SeaIce OrElse
            info.Surface = SurfaceType.LandIce Then
            Return
        End If

        For Each p As SurfacePatch In patches
            If p.Contains(lat, lon) Then
                info.Surface = p.Surface
                info.HeightM = Math.Max(info.HeightM, p.HeightM)
            End If
        Next
    End Sub

    Private Shared Function CreateBaseLandPatches() As List(Of SurfacePatch)
        Dim list As New List(Of SurfacePatch)

        '--- Nordamerika ---
        'Westküste & Kernland
        list.Add(New SurfacePatch(15, 70, -135, -110, SurfaceType.LandPlain, 300)) 'Westen
        list.Add(New SurfacePatch(15, 60, -110, -85, SurfaceType.LandPlain, 250)) 'Zentral
        list.Add(New SurfacePatch(20, 55, -85, -65, SurfaceType.LandPlain, 200)) 'Osten

        '--- Zentralamerika ---
        list.Add(New SurfacePatch(5, 25, -100, -80, SurfaceType.LandPlain, 200))

        '--- Südamerika ---
        list.Add(New SurfacePatch(-55, 10, -80, -50, SurfaceType.LandPlain, 200))

        '--- Afrika ---
        list.Add(New SurfacePatch(5, 35, -20, 50, SurfaceType.LandPlain, 250)) 'Nordafrika
        list.Add(New SurfacePatch(-10, 5, -20, 40, SurfaceType.LandPlain, 300)) 'Zentralafrika
        list.Add(New SurfacePatch(-35, -10, 10, 40, SurfaceType.LandPlain, 300)) 'südliches Afrika

        '--- Europa ---
        list.Add(New SurfacePatch(35, 60, -10, 40, SurfaceType.LandPlain, 300))


        '--- Asien ---
        list.Add(New SurfacePatch(20, 40, 35, 70, SurfaceType.LandPlain, 400)) 'Naher Osten
        list.Add(New SurfacePatch(5, 30, 65, 95, SurfaceType.LandPlain, 300)) 'Südasien/Indien
        list.Add(New SurfacePatch(20, 50, 95, 125, SurfaceType.LandPlain, 300)) 'Ostasien (China Kern)
        list.Add(New SurfacePatch(40, 75, 40, 150, SurfaceType.LandPlain, 200)) 'Sibiren
        list.Add(New SurfacePatch(-10, 10, 95, 140, SurfaceType.LandPlain, 100)) 'Südostasien/Indonesien

        '--- Australien ---
        list.Add(New SurfacePatch(-45, -10, 110, 155, SurfaceType.LandPlain, 200))

        '--- Grönland ---
        list.Add(New SurfacePatch(60, 85, -60, -40, SurfaceType.LandPlain, 500)) 'Erstmal nur das Land, Eis kommt später

        '--- Meere & Binnengewässer
        list.Add(New SurfacePatch(45, 60, -95, -75, SurfaceType.Ocean, -100)) 'Hudson Bay / Große Seen
        list.Add(New SurfacePatch(30, 45, -5, 40, SurfaceType.Ocean, -1700)) 'Mittelmeer
        list.Add(New SurfacePatch(10, 25, -85, -60, SurfaceType.Ocean, -2500)) 'Karibik

        Return list
    End Function

    Private Shared Function CreateMountainPatches() As List(Of SurfacePatch)
        Dim list As New List(Of SurfacePatch)

        'Rocky Mountains
        list.Add(New SurfacePatch(35, 60, -125, -105, SurfaceType.LandMountain, 2500))

        'Anden
        list.Add(New SurfacePatch(-50, 5, -75, -65, SurfaceType.LandMountain, 3000))

        'Alpen & Karpaten (grob)
        list.Add(New SurfacePatch(40, 50, 5, 25, SurfaceType.LandMountain, 2000))

        'Himalaya/Tibet
        list.Add(New SurfacePatch(25, 40, 70, 100, SurfaceType.LandMountain, 6000))

        'Ostafrikanischer Graben
        list.Add(New SurfacePatch(-15, 10, 25, 40, SurfaceType.LandMountain, 2000))

        'Neuseeland als "Gebirge"
        list.Add(New SurfacePatch(-50, -30, 165, 170, SurfaceType.LandMountain, 1500))

        Return list
    End Function

    Private Shared Function CreateBiomePatches() As List(Of SurfacePatch)
        Dim list As New List(Of SurfacePatch)

        '--- Wüsten ---
        list.Add(New SurfacePatch(10, 30, -15, 35, SurfaceType.LandDesert, 250)) 'Sahara
        list.Add(New SurfacePatch(15, 30, 35, 60, SurfaceType.LandDesert, 300)) 'Arabische Wüste
        list.Add(New SurfacePatch(35, 45, 90, 115, SurfaceType.LandDesert, 500)) 'Gobi
        list.Add(New SurfacePatch(-30, -15, 125, 145, SurfaceType.LandDesert, 300)) 'Zentralaustralien
        list.Add(New SurfacePatch(-30, 0, -75, -70, SurfaceType.LandDesert, 500)) 'Atacama

        '--- Tropische Regenwälder
        list.Add(New SurfacePatch(-10, 5, -70, -50, SurfaceType.LandForest, 200)) 'Amazonas
        list.Add(New SurfacePatch(-5, 5, 15, 30, SurfaceType.LandForest, 300)) 'Kongobecken
        list.Add(New SurfacePatch(-5, 10, 95, 120, SurfaceType.LandForest, 200)) 'Südostasien Regenwald
        list.Add(New SurfacePatch(-5, 5, 110, 135, SurfaceType.LandForest, 100)) 'Indonesien/Borneo

        '--- Boreale Nadelwälder
        list.Add(New SurfacePatch(50, 65, -130, -60, SurfaceType.LandForest, 300)) 'Kanada
        list.Add(New SurfacePatch(50, 65, 20, 140, SurfaceType.LandForest, 300)) 'Eurasische Taiga

        Return list
    End Function

    Private Sub ApplyIce(info As SurfaceInfo, lat As Double, lon As Double)

        'Arktisches Meereis (nur dort, wo vorher Ozean war)
        If info.Surface = SurfaceType.Ocean AndAlso lat > 70 Then
            info.Surface = SurfaceType.SeaIce
            info.HeightM = 0.0

            Exit Sub
        End If

        'Grönland
        If lat > 60 AndAlso lat < 85 AndAlso
            lon > -60 AndAlso lon < -40 Then
            info.Surface = SurfaceType.LandIce
            info.HeightM = 2500.0

            Exit Sub
        End If

        'Antarktis
        If lat < -60 Then
            info.Surface = SurfaceType.LandIce
            info.HeightM = 2500.0

            Exit Sub
        End If
    End Sub
End Class
