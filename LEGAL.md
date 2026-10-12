# Legal posture and clean-room policy

*This is a project policy, not legal advice. Have an IP lawyer review it before a public 1.0 release or any commercial use.*

## Why open source and free does not by itself protect us
Copyright, trademark, patent and contract claims do not depend on charging money. The goal is to **not do the things that create claims**, and to be able to show that we did not.

## What Duejoro may do
- Implement **functionality**. Ideas, methods and functions (keeping a list of dates, working out what is overdue, repeating a renewal, exporting a calendar) are not protected by copyright, and many compatible programs already exist.
- Read and write **open formats**: CSV (RFC 4180 and its common variants) and iCalendar (RFC 5545), both public specifications.
- Use **generic interface patterns** common to all list-keeping software (a table, a toolbar, a details pane, summary cards).
- Say truthfully that Duejoro is an **alternative** to commercial contract-, license- and renewal-tracking subscription software (nominative use of a trademark to describe what a product is compared to).

## What Duejoro must not do
| Don't | Why |
|---|---|
| Use another company's product names, logos, icons, illustrations, sounds, screenshots, sample data, help text or marketing copy in the program, documents or website | Trademark and copyright |
| Copy another program's screens, layout or wording pixel for pixel, or reproduce its icon set | Copyright and trade dress |
| Decompile, disassemble or otherwise inspect another company's program to learn how it works | License terms usually forbid it; legal risk |
| Use another company's online services, calendar integrations or SDKs in ways their terms forbid, or imply a tie to them | Contract and trademark |
| Claim affiliation, certification or endorsement | False association |
| Copy code from a program or project whose license is incompatible with AGPL-3.0-or-later | Copyright and license conflict |

## What Duejoro is not
Duejoro helps people **remember dates**. It does not give legal, tax, insurance or compliance advice, does not know what a person's jurisdiction requires, and does not check that a license, permit or certification is valid. Dates in Duejoro are what the person typed in.

## Clean-room practice
1. **Specs only.** Implement from public specifications and from our own synthetic test data. Do not paste another company's text, samples or files into the repository.
2. **Describe by what it does.** Our documents and our interface name features by their function, and our names are our own. The automated test in `tests/Duejoro.Tests` fails if a name we have promised not to use appears in the program's source.
3. **Original look.** Our own name, colors, icon, wording and layout.
4. **Third-party code.** Every dependency needs a license review (AGPL-compatible) and goes into `THIRD-PARTY-NOTICES.md` before it is merged.
5. **Takedown plan.** If anyone objects, respond promptly (through counsel for anything formal) and be ready to rename or restyle.

## Test data
Use only data made for the project (invented names, numbers and dates). Never commit a real person's or company's records.

## License
AGPL-3.0-or-later. Contributors license their work under the same terms.
