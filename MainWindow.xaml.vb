Imports System.Windows
Imports System.Windows.Input
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
        'Erdoberflächenprovider setzen
        _engine.EarthSurfaceProvider = New ToyEarthSurfaceProvider()

        '--- UI-Handler ---
        'Buttons

        AddHandler BtnGenerate.Click, AddressOf BtnGenerate_Click
        AddHandler BtnStep.Click, AddressOf BtnStep_Click
        AddHandler BtnStart.Click, AddressOf BtnStart_Click
        AddHandler BtnStop.Click, AddressOf BtnStop_Click
        AddHandler BtnShowHistory.Click, AddressOf BtnShowHistory_Click

        'Layer-Auswahl
        AddHandler ChkShowTemperature.Checked, AddressOf OnLayerCheckboxChanged
        AddHandler ChkShowTemperature.Unchecked, AddressOf OnLayerCheckboxChanged

        'Mouseovers
        AddHandler ImgTemperature.MouseMove, AddressOf ImgTemperature_MouseMove
        AddHandler ImgTemperature.MouseLeave, AddressOf ImgTemperature_MouseLeave

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

        Dim startYear As Integer
        Dim endYear As Integer
        Dim dtYears As Integer

        If Not TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=True) Then
            'Ungültige Werte wurden nicht korrigiert -> Abbrechen
            Exit Sub
        End If

        _endYear = endYear

        'Modellparamter einmalig vor Start aus der UI übernehmen
        UpdateModelParametersFromUI()

        _isSimulationRunning = True
        _simCts = New CancellationTokenSource()

        'UI-Buttons sperren/umschalten
        SetSimulationUIState(True)

        Try
            'Simulation im Hintergrund-Thread laufen lassen
            Await Task.Run(Sub() RunSimulationLoop(dtYears, endYear, _simCts.Token))
        Catch ex As OperationCanceledException
            'Simulation wurde bewusst abgebrochen -> kein Fehler
        Catch ex As Exception
            'nur echte Fehler anzeigen
            MessageBox.Show($"Fehler in der Simulation: {ex.Message}", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            _isSimulationRunning = False
            SetSimulationUIState(False)
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

        TxtStatusLat.Text = String.Format(CultureInfo.InvariantCulture, "Lat: {0:F1}°", latDeg)
        TxtStatusLon.Text = String.Format(CultureInfo.InvariantCulture, "Lon: {0:F1}°", lonDeg)
        TxtStatusTemp.Text = String.Format(CultureInfo.InvariantCulture, "Temp: {0:F2} °C", tempC)
        TxtStatusSurface.Text = $"Surface: {surfaceName}"
    End Sub

    Private Sub ImgTemperature_MouseLeave(sender As Object, e As MouseEventArgs)
        ClearStatusBar()
    End Sub

    Private Sub ClearStatusBar()
        TxtStatusLat.Text = "Lat: -"
        TxtStatusLon.Text = "Lon: -"
        TxtStatusTemp.Text = "Temp: -"
        TxtStatusSurface.Text = "Surface: -"
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
            Dim height As Integer = Integer.Parse(TxtHeight.Text)

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

        'Farbskala-Extremwerte
        Dim tMinC As Double = -50.0
        Dim tMaxC As Double = 40.0

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

    Private Sub SetSimulationUIState(isRunning As Boolean)
        BtnStart.IsEnabled = Not isRunning
        BtnStep.IsEnabled = Not isRunning
        BtnGenerate.IsEnabled = Not isRunning
        BtnGenerate.IsEnabled = Not isRunning
        BtnShowHistory.IsEnabled = Not isRunning
        BtnStop.IsEnabled = isRunning

        TxtLambda.IsEnabled = Not isRunning
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

        '--- Startjahr ---
        If Not Integer.TryParse(TxtStartYear.Text, startYear) Then
            If showMessages Then
                Dim errMsg As MessageBoxResult
                errMsg = MessageBox.Show("Ungültiges Startjahr. Soll es auf 1850 gesetzt werden?", "Warnung", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes)
                If errMsg = MessageBoxResult.Yes Then
                    startYear = 1850 'Default-Wert setzen
                    TxtStartYear.Text = startYear.ToString()
                Else
                    Return False
                End If
            Else
                startYear = 1850 'Default-Wert setzen
                TxtStartYear.Text = startYear.ToString()
            End If
        End If

        '--- Endjahr ---
        If Not Integer.TryParse(TxtEndYear.Text, endYear) Then
            If showMessages Then
                Dim errMsg As MessageBoxResult
                errMsg = MessageBox.Show($"Ungültiges Endjahr. Soll es auf {(startYear + 250)} gesetzt werden?", "Warnung", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes)
                If errMsg = MessageBoxResult.Yes Then
                    endYear = startYear + 250 'Default-Wert setzen
                    TxtEndYear.Text = endYear.ToString()
                Else
                    Return False
                End If
            Else
                endYear = startYear + 250 'Default-Wert setzen
                TxtEndYear.Text = endYear.ToString()
            End If
        End If

        'Falls Endjahr <= Startjahr
        If endYear <= startYear Then
            If showMessages Then
                Dim errMsg As MessageBoxResult
                errMsg = MessageBox.Show($"Das Endjahr muss größer als das Startjahr sein. Soll es auf {(startYear + 250)} gesetzt werden?", "Warnung", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes)
                If errMsg = MessageBoxResult.Yes Then
                    endYear = startYear + 250
                    TxtEndYear.Text = endYear.ToString()
                Else
                    Return False
                End If
            Else
                endYear = startYear + 250 'Default-Wert setzen
                TxtEndYear.Text = endYear.ToString()
            End If
        End If

        '--- dtYears ---
        If Not Double.TryParse(TxtDtYears.Text.Replace(",", "."), NumberStyles.Float, CultureInfo.InvariantCulture, dtYears) OrElse dtYears <= 0 Then
            If showMessages Then
                Dim errMsg As MessageBoxResult
                errMsg = MessageBox.Show("Bitte ein gültiges Zeitschritt-Intervall eingeben. Soll es auf 1,0 gesetzt werden?", "Warnung", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.Yes)
                If errMsg = MessageBoxResult.Yes Then
                    dtYears = 1.0 'Default-Wert setzen
                    TxtDtYears.Text = dtYears.ToString()
                Else
                    Return False
                End If
            Else
                dtYears = 1.0 'Default-Wert setzen
                TxtDtYears.Text = dtYears.ToString()
            End If
        End If

        Return True
    End Function

End Class
