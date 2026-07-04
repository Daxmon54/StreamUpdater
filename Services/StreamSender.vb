Imports System.IO
Imports System.Net
Imports System.Net.Http
Imports System.Net.Http.Headers
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports System.Threading.Tasks
Imports MySqlConnector
Imports StreamUpdater.Models

Namespace Services

    ''' <summary>Outcome of a send attempt, surfaced to the UI (STC_Info / STC_Status).</summary>
    Public Class SendResult
        Public Property Info As String = ""
        Public Property Status As String = ""
        Public Property Skipped As Boolean = False
        Public Sub New()
        End Sub
        Public Sub New(info As String, status As String)
            Me.Info = info
            Me.Status = status
        End Sub
    End Class

    ''' <summary>
    ''' Ports the send logic from WIN_Main (DataOntvangst3, SendRDS, SendRP, StartFTP,
    ''' Send_DTS, SendText). Each mode maps 1:1 to gnMode 1..7. Network calls are async
    ''' so the UI thread never blocks.
    ''' </summary>
    Public Class StreamSender

        Private Shared ReadOnly _http As New HttpClient(New HttpClientHandler() With {
            .Credentials = Nothing,
            .PreAuthenticate = False
        }) With {.Timeout = TimeSpan.FromSeconds(15)}

        ' gsOldString – used by the RDS and RadioPlayer paths to suppress duplicate sends.
        Private _oldString As String = ""

        ''' <summary>
        ''' Builds the "now playing" line from a parsed track and sends it to the
        ''' configured target. Mirrors DataOntvangst3.
        ''' </summary>
        Public Async Function SendTrackAsync(settings As AppSettings, track As TrackInfo) As Task(Of SendResult)
            Dim liveText As String
            Dim artist = If(track.Artist, "")
            Dim title = If(track.Title, "")

            If track.InfoType = 0 OrElse track.InfoType = 1 Then
                Select Case settings.InfoOrder
                    Case 1 ' artist - title
                        liveText = artist & " " & settings.Sep1 & " " & title
                    Case 2 ' artist - title - year
                        liveText = artist & " " & settings.Sep1 & " " & title & settings.Sep2 & If(track.Year, "")
                    Case Else
                        liveText = artist & " " & settings.Sep1 & " " & title
                End Select
            Else
                liveText = artist
            End If

            ' Ignore lines whose artist matches the configured "skip" value.
            If Not String.IsNullOrEmpty(settings.SkipArtist) AndAlso artist = settings.SkipArtist Then
                Return New SendResult("", "Information ignored") With {.Skipped = True}
            End If

            Dim status = Await DispatchAsync(settings, liveText, track)
            Return New SendResult(liveText, status)
        End Function

        ''' <summary>Sends a plain line (default text / standard text) – mirrors SendText.</summary>
        Public Async Function SendTextAsync(settings As AppSettings, text As String) As Task(Of SendResult)
            Dim status = Await DispatchAsync(settings, text, New TrackInfo() With {.Artist = text, .Title = "", .InfoType = 2})
            Return New SendResult(text, status)
        End Function

        Private Async Function DispatchAsync(settings As AppSettings, liveText As String, track As TrackInfo) As Task(Of String)
            If String.IsNullOrEmpty(liveText) Then Return "No information"

            Dim outText = liveText
            If settings.ReplaceAmp Then outText = outText.Replace("&", "-")

            Try
                Select Case settings.Mode
                    Case StreamMode.Icecast
                        Return Await SendIcecastAsync(settings, outText)
                    Case StreamMode.Rds
                        Return Await SendRdsAsync(settings, outText)
                    Case StreamMode.Shoutcast
                        Return Await SendShoutcastAsync(settings, outText)
                    Case StreamMode.RadioPlayer
                        Return Await SendRadioPlayerAsync(settings, track.Artist, track.Title)
                    Case StreamMode.Website
                        Return Await SendWebsiteAsync(settings, outText)
                    Case StreamMode.Butt
                        Return SendButt(settings, outText)
                    Case StreamMode.Dts
                        Return Await SendDtsAsync(settings, track)
                    Case Else
                        Return "Unknown mode"
                End Select
            Catch ex As Exception
                Return "Error: " & ex.Message
            End Try
        End Function

        ' ---- Mode 1: Icecast ------------------------------------------------
        Private Async Function SendIcecastAsync(s As AppSettings, song As String) As Task(Of String)
            Dim url = $"http://{s.StreamAddress}:{s.StreamPort}/admin/metadata?mount={Uri.EscapeDataString(s.StreamMount)}" &
                      $"&mode=updinfo&charset=UTF-8&song={Uri.EscapeDataString(song)}"
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                AddBasicAuth(req, s.StreamUser, s.StreamPass)
                Dim resp = Await _http.SendAsync(req)
                Return If(resp.IsSuccessStatusCode, "SEND OK", "Icecast error: " & CInt(resp.StatusCode).ToString())
            End Using
        End Function

        ' ---- Mode 3: Shoutcast ---------------------------------------------
        Private Async Function SendShoutcastAsync(s As AppSettings, song As String) As Task(Of String)
            Dim payload = song.Replace("&", "%26")
            Dim url = $"http://{s.StreamAddress}:{s.StreamPort}/admin.cgi?pass={Uri.EscapeDataString(s.StreamPass)}" &
                      $"&mode=updinfo&song={Uri.EscapeDataString(payload)}"
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                req.Headers.UserAgent.ParseAdd("ShoutcastDSP (Mozilla Compatible)")
                Dim resp = Await _http.SendAsync(req)
                Return If(resp.IsSuccessStatusCode, "SEND OK", "Shoutcast error: " & CInt(resp.StatusCode).ToString())
            End Using
        End Function

        ' ---- Mode 2: RDS (TCP socket) --------------------------------------
        Private Async Function SendRdsAsync(s As AppSettings, text As String) As Task(Of String)
            ' Suppress identical consecutive strings, like the original SendRDS.
            If text = _oldString Then Return "Same string"
            _oldString = text

            ' A string of only spaces is treated as blank.
            If text.Trim().Length = 0 Then Return "Blank string"

            Select Case s.RdsEncoder
                Case RdsType.Deva
                    Using client As New TcpClient()
                        Await client.ConnectAsync(s.RdsAddress, s.RdsPort)
                        Using stream = client.GetStream()
                            Await WriteLineAsync(stream, s.RdsPass)
                            If s.RdsRt Then Await WriteLineAsync(stream, "TEXT=" & text)
                            If s.RdsPs Then Await WriteLineAsync(stream, "DPS=" & text)
                        End Using
                    End Using
                    Return "Info sent (Deva)"

                Case RdsType.Orban
                    Using client As New TcpClient()
                        Await client.ConnectAsync(s.RdsAddress, s.RdsPort)
                        Using stream = client.GetStream()
                            If s.RdsRt Then Await WriteLineAsync(stream, "RT=" & text)
                        End Using
                    End Using
                    Return "Info sent (Orban)"

                Case RdsType.Database
                    Return Await SendRdsDatabaseAsync(s, text)

                Case Else
                    Return "Unknown RDS type"
            End Select
        End Function

        ''' <summary>
        ''' Mirrors SendRDS case 3 / InitDB: inserts the current text into the MySQL/MariaDB
        ''' "Messages" table (columns TimeStamp, Editions, TxtMessage). The original used a
        ''' WinDev HFSQL "WebMaster" database; here it is a MySQL schema of the same name.
        ''' </summary>
        Private Shared Function BuildRdsConnectionString(s As AppSettings) As String
            Return New MySqlConnectionStringBuilder With {
                .Server = s.RdsAddress,
                .Port = CUInt(Math.Max(s.RdsPort, 0)),
                .UserID = s.RdsUser,
                .Password = s.RdsPass,
                .Database = If(String.IsNullOrWhiteSpace(s.RdsDatabase), "WebMaster", s.RdsDatabase),
                .ConnectionTimeout = 10UI
            }.ConnectionString
        End Function

        ''' <summary>
        ''' Lightweight connectivity check for the MySQL/MariaDB "Database" encoder: opens a
        ''' connection and runs SELECT 1. Returns "OK" on success or "Error: ..." otherwise.
        ''' </summary>
        Public Shared Async Function TestDatabaseAsync(s As AppSettings) As Task(Of String)
            Try
                Using conn As New MySqlConnection(BuildRdsConnectionString(s))
                    Await conn.OpenAsync()
                    Using cmd = conn.CreateCommand()
                        cmd.CommandText = "SELECT 1"
                        Await cmd.ExecuteScalarAsync()
                    End Using
                End Using
                Return "OK"
            Catch ex As Exception
                Return "Error: " & ex.Message
            End Try
        End Function

        Private Shared Async Function SendRdsDatabaseAsync(s As AppSettings, text As String) As Task(Of String)
            Using conn As New MySqlConnection(BuildRdsConnectionString(s))
                Await conn.OpenAsync()
                Using cmd = conn.CreateCommand()
                    cmd.CommandText = "INSERT INTO Messages (TimeStamp, Editions, TxtMessage) VALUES (@ts, @ed, @txt)"
                    cmd.Parameters.AddWithValue("@ts", DateTime.Now)
                    cmd.Parameters.AddWithValue("@ed", 1)
                    cmd.Parameters.AddWithValue("@txt", text)
                    Await cmd.ExecuteNonQueryAsync()
                End Using
            End Using
            Return "Info sent (Database)"
        End Function

        Private Shared Async Function WriteLineAsync(stream As NetworkStream, line As String) As Task
            Dim bytes = Encoding.UTF8.GetBytes(line & vbCr)
            Await stream.WriteAsync(bytes, 0, bytes.Length)
            Await Task.Delay(50) ' mirrors the Multitask(50) pauses in the original
        End Function

        ' ---- Mode 4: RadioPlayer -------------------------------------------
        Private Async Function SendRadioPlayerAsync(s As AppSettings, artist As String, title As String) As Task(Of String)
            Dim key = If(artist, "") & "-" & If(title, "")
            If key = _oldString Then Return "Same string"
            _oldString = key

            If String.IsNullOrEmpty(title) Then title = "   "
            Dim query = $"rpuid={Uri.EscapeDataString(s.RpUid)}&title={Uri.EscapeDataString(title)}&artist={Uri.EscapeDataString(artist)}"
            Dim url = s.RpIngest & "?" & query
            Using req As New HttpRequestMessage(HttpMethod.Post, url)
                AddBasicAuth(req, s.RpUser, s.RpApiKey)
                Dim resp = Await _http.SendAsync(req)
                Return If(resp.IsSuccessStatusCode, "SEND OK", "RadioPlayer error: " & CInt(resp.StatusCode).ToString())
            End Using
        End Function

        ' ---- Mode 5: Website (write file + FTP) ----------------------------
        Private Async Function SendWebsiteAsync(s As AppSettings, info As String) As Task(Of String)
            Dim song = If(s.WebPrefix.Length > 0, s.WebPrefix & info, info)
            Dim webDir = System.IO.Path.Combine(AppContext.BaseDirectory, "WEB")
            Directory.CreateDirectory(webDir)
            Dim localPath = System.IO.Path.Combine(webDir, s.WebFile)
            File.WriteAllText(localPath, song, New UTF8Encoding(False))
            Return Await StartFtpAsync(s, localPath)
        End Function

        Private Async Function StartFtpAsync(s As AppSettings, localPath As String) As Task(Of String)
            If String.IsNullOrEmpty(s.FtpAddress) Then Return "File written (no FTP configured)"
            Dim fileName = System.IO.Path.GetFileName(localPath)
            Dim remote = s.FtpRemote.TrimEnd("/"c) & "/" & fileName
            Dim host = s.FtpAddress
            If Not host.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) Then host = "ftp://" & host
            Dim uri = $"{host}:{s.FtpPort}{If(remote.StartsWith("/"), remote, "/" & remote)}"

#Disable Warning SYSLIB0014 ' FtpWebRequest is obsolete but remains the simplest built-in FTP client.
            Dim request = CType(WebRequest.Create(uri), FtpWebRequest)
#Enable Warning SYSLIB0014
            request.Method = WebRequestMethods.Ftp.UploadFile
            request.Credentials = New NetworkCredential(s.FtpUser, s.FtpPass)
            request.UseBinary = True
            request.KeepAlive = False

            Dim data = File.ReadAllBytes(localPath)
            Using rs = Await request.GetRequestStreamAsync()
                Await rs.WriteAsync(data, 0, data.Length)
            End Using
            Using resp = CType(Await request.GetResponseAsync(), FtpWebResponse)
                Return "File sent to FTP server (" & resp.StatusDescription.Trim() & ")"
            End Using
        End Function

        ' ---- Mode 6: BUTT (write text file) --------------------------------
        Private Function SendButt(s As AppSettings, info As String) As String
            If String.IsNullOrEmpty(s.ButtFile) Then Return "No BUTT file configured"
            File.WriteAllText(s.ButtFile, info, New UTF8Encoding(False))
            Return "SEND OK"
        End Function

        ' ---- Mode 7: DTS ---------------------------------------------------
        Private Async Function SendDtsAsync(s As AppSettings, track As TrackInfo) As Task(Of String)
            ' Compose the request the way DataOntvangst3 case 7 / Send_DTS does, but
            ' URL-encode the individual query VALUES (the correct, working behaviour).
            Dim url = s.DtsUrl & "?" &
                      "format=" & Uri.EscapeDataString(s.DtsFormat) &
                      "&key=" & Uri.EscapeDataString(s.DtsKey) &
                      "&cid=" & Uri.EscapeDataString(s.DtsUid) &
                      "&title=" & Uri.EscapeDataString(If(track.Title, "")) &
                      "&artist=" & Uri.EscapeDataString(If(track.Artist, "")) &
                      "&duration=" & Uri.EscapeDataString(If(track.Duration, ""))
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                Dim resp = Await _http.SendAsync(req)
                Return If(resp.IsSuccessStatusCode, "SEND OK", "SEND ERROR")
            End Using
        End Function

        Private Shared Sub AddBasicAuth(req As HttpRequestMessage, user As String, pass As String)
            If String.IsNullOrEmpty(user) AndAlso String.IsNullOrEmpty(pass) Then Return
            Dim token = Convert.ToBase64String(Encoding.UTF8.GetBytes(user & ":" & pass))
            req.Headers.Authorization = New AuthenticationHeaderValue("Basic", token)
        End Sub

    End Class

End Namespace
