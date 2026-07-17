# StreamUpdater (VB.NET / WPF)

A conversion of the original WinDev 2026 **StreamUpdater** utility to **Visual Basic .NET on WPF (.NET 8)**.
It watches a JSON "now-playing" file and pushes the current track to a streaming/metadata target,
with a **light and dark theme** option.

## Requirements

- .NET 8 SDK (build) / .NET 8 Desktop Runtime (run) — `net8.0-windows`
- Windows (uses WPF + a WinForms `NotifyIcon` for the system tray)

## Build & run

```sh
dotnet build            # or open StreamUpdater.sln in Visual Studio 2022
dotnet run --project StreamUpdater.vbproj
```

The compiled `StreamUpdater.exe` reads its configuration from `StreamUpdater.ini`
in the same folder as the executable (the `fCurrentDir` equivalent of the original).

## How it maps to the original

| Original (WinDev) | This project |
|---|---|
| `WIN_Main` | `Windows/MainWindow.xaml(.vb)` — status, timeout progress bar, tray, watch pipeline |
| `WIN_Settings` | `Windows/SettingsWindow.xaml(.vb)` — all 7 modes, tabbed |
| `WIN_Display` | `Windows/DisplayWindow.xaml(.vb)` — default text / time-out / source flags |
| `WIN_programs` | `Windows/HourTextWindow` (Programs.ini, prefix `Prog_Day`) |
| `WIN_Standaard_Tekst` | `Windows/HourTextWindow` (Standard.ini, prefix `StdTXT_Day`) |
| `WIN_About` | `Windows/AboutWindow.xaml(.vb)` |
| `LeesIni()` / settings save | `Models/AppSettings.vb` (`Load` / `Save`) |
| `fTrackFile` / `TrackingCallback` | `Services/TrackWatcher.vb` (`FileSystemWatcher`) |
| `DataOntvangst3` / `Send_DTS` / `SendRP` / `SendRDS` / `StartFTP` | `Services/StreamSender.vb` |
| `INIRead` / `INIWrite` | `Services/IniFile.vb` |

### Output modes (`Mode` in `[Common]`, values 1..7)

1. **Icecast** – HTTP `admin/metadata?mode=updinfo`
2. **RDS** – TCP socket (Deva / Orban); *Database sub-type is not ported, see below*
3. **Shoutcast** – HTTP `admin.cgi?mode=updinfo`
4. **RadioPlayer** – HTTP POST to the ingest URL
5. **Website** – writes the file locally then uploads via FTP
6. **BUTT** – writes a local text file
7. **DTS** – HTTP GET `?format=&key=&cid=&title=&artist=&duration=`

## Testing a target

The Settings window has a **Test verzenden** button. It sends a sample track
(`Test Artist – Test Title`) through the currently-selected mode using the values on screen
(without saving), and shows the result — handy for validating an address/credentials before committing.

The **RDS** tab additionally has a **Test DB-verbinding** button that pings the MySQL/MariaDB server
(`SELECT 1`) using the on-screen RDS values, so you can validate database connectivity on its own,
separate from a full send.

## Character encoding (accented / Unicode text)

Accented and Unicode characters (é, à, è, ë, ç, €, …) are preserved end to end:

- **Reading** the watch file honours a UTF-8/UTF-16 BOM, otherwise tries strict UTF-8 and
  falls back to Windows-1252 (ANSI) for legacy files — so both modern UTF-8 and older
  ANSI producers are read correctly.
- **Sending** uses UTF-8: HTTP targets (Icecast/Shoutcast/RadioPlayer/DTS) percent-encode the
  values as UTF-8 (Icecast also sends `charset=UTF-8`); the Website/BUTT file outputs are written
  as UTF-8. `&` is encoded exactly once (`%26`).

## Error logging & debug mode

Errors are **never shown as a pop-up during normal operation** — every failed send is written to a
daily log file:

```
<program folder>\LOG\yyyy-MM-dd.log
```

Each entry is `[timestamp] ERROR: <mode>: <status>`. The `LOG` folder is created automatically.

The **Info** tab has a **Debugmodus** checkbox (persisted to `[App] Debug`). It controls how much is
logged: with it **off**, the concise status is logged; with it **on**, the full diagnostic is also
logged — for HTTP targets (Icecast/Shoutcast/RadioPlayer/DTS) the request URL, HTTP status and
response body; for sockets/FTP/database the full exception.

The manual **Test verzenden** / **Test DB-verbinding** buttons still show their result (and, in debug
mode, the detail) in a dialog, since those are direct feedback to a button press.

## Theme

Light/dark is chosen from the **Weergave** menu and persisted to `[App] DarkTheme` in the INI.
Palettes live in `Themes/Light.xaml` and `Themes/Dark.xaml`; `Themes/Controls.xaml` holds the
shared control styles. Switching is live (`Services/ThemeManager.vb`).

## Known deviations from the original

- **RDS "Database" encoder (type 3)** originally used WinDev's HFSQL engine (`HDescribeConnection` /
  `HAdd` on the *WebMaster* database). This port targets **MySQL/MariaDB** instead (via the
  `MySqlConnector` package): it `INSERT`s into a `Messages` table with columns
  **`TimeStamp` (DATETIME), `Editions` (INT), `TxtMessage` (VARCHAR/TEXT)** — the schema taken from
  the original project. Configure server/port/user/password on the **RDS** settings tab plus the new
  **Databasenaam** field (default `WebMaster`). The connection + insert code is verified; a live
  insert needs your actual database with that table. Suggested table:

  ```sql
  CREATE TABLE Messages (
    MessagesID INT AUTO_INCREMENT PRIMARY KEY,
    TimeStamp  DATETIME,
    Editions   INT,
    TxtMessage VARCHAR(255)
  );
  ```

  Deva and Orban RDS (TCP sockets) work as before.
- **DTS request encoding.** `Send_DTS` in the original URL-encoded the *entire* URL; this port
  URL-encodes the individual query values instead (the correct, working behaviour — verified against
  a local listener).
- **Passwords** are shown/stored as plain text in the settings UI and INI, matching the original
  INI format (the file already stored them unencrypted).
- FTP upload uses the built-in `FtpWebRequest` (marked obsolete in .NET but still functional).

## Watch file format

```json
{ "artist": "CELINE DION", "title": "POUR QUE TU M'AIMES ENCORE", "year": "1995", "duration": "04:33", "infoType": 0 }
```

`infoType` 0 or 1 = song (artist + title, per `InfoOrder`); any other value = single-line text.
