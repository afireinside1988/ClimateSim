Imports System.Windows.Threading
Imports System.Globalization
Imports System.Threading
Imports System.Runtime.InteropServices
Imports System.ComponentModel

Class MainWindow

    'Simulations-Engine
    Private _engine As SimulationEngine

    'Felder fürs Multi-Threading
    Private _simCts As CancellationTokenSource
    Private _uiUpdatePending As Integer = 0

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

        '--- Gitter und Startjahr initialisieren, idealerweise aus der Config ---
        Dim cfg As SimulationConfig = _viewModel.CurrentConfig
        If cfg Is Nothing Then
            cfg = SimulationConfig.CreateDefault()
            _viewModel.CurrentConfig = cfg
        End If

        '--- Engine-Grid und Modell aus der Config initialisieren ---
        _engine.Initialize(cfg.GridWidth, cfg.GridHeight, cfg.StartYear)
        ApplyConfigToModel()

        '--- UI-Handler ---

        'Auf Command-Events reagieren
        AddHandler _viewModel.StartSimulationRequested, AddressOf OnStartSimulationRequested
        AddHandler _viewModel.StopSimulationRequested, AddressOf OnStopSimulationRequested
        AddHandler _viewModel.SpinUpRequested, AddressOf OnSpinUpRequested
        AddHandler _viewModel.StepRequested, AddressOf OnStepRequested
        AddHandler _viewModel.ShowHistoryRequested, AddressOf OnShowHistoryRequested
        AddHandler _viewModel.ShowEarthSurfaceWindowRequested, AddressOf OnShowEarthSurfaceWindowRequested
        AddHandler _viewModel.ShowLandCoverSmokeTestWindowRequested, AddressOf OnShowLandCoverSmokeTestWindowRequested
        AddHandler _viewModel.SimulationConfigRequested, AddressOf OnSimulationConfigRequested

        'Mouseovers
        AddHandler ImgTemperature.MouseMove, AddressOf ImgTemperature_MouseMove
        AddHandler ImgTemperature.MouseLeave, AddressOf ImgTemperature_MouseLeave

        '--- Buttons & Layer initial sperren ---
        _viewModel.IsTemperatureLayerVisible = False

        '--- Erdoberfläche initialisieren ---
        RenderSurfaceLayer()

        '--- Status setzen ---
        _viewModel.StatusText = "Bitte Spin-Up starten."

    End Sub

    Private Async Sub OnSpinUpRequested(sender As Object, e As EventArgs)
        If _viewModel Is Nothing OrElse _viewModel.CurrentConfig Is Nothing Then Return

        '1) Aktuelle Simulations-Konfiguration aus dem ViewModel holen
        Dim cfg As SimulationConfig = _viewModel.CurrentConfig

        'Start- und Endjahr aus der Config lesen
        Dim startYear As Integer = cfg.StartYear
        Dim endYear As Integer = cfg.EndYear

        'Gitterauflösung lesen
        If cfg.GridWidth <= 0 OrElse cfg.GridHeight <= 0 Then
            MessageBox.Show("Bitte gültige Rasterauflösung angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End If
        Dim width As Integer = cfg.GridWidth
        Dim height As Integer = cfg.GridHeight

        '3) Spin-Up-Konfiguration abhängig vom TimeStepMode
        Dim spinUpYears As Integer = 200
        Dim spinUpDtYears As Double
        Dim spinUpUseSeasonal As Boolean

        Select Case cfg.TimeStepMode
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

        'DEBUG:
        Dim gate = SurfaceTypeDebugGate.Run(_engine.EarthSurfaceProvider, _engine.Grid)

        Debug.WriteLine($"SurfaceType DebugGate: {gate.DifferentPixelCount}/{gate.TotalPixelCount} ({gate.DifferentPercent:F4}%) verschieden")



        'Config ins Model übernehmen
        ApplyConfigToModel()

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

        '8) Spin-Up im Hintergrund laufen lassen
        Try
            Await BusyRunner.RunAsync(
                _viewModel, "Spin-Up und Initialisierung",
                Sub(p, ct)
                    'Wichtig: ct kommt vom BusyRunner (Cancel im Overlay)
                    RunSpinUpLoop(spinUpStartYear, startYear, spinUpDtYears, p, ct)
                End Sub, canCancel:=True)

            'Wenn abgebrochen:
            'BusyRunner wirft bei Cancel typischerweise OperationCanceledException (je nachdem wie es behandelt wird)
            ' => hier nach dem Await sind wir nur bei Erfolg

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

            '12) UI aktualisieren
            RenderTemperatureLayer()
            UpdateSimTimeDisplay()
            UpdateCO2Display(co2Now)

            _viewModel.IsInitialized = True
            EnableUIAfterSpinUp()

            _viewModel.StatusText = "Spin-Up abgeschlossen. Modell bereit."
        Catch ex As Exception
            MessageBox.Show($"Fehler beim Spin-Up: {ex.Message}", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            _engine.IsSpinUp = False
            _viewModel.IsSimulationRunning = False
        End Try
    End Sub

    Private Sub OnStepRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _viewModel.IsSimulationRunning Then
            MessageBox.Show("Bitte zuerst initialiseren bzw. laufende Simulation stoppen.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtYears As Double = _viewModel.CurrentConfig.GetDtYearsFromMode()

        SimulateOneStep(dtYears)

    End Sub

    Private Async Sub OnStartSimulationRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim cfg As SimulationConfig = _viewModel.CurrentConfig
        Dim startYear As Integer = cfg.StartYear
        Dim endYear As Integer = cfg.EndYear
        Dim dtYears As Double = _viewModel.CurrentConfig.GetDtYearsFromMode

        _endYear = endYear

        'Konfiguration ans Model übergeben
        ApplyConfigToModel()

        _viewModel.IsSimulationRunning = True
        _viewModel.StatusText = "Simulation läuft..."
        _simCts = New CancellationTokenSource()

        Try
            'Simulation im BusyRunner laufen lassen
            Await BusyRunner.RunAsync(
                _viewModel,
                "Simulation läuft",
                Sub(p, ct)

                    'ct = BusyRunner-Token (hier normalerweise nie gecancelt, weil canCancel:=False)
                    '_simCts.Token = Stop-Token
                    Using linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, _simCts.Token)
                        RunSimulationLoop(dtYears, endYear, linkedCts.Token)
                    End Using

                End Sub, canCancel:=False, showOverlay:=False)
        Catch es As OperationCanceledException
            'Stop gedrückt
            _viewModel.StatusText = "Simulation gestoppt"
        Catch ex As Exception
            'nur echte Fehler anzeigen
            MessageBox.Show($"Fehler in der Simulation: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            _viewModel.IsSimulationRunning = False
            _viewModel.StatusText = "Simulation beendet"
        End Try
    End Sub

    Private Sub OnStopSimulationRequested(sender As Object, e As EventArgs)
        If _simCts IsNot Nothing AndAlso Not _simCts.IsCancellationRequested Then
            _simCts.Cancel()
            _viewModel.StatusText = "Simulation gestoppt"
        End If
    End Sub

    Private Sub OnShowHistoryRequested(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.History.Count = 0 Then
            MessageBox.Show("Keine Simulationsdaten vorhanden.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim wnd As New HistoryWindow(_engine, _viewModel.CurrentConfig.TimeStepMode)
        wnd.Owner = Me
        wnd.Show()
    End Sub

    Private Sub OnShowEarthSurfaceWindowRequested(sender As Object, e As EventArgs)
        Dim wnd As New EarthSurfaceWindow()
        wnd.Owner = Me
        wnd.Show()
    End Sub

    Private Sub OnShowLandCoverSmokeTestWindowRequested(sender As Object, e As EventArgs)
        Dim wnd As New LandCoverWindow()
        wnd.Owner = Me
        wnd.Show()
    End Sub

    Private Sub OnSimulationConfigRequested(sender As Object, e As EventArgs)
        If _viewModel Is Nothing Then Return

        Dim baseConfig As SimulationConfig = _viewModel.CurrentConfig
        If baseConfig Is Nothing Then baseConfig = SimulationConfig.CreateDefault()

        'ViewModel für den Dialog (arbeitet auf einem Klon)
        Dim cfgVm As New SimulationConfigViewModel(baseConfig)

        Dim dlg As New SimulationConfigWindow()
        dlg.Owner = Me
        dlg.DataContext = cfgVm

        Dim result As Boolean? = dlg.ShowDialog()

        If result.HasValue AndAlso result.Value = True Then
            'Nutzer hat OK geklickt -> Warnen, dass Spin-Up ungültig wird
            Dim warn As MessageBoxResult = MessageBox.Show("Durch Änderung der Konfiguration wird der bisherige Spin-Up ungültig. Fortfahren und neuen Spin-Up erforderlich machen?", "Simulations-Konfiguration geändert",
                                                            MessageBoxButton.YesNo,
                                                            MessageBoxImage.Warning,
                                                            MessageBoxResult.Yes)

            If warn = MessageBoxResult.Yes Then
                'Neue Konfiguration übernehmen
                _viewModel.CurrentConfig = cfgVm.Config

                'Spin-Up als ungültig markieren
                _viewModel.IsInitialized = False
                _viewModel.StatusText = "Konfiguration geändert. Bitte Spin-Up neu starten."
            End If

        End If
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

    Private Sub ClearStatusBar()
        If _viewModel Is Nothing Then Return

        _viewModel.StatusLatText = "Lat: -"
        _viewModel.StatusLonText = "Lon: -"
        _viewModel.StatusTempText = "Temp: -"
        _viewModel.StatusSurfaceText = "Surface: -"
    End Sub

    Private Sub RunSpinUpLoop(spinUpStartYear As Integer, targetStartYear As Integer, dtYears As Double, progress As IProgress(Of ProgressInfo), token As CancellationToken)

        Dim totalYears As Double = targetStartYear - spinUpStartYear
        Dim totalSteps As Integer = CInt(Math.Ceiling(totalYears / dtYears))
        If totalSteps <= 0 Then Return

        progress?.Report(New ProgressInfo("Spin-Up wird vorbereitet...", -1))

        For stepIndex As Integer = 1 To totalSteps
            If token.IsCancellationRequested Then Exit For

            _engine.StepSimulation(dtYears)

            'Progress nur alle X Steps reporten (sonst Spam)
            If stepIndex = 1 OrElse stepIndex = totalSteps OrElse (stepIndex Mod 20 = 0) Then
                Dim percent As Integer = CInt(Math.Round(stepIndex * 100.0 / totalSteps))
                progress?.Report(New ProgressInfo($"Spin-Up läuft... ({percent} %)", percent))
            End If

        Next

    End Sub

    Private Sub RunSimulationLoop(dtYears As Double, endYear As Double, token As CancellationToken)

        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _engine.Grid Is Nothing Then Return

        'UI-Update z.B. alle ~75ms (fühlt sich live an, ohne die UI zu fluten)
        Dim sw As Stopwatch = Stopwatch.StartNew()
        Dim nextUiUpdateMs As Long = 0

        While _engine.CurrentYear < endYear AndAlso Not token.IsCancellationRequested

            '1) Simulationsschritt ausführen
            _engine.StepSimulation(dtYears)
            If token.IsCancellationRequested Then Exit While

            '2) Zeitbasiert UI anstoßen (nicht blockieren!)
            Dim nowMs As Long = sw.ElapsedMilliseconds
            If nowMs >= nextUiUpdateMs Then
                nextUiUpdateMs = nowMs + 75         '75ms-Schritte

                'Wenn schon ein UI-Update queued ist: keins nachschieben (keine Warteschlange)
                If Threading.Interlocked.CompareExchange(_uiUpdatePending, 1, 0) = 0 Then

                    'InvokeAsync statt Invoke: blockiert Worker nicht
                    Dispatcher.InvokeAsync(
                        Sub()
                            Try
                                UpdateSimTimeDisplay()
                                UpdateCO2Display(_engine.Model.CO2ppm)
                                RenderTemperatureLayer()
                            Finally
                                Threading.Interlocked.Exchange(_uiUpdatePending, 0)
                            End Try
                        End Sub, DispatcherPriority.Background, CancellationToken.None)
                End If

            End If
        End While

        'Am Ende final UI refresh
        Dispatcher.InvokeAsync(
            Sub()
                UpdateSimTimeDisplay()
                UpdateCO2Display(_engine.Model.CO2ppm)
                RenderTemperatureLayer()
            End Sub, DispatcherPriority.Background, CancellationToken.None).Task.Wait(0, CancellationToken.None)
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
        _viewModel.CurrentYearText = FormatYearWithStepMode(_engine.CurrentYear, _viewModel.CurrentConfig.TimeStepMode)
    End Sub

    Private Sub UpdateCO2Display(co2 As Double)
        If _viewModel Is Nothing Then Return

        'Clamp in den Sliderbereich (damit ViewModel + Slider konsistent bleiben)
        _viewModel.CO2Value = Clamp(co2, 280.0, 1000.0)
    End Sub

    Private Sub EnableUIAfterSpinUp()
        _viewModel.IsTemperatureLayerVisible = True
    End Sub

    Private Sub ApplyTimeStepModeToModel()
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        Dim useSeasonal As Boolean = (_viewModel.CurrentConfig.TimeStepMode = TimeStepMode.Month OrElse _viewModel.CurrentConfig.TimeStepMode = TimeStepMode.Quarter)

        _engine.Model.UseSeasonCycle = useSeasonal
    End Sub

    ''' <summary>
    ''' Übernimmt Parameter aus dem ViewModel ins Klimamodell (aktuell nur Lambda).
    ''' </summary>
    Private Sub ApplyConfigToModel()
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _viewModel Is Nothing Then Return

        Dim cfg As SimulationConfig = _viewModel.CurrentConfig
        If cfg Is Nothing Then Return

        Dim model As ClimateModel2D = _engine.Model

        'Allgemeine physikalische Parameter
        model.ClimateSensitivityLambda = cfg.Lambda

        'Solare Zyklen
        model.SolarCycleMode = cfg.SolarCycleMode

        'Schwabe
        model.UseSchwabeCycle = cfg.UseSchwabeCycle
        model.SchwabeAmplitude = cfg.SchwabeAmplitude
        model.SchwabePeriodYears = cfg.SchwabePeriodYears
        model.SchwabePhaseDeg = cfg.SchwabePhaseDeg

        'Magnetischer Zyklus
        model.UseMagneticCycle = cfg.UseMagneticCycle
        model.MagneticAmplitude = cfg.MagneticAmplitude
        model.MagneticPeriodYears = cfg.MagneticPeriodYears
        model.MagneticPhaseDeg = cfg.MagneticPhaseDeg

        'Gleissberg
        model.UseGleissbergCycle = cfg.UseGleissbergCycle
        model.GleissbergAmplitude = cfg.GleissbergAmplitude
        model.GleissbergPeriodYears = cfg.GleissbergPeriodYears
        model.GleissbergPhaseDeg = cfg.GleissbergPhaseDeg

        'De Vries / Suess
        model.UseDeVriesSuessCycle = cfg.UseDeVriesSuessCycle
        model.DeVriesAmplitude = cfg.DeVriesAmplitude
        model.DeVriesPeriodYears = cfg.DeVriesPeriodYears
        model.DeVriesPhaseDeg = cfg.DeVriesPhaseDeg

    End Sub
End Class
