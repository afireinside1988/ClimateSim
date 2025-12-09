Imports System.Reflection

Public Class AppInfoViewModel
    Inherits ViewModelBase

    Public Shared ReadOnly Property ProductName As String
        Get
            Dim asm As Assembly = Assembly.GetExecutingAssembly()
            Dim attr = asm.GetCustomAttribute(Of AssemblyProductAttribute)()
            If attr IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(attr.Product) Then
                Return attr.Product
            End If
            Return asm.GetName().Name
        End Get
    End Property

    Public Shared ReadOnly Property Title As String
        Get
            Dim asm As Assembly = Assembly.GetExecutingAssembly()
            Dim attr = asm.GetCustomAttribute(Of AssemblyTitleAttribute)()
            If attr IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(attr.Title) Then
                Return attr.Title
            End If
            Return "SCOACH"
        End Get
    End Property
    Public Shared ReadOnly Property Version As Version
        Get
            Dim asm As Assembly = Assembly.GetExecutingAssembly()
            Return asm.GetName().Version
        End Get
    End Property

    ''' <summary>
    ''' Major.Minor.Patch (erste drei Zahlen der AssemblyVersion)
    ''' Beispiel: 0.1.0
    ''' </summary>
    Public Shared ReadOnly Property SemanticVersion As String
        Get
            Dim v = Version
            If v Is Nothing Then Return "0.0.0"
            Return $"{v.Major}.{v.Minor}.{v.Build}"
        End Get
    End Property

    ''' <summary>
    ''' Buildnummer aus der vierten Komponente (Revision).
    ''' Beispiel: der Wert nach dem Stern in 0.1.0.*
    ''' </summary>
    Public Shared ReadOnly Property BuildNumber As Integer
        Get
            Dim v = Version
            If v Is Nothing Then Return 0
            Return v.Revision
        End Get
    End Property

    ''' <summary>
    ''' Vollständige AssemblyVersion, z.B. 0.1.0.1234
    ''' </summary>
    Public Shared ReadOnly Property FullVersion As String
        Get
            Dim v = Version
            If v Is Nothing Then Return "0.0.0.0"
            Return $"{v.Major}.{v.Minor}.{v.Build}.{v.Revision}"
        End Get
    End Property

    ''' <summary>
    ''' Optional: der "Marketing-String" wie 0.1.0-alpha.
    ''' </summary>
    Public Shared ReadOnly Property InformationalVersion As String
        Get
            Dim asm As Assembly = Assembly.GetExecutingAssembly()
            Dim attr = asm.GetCustomAttribute(Of AssemblyInformationalVersionAttribute)()
            If attr IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(attr.InformationalVersion) Then
                Return attr.InformationalVersion
            End If

            ' Fallback
            Return FullVersion
        End Get
    End Property

    ''' <summary>
    ''' Schöner Titel für Window/App: SCOACH – v0.1.0 (Build 1234)
    ''' </summary>
    Public Shared ReadOnly Property AppTitleWithVersion As String
        Get
            Return $"{ProductName} – v{SemanticVersion} (Build {BuildNumber})"
        End Get
    End Property

End Class