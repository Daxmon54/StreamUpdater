Imports System.IO
Imports System.Text

Namespace Services

    ''' <summary>
    ''' Simple daily file logger. Writes to &lt;app folder&gt;\LOG\yyyy-MM-dd.log (one file per
    ''' day), creating the LOG folder on demand. Thread-safe and never throws — a logging
    ''' failure must not interrupt sending.
    ''' </summary>
    Public Module Logger

        Private ReadOnly _lock As New Object()

        ''' <summary>The LOG folder, inside the folder where the program runs.</summary>
        Public ReadOnly Property LogFolder As String
            Get
                Return Path.Combine(AppContext.BaseDirectory, "LOG")
            End Get
        End Property

        Private Function TodayFile() As String
            Return Path.Combine(LogFolder, Date.Now.ToString("yyyy-MM-dd") & ".log")
        End Function

        ''' <summary>Logs an error line, optionally followed by an indented detail block.</summary>
        Public Sub [Error](message As String, Optional detail As String = Nothing)
            Write("ERROR", message, detail)
        End Sub

        Public Sub Info(message As String)
            Write("INFO", message, Nothing)
        End Sub

        Public Sub Write(level As String, message As String, detail As String)
            Try
                SyncLock _lock
                    Directory.CreateDirectory(LogFolder)
                    Dim sb As New StringBuilder()
                    sb.Append("["c).Append(Date.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("] ")
                    sb.Append(level).Append(": ").AppendLine(If(message, ""))
                    If Not String.IsNullOrEmpty(detail) Then
                        For Each line In detail.Replace(vbCrLf, vbLf).Split(ChrW(10))
                            sb.Append("    ").AppendLine(line)
                        Next
                    End If
                    File.AppendAllText(TodayFile(), sb.ToString(), New UTF8Encoding(False))
                End SyncLock
            Catch
                ' Logging must never crash or interrupt the application.
            End Try
        End Sub

    End Module

End Namespace
