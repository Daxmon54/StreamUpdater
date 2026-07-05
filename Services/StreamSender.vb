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
        Public Property IsError As Boolean = False
        ''' <summary>Full diagnostic text, populated only when debug mode is on.</summary>
        Public Property Detail As String = ""
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
    ''' so the UI thread never blocks. When AppSettings.DebugMode is on, failures carry a
    ''' detailed diagnostic (HTTP status + response body, or the full exception) in Detail.
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

            Dim outcome = Await DispatchAsync(settings, liveText, track)
            outcome.Info = liveText
            Return outcome
        End Function

        ''' <summary>Sends a plain line (default text / standard text) – mirrors SendText.</summary>
        Public Async Function SendTextAsync(settings As AppSettings, text As String) As Task(Of SendResult)
            Dim outcome = Await DispatchAsync(settings, text, New TrackInfo() With {.Artist = text, .Title = "", .InfoType = 2})
            outcome.Info = text
            Return outcome
        End Function

        Private Async Function DispatchAsync(settings As AppSettings, liveText As String, track As TrackInfo) As Task(Of SendResult)
            If String.IsNullOrEmpty(liveText) Then Return New SendResult("", "No information")

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
                        Return New SendResult("", "Unknown mode")
                End Select
            Catch ex As Exception
                ' Connection refused / DNS / timeout / socket / FTP / DB errors land here.
                Dim detail = If(settings.DebugMode, ExceptionDetail(ex), "")
                Return New SendResult("", "Error: " & ex.Message) With {.IsError = True, .Detail = detail}
            End Try
        End Function

        ' ---- Shared HTTP send with optional detailed diagnostics -----------
        Private Shared Async Function HttpSend(req As HttpRequestMessage, okStatus As String,
                                               errLabel As String, debug As Boolean) As Task(Of SendResult)
            Using resp = Await _http.SendAsync(req)
                If resp.IsSuccessStatusCode Then Return New SendResult("", okStatus)

                Dim detail = ""
                If debug Then
                    Dim body As String = ""
                    Try
                        body = Await resp.Content.ReadAsStringAsync()
                    Catch
                    End Try
                    detail = "Request : " & req.Method.ToString() & " " & req.RequestUri.ToString() & Environment.NewLine &
                             "Response: HTTP " & CInt(resp.StatusCode) & " " & resp.ReasonPhrase & Environment.NewLine &
                             "Body    :" & Environment.NewLine & Truncate(body, 3000)
                End If
                Return New SendResult("", errLabel & ": HTTP " & CInt(resp.StatusCode)) With {
                    .IsError = True, .Detail = detail}
            End Using
        End Function

        ' ---- Mode 1: Icecast ------------------------------------------------
        Private Async Function SendIcecastAsync(s As AppSettings, song As String) As Task(Of SendResult)
            Dim url = $"http://{s.StreamAddress}:{s.StreamPort}/admin/metadata?mount={Uri.EscapeDataString(s.StreamMount)}" &
                      $"&mode=updinfo&charset=UTF-8&song={Uri.EscapeDataString(song)}"
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                AddBasicAuth(req, s.StreamUser, s.StreamPass)
                Return Await HttpSend(req, "SEND OK", "Icecast error", s.DebugMode)
            End Using
        End Function

        ' ---- Mode 3: Shoutcast ---------------------------------------------
        Private Async Function SendShoutcastAsync(s As AppSettings, song As String) As Task(Of SendResult)
            Dim payload = song.Replace("&", "%26")
            Dim url = $"http://{s.StreamAddress}:{s.StreamPort}/admin.cgi?pass={Uri.EscapeDataString(s.StreamPass)}" &
                      $"&mode=updinfo&song={Uri.EscapeDataString(payload)}"
            Using req As New HttpRequestMessage(HttpMethod.Get, url)
                ' SHOUTcast v1 (DNAS 1.x) speaks old HTTP and closes the connection right after
                ' applying the update; HTTP/1.0 + Connection:close makes that a normal end-of-response.
                req.Version = HttpVersion.Version10
                req.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower
                req.Headers.ConnectionClose = True
                req.Headers.UserAgent.ParseAdd("ShoutcastDSP (Mozilla Compatible)")
                Try
                    Return Await HttpSend(req, "SEND OK", "Shoutcast error", s.DebugMode)
                Catch ex As Exception When IsPrematureEnd(ex)
                    ' The server dropped the connection after receiving the request. For Shoutcast
                    ' updinfo the metadata is applied before the drop, so treat it as sent (as the
                    ' original WinDev app did by ignoring the response).
                    Return New SendResult("", "SEND OK (verbinding door server gesloten)")
                End Try
            End Using
        End Function

        ''' <summary>True if the exception chain is a "response ended prematurely" (server closed early).</summary>
        Private Shared Function IsPrematureEnd(ex As Exception) As Boolean
            Dim cur = ex
            Do While cur IsNot Nothing
                Dim io = TryCast(cur, HttpIOException)
                If io IsNot Nothing AndAlso io.HttpRequestError = HttpRequestError.ResponseEnded Then Return True
                If cur.Message.IndexOf("response ended prematurely", StringComparison.OrdinalIgnoreCase) >= 0 Then Return True
                cur = cur.InnerException
            Loop
            Return False
        End Function

        ' ---- Mode 2: RDS (TCP socket) --------------------------------------
        Private Async Function SendRdsAsync(s As AppSettings, text As String) As Task(Of SendResult)
            ' Suppress identical consecutive strings, like the original SendRDS.
            If text = _oldString Then Return New SendResult("", "Same string")
            _oldString = text

            ' A string of only spaces is treated as blank.
            If text.Trim().Length = 0 Then Return New SendResult("", "Blank string")

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
                    Return New SendResult("", "Info sent (Deva)")

                Case RdsType.Orban
                    Using client As New TcpClient()
                        Await client.ConnectAsync(s.RdsAddress, s.RdsPort)
                        Using stream = client.GetStream()
                            If s.RdsRt Then Await WriteLineAsync(stream, "RT=" & text)
                        End Using
                    End Using
                    Return New SendResult("", "Info sent (Orban)")

                Case RdsType.Database
                    Return Await SendRdsDatabaseAsync(s, text)

                Case Else
                    Return New SendResult("", "Unknown RDS type")
            End Select
        End Function

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
        ''' connection and runs SELECT 1. Returns "OK", or "Error: ..." (full detail in debug mode).
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
                Return "Error: " & If(s.DebugMode, ExceptionDetail(ex), ex.Message)
            End Try
        End Function

        ''' <summary>
        ''' Mirrors SendRDS case 3 / InitDB: inserts the current text into the MySQL/MariaDB
        ''' "Messages" table (columns TimeStamp, Editions, TxtMessage).
        ''' </summary>
        Private Shared Async Function SendRdsDatabaseAsync(s As AppSettings, text As String) As Task(Of SendResult)
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
            Return New SendResult("", "Info sent (Database)")
        End Function

        Private Shared Async Function WriteLineAsync(stream As NetworkStream, line As String) As Task
            Dim bytes = Encoding.UTF8.GetBytes(line & vbCr)
            Await stream.WriteAsync(bytes, 0, bytes.Length)
            Await Task.Delay(50) ' mirrors the Multitask(50) pauses in the original
        End Function

        ' ---- Mode 4: RadioPlayer -------------------------------------------
        Private Async Function SendRadioPlayerAsync(s As AppSettings, artist As String, title As String) As Task(Of SendResult)
            Dim key = If(artist, "") & "-" & If(title, "")
            If key = _oldString Then Return New SendResult("", "Same string")
            _oldString = key

            If String.IsNullOrEmpty(title) Then title = "   "
            Dim query = $"rpuid={Uri.EscapeDataString(s.RpUid)}&title={Uri.EscapeDataString(title)}&artist={Uri.EscapeDataString(artist)}"
            Dim url = s.RpIngest & "?" & query
            Using req As New HttpRequestMessage(HttpMethod.Post, url)
                AddBasicAuth(req, s.RpUser, s.RpApiKey)
                Return Await HttpSend(req, "SEND OK", "RadioPlayer error", s.DebugMode)
            End Using
        End Function

        ' ---- Mode 5: Website (write file + FTP) ----------------------------
        Private Async Function SendWebsiteAsync(s As AppSettings, info As String) As Task(Of SendResult)
            Dim song = If(s.WebPrefix.Length > 0, s.WebPrefix & info, info)
            Dim webDir = System.IO.Path.Combine(AppContext.BaseDirectory, "WEB")
            Directory.CreateDirectory(webDir)
            Dim localPath = System.IO.Path.Combine(webDir, s.WebFile)
            File.WriteAllText(localPath, song, New UTF8Encoding(False))
            Return Await StartFtpAsync(s, localPath)
        End Function

        Private Async Function StartFtpAsync(s As AppSettings, localPath As String) As Task(Of SendResult)
            If String.IsNullOrEmpty(s.FtpAddress) Then Return New SendResult("", "File written (no FTP configured)")
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
                Return New SendResult("", "File sent to FTP server (" & resp.StatusDescription.Trim() & ")")
            End Using
        End Function

        ' ---- Mode 6: BUTT (write text file) --------------------------------
        Private Function SendButt(s As AppSettings, info As String) As SendResult
            If String.IsNullOrEmpty(s.ButtFile) Then Return New SendResult("", "No BUTT file configured")
            File.WriteAllText(s.ButtFile, info, New UTF8Encoding(False))
            Return New SendResult("", "SEND OK")
        End Function

        ' ---- Mode 7: DTS ---------------------------------------------------
        Private Async Function SendDtsAsync(s As AppSettings, track As TrackInfo) As Task(Of SendResult)
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
                Return Await HttpSend(req, "SEND OK", "SEND ERROR", s.DebugMode)
            End Using
        End Function

        Private Shared Sub AddBasicAuth(req As HttpRequestMessage, user As String, pass As String)
            If String.IsNullOrEmpty(user) AndAlso String.IsNullOrEmpty(pass) Then Return
            Dim token = Convert.ToBase64String(Encoding.UTF8.GetBytes(user & ":" & pass))
            req.Headers.Authorization = New AuthenticationHeaderValue("Basic", token)
        End Sub

        ''' <summary>Full exception text (type, message, inner exceptions and stack) for debug mode.</summary>
        Private Shared Function ExceptionDetail(ex As Exception) As String
            Dim sb As New StringBuilder()
            Dim cur = ex
            Do While cur IsNot Nothing
                sb.AppendLine(cur.GetType().FullName & ": " & cur.Message)
                cur = cur.InnerException
                If cur IsNot Nothing Then sb.AppendLine("  --> inner:")
            Loop
            sb.AppendLine()
            sb.AppendLine(ex.ToString())
            Return Truncate(sb.ToString(), 4000)
        End Function

        Private Shared Function Truncate(s As String, max As Integer) As String
            If s Is Nothing Then Return ""
            If s.Length <= max Then Return s
            Return s.Substring(0, max) & "… (" & (s.Length - max) & " more characters)"
        End Function

    End Class

End Namespace
