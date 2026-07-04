Imports System.Runtime.InteropServices
Imports System.Windows
Imports System.Windows.Interop

Namespace Services

    ''' <summary>
    ''' Applies the Windows "immersive dark mode" title-bar attribute so the OS-drawn
    ''' window chrome matches the app's light/dark theme. The client area is themed by
    ''' WPF; this covers the non-client (title bar / borders) that WPF cannot style.
    ''' </summary>
    Public Module NativeTheme

        ' DWMWA_USE_IMMERSIVE_DARK_MODE: 20 on Windows 10 2004+ / Windows 11,
        ' 19 on the earlier 1809–1909 builds. We try 20 first, then fall back to 19.
        Private Const DWMWA_USE_IMMERSIVE_DARK_MODE As Integer = 20
        Private Const DWMWA_USE_IMMERSIVE_DARK_MODE_OLD As Integer = 19

        <DllImport("dwmapi.dll", PreserveSig:=True)>
        Private Function DwmSetWindowAttribute(hwnd As IntPtr, attr As Integer, ByRef attrValue As Integer, attrSize As Integer) As Integer
        End Function

        ''' <summary>Sets the dark/light title bar on a window handle.</summary>
        Public Sub ApplyToHandle(hwnd As IntPtr, dark As Boolean)
            If hwnd = IntPtr.Zero Then Return
            Dim value As Integer = If(dark, 1, 0)
            If DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, value, 4) <> 0 Then
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_OLD, value, 4)
            End If
        End Sub

        ''' <summary>
        ''' Keeps a window's title bar in sync with the active theme: applies it once the
        ''' native handle exists and re-applies whenever the theme is toggled while open.
        ''' Call once per window (e.g. from its constructor).
        ''' </summary>
        Public Sub Track(window As Window)
            Dim applyNow As Action =
                Sub() ApplyToHandle(New WindowInteropHelper(window).Handle, ThemeManager.IsDark)

            If New WindowInteropHelper(window).Handle <> IntPtr.Zero Then
                applyNow()
            Else
                AddHandler window.SourceInitialized, Sub(s, e) applyNow()
            End If

            Dim onThemeChanged As EventHandler = Sub(s, e) applyNow()
            AddHandler ThemeManager.ThemeChanged, onThemeChanged
            AddHandler window.Closed, Sub(s, e) RemoveHandler ThemeManager.ThemeChanged, onThemeChanged
        End Sub

    End Module

End Namespace
