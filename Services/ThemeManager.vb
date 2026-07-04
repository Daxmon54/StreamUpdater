Imports System.Windows

Namespace Services

    ''' <summary>
    ''' Swaps the active theme ResourceDictionary at runtime. Provides the light/dark
    ''' option requested for the .NET version. The current theme dictionary is always
    ''' kept as the first entry in Application.Resources.MergedDictionaries.
    ''' </summary>
    Public Module ThemeManager

        Private Const LightUri As String = "Themes/Light.xaml"
        Private Const DarkUri As String = "Themes/Dark.xaml"

        Private _isDark As Boolean

        Public ReadOnly Property IsDark As Boolean
            Get
                Return _isDark
            End Get
        End Property

        Public Event ThemeChanged As EventHandler

        ''' <summary>Applies the light or dark palette to the whole application.</summary>
        Public Sub Apply(dark As Boolean)
            _isDark = dark
            Dim app = Application.Current
            If app Is Nothing Then Return

            Dim newDict As New ResourceDictionary With {
                .Source = New Uri(If(dark, DarkUri, LightUri), UriKind.Relative)
            }

            ' Remove any previously-applied palette (identified by the marker key) and
            ' insert the new one at the front so control styles resolve against it.
            Dim dicts = app.Resources.MergedDictionaries
            For i = dicts.Count - 1 To 0 Step -1
                If dicts(i).Contains("WindowBackgroundBrush") Then dicts.RemoveAt(i)
            Next
            dicts.Insert(0, newDict)

            RaiseEvent ThemeChanged(Nothing, EventArgs.Empty)
        End Sub

        Public Sub Toggle()
            Apply(Not _isDark)
        End Sub

    End Module

End Namespace
