<p align="center"><img src="src/Duejoro.App/Assets/Duejoro.png" alt="The Duejoro icon: a calendar page with a check mark on an orange tile" width="128" height="128"></p>

# Duejoro

**Free, open-source tracker for the renewals, licenses and deadlines your business cannot afford to miss.**

### [Download Duejoro for Windows](https://github.com/Tusivo/Duejoro/releases/latest)

On that page, download **Duejoro.exe** under **Assets** and double-click it. There is no installer. (Early previews are marked *Pre-release*; if the link above says there is no release, open the [releases page](https://github.com/Tusivo/Duejoro/releases) and choose the newest one.)

[Website](https://tusivo.com/apps/Duejoro/) · [Report a problem](https://github.com/Tusivo/Duejoro/issues) · [Support the project](https://buymeacoffee.com/tusivo)

> **Status: v0.1.0-alpha.1, an early preview.** Keep your own copy of anything important, and expect rough edges.

Duejoro is one program file for Windows. There is nothing to install, no server, no account and no subscription. Put `Duejoro.exe` in a folder and run it.

## What it does
Keep one list of everything that expires, renews or falls due: business licenses, permits, insurance policies, certifications, contracts, leases, subscriptions, warranties and filings.

- **See what needs attention.** Summary cards show how many items are overdue, due soon and coming up, with the yearly cost of what is active.
- **Renew in one step.** Mark an item renewed and Duejoro moves it to its next date, counting on from the old one. Weekly, monthly, yearly and custom repeats are supported, with sensible handling of month ends.
- **Per-item lead time.** Each item has its own "remind me this many days ahead".
- **Keep the paperwork with it.** Attach the scan or PDF of a license or policy; Duejoro keeps a copy beside your records.
- **History.** Every item keeps a short log of what was changed and when it was renewed.
- **Undo and redo** for every change, including imports.
- **Import and export.** Bring a list in from a spreadsheet (CSV), send it out as CSV, or export an iCalendar (.ics) file so the dates show up in the calendar you already use.
- **Reminders from the tray.** Turn on View → Stay in the Tray and Remind Me and closing the window keeps Duejoro running quietly; it pops up a notice when something is overdue or due soon. File → Exit quits.
- **Themes.** Light, Dark and Night.

## Where your data lives
Everything is kept in a **`Duejoro-Data`** folder created in the same folder as `Duejoro.exe`:

| Item | Location |
|---|---|
| Records | `Duejoro-Data\duejoro.json` |
| Attached files | `Duejoro-Data\Documents\` |
| Backups (newest 20) | `Duejoro-Data\Backups\` |
| Settings | `Duejoro-Data\settings.json` |

Move or copy the program together with that folder and your list goes with it. If the program's folder cannot be written to (for example under Program Files), Duejoro uses `%APPDATA%\Duejoro` instead. Duejoro does not encrypt records; see [SECURITY.md](SECURITY.md).

## Updates and signatures
**Help → Check for Updates** asks GitHub for the latest release and installs it only if it carries a valid signature made with the project's private key. An unsigned or altered file is refused. See [docs/RELEASING.md](docs/RELEASING.md).

## Building from source
You need the .NET 10 SDK.

```
powershell -ExecutionPolicy Bypass -File scripts\build.ps1
```

This runs the tests and then publishes a single self-contained `dist\Duejoro.exe` with its SHA-256 beside it.

## More
- [Feature map](docs/FEATURE-MAP.md)
- [Architecture](docs/ARCHITECTURE.md)
- [Security](SECURITY.md)
- [Legal posture](LEGAL.md)
- [Third-party notices](THIRD-PARTY-NOTICES.md)

## License
GNU Affero General Public License 3.0 or later. See [LICENSE](LICENSE).
