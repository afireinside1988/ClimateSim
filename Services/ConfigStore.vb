Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Text.Json.Serialization
Imports System.Windows.Media.TextFormatting

Public Enum ConfigLoadStatus
    Success = 0
    FileNotFound = 1
    JsonInvalid = 2         'Syntax/Parsing kaputt
    SchemaTooNew = 3        'ConfigVersion > CurrentConfigVersion
    InvalidContent = 4      'JSON ok, aber inhaltlich unbrauchbar (z.b. null)
    IOError = 5             'IO/Access/Lock etc.
    UnknownError = 6
End Enum

Public NotInheritable Class ConfigLoadResult
    Public Property Status As ConfigLoadStatus
    Public Property Config As SimulationConfig
    Public Property Message As String
    Public Property [Exception] As Exception

    Public ReadOnly Property IsSuccess As Boolean
        Get
            Return Status = ConfigLoadStatus.Success AndAlso Config IsNot Nothing
        End Get
    End Property

    Public Shared Function Ok(cfg As SimulationConfig) As ConfigLoadResult
        Return New ConfigLoadResult With {.Status = ConfigLoadStatus.Success, .Config = cfg}
    End Function

    Public Shared Function Fail(status As ConfigLoadStatus, msg As String, Optional ex As Exception = Nothing) As ConfigLoadResult
        Return New ConfigLoadResult With {.Status = status, .Message = msg, .Exception = ex}
    End Function
End Class

Public Class ConfigStore

#Region "Pfade"

    Public Shared ReadOnly Property ConfigDirectory As String = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClimateSim", "Config")
    Public Shared ReadOnly Property DefaultConfigPath As String = Path.Combine(ConfigDirectory, "DefaultConfig.json")

#End Region

#Region "JSON-Optionen"

    Private Shared ReadOnly _jsonOptions As JsonSerializerOptions = CreateJsonOptions()
    Public Shared ReadOnly Property JsonOptions As JsonSerializerOptions
        Get
            Return _jsonOptions
        End Get
    End Property

    Private Shared Function CreateJsonOptions() As JsonSerializerOptions
        Dim opt As New JsonSerializerOptions With {
           .WriteIndented = True,
           .ReadCommentHandling = JsonCommentHandling.Skip,
           .AllowTrailingCommas = True,
           .PropertyNameCaseInsensitive = True
           }

        'Enums als Strings speichern (lesbarer & stabiler)
        opt.Converters.Add(New JsonStringEnumConverter())
        Return opt
    End Function

#End Region

#Region "Versionierung"

    Private Shared Function UpgradeIfNeeded(cfg As SimulationConfig, ByRef didUpgrade As Boolean) As SimulationConfig
        didUpgrade = False
        If cfg Is Nothing Then Return Nothing

        Dim v As Integer = cfg.ConfigVersion 'fehlt -> 0

        'Wenn Config aus neuerer App-Version kommt: NICHT anfassen, nur warnen
        If v > SimulationConfig.CurrentConfigVersion Then
            Debug.WriteLine($"ConfigVersion {v} ist neuer als App-Version {SimulationConfig.CurrentConfigVersion}.")
            Return cfg
        End If

        Dim upgraded As SimulationConfig = cfg

        'Step-by-Step Migration:
        If v < 1 Then
            upgraded = Upgrade_0_To_1(upgraded)
#Disable Warning IDE0059 ' Unnötige Zuweisung eines Werts.
            v = 1
#Enable Warning IDE0059 ' Unnötige Zuweisung eines Werts.
            didUpgrade = True
        End If

        'Wenn später Version 2 kommt:
        'If v < 2 Then
        '    upgraded = Upgrade_1_To_2(upgraded)
        '    v = 2
        '    didUpgrade = True
        'End If

        upgraded.ConfigVersion = SimulationConfig.CurrentConfigVersion
        Return upgraded
    End Function

    '--- Erste Migration 0 -> 1 (Platzhalter und Hardening)
    Private Shared Function Upgrade_0_To_1(cfg As SimulationConfig) As SimulationConfig
        If cfg Is Nothing Then Return Nothing

        'Beispiele für typische Migrationen/Hardening:

        'Falls ältere Configs evtl. ungültige Werte haben:
        If cfg.GridWidth <= 0 Then cfg.GridWidth = 360
        If cfg.GridHeight <= 0 Then cfg.GridHeight = 180
        If cfg.EndYear <= cfg.StartYear Then cfg.EndYear = cfg.StartYear + 250

        'Wenn irgendwann neue Properties ergänzt werden, können hier Defaults gesetzt werden:
        'If cfg.SomeNewProperty = 0 Then cfg.SomeNewProperty = 123

        cfg.ConfigVersion = 1
        Return cfg
    End Function

#End Region

    Private Sub New()

    End Sub

#Region "Laden und Speichern"

    Public Shared Function LoadOrCreateDefault() As SimulationConfig
        Directory.CreateDirectory(ConfigDirectory)

        If File.Exists(DefaultConfigPath) Then
            Dim res As ConfigLoadResult = TryLoadFromFile(DefaultConfigPath)
            If res.IsSuccess Then Return res.Config

            'Nur bei wirklich beschädigtem JSON ein Backup anlegen
            If res.Status = ConfigLoadStatus.JsonInvalid Then
                Try
                    Dim backUpPath As String = DefaultConfigPath & ".broken-" & DateTime.Now.ToString("yyyyMMdd-HHmmss") & ".json"
                    File.Copy(DefaultConfigPath, backUpPath, overwrite:=True)
                Catch ex As Exception
                    Debug.WriteLine(ex.Message)
                End Try
            End If

            'Optional: Logging je nach Grund
            MessageBox.Show($"Die Standardkonfiguration konnte nicht geladen werden: {res.Status}{Environment.NewLine}{res.Message}{Environment.NewLine}Es wird eine neue Standardkonfiguration erstellt.", "Fehler beim Laden Standardkonfiguration", MessageBoxButton.OK, MessageBoxImage.Error)

            'Debug.WriteLine($"DefaultConfig load failed: {res.Status} - {res.Message}")
            If res.Exception IsNot Nothing Then Debug.WriteLine(res.Exception.ToString())
        End If

        'Fallback: harte Defaults erstellen & direkt speichern
        Dim cfg As SimulationConfig = SimulationConfig.CreateDefault()
        SaveToFile(DefaultConfigPath, cfg)
        Return cfg
    End Function

    Public Shared Function TryLoadFromFile(path As String) As ConfigLoadResult

        If String.IsNullOrWhiteSpace(path) Then
            Return ConfigLoadResult.Fail(ConfigLoadStatus.FileNotFound, "Kein Dateipfad angegeben.")
        End If

        If Not File.Exists(path) Then
            Return ConfigLoadResult.Fail(ConfigLoadStatus.FileNotFound, $"Datei nicht gefunden: {path}")
        End If

        Try
            Dim json As String = File.ReadAllText(path, Encoding.UTF8)

            Dim cfg As SimulationConfig = JsonSerializer.Deserialize(Of SimulationConfig)(json, JsonOptions)
            If cfg Is Nothing Then
                Return ConfigLoadResult.Fail(ConfigLoadStatus.InvalidContent, "Die JSON-Datei enthält keine gültige Konfiguration (null).")
            End If

            'Schema-Version vor Migration prüfen
            Dim v As Integer = cfg.ConfigVersion 'fehlt -> 0
            If v > SimulationConfig.CurrentConfigVersion Then
                Return ConfigLoadResult.Fail(ConfigLoadStatus.SchemaTooNew, $"Die Konfiguration ist zu neu (ConfigVersion={v}, App unterstützt bis {SimulationConfig.CurrentConfigVersion}).")
            End If

            'Migration/Upgrade (falls nötig)
            Dim didUpgrade As Boolean = False
            cfg = UpgradeIfNeeded(cfg, didUpgrade)

            'Nach Upgrade zurückschreiben, damit die Datei modernisiert wird
            If didUpgrade Then
                Try
                    SaveToFile(path, cfg)
                Catch ex As Exception
                    Debug.WriteLine($"Upgrade-Resave fehlgeschlagen: {ex.Message}")
                End Try
            End If

            Return ConfigLoadResult.Ok(cfg)
        Catch ex As JsonException
            'JSON kaputt / Parsing-Fehler
            Return ConfigLoadResult.Fail(ConfigLoadStatus.JsonInvalid, "Die JSON-Datei ist beschädigt.", ex)
        Catch ex As UnauthorizedAccessException
            Return ConfigLoadResult.Fail(ConfigLoadStatus.IOError, "Kein Zugriff auf die Konfigurationsdatei (Berechtigung verweigert.", ex)
        Catch ex As IOException
            Return ConfigLoadResult.Fail(ConfigLoadStatus.IOError, "Die Datei konnte nicht gelesen werden (IO-Fehler).", ex)
        Catch ex As Exception
            Return ConfigLoadResult.Fail(ConfigLoadStatus.UnknownError, "Unbekannter Fehler beim Laden der Konfiguration.", ex)
        End Try
    End Function

    Public Shared Sub SaveToFile(savePath As String, cfg As SimulationConfig)
        Directory.CreateDirectory(Path.GetDirectoryName(savePath))

        'Atomisch speichern: erst temp, dann replace
        Dim tmp As String = savePath & ".tmp"
        Dim json As String = JsonSerializer.Serialize(cfg, JsonOptions)

        File.WriteAllText(tmp, json, Encoding.UTF8)

        If File.Exists(savePath) Then
            File.Replace(tmp, savePath, destinationBackupFileName:=Nothing)
        Else
            File.Move(tmp, savePath)
        End If
    End Sub

#End Region

End Class
