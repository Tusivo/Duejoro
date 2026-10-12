# Security model

Duejoro holds information a business cares about: what licenses and insurance it has, who is responsible for what, what it pays, and scans of the documents themselves. This is the model it is built to.

## Design principles
- **Local only.** Duejoro does not upload your records or files and has no accounts. Its only network use is **Check for Updates**, which you start yourself, and which sends nothing but a request to GitHub for the list of releases.
- **Your files stay in your folder.** Records, attached files, settings and backups live in a `Duejoro-Data` folder beside the program (or, if that cannot be written, in your own account's application-data folder). Nothing is written elsewhere, and nothing is written to the Windows registry.
- **No deleting behind your back.** Duejoro never deletes a file from disk. Taking an attachment off a record leaves the copy in the Documents folder; a damaged records file is set aside, not removed.
- **Safe writes.** The records file is written to a new file and swapped in, so a crash or power cut cannot leave half a file. A dated backup is made before the first change of each session, and the newest 20 are kept.
- **Updates are signed.** Releases are signed with a key kept offline; the program accepts only updates that verify (see docs/RELEASING.md).

## What it does not protect against
- **Duejoro does not encrypt your records.** Anyone who can open the `Duejoro-Data` folder can read them, as with any file on your computer. Use Windows' own protections (a sign-in password, BitLocker or a protected folder) for sensitive information, and keep Duejoro's folder somewhere only the right people can reach.
- **Attached files are copies.** If you attach a scan of a license, the copy is as readable as any other file in that folder.
- Duejoro is in early development and has had no independent security review.

## Known limitations
- Importing a spreadsheet reads text only; nothing in it is run.
- A very large number of records (tens of thousands) has not been tested.

## Reporting a vulnerability
Please report security problems **privately**, not in a public issue: use GitHub's "Report a vulnerability" button on the repository's Security tab, or write to [info@tusivo.com](mailto:info@tusivo.com) with the subject "Duejoro security". Include the version, what you did and what happened. Please do not attach real records.
