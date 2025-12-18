Public Class AsciiGridHeader

    Public Property NCols As Integer
    Public Property NRows As Integer
    Public Property XllCorner As Double?
    Public Property YllCorner As Double?
    Public Property XllCenter As Double?
    Public Property YllCenter As Double?
    Public Property CellSize As Double
    Public Property NoDataValue As Double?

    Public ReadOnly Property HasCornerOrigin As Boolean
        Get
            Return XllCorner.HasValue AndAlso YllCorner.HasValue
        End Get
    End Property

    Public ReadOnly Property HasCenterOrigin As Boolean
        Get
            Return XllCenter.HasValue AndAlso YllCenter.HasValue
        End Get
    End Property

End Class
