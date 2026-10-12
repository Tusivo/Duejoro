# Making a release

Every Duejoro release is signed. The signing key is yours alone: the private half stays on your computer (never on GitHub), and the public half is built into the program. Check for Updates installs a release only if its signature checks out, so a hijacked GitHub account cannot push a bad update.

## One-time setup: make the signing key

```
cd C:\MyApps\Duejoro
node scripts/release-sign.js keygen
```

It asks you to choose a passphrase (8 or more characters), then:

- saves the **private key**, locked with that passphrase, to `.duejoro-signing\release-private.pem` in your user folder;
- writes the **public key** to `src\Duejoro.Core\release-key.txt`, which the build embeds in the program.

**Back up the private key file and remember the passphrase.** Never put the private key in the repository or on GitHub. Commit `release-key.txt`; it is public by design. The key is made once: `keygen` refuses to overwrite an existing key. This is a different key from the one used for Ledjoro and Pagjoro, so that one project's key can never be used to sign another's.

A program built before the key exists carries no public key and therefore refuses every update (it says so). Make the key first, then build the program you will release.

## Every release

1. Set the version in `Directory.Build.props` (all four version lines) and in the README status line.
2. Test and build: `powershell -ExecutionPolicy Bypass -File scripts\build.ps1` (it runs the tests first). Close Duejoro first, since Windows will not replace a running program.
3. Sign the program:
   ```
   node scripts/release-sign.js sign dist\Duejoro.exe
   ```
   It asks for the passphrase, writes `dist\Duejoro.exe.sig` and checks the result against the public key before finishing. The version signed is the one in `Directory.Build.props`.
4. On GitHub, **Draft a new release** with a tag such as `v0.1.0-alpha.1`, make sure the tag points at your final commit, and attach **both** `Duejoro.exe` and `Duejoro.exe.sig`. Tick **Set as a pre-release** for alpha and beta versions.

A signature is tied to one version and one exact file: change the program after signing and it must be signed again.

## Checking a release yourself

```
node scripts/release-sign.js verify dist\Duejoro.exe --version 0.1.0-alpha.1
```

## Good to know
- A code-signing certificate (Authenticode) is a separate thing: it makes Windows show your name instead of the SmartScreen warning. This release signature does not replace it, and does not depend on it.
- The `.exe`, `.sig` and key files are never committed (see `.gitignore`).
- The `Duejoro-Data` folder that appears beside the program holds a person's records. Never commit it and never attach it to a release.
