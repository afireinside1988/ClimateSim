Imports System.Threading.Tasks

Public NotInheritable Class HeightSpikeAnalyzer

    Private NotInheritable Class LocalBuffers
        Public ReadOnly Neigh8 As Double()
        Public ReadOnly AbsDev8 As Double()
        Public ReadOnly Sort8 As Double()

        Public Sub New()
            Me.Neigh8 = New Double(7) {}
            Me.AbsDev8 = New Double(7) {}
            Me.Sort8 = New Double(7) {}
        End Sub
    End Class

    Private Sub New()

    End Sub

    Public Shared Function BuildSpikeMask(heightM As Single(),
                                          width As Integer,
                                          height As Integer,
                                          opts As HeightSpikeOptions,
                                          ByRef spikeCount As Integer) As Boolean()

        ArgumentNullException.ThrowIfNull(heightM)
        If width <= 0 OrElse height <= 0 Then Throw New ArgumentOutOfRangeException("Ungültige Rastergröße.")
        If heightM.Length <> width * height Then Throw New InvalidOperationException("HeightM-Array hat ungültige Größe.")

        Dim mask As Boolean() = New Boolean(heightM.Length - 1) {}
        Dim countTotal As Integer = 0
        Dim countLock As New Object()

        Parallel.For(0, height,
                    Function() New LocalBuffers(),
                    Function(y As Integer, state As ParallelLoopState, local As LocalBuffers) As LocalBuffers

                        Dim localCount As Integer = 0
                        Dim row As Integer = y * width

                        For x As Integer = 0 To width - 1

                            Dim idx As Integer = row + x

                            Dim center As Double = CDbl(heightM(idx))
                            If Double.IsNaN(center) OrElse Double.IsInfinity(center) Then
                                Continue For
                            End If

                            FillNeighbor8(heightM, width, height, x, y, local.Neigh8)

                            Dim minAbs As Double = Double.PositiveInfinity
                            For i As Integer = 0 To 7
                                Dim d As Double = Math.Abs(center - local.Neigh8(i))
                                If d < minAbs Then minAbs = d
                            Next

                            Dim med As Double = Median8(local.Neigh8, local.Sort8)
                            Dim dev As Double = Math.Abs(center - med)

                            'Robust Scale via MAD
                            For i As Integer = 0 To 7
                                local.AbsDev8(i) = Math.Abs(local.Neigh8(i) - med)
                            Next

                            Dim mad As Double = Median8(local.AbsDev8, local.Sort8)

                            Dim thr As Double = Math.Max(opts.MinAbsDeviationM, opts.RobustFactor * Math.Max(1.0, mad))
                            If dev > thr AndAlso minAbs > opts.MinNeighborDiffM Then
                                mask(idx) = True
                                localCount += 1
                            End If

                        Next

                        If localCount > 0 Then
                            SyncLock countLock
                                countTotal += localCount
                            End SyncLock
                        End If

                        Return local
                    End Function,
                    Sub(local As LocalBuffers)
                        'nothing
                    End Sub)

        spikeCount = countTotal
        Return mask
    End Function

    Private Shared Sub FillNeighbor8(heightM As Single(),
                                     width As Integer,
                                     height As Integer,
                                     x As Integer,
                                     y As Integer,
                                     neigh8 As Double())

        'Lon Wrap
        Dim xm As Integer = If(x = 0, width - 1, x - 1)
        Dim xp As Integer = If(x = width - 1, 0, x + 1)

        'Lat Clamp
        Dim ym As Integer = If(y = 0, 0, y - 1)
        Dim yp As Integer = If(y = height - 1, height - 1, y + 1)

        Dim rowYm As Integer = ym * width
        Dim rowY As Integer = y * width
        Dim rowYp As Integer = yp * width

        'Reihenfolge egal, aber konstant halten
        neigh8(0) = CDbl(heightM(rowYm + xm))
        neigh8(1) = CDbl(heightM(rowYm + x))
        neigh8(2) = CDbl(heightM(rowYm + xp))
        neigh8(3) = CDbl(heightM(rowY + xm))
        neigh8(4) = CDbl(heightM(rowY + xp))
        neigh8(5) = CDbl(heightM(rowYp + xm))
        neigh8(6) = CDbl(heightM(rowYp + x))
        neigh8(7) = CDbl(heightM(rowYp + xp))
    End Sub

    Private Shared Function Median8(values As Double(), sort8 As Double()) As Double

        For i As Integer = 0 To 7
            sort8(i) = values(i)
        Next

        Array.Sort(sort8)

        Return (sort8(3) + sort8(4)) / 2.0
    End Function
End Class
