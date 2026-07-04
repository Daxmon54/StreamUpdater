Imports System.Text.Json.Serialization

Namespace Models

    ''' <summary>
    ''' Mirrors the JSON payload the watched file contains in the original app
    ''' (JZ.artist / JZ.title / JZ.year / JZ.duration / JZ.infoType).
    ''' InfoType 0 or 1 = song (artist + title); anything else = single-line text.
    ''' </summary>
    Public Class TrackInfo

        <JsonPropertyName("artist")>
        Public Property Artist As String = ""

        <JsonPropertyName("title")>
        Public Property Title As String = ""

        <JsonPropertyName("year")>
        Public Property Year As String = ""

        <JsonPropertyName("duration")>
        Public Property Duration As String = ""

        <JsonPropertyName("infoType")>
        Public Property InfoType As Integer = 0

    End Class

End Namespace
