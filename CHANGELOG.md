# 版本记录

## 0.3.7 · 2026-11-02 · Uncommitted

- Pass the popup's rendered/raw selection to the existing vocabulary editor and automatic enrichment, preserving the current recognized source as context. Hide the topmost popup while its modal editor is open, restore it after save/cancel, and keep the main window hidden. Add actual-window/local-mock regression coverage.
- Add the publish-release.ps1 entry point over the existing self-contained win-x64 packager, require clean checkouts for hosted releases, and include application/runtime licenses and notices. Retain the existing dist ignore rule.
- Add Windows build/offline verification CI and version-checked tag release workflows with ZIP/checksum attachments; local validation is static only, with no hosted workflow run or upload.
- Split README installation/developer guidance, document unsigned SmartScreen and SHA256 verification, add private security reporting guidance, contributing/AI diff-review requirements, issue/PR templates, and Apache-2.0 LICENSE/NOTICE (copyright 2026 gaomy199).
- Offline build, full actual-window verification, extracted-package self-tests/UI regressions and source/secret checks pass. Complete mouse-based popup selection/editor/cancel visual inspection on 2026-11-02. Local ZIP and validation evidence: docs/VALIDATION-v0.3.7.md. Hosted workflows remain statically checked only; no commit, push or publication.

## 0.3.6 · 2026-11-01 · Uncommitted

- Add service preset/key settings and a modeless first-use guide; keep keys masked and never prefill saved values. Blank preserves a saved key, replacement writes only to Credential Manager, and cancellation discards the draft.
- Add minimal connection probes and localized status categories, fake-vault/local-handler regressions, legacy configuration inference and credential-origin metadata compatibility.
- Refresh native preset item strings immediately when changing the UI language, preserving selected values and unsaved service/key drafts without triggering preset resets.
- Offline build, full verification with actual windows, source/secret checks, and extracted portable-package self-tests/UI regressions pass. Inspect Chinese/English setup and settings in isolated offline previews; synchronize both user guides. Generate a local 0.3.6 Windows x64 ZIP with bundled .NET 10.0.14. Evidence: docs/VALIDATION-v0.3.6.md. No commit, push or GitHub publication.

## 0.3.5 · 2026-10-31 · Uncommitted

- Replace the popup copy button with a source/translation toggle. Both views use the main window's local KaTeX renderer, selectable content, adaptive height and existing scroll cap; retain the action-row styling and clear prior source state for a new selection or error.
- Use a compact 16px popup reading font, with proportionally scaled formulas, following user review of the first rendered preview.
- Introduce separate text/image model interfaces and a reusable OpenAI-compatible chat completions client. Preserve the existing DeepSeek default URL, model names, request extensions, saved settings and ReadDock credential target; local Windows OCR is unchanged. No service-selection UI is added.
- Add backward-compatible timeout/retry options (100 seconds, zero retries by default), safe error boundaries and local mock regressions for success, 401, 429, timeout/cancellation and non-JSON responses. Tests never read saved keys or contact real services.
- Add an offline switch to the local release packager for already restored dependencies. This task does not commit, push or publish to GitHub.
- Build, full offline/actual-window verification (including the final 16px popup) and workspace/index secret guard pass. Computer Use confirms source/translation switching; the final local ZIP passes extracted self-tests. Evidence: docs/VALIDATION-v0.3.5.md.

## .NET 10 migration · Uncommitted · 2026-10-31

- Retarget Windows Forms to `net10.0-windows` (.NET 10 LTS) and remove the self-contained .NET 8.0.33 runtime pin; use the .NET 10 SDK's runtime selection.
- Update SDK/runtime requirements and release/development guidance. Retain WebView2 1.0.3595.46 after reviewing its cached .NET 5+ build targets; no unrelated package or business-code changes.
- Resolve five new WinForms WFO1000 analyzer errors with explicit default/serialization metadata and one nullable caption warning with an empty-string fallback. Business behavior and ReadDock compatibility identifiers are preserved.
- Build without compiler warnings on SDK 10.0.403; full offline and actual-window verification passes on .NET 10.0.14, including existing WebView2 1.0.3595.46. Source/secret checks pass. Remove temporary migration notices and document the local `publish/` and `dist/` output locations. Evidence: docs/VALIDATION-v0.3.4.md.
- Rebuild the local Windows x64 ZIP with .NET 10.0.14 and SHA-256 checksum. Verify the extracted app's offline and actual-window regressions, bundled-runtime resolution with empty DOTNET_ROOT, and credential scan; inspect its main/settings windows through Computer Use.
- No real credentials or user Data are accessed or changed; no commit or push is performed.

## API credential security · Uncommitted

- Add a shared credential detector/redactor for diagnostics, HTTP responses and errors, settings export, and translation history; preserve ReadDock credential/data compatibility identifiers.
- Developer diagnostics no longer read production credentials or perform live requests. Regression tests use one explicit fake credential and local mock handlers.
- Add a working-tree/index/archive secret guard, disposable negative tests, a pre-commit hook, ignore rules and security development instructions. No user Data, Git history, commits or remote state are changed by this task.

## 0.3.4 · 2026-10-31

- Add a Windows x64 portable Release ZIP that includes .NET 8.0.33. Users can extract and run WordsCat.exe without downloading source, installing the SDK, or building the application.
- Add a fresh-staging release packager, archive dependency/data checks, release metadata, quick-start instructions, SHA-256 checksum and maintainer release guide.
- Put direct binary download links at the top of the English README and update both user guides. Preserve existing settings, credentials and notebooks; WebView2 remains a system prerequisite for formula views, with text fallback.

## Repository organization · 2026-10-31

- Organize the C# project under src/WordsCat with English component directories; move OCR and the default template to scripts/ and templates/.
- Add English README, development/user guides, third-party notices, and a local source audit. Preserve the Chinese user guide under an English file name.
- Keep runtime resources and all existing data/credential compatibility identifiers unchanged. Rebuild and verify from a clean output directory before the initial public GitHub upload.
- Publish the public source repository at https://github.com/gaomy199/WordsCat on main, retaining local history and excluding runtime data, private fixtures, credentials, and build output. Verify the remote commit and all 115 files against the local Git tree.

## 0.3.3 · 2026-10-31

- 悬浮菜单支持悬停展开，离开浮标和列表约 250—325ms 后收起；移入列表保持展开，再次点击浮标收起，继续点击可重新展开。保留拖动定位、菜单操作和托盘入口。
- 修复系统菜单在同一次点击中关闭又重开，以及关闭菜单后浮标可能被主窗口遮挡的问题。
- 左侧品牌/历史栏增加收起和展开按钮，收起后保留窄栏入口，原文/译文获得更多空间；自动记住状态，不清空历史或当前内容。
- 左侧标题向上微调约 12 逻辑像素；修正收起时自动列宽导致展开按钮越界的问题。验证见 docs/VALIDATION-v0.3.3.md。

## 0.3.2 · 2026-10-30

- 补齐粘贴原文、打开单词本、退出按钮的图标；统一设置页签的图标/文字组排版，固定图文间距。
- 修复鼠标点击和关闭设置后的强调外框残留，同时保留键盘导航焦点提示。
- 默认图标改为用户提供的 4.ico，嵌入 EXE 和运行时资源，窗口、托盘与浮标统一使用；保留自定义 Logo 优先级。
- 左上角仅显示当前语言名称，删除第二行重复名称。
- 去掉手动翻译方向选择；文字本地识别中英文，视觉提示自动判断截图正文语言，交换后重新识别。旧配置的手动方向迁移为自动。
- 修复隐藏编辑页尚未创建句柄时不发送文本变化通知、导致自动方向标题未刷新的问题。构建与完整离线验证通过，详见 docs/VALIDATION-v0.3.2.md。

## 0.3.1 · 2026-10-30

- 名称移至最左侧栏顶部，中英文品牌分行显示，增加标题高度以避免裁切；历史紧随其下，移除保留说明。
- 语言选择改为全界面即时预览，与主题保持一致；保存保留、取消恢复。修正表格表头在中英文来回切换时未恢复的问题。
- 增加圆角按钮、圆形交换按钮、柔和阅读/历史卡片，以及带图标和主题色选中态的设置页签。
- 为设置输入项统一增加圆角细边框，缩短语言/主题/条数等短选项宽度，统一内边距与文字对齐。
- 统一下拉框的主题外框与箭头，去掉深色模式中的亮色内框。增加品牌位置/高度、输入容器边界、即时语言预览/撤回及双语表头回归；构建与完整本地验证通过，详见 docs/VALIDATION-v0.3.1.md。

## 0.3.0 · 2026-10-30

- 产品改名为 WordsCat / 单词猫，程序入口 WordsCat.exe，默认文字图标改为「猫」。保留旧凭据目标、配置和数据兼容标识。
- 设置新增中文/英文界面选项，保存立即生效，取消保留原语言。
- 最近翻译移至左侧；原文/译文保持左右双栏；放大保存目标、置顶和阅读提示，修正对齐。
- 简化追问区，只保留输入提示；输入框和追问按钮等高，支持 Enter 发送。
- 新增英→中、中→英方向及交换操作；文本/划词/截图统一遵循方向，新历史保存方向并兼容旧记录。
- 增加双向提示、旧配置/历史、语言保存/取消/重启、布局与上下文清理回归。构建及完整离线验证通过，详见 docs/VALIDATION-v0.3.0.md。

## 0.2.2 · 2026-10-30

- 轻量弹窗按译文行数调整高度，长译文在原高度上限内纵向滚动。
- 修复本地跨源页面导致数学字体回退；英文正文使用 Times New Roman，公式加载专业数学字体，收紧公式间距。
- 增加右侧本地翻译历史，默认 100 条，可设 0 关闭；缩小上限前提示实际删除数量并确认。
- 历史以 Windows 当前账户加密保存，仅保留完成的原文/译文；截图和追问不写入历史，过期记录无历史备份。
- 增加临时图片、当前输入和退出后渲染缓存清理；保留用户笔记和配置。
- 最小化保留任务栏入口，点 × 隐藏到托盘。
- 完整本地验证通过，补充真实数学字体、历史淘汰/警告、弹窗与任务栏回归。

## 0.2.1 · 2026-10-29

- 主界面原文和译文支持离线公式渲染；保留编辑文本与本次原始截图。
- 改善连续截图/取词后主窗口的恢复、激活和最新内容刷新；打开主界面即关闭结果小窗。
- 修复浮标菜单干扰网页取词的焦点问题，增加浏览器剪贴板更新等待。
- 外观调整即时预览，保存保留、取消恢复；修复深色主题禁用按钮文字阴影。
- 自定义浮标 Logo 背景和边框不再使用主题强调色。
- 增加真实 WebView2/KaTeX、主题预览与连续窗口切换回归，以及不读取凭据的离线预览入口。

## 0.2.0 · 2026-10-29

- 增加 DeepSeek 视觉截图识别与翻译，保留本地 OCR 模式。
- 结果使用选区附近的轻量弹窗，支持手动展开主界面。
- 增加多 Markdown 单词本、默认项及保存目标切换。
- 增加自定义 Logo、主题强调色、深浅模式和背景亮度。
- 默认生词模板精简为六字段，保留数学 LaTeX。
- 增加旧配置迁移及相关验证。
- 用户提供的数学截图经授权完成一次真实视觉实测，q/p 与下标、2k 正确保留。

## 0.1.0 · 2026-10-29

- 初始 Windows 工具：悬浮入口、取词、截图 OCR、DeepSeek 翻译。
- 自定义 JSON 模板、Markdown 收藏、重复提醒、备份和撤销。

## 工程整理 · 2026-10-29

- 将 0.2 源码整理为独立本地工程。
- 增加 AGENTS.md、项目状态、需求记录、本地验证脚本和 Git 基线。
- 本次整理未改变程序功能，未上传远程仓库。
