Imports System.Windows.Media.Media3D

Public NotInheritable Class GlobeMeshRenderer

    Public Const EarthRadiusM As Double = 6371000.0

    Private Sub New()
    End Sub

#Region "Mesh-Renderer"

    Public Shared Function RenderSphereMesh(radius As Double, lonSegments As Integer, latSegments As Integer) As MeshGeometry3D

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

                Dim lonDeg As Double = u * 360.0 - 180.0
                Dim theta As Double = -lonDeg * Math.PI / 180.0         '0..2pi

                'Position (Y ist "oben")
                Dim x As Double = r * Math.Cos(theta)
                Dim z As Double = r * Math.Sin(theta)

                mesh.Positions.Add(New Point3D(radius * x, radius * y, radius * z))

                'Normale = Richtung vom Zentrum
                mesh.Normals.Add(New Vector3D(x, y, z))

                'Texturkoordinaten
                'u: lon 0..1, v: lat 0..1 (oben->unten) passt zu equirectangular
                mesh.TextureCoordinates.Add(New Point(u, v))
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
                mesh.TriangleIndices.Add(i1)
                mesh.TriangleIndices.Add(i2)

                mesh.TriangleIndices.Add(i1)
                mesh.TriangleIndices.Add(i3)
                mesh.TriangleIndices.Add(i2)
            Next
        Next

        If mesh.CanFreeze Then mesh.Freeze()
        Return mesh
    End Function

    Public Shared Function RenderDisplacedMesh(source As MeshGeometry3D, height As Single(), w As Integer, h As Integer, exaggeration As Double, Optional includeBathymetry As Boolean = True, Optional resamplingMode As ResamplingMode = ResamplingMode.Bilinear) As MeshGeometry3D

        Dim dst As New MeshGeometry3D()

        '1) Indizes und Texturkoordinaten aus Sphere-Mesh kopieren
        dst.TriangleIndices = New Int32Collection(source.TriangleIndices)
        dst.TextureCoordinates = New PointCollection(source.TextureCoordinates)

        '2) Snapshots: WPF-Collections -> thread-safe Arrays
        Dim posCount As Integer = source.Positions.Count

        Dim srcPosArr(posCount - 1) As Point3D
        For i As Integer = 0 To posCount - 1
            srcPosArr(i) = source.Positions(i)
        Next

        Dim srcUvArr(posCount - 1) As Point
        For i As Integer = 0 To posCount - 1
            srcUvArr(i) = source.TextureCoordinates(i)
        Next

        Dim hasNormals As Boolean = (source.Normals IsNot Nothing AndAlso source.Normals.Count = posCount)
        Dim srcNrmArr() As Vector3D = Nothing
        If hasNormals Then
            ReDim srcNrmArr(posCount - 1)
            For i As Integer = 0 To posCount - 1
                srcNrmArr(i) = source.Normals(i)
            Next
        End If

        Dim scale As Double = (exaggeration / EarthRadiusM)
        Dim w1 As Integer = w - 1
        Dim h1 As Integer = h - 1

        '3) Positions parallel berechnen
        Dim dstPosArr(posCount - 1) As Point3D

        Parallel.For(0, posCount,
                    Sub(i As Integer)

                        Dim p0 As Point3D = srcPosArr(i)
                        Dim uv As Point = srcUvArr(i)

                        Dim uSample As Double = uv.X
                        Dim vSample As Double = uv.Y

                        Dim hm As Double =
                            If(resamplingMode = ResamplingMode.Bilinear,
                                SampleHeightBilinear(height, w, w1, h1, uSample, vSample),
                                SampleHeightNearest(height, w, h, uSample, vSample))

                        If Not includeBathymetry AndAlso hm < 0 Then hm = 0

                        Dim offset As Double = hm * scale

                        Dim n As Vector3D
                        If hasNormals Then
                            n = srcNrmArr(i)        'normalisiert vom Sphere-Mesh
                        Else
                            n = New Vector3D(p0.X, p0.Y, p0.Z)
                            If n.LengthSquared > 0 Then n.Normalize()
                        End If

                        dstPosArr(i) = New Point3D(
                            p0.X + n.X * offset,
                            p0.Y + n.Y * offset,
                            p0.Z + n.Z * offset)

                    End Sub)

        '4) Array -> WPF-Collection
        Dim pc As New Point3DCollection(posCount)
        For i As Integer = 0 To posCount - 1
            pc.Add(dstPosArr(i))
        Next
        dst.Positions = pc

        '5) Normals aus der verformten Geometrie berechnen
        RecalculateNormals(dst)

        If dst.CanFreeze Then dst.Freeze()
        Return dst

    End Function

    Public Shared Function RenderCylinderMeshY(radius As Double, height As Double, segments As Integer, Optional cap As Boolean = True) As MeshGeometry3D

        segments = Math.Max(6, segments)

        Dim mesh As New MeshGeometry3D()

        Dim halfH As Double = height * 0.5
        Dim stride As Integer = segments + 1

        'Mantel
        For i As Integer = 0 To segments
            Dim u As Double = i / CDbl(segments)
            Dim theta As Double = 2.0 * Math.PI * u

            Dim x As Double = Math.Cos(theta)
            Dim z As Double = Math.Sin(theta)

            'unten
            mesh.Positions.Add(New Point3D(radius * x, -halfH, radius * z))
            mesh.Normals.Add(New Vector3D(x, 0, z))
            mesh.TextureCoordinates.Add(New Point(u, 1))

            'oben
            mesh.Positions.Add(New Point3D(radius * x, halfH, radius * z))
            mesh.Normals.Add(New Vector3D(x, 0, z))
            mesh.TextureCoordinates.Add(New Point(u, 0))
        Next

        'Indizes Mantel
        For i As Integer = 0 To segments - 1
            Dim i0 As Integer = i * 2
            Dim i1 As Integer = i0 + 1
            Dim i2 As Integer = i0 + 2
            Dim i3 As Integer = i0 + 3

            'Winding so, dass Normalen nach außen zeigen
            mesh.TriangleIndices.Add(i0) : mesh.TriangleIndices.Add(i1) : mesh.TriangleIndices.Add(i2)
            mesh.TriangleIndices.Add(i2) : mesh.TriangleIndices.Add(i1) : mesh.TriangleIndices.Add(i3)
        Next

        If cap Then
            'Top cap
            Dim topCenterIndex As Integer = mesh.Positions.Count
            mesh.Positions.Add(New Point3D(0, halfH, 0))
            mesh.Normals.Add(New Vector3D(0, 1, 0))
            mesh.TextureCoordinates.Add(New Point(0.5, 0.5))

            Dim topRingStart As Integer = mesh.Positions.Count
            For i As Integer = 0 To segments
                Dim u As Double = i / CDbl(segments)
                Dim theta As Double = 2.0 * Math.PI * u

                Dim x As Double = Math.Cos(theta)
                Dim z As Double = Math.Sin(theta)

                mesh.Positions.Add(New Point3D(radius * x, halfH, radius * z))
                mesh.Normals.Add(New Vector3D(0, 1, 0))
                mesh.TextureCoordinates.Add(New Point(0.5 + 0.5 * x, 0.5 + 0.5 * z))
            Next

            For i As Integer = 0 To segments - 1
                Dim a As Integer = topRingStart + i
                Dim b As Integer = topRingStart + i + 1
                mesh.TriangleIndices.Add(topCenterIndex) : mesh.TriangleIndices.Add(a) : mesh.TriangleIndices.Add(b)
            Next

            'Bottom Cap
            Dim bottomCenterIndex As Integer = mesh.Positions.Count
            mesh.Positions.Add(New Point3D(0, -halfH, 0))
            mesh.Normals.Add(New Vector3D(0, -1, 0))
            mesh.TextureCoordinates.Add(New Point(0.5, 0.5))

            Dim bottomRingStart As Integer = mesh.Positions.Count
            For i As Integer = 0 To segments
                Dim u As Double = i / CDbl(segments)
                Dim theta As Double = 2.0 * Math.PI * u

                Dim x As Double = Math.Cos(theta)
                Dim z As Double = Math.Sin(theta)

                mesh.Positions.Add(New Point3D(radius * x, -halfH, radius * z))
                mesh.Normals.Add(New Vector3D(0, -1, 0))
                mesh.TextureCoordinates.Add(New Point(0.5 + 0.5 * x, 0.5 + 0.5 * z))
            Next

            For i As Integer = 0 To segments - 1
                Dim a As Integer = bottomRingStart + i
                Dim b As Integer = bottomRingStart + i + 1

                'Winding umdrehen, weil Normale nach -Y
                mesh.TriangleIndices.Add(bottomCenterIndex) : mesh.TriangleIndices.Add(b) : mesh.TriangleIndices.Add(a)
            Next
        End If

        If mesh.CanFreeze Then mesh.Freeze()
        Return mesh

    End Function

    Public Shared Function RenderPoleAxisModel() As GeometryModel3D

        'Globus-Radius = 1.0 -> Achse soll etwas überstehen
        Dim protrude As Double = 0.2
        Dim height As Double = 2.0 * (1.0 + protrude)
        Dim radius As Double = 0.001

        Dim mesh As MeshGeometry3D = GlobeMeshRenderer.RenderCylinderMeshY(radius, height, segments:=8, cap:=True)

        Dim brush As New SolidColorBrush(Color.FromRgb(255, 255, 255))
        If brush.CanFreeze Then brush.Freeze()

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(brush))
        mg.Children.Add(New EmissiveMaterial(brush))
        If mg.CanFreeze Then mg.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mg,
            .BackMaterial = mg
        }

        Return gm

    End Function

    Public Shared Function RenderSubsolarMarkerModel() As GeometryModel3D
        Dim mesh As MeshGeometry3D = GlobeMeshRenderer.RenderSphereMesh(radius:=0.01, lonSegments:=16, latSegments:=12)

        Dim brush As New SolidColorBrush(Color.FromRgb(255, 220, 80))     'Marker-Farbe
        If brush.CanFreeze Then brush.Freeze()

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(brush))
        mg.Children.Add(New EmissiveMaterial(brush))
        If mg.CanFreeze Then mg.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mg,
            .BackMaterial = mg
        }

        Return gm

    End Function

    Public Shared Function RenderDayNightTerminatorModel() As GeometryModel3D
        Dim mesh As New MeshGeometry3D()

        Dim brush As New SolidColorBrush(Color.FromRgb(180, 220, 255))
        If brush.CanFreeze Then brush.Freeze()

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(brush))
        mg.Children.Add(New EmissiveMaterial(brush))
        If mg.CanFreeze Then mg.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mg,
            .BackMaterial = mg
        }

        Return gm
    End Function

    Public Shared Function RenderDawnModel() As GeometryModel3D

        'Material: Emissive mit weichem Alpha-Band (0->max->o)
        Dim lg As New LinearGradientBrush()
        lg.StartPoint = New Point(0, 0)
        lg.EndPoint = New Point(0, 1)
        lg.GradientStops.Add(New GradientStop(Color.FromArgb(0, 120, 170, 255), 0.0))
        lg.GradientStops.Add(New GradientStop(Color.FromArgb(80, 120, 170, 255), 0.15))   'Peak-Alpha
        lg.GradientStops.Add(New GradientStop(Color.FromArgb(0, 120, 170, 255), 1.0))
        lg.Freeze()

        Dim mat As New EmissiveMaterial(lg)
        If mat.CanFreeze Then mat.Freeze()

        Dim mesh As New MeshGeometry3D()
        If mesh.CanFreeze Then mesh.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mat,
            .BackMaterial = mat
        }

        Return gm

    End Function

    Public Shared Function RenderObserverPinModel(radius As Double, height As Double, segments As Integer, Optional cap As Boolean = True) As GeometryModel3D

        'Kleiner "Pin-Stift" als Zylinder (Y-Achse)
        Dim mesh As MeshGeometry3D = RenderCylinderMeshY(radius, height, segments, cap)

        Dim brush As New SolidColorBrush(Color.FromRgb(255, 0, 0))     'rot
        If brush.CanFreeze Then brush.Freeze()

        Dim mg As New MaterialGroup()
        mg.Children.Add(New DiffuseMaterial(brush))
        mg.Children.Add(New EmissiveMaterial(brush))
        If mg.CanFreeze Then mg.Freeze()

        Dim gm As New GeometryModel3D With {
            .Geometry = mesh,
            .Material = mg,
            .BackMaterial = mg
        }

        Return gm

    End Function

#End Region

#Region "Resampling"

    Public Shared Function SampleHeightNearest(height As Single(), w As Integer, h As Integer, u As Double, v As Double) As Double

        'Wrap u /Clamp v
        u -= Math.Floor(u)
        v = Clamp(v, 0, 1)

        Dim x As Integer = CInt(Math.Round(u * (w - 1)))
        Dim y As Integer = CInt(Math.Round(v * (h - 1)))

        Dim idx As Integer = y * w + x
        If idx < 0 OrElse idx >= height.Length Then Return 0.0

        Return CDbl(height(idx))

    End Function

    Public Shared Function SampleHeightBilinear(height As Single(), w As Integer, w1 As Integer, h1 As Integer, u As Double, v As Double) As Double

        'Fast Clamp v
        If v <= 0.0 Then
            v = 0.0
        ElseIf v >= 1.0 Then
            v = 1.0
        End If

        'Fast Wrap u: in 99% der Fälle ist u bereits [0..1)
        'u kann durch nummerische Effekte minimal <0 oder >1 sein
        If u < 0.0 OrElse u >= 1.0 Then
            u -= Math.Floor(u) 'Wrap auf [0..1)
            'Falls u durch Rounding exakt 1.0 wird:
            If u >= 1.0 Then u = 0.0
        End If

        'In Pixelspace skalieren
        Dim fx As Double = u * w1
        Dim fy As Double = v * h1

        'Floor für positive Werte: CInt(truncate) == Floor
        Dim x0 As Integer = CInt(fx)
        Dim y0 As Integer = CInt(fy)

        'Nachbarpixel (X wrap, Y clamp)
        Dim x1 As Integer = If(x0 = w1, 0, x0 + 1)
        Dim y1 As Integer = If(y0 = h1, h1, y0 + 1)

        Dim tx As Double = fx - x0
        Dim ty As Double = fy - y0

        'Indexbasis einmalig berechnen
        Dim row0 As Integer = y0 * w
        Dim row1 As Integer = y1 * w


        Dim h00 As Double = height(row0 + x0)
        Dim h10 As Double = height(row0 + x1)
        Dim h01 As Double = height(row1 + x0)
        Dim h11 As Double = height(row1 + x1)

        'bilineares Interpolieren (2 lerps)
        Dim a As Double = h00 + (h10 - h00) * tx
        Dim b As Double = h01 + (h11 - h01) * tx

        Return a + (b - a) * ty

    End Function

#End Region

    Private Shared Sub RecalculateNormals(mesh As MeshGeometry3D)

        If mesh Is Nothing Then Return

        Dim posColl As Point3DCollection = mesh.Positions
        Dim idxColl As Int32Collection = mesh.TriangleIndices

        If posColl Is Nothing OrElse posColl.Count = 0 Then Return
        If idxColl Is Nothing OrElse idxColl.Count < 3 Then Return

        'Snapshot anlegen: WPF-Collections -> thread-safe Arrays
        Dim vCount As Integer = posColl.Count
        Dim idxCount As Integer = idxColl.Count

        Dim pos(vCount - 1) As Point3D
        For i As Integer = 0 To vCount - 1
            pos(i) = posColl(i)
        Next

        Dim idx(idxCount - 1) As Integer
        For i As Integer = 0 To idxCount - 1
            idx(i) = idxColl(i)
        Next

        Dim tricount As Integer = idxCount \ 3

        '=== 1) Pro Thread lokaler Akkumulator (kein Lock in der Hot-Loop) ===
        Dim locals As New List(Of Vector3D())()
        Dim localsLock As New Object()

        Parallel.For(
            0, tricount,
            Function() New Vector3D(vCount - 1) {},         'localInit: eigener Acc pro Thread
            Function(tri As Integer, state As ParallelLoopState, localAcc As Vector3D()) As Vector3D()

                Dim t As Integer = tri * 3
                Dim i0 As Integer = idx(t)
                Dim i1 As Integer = idx(t + 1)
                Dim i2 As Integer = idx(t + 2)

                Dim p0 As Point3D = pos(i0)
                Dim p1 As Point3D = pos(i1)
                Dim p2 As Point3D = pos(i2)

                Dim e1 As Vector3D = p1 - p0
                Dim e2 As Vector3D = p2 - p0

                Dim n As Vector3D = Vector3D.CrossProduct(e1, e2)
                If n.LengthSquared > 0 Then

                    Dim a As Vector3D

                    a = localAcc(i0) : a.X += n.X : a.Y += n.Y : a.Z += n.Z : localAcc(i0) = a
                    a = localAcc(i1) : a.X += n.X : a.Y += n.Y : a.Z += n.Z : localAcc(i1) = a
                    a = localAcc(i2) : a.X += n.X : a.Y += n.Y : a.Z += n.Z : localAcc(i2) = a

                End If

                Return localAcc
            End Function,
            Sub(localAcc As Vector3D())
                SyncLock localsLock
                    locals.Add(localAcc)
                End SyncLock
            End Sub)

        '=== 2) Reduktion: Thread-Akkus -> globaler Acc ===
        Dim acc(vCount - 1) As Vector3D

        'Parallel pro Vertex reduzieren
        Parallel.For(0, vCount,
                Sub(i As Integer)

                    Dim sx As Double = 0
                    Dim sy As Double = 0
                    Dim sz As Double = 0

                    For k As Integer = 0 To locals.Count - 1
                        Dim v As Vector3D = locals(k)(i)
                        sx += v.X
                        sy += v.Y
                        sz += v.Z
                    Next

                    acc(i) = New Vector3D(sx, sy, sz)

                End Sub)

        '=== 3) Normalisieren + Fallback radial
        Dim normalsArr(vCount - 1) As Vector3D

        Parallel.For(0, vCount,
                Sub(i As Integer)

                    Dim n As Vector3D = acc(i)

                    If n.LengthSquared > 0 Then
                        n.Normalize()
                    Else
                        'Fallback: radial
                        Dim p As Point3D = pos(i)
                        n = New Vector3D(p.X, p.Y, p.Z)
                        If n.LengthSquared > 0 Then n.Normalize()
                    End If

                    normalsArr(i) = n

                End Sub)

        'In WPF-Collection packen
        Dim normals As New Vector3DCollection(vCount)
        For i As Integer = 0 To vCount - 1
            normals.Add(normalsArr(i))
        Next

        mesh.Normals = normals
    End Sub

End Class
