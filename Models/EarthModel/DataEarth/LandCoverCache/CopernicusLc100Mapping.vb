' Copernicus LC100 v3.0 Discrete Classification codes:
' Quelle: Copernicus PUM/Validation Report Tabellen (23 Klassen, Codes inkl. 0 und 200).
' - 0   = No input data
' - 200 = Open sea
' - 80  = Permanent water bodies
' - 70  = Snow and Ice
' - 50  = Urban / built up
' - 40  = Cropland
' - 60  = Bare / sparse vegetation
' - 30  = Herbaceous vegetation
' - 20  = Shrubs
' - 90  = Herbaceous wetland
' - 100 = Moss and lichen
' - 111..116 = Closed forest types
' - 121..126 = Open forest types

Module CopernicusLc100Mapping

    Public Function MapCopernicusToLc11(copValue As Integer) As LandCoverClass
        Select Case copValue
            Case 0
                Return LandCoverClass.NoData

            Case 20
                Return LandCoverClass.Shrub

            Case 30
                Return LandCoverClass.GrassHerbaceous

            Case 40
                Return LandCoverClass.Cropland

            Case 50
                Return LandCoverClass.Urban

            Case 60
                Return LandCoverClass.BareSparse

            Case 70
                Return LandCoverClass.SnowIce

            Case 80
                Return LandCoverClass.InlandWater

            Case 90
                Return LandCoverClass.Wetland

            Case 100
                Return LandCoverClass.MossLichen

            Case 111, 112, 113, 114, 115, 116,
                     121, 122, 123, 124, 125, 126
                Return LandCoverClass.Forest

            Case 200
                Return LandCoverClass.OpenWater

            Case Else
                'Unbekannt => NoData
                Return LandCoverClass.NoData
        End Select
    End Function
End Module
