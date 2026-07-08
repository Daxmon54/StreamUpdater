Imports System.IO
Imports System.Text
Imports System.Text.Json
Imports System.Threading
Imports StreamUpdater.Models

Namespace Services

    ''' <summary>
    ''' Watches the configured JSON file for changes and raises TrackChanged when a new
    ''' payload can be read. Replaces WinDev's fTrackFile / TrackingCallback mechanism.
    ''' Events are marshalled through the supplied SynchronizationContext so handlers can
    ''' touch the UI safely.
    ''' </summary>
    Public Class TrackWatcher
        Implements IDisposable

        Private ReadOnly _sync As SynchronizationContext
        Private _watcher As FileSystemWatcher
        Private _path As String
        Private _lastWrite As DateTime = DateTime.MinValue

        Public Event TrackChanged As EventHandler(Of TrackInfo)
        Public Event WatchError As EventHandler(Of String)

        Public Sub New(sync As SynchronizationContext)
            _sync = If(sync, New SynchronizationContext())
        End Sub

        Public ReadOnly Property IsWatching As Boolean
            Get
                Return _watcher IsNot Nothing
            End Get
        End Property

        ''' <summary>Starts watching <paramref name="filePath"/>. A no-op if the path is blank.</summary>
        Public Sub Start(filePath As String)
            [Stop]()
            If String.IsNullOrWhiteSpace(filePath) Then Return
            ' Resolve relative paths against the application folder (fCurrentDir equivalent).
            If Not Path.IsPathRooted(filePath) Then
                filePath = Path.Combine(AppContext.BaseDirectory, filePath)
            End If
            _path = filePath

            Dim dir = Path.GetDirectoryName(filePath)
            Dim name = Path.GetFileName(filePath)
            If String.IsNullOrEmpty(dir) OrElse Not Directory.Exists(dir) Then
                RaiseEvent WatchError(Me, "Watch folder does not exist: " & If(dir, "(empty)"))
                Return
            End If

            _watcher = New FileSystemWatcher(dir, name) With {
                .NotifyFilter = NotifyFilters.LastWrite Or NotifyFilters.Size Or NotifyFilters.FileName,
                .EnableRaisingEvents = True
            }
            AddHandler _watcher.Changed, AddressOf OnChanged
            AddHandler _watcher.Created, AddressOf OnChanged
            AddHandler _watcher.Renamed, AddressOf OnChanged
        End Sub

        Public Sub [Stop]()
            If _watcher IsNot Nothing Then
                RemoveHandler _watcher.Changed, AddressOf OnChanged
                RemoveHandler _watcher.Created, AddressOf OnChanged
                RemoveHandler _watcher.Renamed, AddressOf OnChanged
                _watcher.EnableRaisingEvents = False
                _watcher.Dispose()
                _watcher = Nothing
            End If
        End Sub

        Private Sub OnChanged(sender As Object, e As FileSystemEventArgs)
            ' FileSystemWatcher fires several times per save; de-bounce on write time.
            Try
                Dim info As New FileInfo(_path)
                If Not info.Exists Then Return
                Dim wt = info.LastWriteTimeUtc
                If (wt - _lastWrite).TotalMilliseconds < 250 Then Return
                _lastWrite = wt
            Catch
                ' ignore transient IO
            End Try

            Dim track = ReadTrack()
            If track Is Nothing Then Return
            _sync.Post(Sub() RaiseEvent TrackChanged(Me, track), Nothing)
        End Sub

        ''' <summary>Reads and parses the current file contents, retrying briefly if locked.</summary>
        Public Function ReadTrack() As TrackInfo
            For attempt = 1 To 5
                Try
                    If Not File.Exists(_path) Then Return Nothing
                    Dim bytes As Byte()
                    Using fs As New FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using ms As New MemoryStream()
                            fs.CopyTo(ms)
                            bytes = ms.ToArray()
                        End Using
                    End Using
                    Dim text = DecodeText(bytes)
                    If String.IsNullOrWhiteSpace(text) Then Return Nothing
                    Dim opts As New JsonSerializerOptions With {.PropertyNameCaseInsensitive = True}
                    Return JsonSerializer.Deserialize(Of TrackInfo)(text, opts)
                Catch ex As IOException
                    Thread.Sleep(40) ' file still being written – retry
                Catch ex As JsonException
                    RaiseEvent WatchError(Me, "Invalid JSON in watch file: " & ex.Message)
                    Return Nothing
                End Try
            Next
            Return Nothing
        End Function

        Private Shared _ansi As Encoding

        ''' <summary>
        ''' Decodes the watch file to text, preserving accented/Unicode characters:
        ''' honours a UTF-8/UTF-16 BOM, otherwise tries strict UTF-8 and falls back to the
        ''' Windows-1252 (ANSI) code page for legacy files (as WinDev wrote them).
        ''' </summary>
        Friend Shared Function DecodeText(bytes As Byte()) As String
            If bytes Is Nothing OrElse bytes.Length = 0 Then Return ""

            If bytes.Length >= 3 AndAlso bytes(0) = &HEF AndAlso bytes(1) = &HBB AndAlso bytes(2) = &HBF Then
                Return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3)
            End If
            If bytes.Length >= 2 AndAlso bytes(0) = &HFF AndAlso bytes(1) = &HFE Then
                Return Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2)
            End If
            If bytes.Length >= 2 AndAlso bytes(0) = &HFE AndAlso bytes(1) = &HFF Then
                Return Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2)
            End If

            ' No BOM: valid UTF-8 wins; otherwise treat as legacy ANSI (Windows-1252).
            Try
                Dim strictUtf8 As New UTF8Encoding(encoderShouldEmitUTF8Identifier:=False, throwOnInvalidBytes:=True)
                Return strictUtf8.GetString(bytes)
            Catch ex As DecoderFallbackException
                Return AnsiEncoding().GetString(bytes)
            End Try
        End Function

        Private Shared Function AnsiEncoding() As Encoding
            If _ansi Is Nothing Then
                Try
                    Encoding.RegisterProvider(CodePagesEncodingProvider.Instance)
                    _ansi = Encoding.GetEncoding(1252)
                Catch
                    _ansi = Encoding.Latin1 ' covers é à è etc. even without the code-page provider
                End Try
            End If
            Return _ansi
        End Function

        Public Sub Dispose() Implements IDisposable.Dispose
            [Stop]()
        End Sub

    End Class

End Namespace
