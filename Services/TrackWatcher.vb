Imports System.IO
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
                    Dim text As String
                    Using fs As New FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)
                        Using reader As New StreamReader(fs)
                            text = reader.ReadToEnd()
                        End Using
                    End Using
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

        Public Sub Dispose() Implements IDisposable.Dispose
            [Stop]()
        End Sub

    End Class

End Namespace
