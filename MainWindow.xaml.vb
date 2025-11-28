Imports System.Windows
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Globalization

Class MainWindow

    'Die Simulations-Engine
    Private _engine As SimulationEngine

    'Zeitsteuerung
    Private _endYear As Integer = 2100
    Private _timer As DispatcherTimer

    'Zeitschritt in Jahren für die automatische Simulation (Timer)
    Private _dtYearsPerTick As Double = 1

    Public Sub RefreshFromEngine()
        If _engine Is Nothing Then Return

        UpdateCO2Display(_engine.Model.CO2ppm)
        RenderCurrentGrid()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        _engine = New SimulationEngine()
        'CO2-Szenario setzen
        _engine.CO2Scenario = New DefaultCo2Scenario()
        'Temperatur-Provider setzen
        _engine.TemperatureProvider = New SimpleLatitudinalClimatology()

        AddHandler BtnGenerate.Click, AddressOf BtnGenerate_Click
        AddHandler BtnStep.Click, AddressOf BtnStep_Click
        AddHandler BtnStart.Click, AddressOf BtnStart_Click
        AddHandler BtnStop.Click, AddressOf BtnStop_Click
        AddHandler BtnShowHistory.Click, AddressOf BtnShowHistory_Click

        'Timer einrichten
        _timer = New DispatcherTimer()
        _timer.Interval = TimeSpan.FromMilliseconds(10) 'alle 50ms
        AddHandler _timer.Tick, AddressOf OnTimerTick

        'Beim Start einmal initialisieren
        InitializeModelAndRender()
    End Sub

    Private Sub BtnGenerate_Click(sender As Object, e As RoutedEventArgs)
        InitializeModelAndRender()
    End Sub

    Private Sub BtnStep_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtManual As Double = Double.Parse(TxtDtYears.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
        SimulateOneStep(dtManual)

    End Sub

    Private Sub BtnStart_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        'dt und Intervall aus Textboxen lesen
        Try
            _dtYearsPerTick = Double.Parse(TxtDtYears.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
            Dim intervalMs As Integer = Integer.Parse(TxtIntervalMs.Text)
            If intervalMs < 10 Then intervalMs = 10 'Mindestintervall
            _timer.Interval = TimeSpan.FromMilliseconds(intervalMs)
        Catch ex As Exception
            MessageBox.Show("Fehler beim Lesen von dt/Intervall: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End Try

        _timer.Start()
    End Sub

    Private Sub BtnStop_Click(sender As Object, e As RoutedEventArgs)
        _timer.Stop()
    End Sub

    Private Sub SldCO2_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        Dim value As Integer = CInt(Math.Round(SldCO2.Value))
        TxtCO2Value.Text = $"{value} ppm"

        If _engine.Model IsNot Nothing Then
            _engine.Model.CO2ppm = value
        End If
    End Sub

    Private Sub BtnShowHistory_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.History.Count = 0 Then
            MessageBox.Show("Keine Simulationsdaten vorhanden.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim wnd As New HistoryWindow(_engine)
        wnd.Owner = Me
        wnd.Show()
    End Sub

    Private Sub OnTimerTick(sender As Object, e As EventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _engine.Grid Is Nothing Then Return

        'Prüfen, ob wir das Endjahr im nächsten Schritt überschreiten würden
        Dim plannedEndYear = _engine.CurrentYear + _dtYearsPerTick

        If plannedEndYear >= _endYear Then
            Dim lastDt As Double = _endYear - _engine.CurrentYear

            If lastDt > 0.0 Then
                SimulateOneStep(lastDt)
            End If

            _timer.Stop()
            Return
        End If

        'Normaler Schritt
        SimulateOneStep(_dtYearsPerTick)
    End Sub

    Private Sub InitializeModelAndRender()
        Try

            _timer?.[Stop]()

            Dim width As Integer = Integer.Parse(TxtWidth.Text)
            Dim height As Integer = Integer.Parse(TxtHeight.Text)

            'Startjahr aus Textbox lesen
            Dim startYear As Integer
            Try
                startYear = Integer.Parse(TxtStartYear.Text)
            Catch ex As Exception
                startYear = 1850 'Standardwert
                TxtStartYear.Text = startYear.ToString()
            End Try

            'Endjahr aus Textbox lesen
            Try
                _endYear = Integer.Parse(TxtEndYear.Text)
            Catch ex As Exception
                _endYear = startYear + 250 'Standardmäßig 250 Jahre Simulation
                TxtEndYear.Text = _endYear.ToString()
            End Try

            'Falls jemand ein kleineres End- als Startjahr eingibt, wieder auf 250 Jahre Simulation setzen
            If _endYear <= startYear Then
                _endYear = startYear + 250
                TxtEndYear.Text = _endYear.ToString()
            End If

            'Simulations-Engine initialisieren
            _engine.Initialize(width, height, startYear)

            'Lambda aus UI holen
            UpdateModelParametersFromUI()

            'Anzeige aktualisieren
            UpdateSimTimeDisplay()
            UpdateCO2Display(_engine.Model.CO2ppm)
            RenderCurrentGrid()

        Catch ex As Exception
            MessageBox.Show("Fehler bei der Initialisierung des Modells: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub RenderCurrentGrid()
        If _engine Is Nothing OrElse _engine.Grid Is Nothing Then Return


        'Farbskala
        'Standardwerte, falls Parsing scheitert
        Dim tMinC As Double = -50.0
        Dim tMaxC As Double = 40.0

        Try
            'Kommas durch Punkte ersetzen, damit es kulturunabhängig klappt
            tMinC = Double.Parse(TxtTempMin.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
            tMaxC = Double.Parse(TxtTempMax.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
        Catch ex As Exception
            'Wenn was schiefgeht, einfach die Standardwerte nehmen
            'Optional MessageBox anzeigen
        End Try

        Dim bmp As WriteableBitmap = TemperatureRenderer.RenderTemperatureField(_engine.Grid, tMinC, tMaxC)
        ImgMap.Source = bmp

        'Globalen Mittelwert anzeigen
        Dim meanC As Double = _engine.Grid.ComputeGlobalMeanTemperatureC()
        TxtGlobalMean.Text = $"{meanC:F2} °C"
    End Sub

    Private Sub SimulateOneStep(dtYears As Double)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        'Parameter aus UI übernehmen
        UpdateModelParametersFromUI()

        _engine.StepSimulation(dtYears)

        UpdateCO2Display(_engine.Model.CO2ppm)

        'Visualisierung aktualisieren
        RenderCurrentGrid()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub UpdateSimTimeDisplay()
        TxtSimTime.Text = $"{_engine.SimTimeYears:F1} Jahre"
        TxtCurrentYear.Text = $"{_engine.CurrentYear:F1}"
    End Sub

    Private Sub UpdateModelParametersFromUI()
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        'Klimasensitivität aus Textbox
        Dim lambdaVal As Double = 0.5 'Standard-Wert festlegen
        Try
            lambdaVal = Double.Parse(TxtLambda.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
        Catch ex As Exception
            'Wenn Parsing fehlschlägt, Standardwert verwenden
        End Try

        _engine.Model.ClimateSensitivityLambda = lambdaVal
    End Sub

    Private Sub UpdateCO2Display(co2 As Double)
        'Slider innerhalb seiner Grenzen halten
        Dim val = Math.Max(SldCO2.Minimum, Math.Min(SldCO2.Maximum, co2))
        SldCO2.Value = val
        TxtCO2Value.Text = $"{co2:F0} ppm"
    End Sub

    Private Function GetCO2ForYear(year As Double) As Double
        'Sehr vereinfachtes, grobes Szenario:
        'Kann später mit realen Daten oder komplexeren Modellen ersetzt werden

        '1. Vorindustriell bis 1850: 280 ppm
        If year <= 1850 Then
            Return 280.0
        End If

        '2. 1850 bis 1950: langsamer Anstieg auf 310 ppm
        If year <= 1950 Then
            Dim t As Double = (year - 1850) / (1950.0 - 1850.0)
            Return 280.0 + t * (310.0 - 280.0)
        End If

        '3. 1950 bis 2000: schneller Anstieg auf 370 ppm
        If year <= 2000 Then
            Dim t As Double = (year - 1950) / (2000.0 - 1950.0)
            Return 310.0 + t * (370.0 - 310.0)
        End If

        '4. 2000 bis 2020: Anstieg auf 415 ppm
        If year <= 2020 Then
            Dim t As Double = (year - 2000) / (2020.0 - 2000.0)
            Return 370.0 + t * (415.0 - 370.0)
        End If

        '5. 2020 bis 2100: Szenario - starker Anstieg auf 700 ppm
        If year <= 2100 Then
            Dim t As Double = (year - 2020) / (2100.0 - 2020.0)
            Return 415.0 + t * (700.0 - 415.0)
        End If

        'Nach 2100: konstant 700 ppm (kann später abgesenkt werden)
        Return 700.0
    End Function

End Class
