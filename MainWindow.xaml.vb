Imports System.Windows
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks

Class MainWindow

    'Die Simulations-Engine
    Private _engine As SimulationEngine

    'Felder fürs Multi-Threading
    Private _simCts As CancellationTokenSource
    Private _isSimulationRunning As Boolean = False


    'Default-Werte
    Private _endYear As Integer = 2100 'Standardwert, wird aus Textbox gelesen
    Private _currentLayer As MapLayer = MapLayer.Temperature

    Public Sub RefreshFromEngine()
        If _engine Is Nothing Then Return

        UpdateCO2Display(_engine.Model.CO2ppm)
        RenderTemperatureLayer()
        UpdateSimTimeDisplay()
    End Sub

    Private Sub MainWindow_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded

        _engine = New SimulationEngine()
        'CO2-Szenario setzen
        _engine.CO2Scenario = New DefaultCo2Scenario()
        'Temperatur-Provider setzen
        _engine.TemperatureProvider = New SimpleLatitudinalClimatology()
        'Erdoberflächenprovider setzen
        _engine.EarthSurfaceProvider = New ToyEarthSurfaceProvider()

        'UI-Handler
        AddHandler BtnGenerate.Click, AddressOf BtnGenerate_Click
        AddHandler BtnStep.Click, AddressOf BtnStep_Click
        AddHandler BtnStart.Click, AddressOf BtnStart_Click
        AddHandler BtnStop.Click, AddressOf BtnStop_Click
        AddHandler BtnShowHistory.Click, AddressOf BtnShowHistory_Click
        AddHandler ChkShowTemperature.Checked, AddressOf OnLayerCheckboxChanged
        AddHandler ChkShowTemperature.Unchecked, AddressOf OnLayerCheckboxChanged

        'Beim Start einmal initialisieren
        InitializeModelAndRender()
    End Sub

    Private Sub BtnGenerate_Click(sender As Object, e As RoutedEventArgs)
        InitializeModelAndRender()
    End Sub

    Private Sub BtnStep_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _isSimulationRunning Then
            MessageBox.Show("Bitte zuerst initialiseren bzw. laufende Simulation stoppen.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtYears As Double
        If Not Double.TryParse(TxtDtYears.Text, dtYears) OrElse dtYears <= 0 Then
            MessageBox.Show("Bitte ein gültiges Zeitschritt-Intervall (dt) eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        SimulateOneStep(dtYears)

    End Sub

    Private Async Sub BtnStart_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then
            MessageBox.Show("Bitte zuerst initialiseren", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        'dtYears und Enjahr aus den Textboxen lesen
        Dim dtYears As Double
        If Not Double.TryParse(TxtDtYears.Text, dtYears) OrElse dtYears <= 0 Then
            MessageBox.Show("Bitte ein gültiges Zeitschritt-Intervall (dt) eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        Dim endYear As Double
        If Not Double.TryParse(TxtEndYear.Text, endYear) Then
            MessageBox.Show("Bitte ein gültiges Endjahr eingeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Warning)
            Return
        End If

        'Modellparamter einmalig vor Start aus der UI übernehmen
        UpdateModelParametersFromUI()

        _isSimulationRunning = True
        _simCts = New CancellationTokenSource()

        'UI-Buttons sperren/umschalten
        BtnStart.IsEnabled = False
        BtnStep.IsEnabled = False
        BtnGenerate.IsEnabled = False
        BtnShowHistory.IsEnabled = False
        BtnStop.IsEnabled = True
        TxtLambda.IsEnabled = False

        Try
            'Simulation im Hintergrund-Thread laufen lassen
            Await Task.Run(Sub() RunSimulationLoop(dtYears, endYear, _simCts.Token))
        Catch ex As Exception
            'bewusst abgebrochen -> kein Fehler
        Finally
            _isSimulationRunning = False
            BtnStart.IsEnabled = True
            BtnStep.IsEnabled = True
            BtnGenerate.IsEnabled = True
            BtnStop.IsEnabled = False
            BtnShowHistory.IsEnabled = True
            TxtLambda.IsEnabled = True
        End Try
    End Sub

    Private Sub BtnStop_Click(sender As Object, e As RoutedEventArgs)
        If _simCts IsNot Nothing AndAlso Not _simCts.IsCancellationRequested Then
            _simCts.Cancel()
        End If
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

    Private Sub OnLayerCheckboxChanged(sender As Object, e As RoutedEventArgs)
        If ChkShowTemperature.IsChecked = True Then
            ImgTemperature.Visibility = Visibility.Visible
        Else
            ImgTemperature.Visibility = Visibility.Hidden
        End If
    End Sub

    Private Sub RunSimulationLoop(dtYears As Double, endYear As Double, token As CancellationToken)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _engine.Grid Is Nothing Then Return

        Dim uiUpdateInterval As Integer = 10 'Aktualisierungsrate der UI
        Dim stepCounter As Integer = 0

        While _engine.CurrentYear < endYear
            token.ThrowIfCancellationRequested()

            '1) Modellparamter ggf. aus UI übernehmen (Lambda etc.) -> auf dem UI-Thread
            Dispatcher.Invoke(
                Sub()
                    UpdateModelParametersFromUI()
                End Sub)

            '2) Simulationsschritt ausführen (im Hintergrundthread, rein nummerisch
            _engine.StepSimulation(dtYears)
            stepCounter += 1

            '3) in moderatem Rhythmus die UI aktualisieren
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

    Private Sub InitializeModelAndRender()
        Try

            'Gitternetz-Auflösung aus UI holen
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
        If _engine Is Nothing OrElse _engine.Grid Is Nothing Then Return

        Dim bmp = SurfaceTypeRenderer.RenderSurfaceType(_engine.Grid)
        ImgSurface.Source = bmp
    End Sub

    Private Sub RenderTemperatureLayer()
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
        ImgTemperature.Source = bmp

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
        RenderTemperatureLayer()
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

End Class
