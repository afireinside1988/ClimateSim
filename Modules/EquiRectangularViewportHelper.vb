
Module EquiRectangularViewportHelper

    Public Structure CellHit
        Public LatIdx As Integer
        Public LonIdx As Integer
        Public Index As Integer
        Public Lat As Double
        Public Lon As Double
    End Structure

#Region "Öffentliche Funktionen"

    ''' <summary>
    ''' Ermittelt aus der Position des Mauszeigers über dem Viewport die Zell-Koordinaten in Breiten- und Längengrad
    ''' </summary>
    Public Function TryHitCell(mousePos As Point, viewportSize As Size,
                               contentSize As Size, cellSizeDeg As Double,
                               camera As CameraState, zoom As Double, panPoint As Point,
                               ByRef hit As CellHit) As Boolean

        hit = Nothing

        Dim latCount As Integer = CInt(Math.Floor(contentSize.Height))
        Dim lonCount As Integer = CInt(Math.Floor(contentSize.Width))

        Dim geo = EquiRectangularViewportHelper.ScreenToGeo(mousePos, viewportSize, contentSize, camera, zoom, panPoint)
        If Double.IsNaN(geo.Lat) OrElse Double.IsNaN(geo.Lon) Then Return False

        Dim cell As Double = cellSizeDeg

        Dim latIdx As Integer = CInt(Math.Floor((90.0 - geo.Lat) / cell))
        latIdx = Clamp(latIdx, 0, latCount - 1)

        Dim lonIdx As Integer = CInt(Math.Floor((geo.Lon + 180.0) / cell))
        lonIdx = Clamp(lonIdx, 0, lonCount - 1)

        Dim idx As Integer = latIdx * lonCount + lonIdx

        hit = New CellHit With {
            .LatIdx = latIdx,
            .LonIdx = lonIdx,
            .Index = idx,
            .Lat = geo.Lat,
            .Lon = geo.Lon
        }

        Return True
    End Function

    ''' <summary>
    ''' Begrenzt das Panning des Content im Viewport auf die Content-Dimensionen
    ''' </summary>
    Public Sub ClampPan(viewportSize As Size, contentSize As Size, zoom As Double, ByRef panPoint As Point)

        Dim scaledW As Double = contentSize.Width * zoom
        Dim scaledH As Double = contentSize.Height * zoom

        'Wenn Content kleiner als Viewport: zentrieren (statt oben links lassen)
        If scaledW <= viewportSize.Width Then
            panPoint.X = (viewportSize.Width - scaledW) / 2.0
        Else
            Dim minX As Double = viewportSize.Width - scaledW
            panPoint.X = Clamp(panPoint.X, minX, 0)
        End If

        If scaledH <= viewportSize.Height Then
            panPoint.Y = (viewportSize.Height - scaledH) / 2.0
        Else
            Dim minY As Double = viewportSize.Height - scaledH
            panPoint.Y = Clamp(panPoint.Y, minY, 0)
        End If

    End Sub

    ''' <summary>
    ''' Passt die Content-Größe an die aktuelle Viewport-Größe an
    ''' </summary>
    Public Sub FitToViewport(viewportSize As Size, contentSize As Size, ByRef zoom As Double, ByRef panPoint As Point)

        If viewportSize.Height <= 0 OrElse viewportSize.Width <= 0 Then Return
        If contentSize.Width <= 0 OrElse contentSize.Height <= 0 Then Return

        Dim fitZoom As Double = Math.Min(viewportSize.Width / contentSize.Width, viewportSize.Height / contentSize.Height)

        'Optional: nicht größer als 1 hochskalieren
        fitZoom = Math.Min(fitZoom, 1.0)

        Dim z As Double = Math.Floor(fitZoom * 100) / 100.0
        zoom = Clamp(z, 0.05, 20.0)

        'Zentrieren
        panPoint.X = (viewportSize.Width - contentSize.Width * zoom) / 2.0
        panPoint.Y = (viewportSize.Height - contentSize.Height * zoom) / 2.0

        'Sicherheit
        EquiRectangularViewportHelper.ClampPan(viewportSize, contentSize, zoom, panPoint)

    End Sub

    ''' <summary>
    ''' Übergibt die aktuelle Viewport-Größe an einen Cache
    ''' </summary>
    ''' <param name="vp"></param>
    ''' <param name="lastViewportW"></param>
    ''' <param name="lastViewportH"></param>
    Public Sub RememberViewportSize(vp As Size, ByRef lastViewportSize As Size)
        If vp.Width > 0 Then lastViewportSize.Width = vp.Width
        If vp.Height > 0 Then lastViewportSize.Height = vp.Height
    End Sub

    ''' <summary>
    ''' Begrenzt den Zoombereich
    ''' </summary>
    Public Function SnapZoom(value As Double, wheelDelta As Integer, minZoom As Double, maxZoom As Double) As Double

        value = Clamp(value, minZoom, maxZoom)

        Dim stepSize As Double = GetZoomStepSize(value)

        If wheelDelta > 0 Then
            'hoch -> nächster Wert >= value
            Return Clamp(Math.Ceiling(value / stepSize) * stepSize, minZoom, maxZoom)
        ElseIf wheelDelta < 0 Then
            'runter -> nächster Wert <= value
            Return Clamp(Math.Floor(value / stepSize) * stepSize, minZoom, maxZoom)
        Else
            Return value
        End If
    End Function

#End Region



#Region "Private Funktionen"

    Private Function ScreenToGeo(mousePos As Point,
                                       viewPortSize As Size,
                                       contentSize As Size,
                                       camera As CameraState,
                                       zoom As Double,
                                       panPoint As Point) As (Lat As Double, Lon As Double)

        If viewPortSize.Width <= 0 OrElse viewPortSize.Height <= 0 Then
            Return (Double.NaN, Double.NaN)
        End If

        If contentSize.Width <= 0 OrElse contentSize.Height <= 0 Then
            Return (Double.NaN, Double.NaN)
        End If
        If zoom <= 0 Then Return (Double.NaN, Double.NaN)

        'Mausposition in "Content Space" zurückrechnen (Inverse des RenderTransforms)
        Dim xContent As Double = (mousePos.X - panPoint.X) / zoom
        Dim yContent As Double = (mousePos.Y - panPoint.Y) / zoom

        If xContent < 0 OrElse xContent >= contentSize.Width OrElse yContent < 0 OrElse yContent >= contentSize.Height Then
            Return (Double.NaN, Double.NaN)
        End If

        'Normierte Koordinaten
        Dim xNorm As Double = xContent / contentSize.Width
        Dim yNorm As Double = yContent / contentSize.Height

        'Geo berechnen (inverse Render-Formel)
        Dim lon As Double = camera.CenterLon + (xNorm - 0.5) * camera.SpanLon
        Dim lat As Double = camera.CenterLat + (0.5 - yNorm) * camera.SpanLat

        'Clamp/Wrap
        lat = Clamp(lat, -90.0, 90.0)
        lon = Wrap180(lon)

        Return (lat, lon)

    End Function

    Private Function GetZoomStepSize(z As Double) As Double
        'Schrittweite je nach Zoom-Bereich (fühlt sich "dynamisch" an)
        If z < 0.75 Then Return 0.05
        If z < 1.5 Then Return 0.1
        If z < 3.0 Then Return 0.25
        If z < 8.0 Then Return 0.5
        Return 1.0
    End Function

#End Region
End Module
