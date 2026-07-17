Imports System.IO
Imports System.Reflection
Imports System.Threading
Imports System.Windows
Imports System.Windows.Threading
Imports StreamUpdater.Models
Imports StreamUpdater.Services
Imports WinForms = System.Windows.Forms

''' <summary>
''' Main window – ports WIN_Main. Holds the file watcher, the sender and the
''' default-text countdown timer, and hosts the system-tray icon.
''' </summary>
Partial Class MainWindow
    Inherits Window

    Private ReadOnly _sender As New StreamSender()
    Private _watcher As TrackWatcher
    Private ReadOnly _countdown As New DispatcherTimer With {.Interval = TimeSpan.FromSeconds(1)}
    Private _tray As WinForms.NotifyIcon
    Private _busy As Boolean
    Private _defaultSent As Boolean

    Private ReadOnly Property Settings As AppSettings
        Get
            Return Application.Instance.Settings
        End Get
    End Property

    Public Sub New()
        InitializeComponent()
        NativeTheme.Track(Me)
        AddHandler _countdown.Tick, AddressOf OnCountdownTick
        AddHandler ThemeManager.ThemeChanged, Sub() SyncThemeMenu()
        AddHandler Loaded, AddressOf OnLoaded
        AddHandler Closing, AddressOf HandleClosing
    End Sub

    Private Sub OnLoaded(sender As Object, e As RoutedEventArgs)
        SetupTray()
        SyncThemeMenu()
        StartTracking()
        UpdateTitle()
    End Sub

    ' ---- Tracking pipeline (StartTracking / TrackingCallback / DataOntvangst3) ----

    Public Sub StartTracking()
        StopTracking()
        _watcher = New TrackWatcher(SynchronizationContext.Current)
        AddHandler _watcher.TrackChanged, AddressOf OnTrackChanged
        AddHandler _watcher.WatchError, Sub(s, msg) LblStatus.Text = msg
        _watcher.Start(Settings.WatchFile)

        ' Process whatever is already in the file at startup.
        If _watcher.IsWatching Then
            Dim initial = _watcher.ReadTrack()
            If initial IsNot Nothing Then OnTrackChanged(Me, initial)
        End If
    End Sub

    Public Sub StopTracking()
        If _watcher IsNot Nothing Then
            _watcher.Dispose()
            _watcher = Nothing
        End If
    End Sub

    Private Async Sub OnTrackChanged(sender As Object, track As TrackInfo)
        If _busy Then Return
        _busy = True
        Try
            LblStatus.Text = ""
            LblInfo.Text = ""

            ' Optional diversity delay before sending (gnDelay).
            If Settings.Delay > 0 Then
                LblStatus.Text = "Tijdsvertraging loopt"
                Await Task.Delay(Settings.Delay * 1000)
                LblStatus.Text = ""
            End If

            Dim result = Await _sender.SendTrackAsync(Settings, track)
            If result.Skipped Then
                LblStatus.Text = result.Status
                Return
            End If

            LblInfo.Text = result.Info
            LblStatus.Text = result.Status
            UpdateTrayTip(result.Info)
            StartTimeoutCountdown()
            LogIfError(result)
        Catch ex As Exception
            LblStatus.Text = "Error: " & ex.Message
            Logger.Error("Unexpected error while processing a track update", ex.ToString())
        Finally
            _busy = False
        End Try
    End Sub

    ''' <summary>Writes a failed send to the daily log file (LOG\yyyy-MM-dd.log).</summary>
    Private Sub LogIfError(result As SendResult)
        If result Is Nothing OrElse Not result.IsError Then Return
        Logger.Error($"Mode {Settings.Mode}: {result.Status}", result.Detail)
    End Sub

    ' ---- Default-text countdown (DefaultTimer / SendText) ----

    Private Sub StartTimeoutCountdown()
        _defaultSent = False
        If Settings.TimeOut <= 0 OrElse String.IsNullOrEmpty(Settings.DefaultText) Then
            _countdown.Stop()
            Timer.Value = 0
            Return
        End If
        Timer.Maximum = Settings.TimeOut
        Timer.Value = Settings.TimeOut
        _countdown.Start()
    End Sub

    Private Async Sub OnCountdownTick(sender As Object, e As EventArgs)
        If Timer.Value > 0 Then
            Timer.Value -= 1
            Return
        End If

        _countdown.Stop()
        If _defaultSent Then Return
        _defaultSent = True

        Try
            Dim result = Await _sender.SendTextAsync(Settings, Settings.DefaultText)
            LblInfo.Text = result.Info
            LblStatus.Text = result.Status
            UpdateTrayTip(result.Info)
            LogIfError(result)
        Catch ex As Exception
            LblStatus.Text = "Error: " & ex.Message
            Logger.Error("Unexpected error while sending default text", ex.ToString())
        End Try
    End Sub

    ' ---- System tray ----

    Private Sub SetupTray()
        _tray = New WinForms.NotifyIcon() With {
            .Text = "StreamUpdater",
            .Visible = True,
            .Icon = LoadTrayIcon()
        }
        AddHandler _tray.DoubleClick, Sub() RestoreFromTray()

        Dim menu As New WinForms.ContextMenuStrip()
        menu.Items.Add("Openen", Nothing, Sub(s, e) RestoreFromTray())
        menu.Items.Add(New WinForms.ToolStripSeparator())
        menu.Items.Add("Afsluiten", Nothing, Sub(s, e) ExitApplication())
        _tray.ContextMenuStrip = menu
    End Sub

    Private Function LoadTrayIcon() As System.Drawing.Icon
        Try
            Dim p = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico")
            If File.Exists(p) Then Return New System.Drawing.Icon(p)
        Catch
        End Try
        Return System.Drawing.SystemIcons.Application
    End Function

    Private Sub UpdateTrayTip(info As String)
        If _tray Is Nothing Then Return
        Dim tip = "StreamUpdater - " & Settings.StreamName
        If Not String.IsNullOrEmpty(info) Then tip &= vbCrLf & info
        ' NotifyIcon.Text is limited to 63 characters.
        If tip.Length > 62 Then tip = tip.Substring(0, 62)
        _tray.Text = tip
    End Sub

    Private Sub RestoreFromTray()
        Show()
        WindowState = WindowState.Normal
        Activate()
    End Sub

    ' ---- Menu handlers ----

    Private Sub OnSettings(sender As Object, e As RoutedEventArgs)
        Dim w As New SettingsWindow() With {.Owner = Me}
        If w.ShowDialog() = True Then
            ' Settings changed – reload the watcher and title.
            StartTracking()
            UpdateTitle()
        End If
    End Sub

    Private Sub OnDisplay(sender As Object, e As RoutedEventArgs)
        Dim w As New DisplayWindow() With {.Owner = Me}
        w.ShowDialog()
    End Sub

    Private Sub OnStandardTexts(sender As Object, e As RoutedEventArgs)
        Dim w As New HourTextWindow("Standaard Teksten", "Standard.ini", "StdTXT_Day") With {.Owner = Me}
        w.ShowDialog()
    End Sub

    Private Sub OnPrograms(sender As Object, e As RoutedEventArgs)
        Dim w As New HourTextWindow("Programma's", "Programs.ini", "Prog_Day") With {.Owner = Me}
        w.ShowDialog()
    End Sub

    Private Sub OnMinimize(sender As Object, e As RoutedEventArgs)
        Hide()
    End Sub

    Private Sub OnAbout(sender As Object, e As RoutedEventArgs)
        Dim w As New AboutWindow() With {.Owner = Me}
        w.ShowDialog()
    End Sub

    Private Sub OnExit(sender As Object, e As RoutedEventArgs)
        If MessageBox.Show("Wenst u het programma af te sluiten?", "StreamUpdater",
                           MessageBoxButton.YesNo, MessageBoxImage.Question) = MessageBoxResult.Yes Then
            ExitApplication()
        End If
    End Sub

    ' ---- Theme ----

    Private Sub OnLightTheme(sender As Object, e As RoutedEventArgs)
        SetTheme(False)
    End Sub

    Private Sub OnDarkTheme(sender As Object, e As RoutedEventArgs)
        SetTheme(True)
    End Sub

    Private Sub SetTheme(dark As Boolean)
        ThemeManager.Apply(dark)
        Settings.DarkTheme = dark
        Settings.Save()
        SyncThemeMenu()
    End Sub

    Private Sub SyncThemeMenu()
        MnuDark.IsChecked = ThemeManager.IsDark
        MnuLight.IsChecked = Not ThemeManager.IsDark
    End Sub

    ' ---- Window plumbing ----

    Private Sub UpdateTitle()
        Dim prefix As String
        Select Case Settings.Mode
            Case StreamMode.Icecast : prefix = "Icecast-Updater"
            Case StreamMode.Rds : prefix = "RDS-Updater"
            Case StreamMode.Shoutcast : prefix = "Shoutcast-Updater"
            Case StreamMode.RadioPlayer : prefix = "RadioPlayer-Updater"
            Case StreamMode.Website : prefix = "Website-Updater"
            Case StreamMode.Butt : prefix = "BUTT"
            Case StreamMode.Dts : prefix = "DTS"
            Case Else : prefix = "StreamUpdater"
        End Select
        Dim ver = Assembly.GetExecutingAssembly().GetName().Version.ToString()
        Title = $"{prefix} {Settings.StreamName}  [{ver}]"
    End Sub

    Private Sub HandleClosing(sender As Object, e As System.ComponentModel.CancelEventArgs)
        ' Closing the window minimises to tray instead of quitting (original behaviour).
        If Not _closing Then
            e.Cancel = True
            Hide()
        End If
    End Sub

    Private _closing As Boolean

    Private Sub ExitApplication()
        _closing = True
        _countdown.Stop()
        StopTracking()
        If _tray IsNot Nothing Then
            _tray.Visible = False
            _tray.Dispose()
        End If
        System.Windows.Application.Current.Shutdown()
    End Sub

End Class
