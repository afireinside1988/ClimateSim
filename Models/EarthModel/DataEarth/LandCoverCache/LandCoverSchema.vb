''' <summary>
''' Helper für SOCACH LandCover Schmea:
''' - DisplayName (UI)
''' - ShortName (Legenden/Statusbar)
''' - Gruppierung: Land / Water / Ice / NoData
''' </summary>
Public NotInheritable Class LandCoverSchema

    Public Const SchemaName As String = "SCOACH_LC11_v1"        'Name in Meta
    Public Const NoDataValue As Byte = CByte(LandCoverClass.NoData)

    Public Enum LandCoverGroup As Byte
        NoData = 0
        Land = 1
        Water = 2
        Ice = 3
    End Enum

    Public Shared Function GetDisplayName(cls As LandCoverClass) As String
        Select Case cls
            Case LandCoverClass.NoData : Return "Keine Daten"
            Case LandCoverClass.Forest : Return "Wald"
            Case LandCoverClass.Shrub : Return "Strauchland"
            Case LandCoverClass.GrassHerbaceous : Return "Gras / Unkraut"
            Case LandCoverClass.Wetland : Return "Feuchtgebiet"
            Case LandCoverClass.MossLichen : Return "Moos / Flechten"
            Case LandCoverClass.BareSparse : Return "Karg / Fels / Wüste"
            Case LandCoverClass.Cropland : Return "Ackerland"
            Case LandCoverClass.Urban : Return "Siedlung / Urban"
            Case LandCoverClass.SnowIce : Return "Schnee / Eis"
            Case LandCoverClass.InlandWater : Return "Binnengewässer"
            Case LandCoverClass.OpenWater : Return "Offenes Wasser"
            Case Else : Return $"Unbekannt ({CInt(cls)})"
        End Select
    End Function

    Public Shared Function GetShortName(cls As LandCoverClass) As String
        Select Case cls
            Case LandCoverClass.NoData : Return "NoData"
            Case LandCoverClass.Forest : Return "Forest"
            Case LandCoverClass.Shrub : Return "Shrub"
            Case LandCoverClass.GrassHerbaceous : Return "Grass"
            Case LandCoverClass.Wetland : Return "Wetland"
            Case LandCoverClass.MossLichen : Return "Moss"
            Case LandCoverClass.BareSparse : Return "Bare"
            Case LandCoverClass.Cropland : Return "Crop"
            Case LandCoverClass.Urban : Return "Urban"
            Case LandCoverClass.SnowIce : Return "SnowIce"
            Case LandCoverClass.InlandWater : Return "InlandW"
            Case LandCoverClass.OpenWater : Return "OpenW"
            Case Else : Return $"C({CInt(cls)})"
        End Select
    End Function

    Public Shared Function GetGroup(cls As LandCoverClass) As LandCoverGroup
        Select Case cls
            Case LandCoverClass.NoData
                Return LandCoverGroup.NoData

            Case LandCoverClass.OpenWater, LandCoverClass.InlandWater
                Return LandCoverGroup.Water

            Case LandCoverClass.SnowIce
                Return LandCoverGroup.Ice

            Case Else
                Return LandCoverGroup.Land
        End Select
    End Function

    Public Shared Function GetColor(cls As LandCoverClass) As Color
        Select Case cls
            Case LandCoverClass.NoData : Return Color.FromRgb(40, 40, 40)
            Case LandCoverClass.Forest : Return Color.FromRgb(0, 153, 0)
            Case LandCoverClass.Shrub : Return Color.FromRgb(255, 187, 34)
            Case LandCoverClass.GrassHerbaceous : Return Color.FromRgb(255, 255, 76)
            Case LandCoverClass.Wetland : Return Color.FromRgb(0, 150, 160)
            Case LandCoverClass.MossLichen : Return Color.FromRgb(250, 230, 160)
            Case LandCoverClass.BareSparse : Return Color.FromRgb(180, 180, 180)
            Case LandCoverClass.Cropland : Return Color.FromRgb(240, 150, 255)
            Case LandCoverClass.Urban : Return Color.FromRgb(250, 0, 0)
            Case LandCoverClass.SnowIce : Return Color.FromRgb(240, 240, 240)
            Case LandCoverClass.InlandWater : Return Color.FromRgb(0, 50, 200)
            Case LandCoverClass.OpenWater : Return Color.FromRgb(0, 0, 120)
            Case Else : Return Colors.Magenta   'DEBUG-Farbe
        End Select
    End Function

    Public Shared Function IsWater(cls As LandCoverClass) As Boolean
        Dim g = GetGroup(cls)
        Return g = LandCoverGroup.Water
    End Function

    Public Shared Function IsOpenWater(cls As LandCoverClass) As Boolean
        Return cls = LandCoverClass.OpenWater
    End Function

    Public Shared Function IsInlandWater(cls As LandCoverClass) As Boolean
        Return cls = LandCoverClass.InlandWater
    End Function

    Public Shared Function IsLandIceSurface(cls As LandCoverClass) As Boolean
        Return cls = LandCoverClass.SnowIce
    End Function

    Public Shared ReadOnly Property LegendOrder As IReadOnlyList(Of LandCoverClass)
        Get
            Return _legendOrder
        End Get
    End Property

    Private Shared ReadOnly _legendOrder As LandCoverClass() = {
            LandCoverClass.Forest,
            LandCoverClass.Shrub,
            LandCoverClass.GrassHerbaceous,
            LandCoverClass.Wetland,
            LandCoverClass.MossLichen,
            LandCoverClass.BareSparse,
            LandCoverClass.Cropland,
            LandCoverClass.Urban,
            LandCoverClass.SnowIce,
            LandCoverClass.InlandWater,
            LandCoverClass.OpenWater,
            LandCoverClass.NoData
        }

End Class
