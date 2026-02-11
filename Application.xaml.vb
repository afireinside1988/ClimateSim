Imports System.Windows.Markup

Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Public Sub New()
        'Textbox-Verhalten für Doubles zurücksetzen
        System.Windows.FrameworkCompatibilityPreferences.KeepTextBoxDisplaySynchronizedWithTextProperty = False

        'Language korrekt setzen
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(Globalization.CultureInfo.CurrentUICulture.IetfLanguageTag)))
    End Sub

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)
        GdalHelpers.InitGdal()
    End Sub

End Class
