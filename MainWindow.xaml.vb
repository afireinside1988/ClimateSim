Imports System.Windows
Imports System.Windows.Input
Imports System.Windows.Media.Imaging
Imports System.Windows.Threading
Imports System.Globalization
Imports System.Threading
Imports System.Threading.Tasks
Imports System.Windows.Media.Media3D
Imports System.Runtime.InteropServices




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
    Private _isInitialized As Boolean = False

    'Felder fürs Multi-Threading
    Private _simCts As CancellationTokenSource
    Private _isSimulationRunning As Boolean = False

    Private _memoryEstimateOk As Boolean = True

    'Default-Werte
    Private _endYear As Integer = 2100 'Standardwert, wird aus Textbox gelesen
    Private _currentLayer As MapLayer = MapLayer.Temperature

    'Zeitsteuerung
    Private _timeStepMode As TimeStepMode = TimeStepMode.Year 'aktueller TimeStep, Default 1 Jahr


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
        AddHandler BtnSpinUp.Click, AddressOf BtnSpinUp_Click
        AddHandler BtnStep.Click, AddressOf BtnStep_Click
        AddHandler BtnStart.Click, AddressOf BtnStart_Click
        AddHandler BtnStop.Click, AddressOf BtnStop_Click
        AddHandler BtnShowHistory.Click, AddressOf BtnShowHistory_Click

        'Textboxen
        AddHandler TxtWidth.TextChanged, AddressOf TxtWidth_TextChanged
        AddHandler TxtHeight.TextChanged, AddressOf TxtWidth_TextChanged
        AddHandler TxtStartYear.TextChanged, AddressOf TxtWidth_TextChanged
        AddHandler TxtEndYear.TextChanged, AddressOf TxtWidth_TextChanged

        'Slider
        AddHandler SldDtMode.ValueChanged, AddressOf SldDtMode_ValueChanged
        AddHandler SldLambda.ValueChanged, AddressOf SldLambda_ValueChanged
        'Layer-Auswahl
        AddHandler ChkShowTemperature.Checked, AddressOf OnLayerCheckboxChanged
        AddHandler ChkShowTemperature.Unchecked, AddressOf OnLayerCheckboxChanged

        'Mouseovers
        AddHandler ImgTemperature.MouseMove, AddressOf ImgTemperature_MouseMove
        AddHandler ImgTemperature.MouseLeave, AddressOf ImgTemperature_MouseLeave

        '--- Buttons & Layer initial sperren ---
        BtnStart.IsEnabled = False
        BtnStep.IsEnabled = False
        BtnShowHistory.IsEnabled = False
        ChkShowTemperature.IsChecked = False
        ChkShowTemperature.IsEnabled = False
        SldTemperatureOpacity.IsEnabled = False

        'Status setzen
        TxtStatus.Text = "Bitte Spin-Up starten."
        UpdateMemoryEstimate()

    End Sub

    Private Async Sub BtnSpinUp_Click(sender As Object, e As RoutedEventArgs)

        '1) Simulations-Settings lesen
        Dim startYear As Integer, endYear As Integer
        Dim dtYears As Double
        If Not TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=True) Then
            Return
        End If

        '2) Gitterauflösung lesen
        Dim width As Integer, height As Integer
        If Not Integer.TryParse(TxtWidth.Text, width) OrElse Not Integer.TryParse(TxtHeight.Text, height) Then
            MessageBox.Show("Bitte gültige Rasterauflösung angeben.", "Fehler", MessageBoxButton.OK, MessageBoxImage.Error)
            Return
        End If

        '3) Spin-Up-Parameter
        Dim spinUpYears As Integer = 100
        Dim spinUpDtYears As Double = 0.25 'Quartals-Schritte zur Nutzung des Jahreszeiten-EBM
        Dim spinUpStartYear As Integer = startYear - spinUpYears

        '4) CO2-Wert zum eigentlichen Startjahr als Fixwert für den Spin-Up
        Dim co2AtStart As Double = If(_engine.CO2Scenario IsNot Nothing, _engine.CO2Scenario.GetCO2ForYear(startYear), 280.0)

        '5) Engine initialisieren mit spinUpStartYear
        _engine.Initialize(width, height, spinUpStartYear)

        'Jahresphase auf Frühling / Jahresmittel nah dran
        _engine.Model.CurrentYearFraction = 0.25

        '6) Spin-Up-Modus einschalten
        _engine.IsSpinUp = True
        _engine.SpinUpCO2ppm = co2AtStart

        'UI sperren
        SetUIDuringSpinUp(isRunning:=True)

        Dim cts As New CancellationTokenSource()
        _simCts = cts

        Try
            '7) Spin-Up im Hintergrund laufen lassen
            Await Task.Run(Sub() RunSpinUpLoop(spinUpStartYear, startYear, spinUpDtYears, cts.Token))

            '8) Nach dem Spin-Up: Startjahr zurücksetzen
            _engine.IsSpinUp = False
            _engine.StartYear = startYear
            _engine.SimTimeYears = 0.0
            _engine.CurrentYear = startYear

            'History & Snapshots leeren
            _engine.History.Clear()
            _engine.Snapshots.Clear()

            '9) Initial-History-Eintrag mit "realem" CO2-Szenario
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

            '10) UI aktualisieren & freigeben
            _isInitialized = True
            Dispatcher.Invoke(
                Sub()
                    RenderTemperatureLayer()
                    UpdateSimTimeDisplay()
                    UpdateCO2Display(co2Now)

                    EnableUIAfterSpinUp()
                    TxtStatus.Text = "Spin-Up angeschlossen. Modell bereit."
                End Sub)
        Catch ex As OperationCanceledException
            MessageBox.Show("Spin-Up abgebrochen", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information)
        Finally
            _engine.IsSpinUp = False
            Dispatcher.Invoke(Sub()
                                  BtnSpinUp.IsEnabled = True
                              End Sub)
        End Try
    End Sub

    Private Sub BtnStep_Click(sender As Object, e As RoutedEventArgs)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _isSimulationRunning Then
            MessageBox.Show("Bitte zuerst initialiseren bzw. laufende Simulation stoppen.", "Hinweis",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Return
        End If

        Dim dtYears As Double = GetDtYearsFromMode()

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
        Dim dtYears As Double

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

        Dim wnd As New HistoryWindow(_engine, _timeStepMode)
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

    Private Sub SldDtMode_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        Dim modeIndex As Integer = CInt(Math.Round(SldDtMode.Value))

        Select Case modeIndex
            Case 0
                _timeStepMode = TimeStepMode.Month
                TxtDtModeLabel.Text = "1 Monat"
            Case 1
                _timeStepMode = TimeStepMode.Quarter
                TxtDtModeLabel.Text = "1 Quartal"
            Case 2
                _timeStepMode = TimeStepMode.Year
                TxtDtModeLabel.Text = "1 Jahr"
            Case 3
                _timeStepMode = TimeStepMode.Decade
                TxtDtModeLabel.Text = "10 Jahre"
        End Select

        UpdateMemoryEstimate()
    End Sub

    Private Sub SldLambda_ValueChanged(sender As Object, e As RoutedPropertyChangedEventArgs(Of Double))
        TxtLambdaValue.Text = SldLambda.Value.ToString("0.0", CultureInfo.InvariantCulture)
    End Sub

    Private Sub TxtWidth_TextChanged(sender As Object, e As RoutedEventArgs)
        UpdateMemoryEstimate()
    End Sub

    Private Sub ClearStatusBar()
        TxtStatusLat.Text = "Lat: -"
        TxtStatusLon.Text = "Lon: -"
        TxtStatusTemp.Text = "Temp: -"
        TxtStatusSurface.Text = "Surface: -"
    End Sub

    Private Sub RunSpinUpLoop(spinUpStartYear As Integer, targetStartYear As Integer, dtYears As Double, token As CancellationToken)
        Dim totalYears As Double = targetStartYear - spinUpStartYear
        Dim totalSteps As Integer = CInt(Math.Ceiling(totalYears / dtYears))
        If totalSteps <= 0 Then Return

        For stepIndex As Integer = 1 To totalSteps
            If token.IsCancellationRequested Then
                MessageBox.Show("Spin-Up abgebrochen.", "Hinweis", MessageBoxButton.OK, MessageBoxImage.Information)
                Exit For
            End If

            _engine.StepSimulation(dtYears)

            Dim progress As Double = stepIndex / CDbl(totalSteps)

            'Status im UI aktualisieren
            Dispatcher.Invoke(
                Sub()
                    TxtStatus.Text = $"Spin-Up: {progress * 100.0:F1} %"
                End Sub, DispatcherPriority.Background, token)
        Next

    End Sub

    Private Sub RunSimulationLoop(dtYears As Double, endYear As Double, token As CancellationToken)
        If _engine Is Nothing OrElse _engine.Model Is Nothing OrElse _engine.Grid Is Nothing Then Return

        Dim uiUpdateInterval As Integer = 10 'Aktualisierungsrate der UI
        Dim stepCounter As Integer = 0

        While _engine.CurrentYear < endYear AndAlso Not token.IsCancellationRequested

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
        TxtCurrentYear.Text = FormatYearWithStepMode(_engine.CurrentYear, _timeStepMode)
    End Sub

    Private Sub UpdateModelParametersFromUI()
        If _engine Is Nothing OrElse _engine.Model Is Nothing Then Return

        'Klimasensitivität aus Textbox
        _engine.Model.ClimateSensitivityLambda = SldLambda.Value
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
        BtnSpinUp.IsEnabled = Not isRunning
        BtnSpinUp.IsEnabled = Not isRunning
        BtnShowHistory.IsEnabled = Not isRunning
        BtnStop.IsEnabled = isRunning
    End Sub

    Private Sub SetUIDuringSpinUp(isRunning As Boolean)
        If isRunning Then
            BtnSpinUp.IsEnabled = False
            BtnStart.IsEnabled = False
            BtnStep.IsEnabled = False
            BtnShowHistory.IsEnabled = False
            BtnStop.IsEnabled = True
            ChkShowTemperature.IsEnabled = False
            SldTemperatureOpacity.IsEnabled = False
        Else
            BtnSpinUp.IsEnabled = True
        End If

    End Sub

    Private Sub EnableUIAfterSpinUp()
        BtnStart.IsEnabled = True
        BtnStep.IsEnabled = True
        BtnShowHistory.IsEnabled = True
        ChkShowTemperature.IsEnabled = True
        ChkShowTemperature.IsChecked = True
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
        dtYears = GetDtYearsFromMode()

        Return True
    End Function

    Private Function GetDtYearsFromMode() As Double
        Select Case _timeStepMode
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
        Dim width As Integer, height As Integer
        Dim startYear As Integer, endYear As Integer
        Dim dtYears As Double

        If Not Integer.TryParse(TxtWidth.Text, width) Then Return
        If Not Integer.TryParse(TxtHeight.Text, height) Then Return

        Dim okSettings = TryReadSimulationSettings(startYear, endYear, dtYears, showMessages:=False)
        If Not okSettings Then Return

        Dim estimatedBytes As Long = EstimateMemoryUsageBytes(width, height, startYear, endYear, dtYears)
        Dim availableBytes As Long = GetAvailablePhysicalMemoryBytes()

        Dim estGiB As Double = estimatedBytes / (1024 ^ 3)
        Dim availGiB As Double = availableBytes / (1024 ^ 3)

        TxtMemoryEstimate.Text = $"Speicherprognose: ~{estGiB:F2} GiB (frei: {availGiB:F2} GiB)"

        If estimatedBytes > availableBytes Then
            TxtMemoryEstimate.Foreground = Brushes.Red
            _memoryEstimateOk = False
        Else
            TxtMemoryEstimate.Foreground = Brushes.Black
            _memoryEstimateOk = True
        End If
    End Sub
End Class
