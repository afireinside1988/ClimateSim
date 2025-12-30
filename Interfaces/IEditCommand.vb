Public Interface IEditCommand
    ReadOnly Property Description As String
    Sub Apply(session As EarthSurfaceEditSession)
    Sub Revert(session As EarthSurfaceEditSession)

End Interface
