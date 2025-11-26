Imports System.Windows
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Globalization

Class MainWindow

    Private _grid As ClimateGrid
    Private _model As ClimateModel2D

    'Zeitschritt in Jahren für die automatische Simulation (Timer)
    Private _dtYearsPerTick As Double = 0.2

    'Gesamt-Simulationszeit in Jahren
    Private _simTimeYears As Double = 0.0

    'Timer für automatische Simulation
    Private _timer As DispatcherTimer

    'Zeitsteuerung
    Private _startYear As Integer = 1850
    Private _currentYear As Double = 1850.0

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        AddHandler BtnGenerate.Click, AddressOf BtnGenerate_Click
        AddHandler BtnStep.Click, AddressOf BtnStep_Click
        AddHandler BtnStart.Click, AddressOf BtnStart_Click
        AddHandler BtnStop.Click, AddressOf BtnStop_Click

        'Timer einrichten
        _timer = New DispatcherTimer()
        _timer.Interval = TimeSpan.FromMilliseconds(50) 'alle 50ms
        AddHandler _timer.Tick, AddressOf OnTimerTick

        'Beim Start einmal initialisieren
        InitializeModelAndRender()
    End Sub

    Private Sub BtnGenerate_Click(sender As Object, e As RoutedEventArgs)
        InitializeModelAndRender()
    End Sub

    Private Sub BtnStep_Click(sender As Object, e As RoutedEventArgs)
        If _model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtManual As Double = Double.Parse(TxtDtYears.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
        SimulateOneStep(dtManual)

    End Sub

    Private Sub BtnStart_Click(sender As Object, e As RoutedEventArgs)
        If _model Is Nothing Then
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

        If _model IsNot Nothing Then
            _model.CO2ppm = value
        End If
    End Sub

    Private Sub OnTimerTick(sender As Object, e As EventArgs)
        If _model Is Nothing OrElse _grid Is Nothing Then Return

        SimulateOneStep(_dtYearsPerTick)
    End Sub

    Private Sub InitializeModelAndRender()
        Try

            _timer?.[Stop]()

            Dim width As Integer = Integer.Parse(TxtWidth.Text)
            Dim height As Integer = Integer.Parse(TxtHeight.Text)

            'Startjahr aus Textbox lesen
            Try
                _startYear = Integer.Parse(TxtStartYear.Text)
            Catch ex As Exception
                _startYear = 1850 'Standardwert
                TxtStartYear.Text = _startYear.ToString()
            End Try

            _grid = New ClimateGrid(width, height)
            ClimateInitializer.InitializeSimpleLatitudeProfile(_grid)

            _model = New ClimateModel2D(_grid)

            'Modell-Basiswerte
            _model.CO2Base = 280.0 'vorindustriell
            _simTimeYears = 0.0
            _currentYear = _startYear

            UpdateModelParametersFromUI()
            UpdateSimTimeDisplay()

            'CO2 aus Szenario berechnen und anzeigen
            Dim co2Now As Double = GetCO2ForYear(_currentYear)
            _model.CO2ppm = co2Now
            UpdateCO2Display(co2Now)

            RenderCurrentGrid()
        Catch ex As Exception
            MessageBox.Show("Fehler bei der Initialisierung des Modells: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub RenderCurrentGrid()
        If _grid Is Nothing Then Return

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

        Dim bmp As WriteableBitmap = TemperatureRenderer.RenderTemperatureField(_grid, tMinC, tMaxC)
        ImgMap.Source = bmp

        'Globalen Mittelwert anzeigen
        Dim meanC As Double = _grid.ComputeGlobalMeanTemperatureC()
        TxtGlobalMean.Text = $"{meanC:F2} °C"
    End Sub

    Private Sub SimulateOneStep(dtYears As Double)
        If _model Is Nothing OrElse _grid Is Nothing Then Return

        'Parameter aus UI übernehmen
        UpdateModelParametersFromUI()

        'Physik-Schritt
        _model.StepSimulation(dtYears)
        _simTimeYears += dtYears

        'Jahr aktualisieren
        _currentYear = _startYear + _simTimeYears

        'CO2 für das aktuelle Jahr berechnen und setzen
        Dim co2Now As Double = GetCO2ForYear(_currentYear)
        _model.CO2ppm = co2Now
        UpdateCO2Display(co2Now)

        'Visualisierung aktualisieren
        RenderCurrentGrid()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub UpdateSimTimeDisplay()
        TxtSimTime.Text = $"{_simTimeYears:F1} Jahre"
        TxtCurrentYear.Text = $"{_currentYear:F1}"
    End Sub

    Private Sub UpdateModelParametersFromUI()
        If _model Is Nothing Then Return

        'CO2Base fix (vorindustriell)
        _model.CO2Base = 280.0

        'Klimasensitivität aus Textbox
        Dim lambdaVal As Double = 0.5

        Try
            lambdaVal = Double.Parse(TxtLambda.Text.Replace(",", "."), Globalization.CultureInfo.InvariantCulture)
        Catch ex As Exception
            'Wenn Parsing fehlschlägt, Standardwert verwenden
        End Try

        _model.ClimateSensitivityLambda = lambdaVal
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

    '---DEBUG-Funktion zum Generieren und Rendern eines Temperaturfeldes---
    Private Sub GenerateAndRenderField()
        Try
            Dim width As Integer = Integer.Parse(TxtWidth.Text)
            Dim height As Integer = Integer.Parse(TxtHeight.Text)
            Dim tMinC As Double = Double.Parse(TxtTempMin.Text)
            Dim tMaxC As Double = Double.Parse(TxtTempMax.Text)

            Dim grid As New ClimateGrid(width, height)

            'Erstes einfaches Temperaturprofil initialisieren
            ClimateInitializer.InitializeSimpleLatitudeProfile(grid)

            ' --- Debug: Min/Max-Temperaturen in °C ausgeben ---
            Dim minC As Double = Double.MaxValue
            Dim maxC As Double = Double.MinValue

            For lat As Integer = 0 To grid.Height - 1
                For lon As Integer = 0 To grid.Width - 1
                    Dim tempC As Double = grid.GetCell(lat, lon).TemperatureK - 273.15
                    If tempC < minC Then minC = tempC
                    If tempC > maxC Then maxC = tempC
                Next
            Next

            'Nur zur Kontrolle - später wieder rauswerfen
            MessageBox.Show($"Temp-Min: {minC:F1} °C" & Environment.NewLine &
                            $"Temp-Max: {maxC:F1} °C",
                            "Debug Temperaturbereich", MessageBoxButton.OK, MessageBoxImage.Information)

            'Rendern
            Dim bmp As WriteableBitmap = TemperatureRenderer.RenderTemperatureField(grid, tMinC, tMaxC)
            ImgMap.Source = bmp

        Catch ex As Exception
            MessageBox.Show("Fehler bei der Generierung oder Anzeige des Temperaturfeldes: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub
End Class
