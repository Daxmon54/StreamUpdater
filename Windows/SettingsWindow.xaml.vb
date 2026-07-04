Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports Microsoft.Win32
Imports StreamUpdater.Models
Imports StreamUpdater.Services

''' <summary>Ports WIN_Settings – edits every mode's configuration and saves to the INI.</summary>
Partial Class SettingsWindow
    Inherits Window

    Private ReadOnly _settings As AppSettings

    Public Sub New()
        InitializeComponent()
        _settings = Application.Instance.Settings
        PopulateChoices()
        LoadFromSettings()
    End Sub

    Private Sub PopulateChoices()
        CboMode.Items.Clear()
        AddModeItem("1 – Icecast", StreamMode.Icecast)
        AddModeItem("2 – RDS", StreamMode.Rds)
        AddModeItem("3 – Shoutcast", StreamMode.Shoutcast)
        AddModeItem("4 – RadioPlayer", StreamMode.RadioPlayer)
        AddModeItem("5 – Website / FTP", StreamMode.Website)
        AddModeItem("6 – BUTT", StreamMode.Butt)
        AddModeItem("7 – DTS", StreamMode.Dts)

        CboRds.Items.Clear()
        CboRds.Items.Add(New ComboBoxItem With {.Content = "Deva", .Tag = RdsType.Deva})
        CboRds.Items.Add(New ComboBoxItem With {.Content = "Orban", .Tag = RdsType.Orban})
        CboRds.Items.Add(New ComboBoxItem With {.Content = "Database", .Tag = RdsType.Database})

        CboOrder.Items.Clear()
        CboOrder.Items.Add(New ComboBoxItem With {.Content = "Artiest - Titel", .Tag = 1})
        CboOrder.Items.Add(New ComboBoxItem With {.Content = "Artiest - Titel - Jaar", .Tag = 2})
    End Sub

    Private Sub AddModeItem(text As String, mode As StreamMode)
        CboMode.Items.Add(New ComboBoxItem With {.Content = text, .Tag = mode})
    End Sub

    Private Sub LoadFromSettings()
        Dim s = _settings

        TxtName.Text = s.StreamName
        TxtWatch.Text = s.WatchFile
        SelectByTag(CboMode, s.Mode)

        TxtStreamAddr.Text = s.StreamAddress
        TxtStreamUser.Text = s.StreamUser
        TxtStreamPass.Text = s.StreamPass
        TxtStreamPort.Text = s.StreamPort.ToString()
        TxtStreamMount.Text = s.StreamMount

        SelectByTag(CboRds, s.RdsEncoder)
        TxtRdsAddr.Text = s.RdsAddress
        TxtRdsPort.Text = s.RdsPort.ToString()
        TxtRdsUser.Text = s.RdsUser
        TxtRdsPass.Text = s.RdsPass
        TxtRdsDb.Text = s.RdsDatabase
        ChkRdsRt.IsChecked = s.RdsRt
        ChkRdsPs.IsChecked = s.RdsPs
        ChkRdsClock.IsChecked = s.RdsClock
        TxtRdsPsText.Text = s.RdsPsText

        TxtRpUid.Text = s.RpUid
        TxtRpIngest.Text = s.RpIngest
        TxtRpApiKey.Text = s.RpApiKey
        TxtRpUser.Text = s.RpUser

        TxtFtpAddr.Text = s.FtpAddress
        TxtFtpPort.Text = s.FtpPort.ToString()
        TxtFtpUser.Text = s.FtpUser
        TxtFtpPass.Text = s.FtpPass
        TxtFtpRemote.Text = s.FtpRemote
        TxtWebPrefix.Text = s.WebPrefix
        TxtWebFile.Text = s.WebFile

        TxtButtFile.Text = s.ButtFile
        ChkButtDelete.IsChecked = s.ButtDelete

        TxtDtsUrl.Text = s.DtsUrl
        TxtDtsKey.Text = s.DtsKey
        TxtDtsUid.Text = s.DtsUid
        TxtDtsFormat.Text = s.DtsFormat
        TxtDtsStatus.Text = s.DtsStatus
        TxtDtsType.Text = s.DtsType

        TxtDefaultText.Text = s.DefaultText
        TxtDelay.Text = s.Delay.ToString()
        TxtTimeOut.Text = s.TimeOut.ToString()
        TxtSkip.Text = s.SkipArtist
        SelectByTag(CboOrder, s.InfoOrder)
        TxtSep1.Text = s.Sep1
        TxtSep2.Text = s.Sep2
        ChkReplaceAmp.IsChecked = s.ReplaceAmp
        ChkNoAudio.IsChecked = s.NoAudioItem

        SelectTabForMode(s.Mode)
    End Sub

    Private Sub SaveToSettings()
        ApplyToSettings(_settings)
        _settings.Save()
    End Sub

    ''' <summary>Copies the on-screen values into <paramref name="s"/> without persisting.</summary>
    Private Sub ApplyToSettings(s As AppSettings)
        s.StreamName = TxtName.Text.Trim()
        s.WatchFile = TxtWatch.Text.Trim()
        s.Mode = CType(SelectedTag(CboMode, StreamMode.Icecast), StreamMode)

        s.StreamAddress = TxtStreamAddr.Text.Trim()
        s.StreamUser = TxtStreamUser.Text.Trim()
        s.StreamPass = TxtStreamPass.Text
        s.StreamPort = ParseInt(TxtStreamPort.Text, 0)
        s.StreamMount = TxtStreamMount.Text.Trim()

        s.RdsEncoder = CType(SelectedTag(CboRds, RdsType.Deva), RdsType)
        s.RdsAddress = TxtRdsAddr.Text.Trim()
        s.RdsPort = ParseInt(TxtRdsPort.Text, 8000)
        s.RdsUser = TxtRdsUser.Text.Trim()
        s.RdsPass = TxtRdsPass.Text
        s.RdsDatabase = TxtRdsDb.Text.Trim()
        s.RdsRt = ChkRdsRt.IsChecked.GetValueOrDefault()
        s.RdsPs = ChkRdsPs.IsChecked.GetValueOrDefault()
        s.RdsClock = ChkRdsClock.IsChecked.GetValueOrDefault()
        s.RdsPsText = TxtRdsPsText.Text

        s.RpUid = TxtRpUid.Text.Trim()
        s.RpIngest = TxtRpIngest.Text.Trim()
        s.RpApiKey = TxtRpApiKey.Text
        s.RpUser = TxtRpUser.Text.Trim()

        s.FtpAddress = TxtFtpAddr.Text.Trim()
        s.FtpPort = ParseInt(TxtFtpPort.Text, 21)
        s.FtpUser = TxtFtpUser.Text.Trim()
        s.FtpPass = TxtFtpPass.Text
        s.FtpRemote = TxtFtpRemote.Text.Trim()
        s.WebPrefix = TxtWebPrefix.Text
        s.WebFile = TxtWebFile.Text.Trim()

        s.ButtFile = TxtButtFile.Text.Trim()
        s.ButtDelete = ChkButtDelete.IsChecked.GetValueOrDefault()

        s.DtsUrl = TxtDtsUrl.Text.Trim()
        s.DtsKey = TxtDtsKey.Text.Trim()
        s.DtsUid = TxtDtsUid.Text.Trim()
        s.DtsFormat = TxtDtsFormat.Text.Trim()
        s.DtsStatus = TxtDtsStatus.Text.Trim()
        s.DtsType = TxtDtsType.Text.Trim()

        s.DefaultText = TxtDefaultText.Text
        s.Delay = ParseInt(TxtDelay.Text, 0)
        s.TimeOut = ParseInt(TxtTimeOut.Text, 60)
        s.SkipArtist = TxtSkip.Text
        s.InfoOrder = CInt(SelectedTag(CboOrder, 1))
        s.Sep1 = TxtSep1.Text
        s.Sep2 = TxtSep2.Text
        s.ReplaceAmp = ChkReplaceAmp.IsChecked.GetValueOrDefault()
        s.NoAudioItem = ChkNoAudio.IsChecked.GetValueOrDefault()
    End Sub

    ' ---- Helpers ----

    Private Shared Sub SelectByTag(combo As ComboBox, tag As Object)
        For Each item As ComboBoxItem In combo.Items
            If Equals(item.Tag, tag) Then
                combo.SelectedItem = item
                Return
            End If
        Next
        If combo.Items.Count > 0 Then combo.SelectedIndex = 0
    End Sub

    Private Shared Function SelectedTag(combo As ComboBox, fallback As Object) As Object
        Dim item = TryCast(combo.SelectedItem, ComboBoxItem)
        Return If(item?.Tag, fallback)
    End Function

    Private Shared Function ParseInt(text As String, fallback As Integer) As Integer
        Dim n As Integer
        If Integer.TryParse(If(text, "").Trim(), n) Then Return n
        Return fallback
    End Function

    Private Sub SelectTabForMode(mode As StreamMode)
        ' Jump to the tab most relevant to the selected mode.
        Dim index As Integer
        Select Case mode
            Case StreamMode.Icecast, StreamMode.Shoutcast : index = 0
            Case StreamMode.Rds : index = 1
            Case StreamMode.RadioPlayer : index = 2
            Case StreamMode.Website : index = 3
            Case StreamMode.Butt : index = 4
            Case StreamMode.Dts : index = 5
            Case Else : index = 0
        End Select
        Tabs.SelectedIndex = index
    End Sub

    ' ---- Events ----

    Private Sub OnModeChanged(sender As Object, e As SelectionChangedEventArgs)
        If Not IsLoaded Then Return
        Dim item = TryCast(CboMode.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then SelectTabForMode(CType(item.Tag, StreamMode))
    End Sub

    Private Sub OnBrowseWatch(sender As Object, e As RoutedEventArgs)
        Dim dlg As New OpenFileDialog With {.Filter = "JSON / tekst|*.json;*.txt|Alle bestanden|*.*"}
        If dlg.ShowDialog(Me) = True Then TxtWatch.Text = dlg.FileName
    End Sub

    Private Sub OnBrowseButt(sender As Object, e As RoutedEventArgs)
        Dim dlg As New SaveFileDialog With {.Filter = "Tekstbestand|*.txt|Alle bestanden|*.*", .OverwritePrompt = False}
        If dlg.ShowDialog(Me) = True Then TxtButtFile.Text = dlg.FileName
    End Sub

    ''' <summary>
    ''' Fires a sample track through the currently-selected mode using the on-screen
    ''' values, without saving. Lets the user validate a target before committing.
    ''' </summary>
    Private Async Sub OnTest(sender As Object, e As RoutedEventArgs)
        ' Build a throwaway settings object from the current form.
        Dim probe As New AppSettings()
        ApplyToSettings(probe)

        Dim sample As New TrackInfo With {
            .Artist = "Test Artist",
            .Title = "Test Title",
            .Year = "2024",
            .Duration = "03:30",
            .InfoType = 0
        }

        BtnTest.IsEnabled = False
        Dim previousCursor = Cursor
        Cursor = Cursors.Wait
        Try
            Dim result = Await New StreamSender().SendTrackAsync(probe, sample)
            Dim message = $"Mode: {probe.Mode}" & vbCrLf &
                          $"Verzonden: {result.Info}" & vbCrLf & vbCrLf &
                          $"Resultaat: {result.Status}"
            Dim ok = result.Status.StartsWith("SEND OK", StringComparison.OrdinalIgnoreCase) OrElse
                     result.Status.StartsWith("Info sent", StringComparison.OrdinalIgnoreCase) OrElse
                     result.Status.StartsWith("File", StringComparison.OrdinalIgnoreCase)
            MessageBox.Show(message, "Test verzenden", MessageBoxButton.OK,
                            If(ok, MessageBoxImage.Information, MessageBoxImage.Warning))
        Catch ex As Exception
            MessageBox.Show("Test mislukt: " & ex.Message, "Test verzenden",
                            MessageBoxButton.OK, MessageBoxImage.Error)
        Finally
            Cursor = previousCursor
            BtnTest.IsEnabled = True
        End Try
    End Sub

    ''' <summary>Pings the MySQL/MariaDB server for the "Database" encoder (SELECT 1).</summary>
    Private Async Sub OnTestDb(sender As Object, e As RoutedEventArgs)
        Dim probe As New AppSettings()
        ApplyToSettings(probe)

        BtnTestDb.IsEnabled = False
        Dim previousCursor = Cursor
        Cursor = Cursors.Wait
        Try
            Dim result = Await StreamSender.TestDatabaseAsync(probe)
            If result = "OK" Then
                MessageBox.Show($"Verbinding met '{probe.RdsDatabase}' op {probe.RdsAddress}:{probe.RdsPort} is geslaagd.",
                                "Test DB-verbinding", MessageBoxButton.OK, MessageBoxImage.Information)
            Else
                MessageBox.Show("Verbinding mislukt:" & vbCrLf & vbCrLf & result,
                                "Test DB-verbinding", MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
        Finally
            Cursor = previousCursor
            BtnTestDb.IsEnabled = True
        End Try
    End Sub

    Private Sub OnOk(sender As Object, e As RoutedEventArgs)
        SaveToSettings()
        DialogResult = True
        Close()
    End Sub

    Private Sub OnCancel(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub

End Class
