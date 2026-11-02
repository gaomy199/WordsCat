# Security policy

## Report privately

Use [GitHub private vulnerability reporting](https://github.com/gaomy199/WordsCat/security/advisories/new). Reports go to the repository maintainers privately. Include the affected version, impact, and minimal reproduction with synthetic data. Do not attach credentials, private documents, user Data, or an unredacted settings/history export.

Repository administrators must enable private vulnerability reporting before publishing a release. If the private form is unavailable, open an issue titled "Private security contact requested" with no vulnerability details or sensitive attachments; wait for the maintainer to arrange a private channel. Do not disclose exploit details in public issues or pull requests. No response-time guarantee is offered.

Fixes are prepared for the current development version. Reproduce reports against the latest available release; older versions do not receive a separate maintenance guarantee.

## API keys and data

Production keys are stored only in Windows Credential Manager, using the compatibility target `ReadDock.DeepSeek.ApiKey.v1`. No environment-variable or file fallback is supported. Settings always show a blank masked key field; blank preserves the saved key, a replacement is saved to Credential Manager, and cancellation discards the draft. New credential metadata binds the service origin; existing ReadDock credentials remain compatible.

Keys and Authorization headers must never appear in source, tests, logs, diagnostics, exported settings, or history. Automated tests use a fixed fake credential and local mock handlers, without reading real credentials or contacting model services. If a key is exposed, revoke it at the provider immediately; deleting a working file does not remove prior Git history.

Visual recognition sends the selected image to the configured provider. Local Windows OCR keeps image recognition local, then model translation sends the recognized text. Follow-ups use the current selection's context. Provider data handling is governed by its own policy. Translation history is encrypted locally for the current Windows account; local encryption does not protect a compromised account.

## Download integrity

Download named Windows ZIP assets from this repository's Releases, compare SHA-256 with the matching `SHA256SUMS.txt`, and keep the extracted files together. Builds are currently unsigned and may trigger Windows SmartScreen. A matching checksum checks integrity against the published checksum; it is not a code-signing certificate. Never disable Windows security protections to install a copy from an untrusted source.
