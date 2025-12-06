Imports System.Windows.Threading
Imports System.Globalization
Imports System.Threading
Imports System.Runtime.InteropServices
Imports System.ComponentModel

Class MainWindow

    <StructLayout(LayoutKind.Sequential, CharSet:=CharSet.Auto)>
    Private Structure MEMORYSTATUSEX
        Public dwLength As UInteger
        Public dwMemoryLoad As UInteger
        Public ullTotalPhys As ULong
        Public ullAvailPhys As ULong
        Public ullTotalPageFile As ULong
        Public ullAvailPageFile As ULong
        Public ullTotalVirtual As ULong
        Public ullAvailVirtual As ULong
        Public ullAvailExtendedVirtual As ULong
    End Structure

    <DllImport("kernel32.dll", CharSet:=CharSet.Auto, SetLastError:=True)>
    Private Shared Function GlobalMemoryStatusEx(ByRef lpBuffer As MEMORYSTATUSEX) As Boolean
    End Function

    'Simulations-Engine
    Private _engine As SimulationEngine

    'Felder fürs Multi-Threading
    Private _simCts As CancellationTokenSource

    Private _memoryEstimateOk As Boolean = True

    'Default-Werte
    Private _endYear As Integer = 2100 'Standardwert, wird aus Textbox gelesen
    Private _currentLayer As MapLayer = MapLayer.Temperature

    'MVVM-Implementierung
    Private _viewModel As MainViewModel

    Public Sub RefreshFromEngine()
        If _engine Is Nothing Then Return

        UpdateCO2Display(_engine.Model.CO2ppm)
        RenderTemperatureLayer()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        _viewModel = New MainViewModel()
        Me.DataContext = _viewModel

        _engine = _viewModel.Engine

        '--- UI-Handler ---
        'Auf Änderungen im ViewModel reagieren
        AddHandler _viewModel.PropertyChanged, AddressOf ViewModel_PropertyChanged

        'Auf Command-Events reagieren
        AddHandler _viewModel.StartSimulationRequested, AddressOf OnStartSimulationRequested
        AddHandler _viewModel.StopSimulationRequested, AddressOf OnStopSimulationRequested
        AddHandler _viewModel.SpinUpRequested, AddressOf OnSpinUpRequested
        AddHandler _viewModel.StepRequested, AddressOf OnStepRequested
        AddHandler _viewModel.ShowHistoryRequested, AddressOf OnShowHistoryRequested

        'Mouseovers
        AddHandler ImgTemperature.MouseMove, AddressOf ImgTemperature_MouseMove
        AddHandler ImgTemperature.MouseLeave, AddressOf ImgTemperature_MouseLeave

        '--- Buttons & Layer initial sperren ---
        _viewModel.IsTemperatureLayerVisible = False
        ChkShowTemperature.IsChecked = False

        'Erdoberfläche initialisieren
        _engine.Initialize(360, 180, 1850)
        ApplyViewModelToModel()
        RenderSurfaceLayer()

        'Status setzen
        _viewModel.StatusText = "Bitte Spin-Up starten."
        UpdateMemoryEstimate()

    End Sub

    Private Async Sub OnSpinUpRequested(sender As Object, e As EventArgs)

        '1) Simulations-Settings lesen
        Dim startYear As Integer, endYear As Integer
        Dim dtYears As Double
        If Not TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=True) Then
            Return
        End If

        '2) Gitterauflösung lesen
        Dim width As Integer, height As Integer
        If Not Integer.TryParse(_viewModel.GridWidth, width) OrElse Not Integer.TryParse(_viewModel.GridHeigth, height) Then
            MessageBox.Show("Bitte gültige Rasterauflösung angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End If



        '3) Spin-Up-Konfiguration anhängig vom TimeStepMode
        Dim spinUpYears As Integer = 200
        Dim spinUpDtYears As Double
        Dim spinUpUseSeasonal As Boolean

        Select Case _viewModel.TimeStepMode
            Case TimeStepMode.Month, TimeStepMode.Quarter   'Feiner Modus -> saisonales EBM
                spinUpDtYears = 0.25                        'Quartalsschritte zur Beschleunigung des Spin-Ups
                spinUpUseSeasonal = True
            Case TimeStepMode.Year, TimeStepMode.Decade     'Grober Modus -> Budyko/Sellers-EBM
                spinUpDtYears = 1.0                         'Jahresschritte
                spinUpUseSeasonal = False
        End Select

        Dim spinUpStartYear As Integer = startYear - spinUpYears

        '4) CO2-Wert zum eigentlichen Startjahr als Fixwert für den Spin-Up
        Dim co2AtStart As Double = If(_engine.CO2Scenario IsNot Nothing, _engine.CO2Scenario.GetCO2ForYear(startYear), 280.0)

        '5) Engine initialisieren mit spinUpStartYear
        _engine.Initialize(width, height, spinUpStartYear)

        'Lambda aus dem ViewModel ins Modell übernehmen
        ApplyViewModelToModel()

        'Basis-Layer rendern
        RenderSurfaceLayer()

        '6) EBM-Modus für den Spin-Up setzen
        _engine.Model.UseSeasonCycle = spinUpUseSeasonal

        If spinUpUseSeasonal Then
            _engine.Model.CurrentYearFraction = 0.25        'Bei saisonalem EBM mit Frühling starten (Nähe des Jahresmittels)
        Else
            _engine.Model.CurrentYearFraction = 0.0         'Bei Budyko/Sellers-EBM mit Jahresbeginn, Jahresphase spielt keine Rolle
        End If

        '7) Spin-Up-Modus einschalten
        _engine.IsSpinUp = True
        _engine.SpinUpCO2ppm = co2AtStart

        'UI sperren
        _viewModel.IsSimulationRunning = True

        Dim cts As New CancellationTokenSource()
        _simCts = cts

        '8) Spin-Up im Hintergrund laufen lassen
        Try
            Await Task.Run(Sub() RunSpinUpLoop(spinUpStartYear, startYear, spinUpDtYears, cts.Token))

            'Wenn Spin-Up abgebrochen wurde, darauf reagieren
            If _simCts IsNot Nothing AndAlso _simCts.IsCancellationRequested Then
                Dispatcher.Invoke(Sub()
                                      _viewModel.StatusText = "Spin-Up abgebrochen."
                                      'UI teilweise wieder freigeben, aber NICHT als "initialized" markieren
                                      BtnSpinUp.IsEnabled = True
                                  End Sub)

                Return '<<< da Spin-Up abgebrochen wurde, nicht weiter initialisieren

            End If

            '9) Nach dem Spin-Up: Startjahr zurücksetzen
            _engine.IsSpinUp = False
            _engine.StartYear = startYear
            _engine.SimTimeYears = 0.0
            _engine.CurrentYear = startYear

            'History & Snapshots leeren
            _engine.History.Clear()
            _engine.Snapshots.Clear()

            '10) Initial-History-Eintrag mit "echtem" CO2-Szenario
            Dim co2Now As Double = If(_engine.CO2Scenario IsNot Nothing, _engine.CO2Scenario.GetCO2ForYear(startYear), co2AtStart)
            _engine.Model.CO2ppm = co2Now

            Dim meanC As Double = _engine.Grid.ComputeGlobalMeanTemperatureC()

            _engine.History.Add(New SimulationRecord With {
                                .SimTimeYears = 0.0,
                                .Year = startYear,
                                .GlobalMeanTempC = meanC,
                                .CO2ppm = co2Now
                                })

            Dim snap As GridSnapshot = _engine.CreateSnapshotFromGrid()
            If snap IsNot Nothing Then
                _engine.Snapshots.Add(snap)
            End If

            '11) EBM-Modus jetzt wieder an den TimeStepMode der "eigentlichen" Simulation anpassen
            ApplyTimeStepModeToModel()

            '12) UI aktualisieren & freigeben

            Dispatcher.Invoke(
                Sub()
                    RenderTemperatureLayer()
                    UpdateSimTimeDisplay()
                    UpdateCO2Display(co2Now)

                    _viewModel.IsInitialized = True
                    EnableUIAfterSpinUp()
                    _viewModel.StatusText = "Spin-Up angeschlossen. Modell bereit."
                End Sub)
        Catch ex As Exception
            MessageBox.Show($"Fehler beim Spin-Up: {ex.Message}", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            _engine.IsSpinUp = False
            Dispatcher.Invoke(Sub()
                                  _viewModel.IsSimulationRunning = False
                              End Sub)
        End Try
    End Sub

    Private Sub OnStepRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _viewModel.IsSimulationRunning Then
            MessageBox.Show("Bitte zuerst initialiseren bzw. laufende Simulation stoppen.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtYears As Double = GetDtYearsFromMode()

        SimulateOneStep(dtYears)

    End Sub

    Private Async Sub OnStartSimulationRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim startYear As Integer
        Dim endYear As Integer
        Dim dtYears As Double

        If Not TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=True) Then
            'Ungültige Werte wurden nicht korrigiert -> Abbrechen
            Exit Sub
        End If

        _endYear = endYear

        ApplyViewModelToModel()   'Modellparamter einmalig vor Start aus der UI übernehmen
        ApplyTimeStepModeToModel()      'Gewählten TimeStep an Modell übergeben

        _viewModel.IsSimulationRunning = True
        _simCts = New CancellationTokenSource()


        SetSimulationUIState(True)      'UI-Buttons sperren/umschalten

        Try
            'Simulation im Hintergrund-Thread laufen lassen
            Await Task.Run(Sub() RunSimulationLoop(dtYears, endYear, _simCts.Token))
        Catch ex As Exception
            'nur echte Fehler anzeigen
            MessageBox.Show($"Fehler in der Simulation: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            _viewModel.IsSimulationRunning = False
            SetSimulationUIState(False)
        End Try
    End Sub

    Private Sub OnStopSimulationRequested(sender As Object, e As EventArgs)
        If _simCts IsNot Nothing AndAlso Not _simCts.IsCancellationRequested Then
            _simCts.Cancel()
        End If
    End Sub

    Private Sub OnShowHistoryRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.History.Count = 0 Then
            MessageBox.Show("Keine Simulationsdaten vorhanden.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim wnd As New HistoryWindow(_engine, _viewModel.TimeStepMode)
        wnd.Owner = Me
        wnd.Show()
    End Sub

    Private Sub ImgTemperature_MouseMove(sender As Object, e As MouseEventArgs)
        If _engine Is Nothing OrElse _engine.Grid Is Nothing Then
            ClearStatusBar()
            Return
        End If

        Dim img = DirectCast(sender, Image)

        'Position der Maus relativ zum Image
        Dim pos = e.GetPosition(img)

        'Falls Maus außerhalb des tatsächlich sichtbaren Bereichs: kein ToolTip
        If pos.X < 0 OrElse pos.Y < 0 OrElse
                pos.X > img.ActualWidth OrElse pos.Y > img.ActualHeight Then
            ClearStatusBar()
            Return
        End If

        Dim gridWidth = _engine.Grid.Width
        Dim gridHeight = _engine.Grid.Height

        If gridWidth <= 0 OrElse gridHeight <= 0 Then
            ClearStatusBar()
            Return
        End If

        'Pixelkoordinate in der Simulation ermitteln
        Dim lonIndex As Integer = CInt(Math.Floor(pos.X / img.ActualWidth * gridWidth))
        Dim latIndex As Integer = CInt(Math.Floor(pos.Y / img.ActualHeight * gridHeight))

        'Clamp für Sicherheit
        lonIndex = Math.Max(0, Math.Min(gridWidth - 1, lonIndex))
        latIndex = Math.Max(0, Math.Min(gridHeight - 1, latIndex))

        Dim cell As ClimateCell = _engine.Grid.GetCell(latIndex, lonIndex)
        If cell Is Nothing Then
            ClearStatusBar()
            Return
        End If

        Dim tempC As Double = cell.TemperatureK - 273.5
        Dim latDeg As Double = cell.LatitudeDeg
        Dim lonDeg As Double = cell.LongitudeDeg
        Dim surfaceName As String = cell.Surface.ToString()

        _viewModel.StatusLatText = String.Format(CultureInfo.InvariantCulture, "Lat: {0:F1}°", latDeg)
        _viewModel.StatusLonText = String.Format(CultureInfo.InvariantCulture, "Lon: {0:F1}°", lonDeg)
        _viewModel.StatusTempText = String.Format(CultureInfo.InvariantCulture, "Temp: {0:F2} °C", tempC)
        _viewModel.StatusSurfaceText = $"Surface: {surfaceName}"
    End Sub

    Private Sub ImgTemperature_MouseLeave(sender As Object, e As MouseEventArgs)
        ClearStatusBar()
    End Sub



    Private Sub ViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        Select Case e.PropertyName
            Case NameOf(MainViewModel.GridWidth),
                 NameOf(MainViewModel.GridHeigth),
                 NameOf(MainViewModel.StartYear),
                 NameOf(MainViewModel.EndYear),
                 NameOf(MainViewModel.TimeStepMode)
                UpdateMemoryEstimate()

            Case NameOf(MainViewModel.CO2Value)
                'CO2 aus dem ViewModel ins Modell übertragen
                If _engine IsNot Nothing AndAlso _engine.Model IsNot Nothing Then
                    _engine.Model.CO2ppm = _viewModel.CO2Value
                End If
            Case NameOf(MainViewModel.Lambda)
                'Klimasensitivität ins Modell durchreichen
                ApplyViewModelToModel()
        End Select
    End Sub

    Private Sub ClearStatusBar()
        If _viewModel Is Nothing Then Return

        _viewModel.StatusLatText = "Lat: -"
        _viewModel.StatusLonText = "Lon: -"
        _viewModel.StatusTempText = "Temp: -"
        _viewModel.StatusSurfaceText = "Surface: -"
    End Sub

    Private Sub RunSpinUpLoop(spinUpStartYear As Integer, targetStartYear As Integer, dtYears As Double, token As CancellationToken)
        Dim totalYears As Double = targetStartYear - spinUpStartYear
        Dim totalSteps As Integer = CInt(Math.Ceiling(totalYears / dtYears))
        If totalSteps <= 0 Then Return

        For stepIndex As Integer = 1 To totalSteps
            If token.IsCancellationRequested Then
                Exit For
            End If

            _engine.StepSimulation(dtYears)

            Dim progress As Double = stepIndex / CDbl(totalSteps)

            'Status im UI aktualisieren
            Dispatcher.Invoke(
                Sub()
                    _viewModel.StatusText = $"Spin-Up: {progress * 100.0:F1} %"
                End Sub, DispatcherPriority.Background, CancellationToken.None)
        Next

    End Sub

    Private Sub RunSimulationLoop(dtYears As Double, endYear As Double, token As CancellationToken)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _engine.Grid Is Nothing Then Return

        Dim uiUpdateInterval As Integer = 10 'Aktualisierungsrate der UI
        Dim stepCounter As Integer = 0

        While _engine.CurrentYear < endYear AndAlso Not token.IsCancellationRequested

            '1) Simulationsschritt ausführen (im Hintergrundthread, rein nummerisch
            _engine.StepSimulation(dtYears)
            stepCounter += 1

            '2) in moderatem Rhythmus die UI aktualisieren
            If stepCounter Mod uiUpdateInterval = 0 Then
                Dispatcher.Invoke(
                    Sub()
                        UpdateSimTimeDisplay()
                        UpdateCO2Display(_engine.Model.CO2ppm)
                        RenderTemperatureLayer()
                    End Sub)
            End If
        End While

        'Am Ende final UI refresh
        Dispatcher.Invoke(
            Sub()
                UpdateSimTimeDisplay()
                UpdateCO2Display(_engine.Model.CO2ppm)
                RenderTemperatureLayer()
            End Sub)
    End Sub

    <Obsolete("Wird durch Spin-Up-Routine nicht mehr benötigt")>
    Private Sub InitializeModelAndRender()
        Try

            'Gitternetz-Auflösung aus UI holen

            'Startjahr aus Textbox lesen
            Dim startYear As Integer
            Dim endYear As Integer
            Dim dtYears As Double

            If Not TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=False) Then
                'Wenn die Werte nicht stimmen, Initialisierung abbrechen
                Exit Sub
            End If

            _endYear = endYear

            Dim width As Integer = Integer.Parse(TxtWidth.Text)
            Dim height As Integer = Integer.Parse(TxtHeigth.Text)

            'Simulations-Engine initialisieren
            _engine.Initialize(width, height, startYear)

            'Lambda aus UI holen
            ApplyViewModelToModel()

            'Basis-Layer rendern
            RenderSurfaceLayer()

            'Anzeige aktualisieren
            UpdateSimTimeDisplay()
            UpdateCO2Display(_engine.Model.CO2ppm)
            RenderTemperatureLayer()

        Catch ex As Exception
            MessageBox.Show("Fehler bei der Initialisierung des Modells: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub RenderSurfaceLayer()
        If _viewModel Is Nothing OrElse _engine Is Nothing OrElse _engine.Grid Is Nothing Then Return

        Dim bmp = SurfaceTypeRenderer.RenderSurfaceType(_engine.Grid)
        _viewModel.SurfaceImage = bmp
    End Sub

    Private Sub RenderTemperatureLayer()
        If _viewModel Is Nothing OrElse _engine Is Nothing OrElse _engine.Grid Is Nothing Then Return

        'Farbskala-Extremwerte
        Dim tMinC As Double = -50.0
        Dim tMaxC As Double = 40.0

        Dim bmp As WriteableBitmap = TemperatureRenderer.RenderTemperatureField(_engine.Grid, tMinC, tMaxC)
        _viewModel.TemperatureImage = bmp

        'Globalen Mittelwert anzeigen
        Dim meanC As Double = _engine.Grid.ComputeGlobalMeanTemperatureC()
        _viewModel.GlobalMeanText = $"{meanC:F2} °C"

    End Sub

    Private Sub SimulateOneStep(dtYears As Double)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        _engine.StepSimulation(dtYears)

        UpdateCO2Display(_engine.Model.CO2ppm)

        'Visualisierung aktualisieren
        RenderTemperatureLayer()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub UpdateSimTimeDisplay()
        _viewModel.SimTimeText = $"{_engine.SimTimeYears:F1} Jahre"
        _viewModel.CurrentYearText = FormatYearWithStepMode(_engine.CurrentYear, _viewModel.TimeStepMode)
    End Sub

    Private Sub UpdateCO2Display(co2 As Double)
        If _viewModel Is Nothing Then Return

        'Clamp in den Sliderbereich (damit ViewModel + Slider konsistent bleiben)
        Dim clamped As Double = Math.Max(280.0, Math.Min(1000.0, co2))
        _viewModel.CO2Value = clamped
    End Sub

    Private Sub SetSimulationUIState(isRunning As Boolean)
        'Buttons werden jetzt über Commands/CanExecute gesteuert.
        'Bei Bedarf noch andere UI-Elemente steuern oder später als Obsolete rausschmeißen
    End Sub

    <Obsolete("Brauchen wir durch MVVM nicht mehr")>
    Private Sub SetUIDuringSpinUp(isRunning As Boolean)
        If isRunning Then
            'BtnStop.IsEnabled = True 'Stop während SpinUp explizit erlaubt
            ChkShowTemperature.IsEnabled = False
            SldTemperatureOpacity.IsEnabled = False
        Else
            ChkShowTemperature.IsEnabled = True
            SldTemperatureOpacity.IsEnabled = True
        End If

    End Sub

    Private Sub EnableUIAfterSpinUp()
        'Buttons durch Commands/CanExecute geregelt
        ChkShowTemperature.IsEnabled = True
        _viewModel.IsTemperatureLayerVisible = True
        SldTemperatureOpacity.IsEnabled = True
    End Sub

    ''' <summary>
    ''' Liest Startjahr, Endjahr und dt aus den Textboxen und normiert sie.
    ''' </summary>
    ''' <param name="startYear">Startjahr der Simulation</param>
    ''' <param name="endYear">Endjahr der Simulation</param>
    ''' <param name="dtYears">Simulations-Ticks in Jahren</param>
    ''' <param name="showMessages">Legt fest, ob Fehlermeldungen angezeigt werden sollen. Wenn False, werden automatisch Standardwerte festgesetzt.</param>
    ''' <returns>Gibt True zurück, wenn alle Werte korrekt sind oder korrigiert wurden, sonst False</returns>
    Private Function TryReadSimulationSettings(ByRef startYear As Integer, ByRef endYear As Integer, ByRef dtYears As Double, Optional showMessages As Boolean = True) As Boolean

        If _viewModel Is Nothing Then Return False

        '--- Start- und Endjahr direkt aus dem ViewModel  ---
        startYear = _viewModel.StartYear
        endYear = _viewModel.EndYear

        '--- Endjahr > Startjahr erzwingen
        If endYear <= startYear Then
            Dim suggestedEnd As Integer = startYear + 250

            If showMessages Then
                Dim errMsg As MessageBoxResult =
                    MessageBox.Show($"Das Endjahr muss größer als das Startjahr sein. " &
                                    $"Soll es auf {suggestedEnd} gesetzt werden?",
                                    MessageBoxButton.YesNo,
                                    MessageBoxImage.Warning,
                                    MessageBoxResult.Yes)

                If errMsg = MessageBoxResult.Yes Then
                    endYear = suggestedEnd
                    _viewModel.EndYear = suggestedEnd 'ViewModel (und damit TextBox) aktualisieren
                Else
                    Return False
                End If
            Else
                endYear = suggestedEnd
                _viewModel.EndYear = suggestedEnd
            End If
        End If

        '--- dtYears kommt jetzt ausschließlich aus dem TimeStepMode---
        dtYears = GetDtYearsFromMode()

        Return True
    End Function

    Private Function GetDtYearsFromMode() As Double

        Dim mode As TimeStepMode = If(_viewModel IsNot Nothing, _viewModel.TimeStepMode, TimeStepMode.Year)


        Select Case mode
            Case TimeStepMode.Month
                Return (1.0 / 12.0)
            Case TimeStepMode.Quarter
                Return 0.25
            Case TimeStepMode.Year
                Return 1
            Case TimeStepMode.Decade
                Return 10
            Case Else
                Return 1
        End Select
    End Function

    Private Function EstimateMemoryUsageBytes(width As Integer, height As Integer, startYear As Integer, endYear As Integer, dtYears As Double) As Long
        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)
        If dtYears <= 0.0 OrElse totalYears <= 0.0 Then Return 0

        Dim steps As Long = CLng(Math.Ceiling(totalYears / dtYears))
        Dim cells As Long = CLng(width) * CLng(height)

        'Double pro Zelle
        Dim bytesPerSnapshot As Double = cells * 8.0

        'Overhead-Faktor
        Dim overheadFactor As Double = 1.3 '30% Overhead

        Dim totalBytes As Double = steps * bytesPerSnapshot * overheadFactor
        If totalBytes > Long.MaxValue Then
            Return Long.MaxValue
        End If

        Return CLng(totalBytes)
    End Function

    Private Function GetAvailablePhysicalMemoryBytes() As Long
        Dim mem As New MEMORYSTATUSEX()
        mem.dwLength = CUInt(Marshal.SizeOf(Of MEMORYSTATUSEX)())

        If Not GlobalMemoryStatusEx(mem) Then
            Return 0
        End If

        If mem.ullAvailPhys > Long.MaxValue Then
            Return Long.MaxValue
        End If

        Return CLng(mem.ullAvailPhys)
    End Function

    Private Sub UpdateMemoryEstimate()
        If _viewModel Is Nothing Then Return

        Dim width As Integer = _viewModel.GridWidth
        Dim height As Integer = _viewModel.GridHeigth
        Dim startYear As Integer = _viewModel.StartYear
        Dim endYear As Integer = _viewModel.EndYear

        Dim dtYears As Double = GetDtYearsFromMode()

        Dim totalYears As Double = Math.Max(0.0, endYear - startYear)
        If width <= 0 OrElse height <= 0 OrElse totalYears <= 0 OrElse dtYears <= 0 Then
            _viewModel.MemoryEstimateText = "Speicherprognose: n/a"
            _viewModel.MemoryEstimateBrush = Brushes.Gray
            _memoryEstimateOk = False
            Return
        End If

        Dim estimatedBytes As Long = EstimateMemoryUsageBytes(width, height, startYear, endYear, dtYears)
        Dim availableBytes As Long = GetAvailablePhysicalMemoryBytes()

        Dim estGiB As Double = estimatedBytes / (1024 ^ 3)
        Dim availGiB As Double = availableBytes / (1024 ^ 3)

        _viewModel.MemoryEstimateText = $"Speicherprognose: ~{estGiB:F2} GiB (frei: {availGiB:F2} GiB)"

        If estimatedBytes > availableBytes Then
            _viewModel.MemoryEstimateBrush = Brushes.Red
            _memoryEstimateOk = False
        Else
            _viewModel.MemoryEstimateBrush = Brushes.Black
            _memoryEstimateOk = True
        End If
    End Sub

    Private Sub ApplyTimeStepModeToModel()
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        Dim useSeasonal As Boolean = (_viewModel.TimeStepMode = TimeStepMode.Month OrElse _viewModel.TimeStepMode = TimeStepMode.Quarter)

        _engine.Model.UseSeasonCycle = useSeasonal
    End Sub

    ''' <summary>
    ''' Übernimmt Parameter aus dem ViewModel ins Klimamodell (aktuell nur Lambda).
    ''' </summary>
    Private Sub ApplyViewModelToModel()
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _viewModel Is Nothing Then Return

        _engine.Model.ClimateSensitivityLambda = _viewModel.Lambda
    End Sub

End Class
