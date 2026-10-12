# Third-party notices

Duejoro is licensed under the GNU Affero General Public License 3.0 or later. This file lists the other software it uses, and the license of each. A component is added here **before** it is merged (see `LEGAL.md`).

## Shipped inside Duejoro.exe
| Component | Used for | License |
|---|---|---|
| .NET 10 runtime, WPF and Windows Forms (Microsoft) | The program's runtime, its window and the notification-area icon | MIT |

Duejoro uses no other libraries: reading and writing the records, spreadsheets (CSV) and calendar files, and checking update signatures are done with what .NET provides.

## Tests only (not shipped)
| Component | Used for | License |
|---|---|---|
| xUnit | Unit tests | Apache-2.0 |

## Standards implemented
- RFC 4180 (CSV) and the common variants spreadsheets write
- RFC 5545 (iCalendar)

The standards are public specifications; no text from them is reproduced here.
