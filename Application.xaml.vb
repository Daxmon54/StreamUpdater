Imports System.Windows
Imports StreamUpdater.Models
Imports StreamUpdater.Services

''' <summary>
''' Application entry point. Loads settings, applies the saved theme and shows the
''' main window. Ensures only a single instance runs (the original was a tray app).
''' </summary>
Partial Class Application
    Inherits System.Windows.Application

    Private _mutex As System.Threading.Mutex

    Public Property Settings As AppSettings

    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        MyBase.OnStartup(e)

        ' Single-instance guard.
        Dim createdNew As Boolean
        _mutex = New System.Threading.Mutex(True, "StreamUpdater_SingleInstance_2E1F", createdNew)
        If Not createdNew Then
            MessageBox.Show("StreamUpdater is already running.", "StreamUpdater",
                            MessageBoxButton.OK, MessageBoxImage.Information)
            Shutdown()
            Return
        End If

        Settings = AppSettings.Load()
        ThemeManager.Apply(Settings.DarkTheme)

        ' Keep the process alive while minimised to the tray.
        ShutdownMode = ShutdownMode.OnExplicitShutdown

        Dim main As New MainWindow()
        main.Show()
    End Sub

    Protected Overrides Sub OnExit(e As ExitEventArgs)
        _mutex?.Dispose()
        MyBase.OnExit(e)
    End Sub

    ''' <summary>Convenience accessor used throughout the windows.</summary>
    Public Shared ReadOnly Property Instance As Application
        Get
            Return CType(System.Windows.Application.Current, Application)
        End Get
    End Property

End Class
