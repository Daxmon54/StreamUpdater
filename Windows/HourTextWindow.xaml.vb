Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Controls
Imports StreamUpdater.Services

''' <summary>
''' Generic weekday-by-hour text editor, reused for WIN_programs (Programs.ini) and
''' WIN_Standaard_Tekst (Standard.ini). Edits 7 days × 24 hours and saves the lot.
''' NOTE: identifiers avoid the names Day/Hour, which collide with VB's intrinsic
''' Microsoft.VisualBasic date functions.
''' </summary>
Partial Class HourTextWindow
    Inherits Window

    Private ReadOnly _store As HourTextStore
    ' Backing store [day 1..7][hour 0..23]; index 0 unused so days map directly.
    Private ReadOnly _values(7, 23) As String
    Private ReadOnly _rows As New ObservableCollection(Of HourRow)()
    Private _currentDay As Integer = 1
    Private _suppress As Boolean

    Private Shared ReadOnly DayNames() As String =
        {"Maandag", "Dinsdag", "Woensdag", "Donderdag", "Vrijdag", "Zaterdag", "Zondag"}

    Public Sub New(title As String, fileName As String, sectionPrefix As String)
        InitializeComponent()
        NativeTheme.Track(Me)
        Me.Title = title
        _store = New HourTextStore(fileName, sectionPrefix)

        For dayIndex = 1 To 7
            For hourIndex = 0 To 23
                _values(dayIndex, hourIndex) = _store.GetText(dayIndex, hourIndex)
            Next
        Next

        _suppress = True
        For i = 0 To 6
            CboDay.Items.Add(New ComboBoxItem With {.Content = DayNames(i), .Tag = i + 1})
        Next
        CboDay.SelectedIndex = 0
        _suppress = False

        Grid.ItemsSource = _rows
        LoadDay(1)
    End Sub

    Private Sub LoadDay(dayIndex As Integer)
        CommitGrid() ' persist edits of the previous day into the backing array
        _currentDay = dayIndex
        _rows.Clear()
        For hourIndex = 0 To 23
            _rows.Add(New HourRow(hourIndex, _values(dayIndex, hourIndex)))
        Next
    End Sub

    ''' <summary>Copies the currently displayed rows back into the backing array.</summary>
    Private Sub CommitGrid()
        Grid.CommitEdit(DataGridEditingUnit.Row, True)
        For Each r In _rows
            _values(_currentDay, r.HourIndex) = r.Text
        Next
    End Sub

    Private Sub OnDayChanged(sender As Object, e As SelectionChangedEventArgs)
        If _suppress Then Return
        Dim item = TryCast(CboDay.SelectedItem, ComboBoxItem)
        If item IsNot Nothing Then LoadDay(CInt(item.Tag))
    End Sub

    Private Sub OnSave(sender As Object, e As RoutedEventArgs)
        CommitGrid()
        For dayIndex = 1 To 7
            For hourIndex = 0 To 23
                _store.SetText(dayIndex, hourIndex, If(_values(dayIndex, hourIndex), ""))
            Next
        Next
        _store.Save()
        MessageBox.Show("Teksten bewaard.", Title, MessageBoxButton.OK, MessageBoxImage.Information)
    End Sub

    Private Sub OnClose(sender As Object, e As RoutedEventArgs)
        Close()
    End Sub

    ''' <summary>One editable hour row.</summary>
    Public Class HourRow
        Implements INotifyPropertyChanged

        Private _text As String
        Public ReadOnly Property HourIndex As Integer

        Public Sub New(hourIndex As Integer, text As String)
            Me.HourIndex = hourIndex
            _text = text
        End Sub

        Public ReadOnly Property HourLabel As String
            Get
                Return HourIndex.ToString("00") & ":00"
            End Get
        End Property

        Public Property Text As String
            Get
                Return _text
            End Get
            Set(value As String)
                _text = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Text)))
            End Set
        End Property

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    End Class

End Class
