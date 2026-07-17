Imports System.IO
Imports StreamUpdater.Services

Namespace Models

    ''' <summary>The output target, matching gnMode (1..7) in the original WinDev project.</summary>
    Public Enum StreamMode
        Icecast = 1
        Rds = 2
        Shoutcast = 3
        RadioPlayer = 4
        Website = 5
        Butt = 6
        Dts = 7
    End Enum

    ''' <summary>RDS encoder type, matching gnRDS_Type (COMBO_Encoder).</summary>
    Public Enum RdsType
        Deva = 1
        Orban = 2
        Database = 3
    End Enum

    ''' <summary>
    ''' Strongly-typed view over StreamUpdater.ini. Field names follow the original
    ''' global variables so the port stays easy to cross-reference. Load()/Save()
    ''' reproduce LeesIni() and the WIN_Settings "OK" button respectively.
    ''' </summary>
    Public Class AppSettings

        Public Const IniName As String = "StreamUpdater.ini"

        ' --- Common ---
        Public Property StreamName As String = ""
        Public Property WatchFile As String = ""
        Public Property Mode As StreamMode = StreamMode.Icecast
        Public Property Delay As Integer = 0
        Public Property InfoOrder As Integer = 1
        Public Property Sep1 As String = "-"
        Public Property Sep2 As String = "-"

        ' --- Stream (Icecast / Shoutcast direct) ---
        Public Property StreamAddress As String = ""
        Public Property StreamUser As String = ""
        Public Property StreamPass As String = ""
        Public Property StreamPort As Integer = 0
        Public Property StreamMount As String = ""

        ' --- RDS ---
        Public Property RdsEncoder As RdsType = RdsType.Deva
        Public Property RdsAddress As String = "127.0.0.1"
        Public Property RdsPort As Integer = 8000
        Public Property RdsPass As String = ""
        Public Property RdsUser As String = ""
        Public Property RdsClock As Boolean = False
        Public Property RdsPs As Boolean = False
        Public Property RdsRt As Boolean = True
        Public Property RdsPsText As String = ""
        ' MySQL/MariaDB schema for the "Database" encoder (original HFSQL DB was "WebMaster").
        Public Property RdsDatabase As String = "WebMaster"

        ' --- FTP / Website ---
        Public Property FtpAddress As String = ""
        Public Property FtpUser As String = ""
        Public Property FtpPass As String = ""
        Public Property FtpPort As Integer = 21
        Public Property FtpRemote As String = ""
        Public Property WebPrefix As String = ""
        Public Property WebFile As String = "livetrack.txt"

        ' --- RadioPlayer ---
        Public Property RpUid As String = ""
        Public Property RpIngest As String = ""
        Public Property RpApiKey As String = ""
        Public Property RpUser As String = ""

        ' --- BUTT ---
        Public Property ButtFile As String = ""
        Public Property ButtDelete As Boolean = False

        ' --- DTS ---
        Public Property DtsUrl As String = ""
        Public Property DtsKey As String = ""
        Public Property DtsUid As String = ""
        Public Property DtsFormat As String = ""
        Public Property DtsStatus As String = ""
        Public Property DtsType As String = ""

        ' --- Info / default text ---
        Public Property InfoType As Integer = 1
        Public Property DefaultText As String = ""
        Public Property TimeOut As Integer = 60
        Public Property SkipArtist As String = ""

        ' --- Source ---
        Public Property ReplaceAmp As Boolean = False
        Public Property NoAudioItem As Boolean = False

        ' --- App-specific (new in the .NET port) ---
        Public Property DarkTheme As Boolean = False
        ''' <summary>When on, the full diagnostic (HTTP body / exception) is also written to the log.</summary>
        Public Property DebugMode As Boolean = False

        Public Shared ReadOnly Property IniPath As String
            Get
                Return System.IO.Path.Combine(AppContext.BaseDirectory, IniName)
            End Get
        End Property

        ''' <summary>Reads every value from StreamUpdater.ini (equivalent to LeesIni()).</summary>
        Public Shared Function Load() As AppSettings
            Dim ini As New IniFile(IniPath)
            Dim s As New AppSettings()

            s.StreamName = ini.ReadString("Common", "Name", "")
            s.WatchFile = ini.ReadString("Common", "WatchFile_Extended", "")
            s.Mode = CType(Clamp(ini.ReadInt("Common", "Mode", 1), 1, 7), StreamMode)
            s.Delay = ini.ReadInt("Common", "Delay", 0)
            s.InfoOrder = ini.ReadInt("Common", "InfoOrder", 1)
            s.Sep1 = ini.ReadString("Common", "Sep_1", "-")
            s.Sep2 = ini.ReadString("Common", "Sep_2", "-")

            s.StreamAddress = ini.ReadString("Stream", "Adress", "")
            s.StreamUser = ini.ReadString("Stream", "User", "")
            s.StreamPass = ini.ReadString("Stream", "Pass", "")
            s.StreamPort = ini.ReadInt("Stream", "Port", 0)
            s.StreamMount = ini.ReadString("Stream", "Mount", "")

            s.RdsEncoder = CType(Clamp(ini.ReadInt("RDS", "Type", 1), 1, 3), RdsType)
            s.RdsAddress = ini.ReadString("RDS", "Adress", "127.0.0.1")
            s.RdsPort = ini.ReadInt("RDS", "Port", 8000)
            s.RdsPass = ini.ReadString("RDS", "Pasw", "")
            s.RdsUser = ini.ReadString("RDS", "User", "")
            s.RdsClock = ini.ReadBool("RDS", "Clock", False)
            s.RdsPs = ini.ReadBool("RDS", "PS", False)
            s.RdsRt = ini.ReadBool("RDS", "RT", True)
            s.RdsPsText = ini.ReadString("RDS", "PS_Text", "")
            s.RdsDatabase = ini.ReadString("RDS", "Database", "WebMaster")

            s.FtpAddress = ini.ReadString("FTP", "Adress", "")
            s.FtpUser = ini.ReadString("FTP", "User", "")
            s.FtpPass = ini.ReadString("FTP", "Passw", "")
            s.FtpPort = ini.ReadInt("FTP", "Port", 21)
            s.FtpRemote = ini.ReadString("FTP", "Remote", "")
            s.WebPrefix = ini.ReadString("FTP", "Prefix", "")
            s.WebFile = ini.ReadString("FTP", "WebFile", "livetrack.txt")

            s.RpUid = ini.ReadString("RP", "UID", "")
            s.RpIngest = ini.ReadString("RP", "Ingest", "")
            s.RpApiKey = ini.ReadString("RP", "API_Key", "")
            s.RpUser = ini.ReadString("RP", "User", "")

            s.ButtFile = ini.ReadString("BUTT", "File", "")
            s.ButtDelete = ini.ReadBool("BUTT", "Delete", False)

            s.DtsUrl = ini.ReadString("DTS", "URL", "")
            s.DtsKey = ini.ReadString("DTS", "Key", "")
            s.DtsUid = ini.ReadString("DTS", "UID", "")
            s.DtsFormat = ini.ReadString("DTS", "Format", "")
            s.DtsStatus = ini.ReadString("DTS", "Status", "")
            s.DtsType = ini.ReadString("DTS", "Type", "")

            s.InfoType = ini.ReadInt("Info", "Type", 1)
            s.DefaultText = ini.ReadString("Info", "Default", "")
            s.TimeOut = ini.ReadInt("Info", "TimeOut", 60)
            s.SkipArtist = ini.ReadString("Info", "Skip", "")

            s.ReplaceAmp = ini.ReadBool("Source", "ReplaceAmp", False)
            s.NoAudioItem = ini.ReadBool("Source", "NoAudioItem", False)

            s.DarkTheme = ini.ReadBool("App", "DarkTheme", False)
            s.DebugMode = ini.ReadBool("App", "Debug", False)

            Return s
        End Function

        ''' <summary>Persists all values back to StreamUpdater.ini (equivalent to WIN_Settings.BTN_OK).</summary>
        Public Sub Save()
            Dim ini As New IniFile(IniPath)

            ini.Write("Common", "Name", StreamName)
            ini.Write("Common", "WatchFile_Extended", WatchFile)
            ini.Write("Common", "Mode", CInt(Mode).ToString())
            ini.Write("Common", "Delay", Delay.ToString())
            ini.Write("Common", "InfoOrder", InfoOrder.ToString())
            ini.Write("Common", "Sep_1", Sep1)
            ini.Write("Common", "Sep_2", Sep2)

            ini.Write("Stream", "Adress", StreamAddress)
            ini.Write("Stream", "User", StreamUser)
            ini.Write("Stream", "Pass", StreamPass)
            ini.Write("Stream", "Port", StreamPort.ToString())
            ini.Write("Stream", "Mount", StreamMount)

            ini.Write("RDS", "Type", CInt(RdsEncoder).ToString())
            ini.Write("RDS", "Adress", RdsAddress)
            ini.Write("RDS", "Port", RdsPort.ToString())
            ini.Write("RDS", "Pasw", RdsPass)
            ini.Write("RDS", "User", RdsUser)
            ini.WriteBool("RDS", "Clock", RdsClock)
            ini.WriteBool("RDS", "PS", RdsPs)
            ini.WriteBool("RDS", "RT", RdsRt)
            ini.Write("RDS", "PS_Text", RdsPsText)
            ini.Write("RDS", "Database", RdsDatabase)

            ini.Write("FTP", "Adress", FtpAddress)
            ini.Write("FTP", "User", FtpUser)
            ini.Write("FTP", "Passw", FtpPass)
            ini.Write("FTP", "Port", FtpPort.ToString())
            ini.Write("FTP", "Remote", FtpRemote)
            ini.Write("FTP", "Prefix", WebPrefix)
            ini.Write("FTP", "WebFile", WebFile)

            ini.Write("RP", "UID", RpUid)
            ini.Write("RP", "Ingest", RpIngest)
            ini.Write("RP", "API_Key", RpApiKey)
            ini.Write("RP", "User", RpUser)

            ini.Write("BUTT", "File", ButtFile)
            ini.WriteBool("BUTT", "Delete", ButtDelete)

            ini.Write("DTS", "URL", DtsUrl)
            ini.Write("DTS", "Key", DtsKey)
            ini.Write("DTS", "UID", DtsUid)
            ini.Write("DTS", "Format", DtsFormat)
            ini.Write("DTS", "Status", DtsStatus)
            ini.Write("DTS", "Type", DtsType)

            ini.Write("Info", "Type", InfoType.ToString())
            ini.Write("Info", "Default", DefaultText)
            ini.Write("Info", "TimeOut", TimeOut.ToString())
            ini.Write("Info", "Skip", SkipArtist)

            ini.WriteBool("Source", "ReplaceAmp", ReplaceAmp)
            ini.WriteBool("Source", "NoAudioItem", NoAudioItem)

            ini.WriteBool("App", "DarkTheme", DarkTheme)
            ini.WriteBool("App", "Debug", DebugMode)

            ini.Save()
        End Sub

        Private Shared Function Clamp(value As Integer, lo As Integer, hi As Integer) As Integer
            If value < lo Then Return lo
            If value > hi Then Return hi
            Return value
        End Function

    End Class

End Namespace
