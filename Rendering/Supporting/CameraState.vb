Public Structure CameraState
    Public Property CenterLat As Double         'Clamp -90..+90
    Public Property CenterLon As Double         'unbounded, wrap erst bei Sampling
    Public SpanLat As Double                    'z.b. 180 = ganze Welt in Höhe
    Public SpanLon As Double                    'z.b. 360 = ganze Welt in Breite

    Public Shared ReadOnly Property World As CameraState
        Get
            Return New CameraState With {.CenterLat = 0, .CenterLon = 0, .SpanLat = 180, .SpanLon = 360}
        End Get
    End Property

End Structure
