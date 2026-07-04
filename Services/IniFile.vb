Imports System.Collections.Generic
Imports System.IO
Imports System.Text

Namespace Services

    ''' <summary>
    ''' Minimal INI reader/writer that mirrors WinDev's INIRead / INIWrite semantics
    ''' (case-insensitive sections and keys, last value wins, sections preserved on write).
    ''' Implemented in managed code so it works identically on any .NET 8 host.
    ''' </summary>
    Public Class IniFile

        Private ReadOnly _path As String
        ' section -> (key -> value), all keys compared case-insensitively.
        Private ReadOnly _data As New Dictionary(Of String, Dictionary(Of String, String))(StringComparer.OrdinalIgnoreCase)
        ' Preserve original section ordering for tidy output.
        Private ReadOnly _order As New List(Of String)()

        Public Sub New(path As String)
            _path = path
            Load()
        End Sub

        Public ReadOnly Property Path As String
            Get
                Return _path
            End Get
        End Property

        Private Sub Load()
            _data.Clear()
            _order.Clear()
            If Not File.Exists(_path) Then Return

            Dim current As String = ""
            EnsureSection(current)

            For Each raw In File.ReadAllLines(_path, Encoding.UTF8)
                Dim line = raw.Trim()
                If line.Length = 0 OrElse line.StartsWith(";") OrElse line.StartsWith("#") Then Continue For

                If line.StartsWith("[") AndAlso line.EndsWith("]") Then
                    current = line.Substring(1, line.Length - 2).Trim()
                    EnsureSection(current)
                Else
                    Dim eq = line.IndexOf("="c)
                    If eq < 0 Then Continue For
                    Dim key = line.Substring(0, eq).Trim()
                    Dim value = line.Substring(eq + 1).Trim()
                    EnsureSection(current)
                    _data(current)(key) = value
                End If
            Next
        End Sub

        Private Sub EnsureSection(section As String)
            If Not _data.ContainsKey(section) Then
                _data(section) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                _order.Add(section)
            End If
        End Sub

        ''' <summary>Reads a string value, returning <paramref name="fallback"/> when absent.</summary>
        Public Function ReadString(section As String, key As String, fallback As String) As String
            Dim s As Dictionary(Of String, String) = Nothing
            If _data.TryGetValue(section, s) Then
                Dim v As String = Nothing
                If s.TryGetValue(key, v) Then Return v
            End If
            Return fallback
        End Function

        ''' <summary>Reads an integer value; non-numeric or missing entries yield <paramref name="fallback"/>.</summary>
        Public Function ReadInt(section As String, key As String, fallback As Integer) As Integer
            Dim v = ReadString(section, key, Nothing)
            Dim n As Integer
            If v IsNot Nothing AndAlso Integer.TryParse(v.Trim(), n) Then Return n
            Return fallback
        End Function

        ''' <summary>Reads a boolean stored as "1"/"0" (WinDev convention).</summary>
        Public Function ReadBool(section As String, key As String, fallback As Boolean) As Boolean
            Dim v = ReadString(section, key, Nothing)
            If v Is Nothing Then Return fallback
            Return v.Trim() = "1"
        End Function

        Public Sub Write(section As String, key As String, value As String)
            EnsureSection(section)
            _data(section)(key) = If(value, "")
        End Sub

        Public Sub WriteBool(section As String, key As String, value As Boolean)
            Write(section, key, If(value, "1", "0"))
        End Sub

        ''' <summary>Returns every key in a section (empty when the section does not exist).</summary>
        Public Function Keys(section As String) As IEnumerable(Of String)
            Dim s As Dictionary(Of String, String) = Nothing
            If _data.TryGetValue(section, s) Then Return New List(Of String)(s.Keys)
            Return New List(Of String)()
        End Function

        Public Sub Save()
            Dim sb As New StringBuilder()
            For Each section In _order
                Dim s = _data(section)
                If s.Count = 0 AndAlso section.Length = 0 Then Continue For
                If section.Length > 0 Then sb.Append("["c).Append(section).Append("]"c).AppendLine()
                For Each kv In s
                    sb.Append(kv.Key).Append("="c).Append(kv.Value).AppendLine()
                Next
            Next
            Dim dir = System.IO.Path.GetDirectoryName(_path)
            If Not String.IsNullOrEmpty(dir) AndAlso Not Directory.Exists(dir) Then Directory.CreateDirectory(dir)
            File.WriteAllText(_path, sb.ToString(), New UTF8Encoding(False))
        End Sub

    End Class

End Namespace
