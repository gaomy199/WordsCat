# Repository preparation

Date: 2026-10-31. Application version: 0.3.3.

## Source layout

The application project is `src/WordsCat/WordsCat.csproj`. Source files are grouped under UI, Reading, Persistence, Services, Platform, Localization, Models, and Diagnostics. Repository-owned resources live in assets/, scripts/, and templates/. Root build.ps1 and verify.ps1 remain the main entry points.

The main README, development guide, user guide, and repository paths use English. The detailed Chinese user guide is retained as docs/USER_GUIDE.zh-CN.md. Historical Chinese project-state and verification records remain available under English paths.

No runtime functionality was changed by the reorganization. Published resource locations, namespace and credential compatibility identifiers, data paths, template migration, and notebook markers remain unchanged. The exact legacy template fixture is preserved.

## Verification

- Built from the new project path into a fresh .test-results/repository-publish directory with no compile warnings.
- verify.ps1 -PublishPath .test-results/repository-publish -IncludeUi passed. Report: .test-results/37ef6c20d133435d85d779cc87b4c2fa/.
- Confirmed the clean output includes the executable, OCR helper, default template, both user guides, formula reader and fonts, and the WebView2 license.
- scripts/check-source.ps1 checks repository paths, excluded data/build paths, and common credential patterns without reading Windows Credential Manager or sending data over the network.
- Existing private fixtures, Data, publish, build intermediates, and test results remain ignored. No real model requests were made.

## GitHub destination

The user requested a public repository named WordsCat. The connected account is gaomy199. Only the reviewed source snapshot and bundled assets/notices are intended for upload. User settings, translation history, private samples, generated binaries, and test outputs are excluded.

The user created the public repository at https://github.com/gaomy199/WordsCat. After reconnecting, the current repository is empty. The connector still returns HTTP 403 for writes, so the upload uses native Git with the existing Git Credential Manager account `gaomy199` instead.

Two GitHub accounts are stored locally. Supplying the username explicitly resolves non-interactive account selection; the account was verified against GitHub without displaying or saving a token. `origin` points to the public repository. The reviewed local history is preserved, and the old baseline was also checked for common credential patterns before uploading.

## Publication result

Source publication succeeded on `main`. The first complete source commit is `d2ab4975f2c3c7a16f4f60d98a63fa55702aab4b`; its Git tree is `2c2b7d2f49512084dccb51f4a3b6918b9da8b439`, identical locally and on GitHub. The remote API confirmed public visibility, `main` as the default branch, and all 115 reviewed files. No runtime data, private captures, credentials, executable binaries, build intermediates, or local test output were included. The local `main` branch tracks `origin/main`; the earlier local development branch is retained.

Subsequent documentation commits record this verification on the same branch. No binary release or installation package was published as part of this source upload.
