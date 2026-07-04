Imports System.Diagnostics
Imports System.Reflection
Imports System.Windows
Imports System.Windows.Navigation

''' <summary>Ports WIN_About – logo, version (from the assembly) and a website link.</summary>
Partial Class AboutWindow
    Inherits Window

    Private Const SiteUrl As String = "https://www.example.com/"

    Public Sub New()
        InitializeComponent()
        Dim ver = Assembly.GetExecutingAssembly().GetName().Version.ToString()
        LblVersion.Text = "Versie: " & ver
        LinkSite.NavigateUri = New Uri(SiteUrl)
    End Sub

    Private Sub OnLink(sender As Object, e As RoutedEventArgs)
        Try
            Process.Start(New ProcessStartInfo(SiteUrl) With {.UseShellExecute = True})
        Catch ex As Exception
            MessageBox.Show(ex.Message, "StreamUpdater", MessageBoxButton.OK, MessageBoxImage.Warning)
        End Try
    End Sub

    Private Sub OnClose(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

End Class
