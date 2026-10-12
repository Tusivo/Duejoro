# Architecture

Three projects, one solution (`Duejoro.sln`), .NET 10.

```
src/Duejoro.Core   net10.0          logic, storage, import/export, updater (no UI)
src/Duejoro.App    net10.0-windows  WPF window; assembly name "Duejoro"
tests/Duejoro.Tests                 xUnit tests for Core and policy checks
```

Core has no third-party packages. The App references Core only.

## Core
| File | Role |
|---|---|
| `Obligation.cs` | The record: name, category, owner, issuer, reference, due date, lead time, repeat, cost, notes, done flag, attachments, history. `DueStatus`: Overdue, DueSoon, Upcoming, NoDate, Done. |
| `DueMath.cs` | Pure date logic: status, days left, human description ("in 12 days"), month/year advance with month-end handling, next due date. |
| `ObligationStore.cs` | Loads and saves `duejoro.json`; atomic write (temp file then move); dated backup before the session's first change (newest 20 kept); damaged file set aside and newest backup restored; in-memory undo/redo; attachment copying. |
| `Exporters.cs` | CSV import/export (tolerant of headers, delimiters and date formats) and iCalendar export (all-day events, alarms, escaping, line folding). |
| `AppPaths.cs` | Chooses the data folder: `Duejoro-Data` beside `Duejoro.exe`, otherwise `%APPDATA%\Duejoro`. |
| `AppSettings.cs` | Theme and window placement. |
| `Updater.cs` | Reads GitHub releases and accepts only those with `Duejoro.exe` and `Duejoro.exe.sig` that verify against the embedded public key (ECDSA P-256). |
| `AppInfo.cs`, `PlainText.cs` | Names/links; small text helpers. |

## App
Code-behind WPF, no MVVM framework.

| File | Role |
|---|---|
| `MainWindow.xaml` / `.xaml.cs` | Menus, toolbar, summary cards, filters, table, details pane, status bar; all item actions. |
| `MainWindow.Theme.cs` | Light/Dark via `ThemeMode`, plus the custom Night palette. |
| `MainWindow.Menu.cs` | About, Check for Updates, help links. |
| `MainWindow.Shortcuts.cs` | Keyboard shortcut list. |
| `EditWindow.cs` | Item editor and the Renew dialog, built in code. |
| `Dialogs.cs`, `Icons.cs` | Small dialog helpers; vector toolbar icons. |
| `StartupBounds.cs` | Opens 1920x1080 centered (or the remembered placement). |

## Data flow
UI action → `ObligationStore` method (records an undo snapshot, changes the list, writes the file atomically) → window refreshes its rows from the store. The window never writes files itself except through the store.

## Signing
`scripts/release-sign.js` makes the key and signs releases. The signed message is `duejoro-release-v1\n<version>\n<sha256>\n`. The public key is embedded from `src/Duejoro.Core/release-key.txt` when that file exists. See [RELEASING.md](RELEASING.md).
