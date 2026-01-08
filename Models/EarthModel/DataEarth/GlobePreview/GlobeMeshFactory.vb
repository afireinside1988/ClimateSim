Imports System.Transactions
Imports System.Windows.Media.Media3D

Public NotInheritable Class GlobeMeshFactory

    Private Sub New()
    End Sub

    Public Shared Function CreateSphereMesh(radius As Double, lonSegments As Integer, latSegments As Integer) As MeshGeometry3D

        lonSegments = Math.Max(8, lonSegments)
        latSegments = Math.Max(6, latSegments)

        Dim mesh As New MeshGeometry3D()

        'Wir duplizieren die Naht (u=0 und u=1), damit die Textur sauber wrappt
        For lat As Integer = 0 To latSegments
            Dim v As Double = lat / CDbl(latSegments)           '0..1 (oben->unten)
            Dim phi As Double = Math.PI * v                     '0..pi

            Dim y As Double = Math.Cos(phi)                     '+1..-1
            Dim r As Double = Math.Sin(phi)                     '0..1

            For lon As Integer = 0 To lonSegments
                Dim u As Double = lon / CDbl(lonSegments)       '0..1
                Dim theta As Double = 2.0 * Math.PI * u         '0..2pi

                'Position (Y ist "oben")
                Dim x As Double = r * Math.Cos(theta)
                Dim z As Double = r * Math.Sin(theta)

                mesh.Positions.Add(New Point3D(radius * x, radius * y, radius * z))

                'Normale = Richtung vom Zentrum
                mesh.Normals.Add(New Vector3D(x, y, z))

                'Texturkoordinaten
                'u: lon 0..1, v: lat 0..1 (oben->unten) passt zu equirectangular
                mesh.TextureCoordinates.Add(New Point(1.0 - u, v))
            Next
        Next

        Dim stride As Integer = lonSegments + 1

        For lat As Integer = 0 To latSegments - 1

            For lon As Integer = 0 To lonSegments - 1

                Dim i0 As Integer = lat * stride + lon
                Dim i1 As Integer = i0 + 1
                Dim i2 As Integer = i0 + stride
                Dim i3 As Integer = i2 + 1

                'Zwei Dreiecke pro Quadrat
                mesh.TriangleIndices.Add(i0)
                mesh.TriangleIndices.Add(i2)
                mesh.TriangleIndices.Add(i1)

                mesh.TriangleIndices.Add(i1)
                mesh.TriangleIndices.Add(i2)
                mesh.TriangleIndices.Add(i3)
            Next
        Next

        If mesh.CanFreeze Then mesh.Freeze()
        Return mesh
    End Function
End Class
