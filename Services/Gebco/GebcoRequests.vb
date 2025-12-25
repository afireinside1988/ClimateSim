Imports System.Runtime.InteropServices


''' <summary>
''' Index für Row-Spans eines Tiles:
''' RowStart() = Startindex im Request-Array (oder -1 wenn leer)
''' RowCount() = Anzahl Requests in dieser Row
''' </summary>
Public Structure TileRowIndex
    Public RowStart() As Integer        'Len = TileSize
    Public RowCount() As Integer        'Len = TileSize
End Structure


''' <summary>
''' Requests eines Tiles als zusammenhängendes Array + RowIndex-Spans
''' </summary>
''' <typeparam name="TReq"></typeparam>
Public Structure TileRequests(Of TReq)
    Public Index As TileRowIndex
    Public Requests() As TReq
End Structure

''' <summary>
''' Kompaktformat für Bilinear-Requests
''' Col0/Col1 passen in UShort (0..21599).
''' </summary>
<StructLayout(LayoutKind.Sequential)>
Public Structure BilinearRequestPacked
    Public TargetIndex As Integer
    Public Part As Byte             '0=Row0, 1=Row1

    Public Col0 As UShort
    Public Col1 As UShort

    Public Wx As Single
    Public Wy As Single
End Structure

<StructLayout(LayoutKind.Sequential)>
Public Structure NearestRequestPacked
    Public TargetIndex As Integer
    Public Col As UShort
End Structure


