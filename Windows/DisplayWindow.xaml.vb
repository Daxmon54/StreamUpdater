Imports System.Windows
Imports StreamUpdater.Models

''' <summary>Ports the essential settings from WIN_Display (default text + time-out + source flags).</summary>
Partial Class DisplayWindow
    Inherits Window

    Private ReadOnly _settings As AppSettings

    Public Sub New()
        InitializeComponent()
        _settings = Application.Instance.Settings
        TxtDefault.Text = _settings.DefaultText
        TxtTimeout.Text = _settings.TimeOut.ToString()
        ChkReplaceAmp.IsChecked = _settings.ReplaceAmp
        ChkNoAudio.IsChecked = _settings.NoAudioItem
    End Sub

    Private Sub OnOk(sender As Object, e As RoutedEventArgs)
        _settings.DefaultText = TxtDefault.Text
        Dim n As Integer
        If Integer.TryParse(TxtTimeout.Text.Trim(), n) Then _settings.TimeOut = n
        _settings.ReplaceAmp = ChkReplaceAmp.IsChecked.GetValueOrDefault()
        _settings.NoAudioItem = ChkNoAudio.IsChecked.GetValueOrDefault()
        _settings.Save()
        MessageBox.Show("Herstart het programma om de wijzigingen door te voeren.", "Infoweergave",
                        MessageBoxButton.OK, MessageBoxImage.Information)
        DialogResult = True
        Close()
    End Sub

    Private Sub OnCancel(sender As Object, e As RoutedEventArgs)
        DialogResult = False
        Close()
    End Sub

End Class
