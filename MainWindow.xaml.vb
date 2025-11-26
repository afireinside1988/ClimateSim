Imports System.Windows
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Globalization

Class MainWindow

    Private _grid As ClimateGrid
    Private _model As ClimateModel2D

    'Zeitschritt in Jahren für jeden Klick auf "1 Simumationsschritt"
    Private Const DefaultDtYears As Double = 0.5

    'Zeitschritt in Jahren für die automatische Simulation (Timer)
    Private _dtYearsPerTick As Double = 0.2

    'Gesamt-Simulationszeit in Jahren
    Private _simTimeYears As Double = 0.0

    'Timer für automatische Simulation
    Private _timer As DispatcherTimer

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

        Dim dtManual As Double = 0.5 'dein bisheriger dt-Wert
        _model.StepSimulation(dtManual)
        _simTimeYears += dtManual

        RenderCurrentGrid()
        UpdateSimTimeDisplay()
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

    Private Sub OnTimerTick(sender As Object, e As EventArgs)
        If _model Is Nothing OrElse _grid Is Nothing Then Return

        _model.StepSimulation(_dtYearsPerTick)
        _simTimeYears += _dtYearsPerTick

        RenderCurrentGrid()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub InitializeModelAndRender()
        Try

            _timer?.Stop()

            Dim width As Integer = Integer.Parse(TxtWidth.Text)
            Dim height As Integer = Integer.Parse(TxtHeight.Text)

            _grid = New ClimateGrid(width, height)
            ClimateInitializer.InitializeSimpleLatitudeProfile(_grid)

            _model = New ClimateModel2D(_grid)

            _simTimeYears = 0.0
            UpdateSimTimeDisplay()

            RenderCurrentGrid()
        Catch ex As Exception
            MessageBox.Show("Fehler bei der Initialisierung des Modells: " & ex.Message,
                            "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        End Try
    End Sub

    Private Sub RenderCurrentGrid()
        If _grid Is Nothing Then Return

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
    End Sub

    Private Sub UpdateSimTimeDisplay()
        TxtSimTime.Text = $"{_simTimeYears:F1} Jahre"
    End Sub

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
