Imports System.IO

Namespace Services

    ''' <summary>
    ''' Reads/writes the weekday-by-hour tables used by WIN_programs (Programs.ini,
    ''' section prefix "Prog_Day") and WIN_Standaard_Tekst (Standard.ini, prefix
    ''' "StdTXT_Day"). Keys are HR0..HR23; days are 1..7 (Monday..Sunday).
    ''' </summary>
    Public Class HourTextStore

        Private ReadOnly _ini As IniFile
        Private ReadOnly _prefix As String

        Public Sub New(fileName As String, sectionPrefix As String)
            _ini = New IniFile(System.IO.Path.Combine(AppContext.BaseDirectory, fileName))
            _prefix = sectionPrefix
        End Sub

        Private Function Section(day As Integer) As String
            Return _prefix & day.ToString()
        End Function

        Public Function GetText(day As Integer, hour As Integer) As String
            Return _ini.ReadString(Section(day), "HR" & hour.ToString(), "")
        End Function

        Public Sub SetText(day As Integer, hour As Integer, value As String)
            _ini.Write(Section(day), "HR" & hour.ToString(), value)
        End Sub

        Public Sub Save()
            _ini.Save()
        End Sub

    End Class

End Namespace
