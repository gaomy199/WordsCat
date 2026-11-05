# WordsCat user guide

Version **0.3.18** · Windows 11

WordsCat translates short Chinese or English selections and saves reviewed vocabulary to Markdown notebooks.

## Start and configure

Download a `WordsCat-VERSION-win-x64.zip` asset from [GitHub Releases](https://github.com/gaomy199/WordsCat/releases), extract the complete archive, and run `WordsCat/WordsCat.exe`. Self-contained portable releases built from this source bundle .NET 10 Windows Desktop Runtime; no SDK, source download, or build step is needed to run them. For source builds, install the .NET 10 SDK; the default framework-dependent output needs .NET 10 Windows Desktop Runtime (x64). Keep all extracted files together. Formula reading also uses the Microsoft Edge WebView2 Runtime; text views remain available if it is missing.

When packaging locally, the ready-to-run ZIP is written under `dist/`, with its checksum in `dist/SHA256SUMS.txt`. The default `publish/` folder contains the framework-dependent development build.

Version 0.3.18 is built locally as `publish/WordsCat.exe`. No new ZIP is generated for this version. Check the chosen Release for its actual version.

On first launch without a saved key, a short, dismissible guide offers **Open settings** or **Set up later**. Open **Settings → Models & recognition → Model service** and choose **DeepSeek**, **Gitee AI · Moark**, or **Custom OpenAI-compatible**. DeepSeek retains `https://api.deepseek.com` and `deepseek-flash`. The Gitee preset uses `https://ai.gitee.com/v1`; a Gitee homepage URL is also normalized to this API path. For a custom service, enter its HTTPS Base URL.

A fixed row of dots indicates a saved key for the current service. These dots are a placeholder: the actual saved key never enters the editor and cannot be copied. New input is masked; blank keeps the existing key. Keys remain only in Windows Credential Manager, with separate entries by service origin; the DeepSeek compatibility target is unchanged. Cancel discards unsaved input.

Enter the key and click **Fetch models**. This saves a newly entered key immediately, then reads the service's `/models` endpoint without sending document content. Cancelling settings afterwards does not undo that explicit key save. Select text and vision models from the returned dropdowns, then save settings. Model IDs can also be entered manually if discovery is unsupported. The list does not establish image capability or account entitlement: select an image-capable vision model, or use Windows OCR. A blank vision model uses the text model. This setup changes only WordsCat, with no Codex configuration or login changes. See [Gitee's compatible API guide](https://ai.gitee.com/docs/integrations/Development-Tools/catpaw).

Save settings, reopen them, and choose **Test connection**. This sends one fixed short text request using the saved key, with no document text or image and no retry. The test follows the configured request timeout (100 seconds by default), including for queued or slower models. Gitee Qwen3-32B uses a `/no_think` short-answer prompt and a 128-token cap; other probes retain their 8-token cap. Results distinguish missing/invalid keys, quota/rate limits, DNS, HTTPS, proxy, other network failures, timeouts, truncated outputs and malformed responses. A successful text probe does not verify image support. Unsaved key changes must be saved first.

In **Notebooks & template**, add Markdown notebook destinations and choose a default. Generated names such as “Notebook 1” follow the selected filename; custom names are retained. Change the current target in the main window or vocabulary editor, independently of the startup default. The popup displays the current notebook in its title and retains the v0.3.9 layout with three bottom actions. Existing custom templates and notebooks are preserved when upgrading.

At each startup, WordsCat also discovers `.md` files directly inside its `Data` folder and adds unregistered files by filename, up to 40 notebooks. Existing configured targets and defaults are kept; duplicate paths are skipped. Copy notebooks into the new version's `Data` folder and restart to use them without registering each file. Subfolders are not scanned and notebook contents are not changed. A removed list entry will be discovered again next startup if its file remains there. Keep the entire old `Data` folder, including `settings.json` and custom templates, to preserve settings as well as notebooks.

Choose English or Chinese under **Appearance**. Language and appearance changes preview immediately; save to keep them, or cancel to restore the saved settings.

## Translate while reading

- Select text in a reader or browser and use **Alt+X**.
- Use **Alt+S** to capture text from a scanned PDF or image.
- Use **Paste** or the source editor to supply text manually, then choose **Translate source**.

These are the default shortcuts. Under **Settings → Reading preferences**, click a shortcut box and press a new combination containing Ctrl or Alt (optionally Shift). Customize selection translation, capture, vocabulary, and show/hide main window. Save to apply immediately and retain it after restart; Cancel keeps the saved bindings. **Restore default shortcuts** resets the draft. Duplicate or unavailable combinations prevent saving and show a message. The app temporarily releases its global shortcuts while settings are open so they can be recorded.

Chinese and English are detected automatically. The left pane is the source; the right pane is the translation. **Swap** moves the initial translation into the source and detects its language again. Editing a completed source clears its previous translation and follow-up context.

The main reading panes use 16px for Chinese characters and punctuation, while English stays at 18px and formulas retain their original size. Both panes use compact reading tabs, with a separate gap before the action and follow-up area. The follow-up box is a vertically centered single line; Enter sends the question.

Capture results first appear in a lightweight popup near the selection. Open the main window from that popup when you need more space. The source tabs let you read formulas, edit text, and inspect the current capture. Captures are transient and are not stored in translation history.

Selection shortcuts show the popup before text capture and translation finish. Native text remains visible while the formula reader initializes. Click the system tray icon once with the left mouse button to open the main window after hiding it; right-click still opens the tray menu. Window changes skip unchanged formula renders and reduce repainting.

The popup renders source and translation with the same local KaTeX reader as the main window. Choose **Show source**, then **Show translation** to switch back; select text in the reading area to copy it. New selections and errors clear the prior source/toggle state. Missing WebView2 falls back to selectable plain text.

Drag the popup title to move it. It keeps that position while switching source/translation or updating content, until the popup closes. A new popup opened by selection or capture uses the original near-selection placement rules.

Select a word in either popup view and choose **Save word**. The editor starts with that selection and uses the recognized source as context for the existing automatic enrichment. The popup is temporarily hidden while editing and returns when the editor closes; the main window stays hidden. A saved API key is required for model enrichment; manual editing remains available.

Visual recognition sends the selected image to the configured model service. Local Windows OCR recognizes the image on the computer; model translation then sends recognized text. Review mathematical symbols against the original capture. Recognition quality varies with the source image and model.

## Floating entry and sidebar

Hover over the floating icon briefly to open its menu. Move away from both the icon and list to close it automatically. Clicking toggles the menu; dragging moves the icon without opening it.

Use the sidebar button beside the brand name to collapse the brand/history area. A narrow rail retains the expand button. This gives the reading panes more space without clearing content. The app remembers the sidebar state.

## Follow-up questions

After translating, type a question below the reading panes and press Enter or click **Ask**. Questions apply only to the current selection. A new translation starts independently. Responses use the current interface language.

## Save vocabulary

Select a word or phrase in the source and choose **Save word** (or **Alt+W**). Review its editable fields and the target notebook before saving. Base cards include word, lemma, part of speech, meaning, the source sentence, and its translation. Meaning lists the contextual sense first, followed by other common senses. In **Settings → Notebooks & template**, use the four switches for common word forms (enabled by default, up to 3), IPA, AI examples with translations, and common collocations (the last three are disabled by default). Each word form has its spelling, part of speech and meaning; for generalizable, examples include generalize, generalization and generalized. IPA follows the lemma and precedes part of speech in the editor and standard Markdown. AI examples never replace the verified source sentence. Changes affect new entries. Old switch values migrate to the word-forms switch. Legacy contextual-meaning labels update in memory. Custom inline layouts, template files and saved notes are preserved.

The editor offers **Add IPA**, **AI examples** and **Add collocations** before **Cancel**. Click once to add and fill the chosen fields; the button stays lightly highlighted. Click again to remove that supplement from the editor, Markdown preview and saved entry; the button returns to its neutral color. AI examples and their translations switch together. Fields already present from defaults or the template start highlighted. Other edits are retained. These actions do not change the settings defaults or template file; initial automatic enrichment still follows the defaults. Review the new fields and Markdown before saving.

The app writes deterministic Markdown and checks duplicates only in the selected notebook. For a duplicate, **Yes** replaces its existing entries with the current entry; **No** returns to editing. Other words remain intact. Backups and undo with external-edit checks apply to replacements too. Unsafe entry boundaries prevent replacement. Switching the current destination does not replace the default notebook. Your notebooks remain normal Markdown files usable in Obsidian or another editor.

## History, closing, and upgrades

The history sidebar keeps the most recent completed source/translation pairs, encrypted for the current Windows account. The default limit is 100. Change it under Settings; reducing the limit prompts before deleting older entries, and zero disables and clears history. Saved vocabulary is unaffected.

Minimize retains the taskbar entry and current content. Closing with **×** hides to the tray and clears transient input. Use **Exit** to quit the program.

Before upgrading, exit the old version. Preserve its `Data/` directory and existing Markdown notebooks. Keep the new executable, its companion files and Data together; do not move only the executable. Internal notebook, template and logo paths are saved relative to the settings.json directory (normally Data), so moving the whole folder preserves them. External Obsidian notebooks keep absolute paths. Missing old absolute Data paths recover when the same file exists in the current Data folder; reselect any missing external custom template. The original Windows account's stored API key remains compatible.

For the detailed Chinese guide, see [USER_GUIDE.zh-CN.md](USER_GUIDE.zh-CN.md).
