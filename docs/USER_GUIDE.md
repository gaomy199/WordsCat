# WordsCat user guide

Version **0.3.8** · Windows 11

WordsCat translates short Chinese or English selections and saves reviewed vocabulary to Markdown notebooks.

## Start and configure

Download a `WordsCat-VERSION-win-x64.zip` asset from [GitHub Releases](https://github.com/gaomy199/WordsCat/releases), extract the complete archive, and run `WordsCat/WordsCat.exe`. Self-contained portable releases built from this source bundle .NET 10 Windows Desktop Runtime; no SDK, source download, or build step is needed to run them. For source builds, install the .NET 10 SDK; the default framework-dependent output needs .NET 10 Windows Desktop Runtime (x64). Keep all extracted files together. Formula reading also uses the Microsoft Edge WebView2 Runtime; text views remain available if it is missing.

When packaging locally, the ready-to-run ZIP is written under `dist/`, with its checksum in `dist/SHA256SUMS.txt`. The default `publish/` folder contains the framework-dependent development build.

Version 0.3.8 in this workspace is available as a local `publish/WordsCat.exe` build. This task does not create or publish a 0.3.8 ZIP; existing local archives remain their original versions. Check the chosen Release for its actual version.

On first launch without a saved key, a short, dismissible guide offers **Open settings** or **Set up later**. Text views remain usable. Open **Settings → Models & recognition → Model service** and choose **DeepSeek** or **Custom OpenAI-compatible**. DeepSeek retains the default Base URL `https://api.deepseek.com` and model `deepseek-flash`. For a custom service, enter its HTTPS Base URL and model name. The optional vision model defaults to the text model when blank; image input requires a model that supports it.

The API key input is masked and always opens empty. Keys are stored only in Windows Credential Manager, never in settings JSON. Leave it blank to keep the saved key; enter a new value and save to replace it. Cancel discards the input. Switching service origins requires saving that service's key. Existing ReadDock credentials remain compatible.

Save settings, reopen them, and choose **Test connection**. This sends one minimal text request using the saved key, with no document text or image, no retry, and a timeout of up to 15 seconds. Results distinguish a missing key, 401 invalid key, 402/429 quota or rate limit, network/timeout, and an unexpected response format. A successful text probe does not verify image support. Unsaved key/address changes must be saved first.

In **Notebooks & template**, add Markdown notebook destinations and choose a default. Existing custom templates and notebooks are preserved when upgrading.

Choose English or Chinese under **Appearance**. Language and appearance changes preview immediately; save to keep them, or cancel to restore the saved settings.

## Translate while reading

- Select text in a reader or browser and use **Ctrl+Alt+D**.
- Use **Ctrl+Alt+S** to capture text from a scanned PDF or image.
- Use **Paste** or the source editor to supply text manually, then choose **Translate source**.

Chinese and English are detected automatically. The left pane is the source; the right pane is the translation. **Swap** moves the initial translation into the source and detects its language again. Editing a completed source clears its previous translation and follow-up context.

Capture results first appear in a lightweight popup near the selection. Open the main window from that popup when you need more space. The source tabs let you read formulas, edit text, and inspect the current capture. Captures are transient and are not stored in translation history.

The popup renders source and translation with the same local KaTeX reader as the main window. Choose **Show source**, then **Show translation** to switch back; select text in the reading area to copy it. New selections and errors clear the prior source/toggle state. Missing WebView2 falls back to selectable plain text.

Select a word in either popup view and choose **Save word**. The editor starts with that selection and uses the recognized source as context for the existing automatic enrichment. The popup is temporarily hidden while editing and returns when the editor closes; the main window stays hidden. A saved API key is required for model enrichment; manual editing remains available.

Visual recognition sends the selected image to the configured model service. Local Windows OCR recognizes the image on the computer; model translation then sends recognized text. Review mathematical symbols against the original capture. Recognition quality varies with the source image and model.

## Floating entry and sidebar

Hover over the floating icon briefly to open its menu. Move away from both the icon and list to close it automatically. Clicking toggles the menu; dragging moves the icon without opening it.

Use the sidebar button beside the brand name to collapse the brand/history area. A narrow rail retains the expand button. This gives the reading panes more space without clearing content. The app remembers the sidebar state.

## Follow-up questions

After translating, type a question below the reading panes and press Enter or click **Ask**. Questions apply only to the current selection. A new translation starts independently. Responses use the current interface language.

## Save vocabulary

Select a word or phrase in the source and choose **Save word** (or **Ctrl+Alt+W**). Review its editable fields and the target notebook before saving. Base cards include word, lemma, contextual part of speech and meaning, the source sentence, and its translation. In **Settings → Notebooks & template**, use the four switches to add other common senses (enabled by default), IPA, AI examples with translations, or common collocations (the last three are disabled by default). Added content appears as separate editable fields and in the Markdown preview; AI examples never replace the verified source sentence. Changes affect new entries. Existing custom fields and layouts remain in use; template files and saved notes are preserved.

The app appends deterministic Markdown, checks duplicates within the selected notebook, and supports undo with external-edit checks. Switching the current destination does not replace the default notebook. Your notebooks remain normal Markdown files usable in Obsidian or another editor.

## History, closing, and upgrades

The history sidebar keeps the most recent completed source/translation pairs, encrypted for the current Windows account. The default limit is 100. Change it under Settings; reducing the limit prompts before deleting older entries, and zero disables and clears history. Saved vocabulary is unaffected.

Minimize retains the taskbar entry and current content. Closing with **×** hides to the tray and clears transient input. Use **Exit** to quit the program.

Before upgrading, exit the old version. Preserve its `Data/` directory and existing Markdown notebooks. Keep the new executable, its companion files and Data together; do not move only the executable. Internal notebook, template and logo paths are saved relative to the settings.json directory (normally Data), so moving the whole folder preserves them. External Obsidian notebooks keep absolute paths. Missing old absolute Data paths recover when the same file exists in the current Data folder; reselect any missing external custom template. The original Windows account's stored API key remains compatible.

For the detailed Chinese guide, see [USER_GUIDE.zh-CN.md](USER_GUIDE.zh-CN.md).
