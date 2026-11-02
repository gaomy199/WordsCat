# 项目状态

更新日期：2026-11-02。应用版本：**0.3.7，功能已完成本地验收**。小窗收藏、发布脚本与开源文件已由用户纳入本地提交；本次只读检查时 HEAD 为 0f1e59b。本轮更新 README、变更记录和项目状态，文档修改尚未提交，不改应用版本或行为。0.3.7 便携包和验证证据继续保留；本轮未联网核实远端提交、工作流或 Release 状态。下文未提交/未发布说明记录的是各开发任务结束时的历史状态。

## 用户与使用环境

用户在 Windows 11 上用 Zotero、WPS 阅读英文文献，包括扫描 PDF。希望按需划词/截图翻译，在保持英文阅读的同时选择性积累生词，保存到 Obsidian 的 Markdown 单词本。

当前正式源码根目录就是本项目目录，不再依赖原聊天的 work 文件夹。新聊天应从这个目录继续开发。

## 已完成

- 悬浮图标、菜单、快捷键、托盘入口。
- DeepSeek 文本翻译和截图视觉识别；可切换本地 Windows OCR。
- 截图后显示选区附近的轻量弹窗，提供手动打开主界面按钮。
- 识别原文可编辑，词条可编辑预览；保留 LaTeX 数学表达式。
- 多 Markdown 单词本、默认项、主窗口和收藏窗口切换；按文件去重。
- 默认六个字段：word、lemma、part_of_speech、meaning、example、example_translation。
- Logo、强调色、深浅模式、背景亮度设置。
- UTF-8 追加、文件锁、备份、撤销、外部编辑冲突保护。
- 0.1 单个笔记路径迁移；原版默认模板备份升级，自定义模板保留。

## 技术方案与关键选择

原生 C# / .NET 10（LTS）WinForms，目标 net10.0-windows；SDK 10.0.403 构建及 .NET 10.0.14 实际窗口回归通过。0.2.1 加入微软 WebView2 SDK 1.0.3595.46 和随包 KaTeX 0.16.22，用本地阅读视图渲染公式；不改变原生程序技术栈。运行依赖 Edge WebView2 Runtime，缺失时回退文本页。Windows OCR 通过随程序附带的 `ocr.ps1` 调用 WinRT。当前电脑只有中文 OCR 模型，对数学斜体和下标不可靠；数学文献默认采用 DeepSeek 视觉，直接发送框选图片。

默认官方接口地址 `https://api.deepseek.com`；文本和视觉模型可配置，发布时默认 `deepseek-flash`。不要依据旧常识擅自切换模型；调整接入前核对当时官方接口。

API Key 在 Windows 凭据管理器 `ReadDock.DeepSeek.ApiKey.v1` 中。每次新翻译只发送当前选区，只有当前选区的追问保留内存上下文；不创建聊天客户端会话。0.2.2 按用户要求增加本地翻译历史：默认最近 100 条，仅存已完成翻译的原文/译文，DPAPI 当前用户加密；设置 0 关闭并清空。

## 真实验证证据

2026-10-29，经用户明确授权，把其提供的一张数学文献截图发送到 DeepSeek 官方 API，实测一次成功。识别与译文都保留 `q_1, q_2, …, q_k`、`p_1, p_2, …, p_k` 和 `2k`，输入 488、输出 180 tokens。该一次性授权已用完，不能推断为今后所有样本上传授权。

本地自动验证通过；详见 `docs/VALIDATION-v0.2.md`。深色主界面、设置页、多单词本配置、自定义图标、轻量示例弹窗已在当前 150% 缩放下打开检查。

工程整理后，已从本正式目录重新运行 `build.ps1` 与 `verify.ps1`，构建和本地自动验证均通过。此轮仅验证工程可独立接续，没有再次调用真实模型接口。

原始数学截图仅在 `fixtures/private/math-screenshot.png` 本地保留，未纳入 Git 或工程备份 ZIP。`fixtures/vocabulary-v0.1.json` 为已纳入 Git 的旧模板回归样本，必须保留精确字节。

## 当前边界

- 公式阅读视图重排识别文本，保留段落与行间公式结构；不承诺逐像素复刻原图字体、行宽或多栏排版。「原始截图」保留本次原图供核对，关闭/清空本次内容后释放，不落盘持久保存。
- 复杂或不受 KaTeX 支持的表达式仍可能显示源码；0.2.1 没有重新上传用户截图进行视觉识别质量评估。
- 尚未对多种数学字体、复杂公式和广泛扫描文献评估准确率。
- 混合 DPI 多屏、不同 Zotero/WPS 版本、Obsidian 云同步环境需继续观察。
- 视觉测试通过显式诊断入口完成，不等于所有截图 UI 操作都做过端到端联网测试。
- 当前没有 Zotero 内部元数据插件、千问账号登录或 Codex harness 接入；这些只是曾讨论过的后续方向。
- 尚无自动更新、安装程序或签名；公开源码仓库为 https://github.com/gaomy199/WordsCat。

## 下一步

本轮 README 更新已完成，文档修改可由用户审阅后自行提交。publish/ 为 0.3.7 开发构建，便携包位于 dist/WordsCat-0.3.7-win-x64.zip；此包来自先前未提交工作区，其 sourceDirty/revision 仍记录打包时的真实状态。公开发布应按 docs/RELEASING.md 从对应干净提交重新构建。远端工作流和发布结果应在 GitHub Actions/Releases 确认；发布前确认私密漏洞报告已开启。本轮不提交/推送/tag，不新增功能，既有模型实测与多屏边界保持不变。

## 2026-11-02 README 公开入口整理

- 保持英文主 README，在顶部加入下载、中文/英文指南和变更记录入口。重排功能、用户安装、首次配置、快捷键、数据与凭据、开发构建及许可内容。
- 删除容易过时的“当前工作区未提交/尚未发布”首页说明，不推断远端状态。用 Releases/Actions 链接引导读者查看实际发布与运行结果，过程信息保留在验证及状态记录。
- 明确 Source code、publish/ 和 dist/ 的不同用途；保留未签名 SmartScreen、SHA256 核验、WebView2 文本回退及升级保留数据的说明。首次配置使用实际英文界面名称 Models & OCR，并说明保存后测试已保存凭据。
- 仅修改 README.md、CHANGELOG.md、docs/PROJECT_STATE.md，版本保持 0.3.7；不改代码、用户 Data、测试或归档，不重建先前已验收的包。本轮检查 Markdown 链接、代码围栏、差异格式及这三份文档的密钥模式，不重跑应用构建/UI 验证。先前 0.3.7 功能验收证据仍见 docs/VALIDATION-v0.3.7.md。
- 不读取真实凭据、联网、提交或推送；远端发布状态未核实。无本轮待实现事项。

## 2026-11-02 0.3.7 小窗收藏与开源发布准备：完成验收

- 小窗现在读取自己的渲染/文本选词，并沿用本次识别原文作为已有词条补全语境，不再误读主窗口的选词。收藏编辑期间暂时隐藏置顶小窗，编辑窗口继承置顶状态；保存/取消后恢复同一个小窗，主窗口保持隐藏。未新增翻译功能或修改凭据来源。
- 新增 PopupWordSelfTests 实际 STA 回归，覆盖原文/译文选词、主窗口旧选词干扰、本地模拟自动补全、窗口所有者及层级、保存与取消、小窗恢复和新选区清理。测试使用固定假凭据、假凭据库和本地 HTTP handler，不读取真实 Key。
- 2026-11-01 build.ps1 -Offline 无警告，verify.ps1 -IncludeUi 全部通过，报告 .test-results/6eb0ea29b3ca4150a0c1f6da8f3b9104/。最终 ZIP 解压后的非 UI 和完整实际窗口验证通过，报告 .test-results/portable-0.3.7-169d6e66e7a242ce8f30b40c4fd9eb2f/。check-source.ps1 检查 147 个源码文件通过，并执行覆盖新 ZIP、二进制、忽略产物及 index 的 check-secrets，零命中。
- 2026-11-02 Computer Use 用最终包组件及隔离临时工具实际双击选中 robust、点击收藏，确认单词/原形/本地模拟释义/原句已填入，收藏窗口完整显示、小窗未遮挡；取消后原小窗恢复。临时工具只为定位窗口增加任务栏入口，产品任务栏行为未改，真实层级仍由实际窗口回归覆盖。目视测试窗口已关闭。
- 新增 scripts/publish-release.ps1 入口并复用现有自包含 win-x64 打包器；dist/ 已有忽略规则，不重复添加。RequireClean 在脏工作区拒绝打包的负向检查通过。包内要求 Apache LICENSE/NOTICE、SECURITY.md、KaTeX/WebView2 与实际运行时包中的许可/声明；运行时法律文件逐字节核对通过。
- 新增 .github/workflows/ci.yml（Windows/.NET 10 构建、无 UI verify、密钥/源码检查）和 release.yml（v* tag、版本一致、干净源码、自包含打包、解压验证、Release ZIP/SHA256 附件）。已静态复核触发、权限和环境变量引用，PowerShell 解析器接受脚本及四个内嵌发布段。未运行托管工作流或访问远端发布接口。
- README 已拆分用户安装和开发构建，补充未签名 SmartScreen 与 SHA256 核验；新增 SECURITY、CONTRIBUTING、issue/PR 模板。用户确认 Apache-2.0，署名 2026 gaomy199；第三方声明同步随包 .NET 和本地公式资源。
- dist/WordsCat-0.3.7-win-x64.zip 为 53,316,272 字节、357 文件，随包 .NET Core/Windows Desktop 10.0.14。SHA-256：ffa6e90d5281aa7284db21047c1afb378747d2a3c156fed51e67cc0c3fb068f0。2026-11-02 再次核对归档校验值一致；sourceDirty 为 true，不是已发布版本。
- 未提交/推送/tag、未读取真实凭据、请求模型服务或改动用户 Data；旧 fixture 精确字节及 Git index 保持。完整证据与外部后续事项见 docs/VALIDATION-v0.3.7.md。
- 2026-11-02 文档收尾后再次运行 check-source.ps1，147 个源码文件及全工作区/index 密钥扫描通过，零命中；记录 .test-results/source-check-v0.3.7.txt。所有本地验收完成，无待修代码问题。

## 2026-11-01 0.3.6 设置与首次引导：完成验收

- 修复原生 ComboBox 缓存旧语言文字：重新生成选项显示字符串，刷新期间保护预设/主题/语言事件，避免递归、重置服务和清空草稿。实际窗口回归新增双向语言切换及地址/模型/未保存密钥保留断言。
- build.ps1 -Offline 成功，无编译警告；verify.ps1 -IncludeUi 通过，完整报告 .test-results/1f81c32a7de24a39b150fb4d8d3b9b86/。verify 前置 check-secrets 对工作区/暂存内容扫描零命中。补强草稿断言后再次构建，新 ZIP 解压后的非 UI 和完整实际窗口回归均通过，报告 .test-results/portable-0.3.6-253dd5cc8d644835bac225d8ee9cba52/。
- scripts/check-source.ps1 通过，检查 134 个源码文件，并调用 check-secrets 再次扫描全工作区/index（含新的 ZIP、二进制及忽略目录），零命中。所有自动测试只用固定假密钥、注入的假凭据库和本地 HTTP handler。
- Computer Use 检查实际解压包的中文 DeepSeek/英文自定义服务设置、双语非模态引导、空白受保护的 Key 框和离线连接提示；均使用新建临时配置，不读取真实凭据。预览已关闭。语言即时刷新与带假密钥的保存/取消行为由实际 STA 回归覆盖。
- 同步 README、双语用户指南、开发/需求/发布说明和变更记录，移除临时未验收提示；明确留空保留密钥、保存后测试、结果分类和模型视觉能力边界。
- 便携包 dist/WordsCat-0.3.6-win-x64.zip：53,290,006 字节，351 文件，随包 .NET Core/Windows Desktop 10.0.14。SHA-256：225b926d261685e16b321a9cc65f003d26980494f48c8005176ffc35d824314c。空 DOTNET_ROOT 下解压程序自测通过，宿主跟踪确认加载包内 coreclr.dll。包来自未提交工作区，sourceDirty 为 true，未上传。
- 未读取真实 Key、请求模型服务、改动用户 Data、暂存区或 Git 历史；旧 fixture 精确字节保持。当前无本轮遗留项；完整证据见 docs/VALIDATION-v0.3.6.md。

## 2026-10-31 0.3.6 设置与首次引导：当时保存的进度（现已完成）

- 已实现设置页模型服务分区、DeepSeek/自定义 OpenAI 兼容预设、Base URL/模型/API Key 字段。Key 始终掩码、初始留空；留空保存不覆盖已保存值，取消清除草稿。DeepSeek 预设应用原默认值，自定义草稿在切换中保留。
- 已引入 ICredentialVault 测试隔离及生产 Windows 凭据实现。设置页/启动仅检查存在性和来源元数据，不解码 Key；实际测试或翻译才读取已保存 Key。ReadDock.DeepSeek.ApiKey.v1 不变，旧备注兼容，新密钥备注绑定 HTTPS origin，避免切换服务时沿用另一来源的密钥；无文件/环境密钥回退。维护和测试未调用真实凭据接口。
- ConnectionTester 使用单条固定 Reply OK.、max_tokens 8、最多 15 秒、零重试；按类型/HTTP 状态分类未配置、401、402/429、网络/超时、格式异常等，不回显响应体或异常。新输入 Key 或未保存的新地址须先保存，再测试保存的凭据；测试时禁用相关输入。
- ServiceSetupGuide 是可关闭的非模态引导，链接设置、允许稍后使用文本视图，并持久化 service_guide_seen。model_provider 对旧设置按原 Base URL 推断，不新增翻译功能。本地 OCR 保持原路径。
- 最新 build.ps1 -Offline 成功，无编译警告。最新隔离非 UI 自测通过，报告 .test-results/service-setup-trial-0d2dd9ac901b482e8a9807de5156f8eb/test-results.txt。最新 UI 试跑失败：Service setup regression: provider preset localizes immediately；报告同目录 ui/ui-test-failure.txt。更早的试跑 a7a11e2872074ce9a8ab2b09bc4b5b3a 在加入此即时切换断言前通过，不能代替最终验收。
- 当时待修复：设置中切换语言后，自定义预设 ComboBox 的显示文字未立即变成英文。该问题已在 2026-11-01 修复，回归覆盖原生显示缓存及草稿保留。
- 当时保存的验收和打包待办已在 2026-11-01 完成，详见上方完成记录。此历史段落保留早期试跑证据，不代表当前未完成项。仍未 commit/push 或发布。

## 2026-10-31 0.3.5 小面板与模型服务

- 小面板「复制译文」改为「显示原文 / 显示译文」，复用主界面本地 KaTeX、CSP、textContent 与公式字体；不增加页签、不改变原操作栏样式。按渲染高度收缩，保留原 330 逻辑像素上限及滚动。视觉结果传入本次识别原文，文本翻译传入本次原文；加载、错误及关闭释放切换缓冲。缺少 WebView2 仍可使用文本回退。
- 用户目视反馈字体偏大，小面板阅读字号由 18px 调为 16px，公式按比例缩小；主阅读器字号仍沿用原值。增加实际 DOM 字号回归，并在最终验收中验证。
- 引入 ITextModelClient、IImageModelClient 与 IModelService；OpenAiCompatibleClient 实现 chat completions。业务窗口依赖接口，配置通过 ModelRequestOptions 传入 BaseUrl、Model、总超时及重试次数。原默认 https://api.deepseek.com、deepseek-flash、thinking disabled 与原图精度参数保留在默认兼容预设；通用端点不携带 DeepSeek 扩展。
- 旧 base_url/model/vision_model、ReadDock.DeepSeek.ApiKey.v1、命名空间、笔记与历史格式不变；新增 request_timeout_seconds 默认 100、max_retries 默认 0。默认不自动重试；显式启用时只重试 429/5xx/传输故障，总截止时间覆盖退避；401 和非 JSON 不重试。没有配置文件/环境密钥回退，本地 Windows OCR 路径不变。
- 已用固定假值及本地 HTTP handler 覆盖新旧配置往返、凭据目标常量、通用/默认请求、图片能力、401/429、重试边界、超时/取消与非 JSON；新小窗实际 STA 回归覆盖原文/译文公式、字体、惰性 HTML、滚动、新选区和错误重置。串行试跑报告 .test-results/model-popup-trial-d99b835c228f48bea3c7aa0264391086/ 通过。最终验收与目视检查见 docs/VALIDATION-v0.3.5.md。
- 最终 build.ps1 -Offline 无警告，verify.ps1 -IncludeUi 通过，报告 .test-results/c327184dcf1d4d44bfdd809a7c6b9430/，包含实际 DOM 16px 字号断言及全部旧回归。verify 前置 scripts/check-secrets.ps1 对全工作区/暂存区（含 ZIP 和忽略产物）扫描零命中。Computer Use 对实际新 ZIP 组件目视检查原文/译文按钮、公式与 16px 字号，测试预览已关闭。
- 最终便携包 dist/WordsCat-0.3.5-win-x64.zip 为 53,276,844 字节，351 文件，随包 .NET 10.0.14；校验值见 dist/SHA256SUMS.txt，实际解压后的程序离线自测通过。包仅本地生成、sourceDirty 为 true，未上传 GitHub。用户 Data、OCR 脚本和旧 fixture 精确字节未改，Git 暂存区未变；不 commit/push，无本轮未完成项。

## 2026-10-31 .NET 10 迁移与验证

- TargetFramework 由 net8.0-windows 改为 net10.0-windows，移除自包含发布固定的 RuntimeFrameworkVersion 8.0.33，交由 .NET 10 SDK 选择对应运行时补丁。应用版本保持 0.3.4，业务逻辑不改。
- 已检查 build.ps1、verify.ps1、scripts/ 下所有脚本；没有其他 SDK/运行时版本固定值，仓库没有 global.json。README、双语用户指南、开发/发布说明及 AGENTS 同步 .NET 10 SDK 与 Windows Desktop Runtime 要求；历史验证记录不改写。
- 用户安装 SDK 10.0.403 与 Windows Desktop Runtime 10.0.14 后完成真实编译。首次发现五处 WFO1000 属性序列化错误，按原默认值增加 DefaultValue，并为仅运行时使用的 PreserveOnClose 明确隐藏设计器序列化；DataGridView 按钮标题增加空字符串回退修复 CS8601。均未改变业务逻辑、UI 默认值或兼容标识。
- build.ps1 成功，无编译警告。NuGet 仅 WebView2 1.0.3595.46，保留原版本并通过真实公式/字体验证，不做无关升级。
- verify.ps1 -IncludeUi 在正常桌面权限下全部通过，最终报告 .test-results/4195722ba080404aab7d4f74ebde9c5a/。受限环境首轮 WebView2 停在 initializing，失败报告 .test-results/015152e39e314dc5a094750261a08698/ 保留，未用模拟输出代替。
- scripts/check-source.ps1 通过，检查 124 个源码文件；包含全工作区/暂存区密钥守卫，零命中。移除 README、双语指南、开发/发布说明和变更记录中的临时未验证提示。详见 docs/VALIDATION-v0.3.4.md。
- 已重新生成 dist/WordsCat-0.3.4-win-x64.zip（53,268,461 字节，351 个文件），随包 .NET Core 与 Windows Desktop 均为 10.0.14；SHA-256 见 dist/SHA256SUMS.txt。实际解压至 .test-results/net10-portable-29762061ffe94a1180d04ff98ad8a456/，离线自测与实际窗口回归均通过，空 DOTNET_ROOT 启动跟踪确认使用包内 CoreCLR，解压目录密钥扫描零命中。Computer Use 目视检查双栏公式、主界面和设置页，测试预览已关闭。此包仅本地生成，sourceDirty 为 true，未上传 GitHub。
- 未读取真实密钥、调用模型服务、改动用户 Data 或 Git 暂存区/历史，不 commit/push。原有混合 DPI、多屏、真实翻译质量与签名/安装程序等边界不变。

## 2026-10-31 API 密钥安全专项

- 初步扫描覆盖 710 个文本文件，没有疑似密钥字面量命中；新增守卫继续覆盖隐藏本地配置、被 Git 忽略的构建/发布/测试目录、UTF-8/UTF-16 编译字符串、ZIP/NuGet 内文件，以及真实 Git index blob。Git 内部历史不扫描、不改写。输出只包含位置，可选类型；不读取凭据管理器或环境变量中的密钥。
- 新增共享 SecretRedactor、设置 JSON 字符串序列化脱敏；处理异常/诊断文件、状态/错误显示、HTTP 返回内容、传输异常和历史记录。HTTP 错误不输出响应体或 Authorization；敏感传输异常不保留内层异常。历史只在内存或后续正常写入时脱敏，本轮没有打开真实用户历史或改动 Data。
- 生产请求继续只用凭据管理器中的已保存值；设置中的连接测试必须使用已保存凭据。开发 --vision-check 诊断入口禁用，不再读取真实 Key 或发送图片。测试统一固定假值与本地 Mock；补充设置文件、加密历史、旧历史内存脱敏、嵌套异常和模型回显回归。
- 新增 scripts/check-secrets.ps1、scripts/test-secret-guard.ps1 和 .githooks/pre-commit；接入 verify.ps1 与 scripts/check-source.ps1。根 AGENTS.md、开发说明和忽略规则已补充安全约定；本轮不修改 Git hooksPath，启用命令由用户自行执行。
- 负向测试已通过：UTF-8、UTF-16、编译字符串、ZIP 条目和暂存区独有探针均非零退出，输出仅位置；探针及独立测试索引在 finally 中删除，没有执行 commit。旧模板 fixture 的 Git blob 保持不变。开发视觉诊断实际调用验证返回禁用提示，不读取图片或凭据。离线构建使用 --no-restore，不访问 NuGet。
- 本轮没有发现需要删除的真实硬编码密钥。第三方原生 DLL 的空 Authorization 模板曾误报，已通过排除字符串结束符修正规则，未排除第三方目录。扫描结果仅是当前内容模式审计，不代表旧 Git 历史已清理。
- 最终离线 build.ps1 -Offline 无警告成功，verify.ps1 通过（含全目录/index 守卫及新增安全回归），报告 .test-results/f1663b1600784b21998b1d8d52b4842c/。check-source.ps1 通过，检查 124 个源码文件，并再次执行全工作区/index 守卫，零命中。HEAD 与真实暂存区保持不变；本轮没有联网、读取真实凭据、改动用户 Data、提交、推送或历史重写。完整改动清单与验收见 docs/SECURITY_AUDIT.md。

## 2026-10-31 英文项目整理与 GitHub 准备

- 项目入口迁至 src/WordsCat/WordsCat.csproj；源码按 UI、Reading、Persistence、Services、Platform、Localization、Models、Diagnostics 分目录。OCR 脚本在 scripts/，默认模板在 templates/，资源在 assets/。根目录 build.ps1/verify.ps1 继续可用。
- README、开发指南、英文用户指南及 AGENTS.md 使用英文；详细中文用户指南保留为 docs/USER_GUIDE.zh-CN.md，所有待上传路径采用英文。保留 ReadDock namespace、凭据目标、数据路径、互斥体和笔记标记等兼容标识。
- 加入第三方资源说明和微软 WebView2 原始许可证；KaTeX 原许可证和完整字体保留。新增 scripts/check-source.ps1 检查英文路径、私有/构建文件和常见凭据格式；不读取系统凭据。旧模板 fixture 的 Git blob 不变。
- 新目录独立构建到 .test-results/repository-publish，无警告；完整离线回归及实际 STA 窗口验证通过，报告 .test-results/37ef6c20d133435d85d779cc87b4c2fa/。确认 OCR、模板、图标、公式字体、用户指南及第三方许可进入干净输出目录。
- 用户明确要求公开仓库 WordsCat，并已创建 gaomy199/WordsCat。GitHub 连接仍返回 403，故改用本机已有 Git Credential Manager 登录；指定 username=gaomy199 后可非交互读取凭据并验证同名 GitHub 账号，不输出或保存 Token。此前未指定账号时有两个登录，无法自动选择，不能据此认定本机没有凭据。
- 本轮没有读取真实模型 Key、调用模型或上传私有文献。详见 docs/REPOSITORY_PREPARATION.md。
- 本地整理快照为 451dd6d，后续 2128179 记录首次上传阻塞。用户重新连接后仓库已是空仓库，当前 ID 为 1440444826；重新检查 refs 确认没有分支，原生 Git 可连通。GitHub connector 的仓库列表仍为空，创建 README 也返回 403；这只说明 connector 无访问权，不代表原生 Git 登录无效。根目录已配置 origin 指向公开仓库；当前上传不需要用户再提供 Token。
- 已通过原生 Git 成功上传 main，首次完整源码提交 d2ab4975f2c3c7a16f4f60d98a63fa55702aab4b，远端 Git tree 为 2c2b7d2f49512084dccb51f4a3b6918b9da8b439，与本地完全一致。GitHub API 确认仓库公开、默认分支 main，递归树含 115 个文件；无 Data、私有 fixtures、配置、凭据、EXE/DLL 或构建/测试产物。本地 main 跟踪 origin/main，旧 codex/readdock 分支保留。之后的接续记录提交只更新文档，并继续同步到同一分支。

## 0.3.3 本轮改动与验证

- 悬停约 350ms 展开浮标菜单；鼠标离开浮标及菜单后约 250—325ms 自动收起，移入列表重置关闭等待。轮询计时器只在该浮标菜单打开时运行，不影响托盘菜单。
- 单击切换展开/收起，支持连续第三次点击重新展开。浮标展开菜单时暂时使用非模态 AutoClose=false，由浮标管理离开、重复点击、菜单操作和 Escape 的关闭；关闭恢复 AutoClose=true，避免系统菜单消费同一次点击后又重新弹出。菜单操作仍只执行一次，拖动保存位置而不展开。
- 真实鼠标检查发现关闭非模态 ToolStrip 后其来源浮标可能落到主窗口后面；关闭后恢复浮标置顶并重绘，不激活阅读器之外的窗口。修正后连续三次点击及悬停/移出检查通过。
- 左侧增加带提示及中英文无障碍名称的收起/展开图标。展开宽 260、收起宽 60 逻辑像素；收起保留窄栏按钮，隐藏品牌和历史而不释放数据，双栏阅读区扩展。sidebar_collapsed 写入已有设置，旧配置默认展开，保存其他设置保留该字段。
- 品牌所在行由 68 改为 56，顶部留白由 18 改为 12 逻辑像素，使标题中心上移约 12；字号不变。首次实际布局发现默认自动列宽使收起按钮越界，已改为百分比约束并加父容器边界回归。
- build.ps1 无警告成功；verify.ps1 -IncludeUi 全部通过，最终完整报告 .test-results/2676cdead5704d69891ce627006dac98/。新增回归覆盖菜单开关、移入保持、跨越等待/重置、移出关闭、动作执行一次、拖动、托盘 AutoClose 恢复，以及三轮侧栏折叠和状态持久化、原文/历史保留、按钮边界及阅读宽度恢复。详见 docs/VALIDATION-v0.3.3.md。
- Computer Use 已在隔离离线目录检查实际鼠标开关、悬停、移出、选择设置后无菜单残留，以及侧栏收起/展开和启动恢复收起状态。没有读取真实 Key、请求模型、上传文献或改写用户 E 盘程序及 Data。原有混合 DPI/多屏等边界继续保留。

## 0.3.2 本轮改动与验证

- 补齐粘贴原文、打开单词本、退出的矢量图标；设置导航把图标和文字按一组居中，固定间距 8 逻辑像素，中英文短标题不再产生过大留白。
- 原按钮直接用 Focused 绘制强调边框，鼠标点击后会一直保留。现在区分应用内鼠标和键盘导航方式，点击完成、失去焦点或释放捕获时清除 hover/pressed 状态；鼠标操作不残留强调框，Tab/方向键/空格等键盘导航仍显示焦点。没有额外后台监听或服务。
- 默认图标是用户提供的 4.ico，精确复制为 assets/wordscat.ico（SHA-256：3CD502ADF719C3B9908D573CB50A3ED87E6FE87E7F045A2F2F54DB6165877CEE），编译为 EXE 图标并嵌入运行时资源；用于主窗口、设置等窗口、托盘、浮标和默认 Logo 预览，不依赖用户图片绝对路径。已有自定义 Logo 仍优先使用。
- 左上角仅显示当前语言名称：中文「单词猫」、英文「WordsCat」，不再显示另一语言副标题。
- 删除手动方向下拉，改成只读自动方向提示。文本依据正文汉字数与英文词数判断，忽略 LaTeX 数学块、代码/链接和单字母变量；英文→中文、中文→英文。界面语言独立；只识别方向，不在每次输入时自动请求 API。截图视觉提示先识别正文语言，再翻译为另一语言。
- 配置中保留旧 translation_direction 字段以兼容，旧手动选择迁移为 auto。新历史仍存实际方向；旧历史可正常打开，追问按该历史方向关联当前选区。交换把首次译文放入左侧并重新识别，清除旧译文和追问；修改已翻译原文也清除旧上下文。
- 首次 UI 回归发现隐藏原文编辑页尚未创建句柄时不发送 TextChanged，导致自动标题未更新；已在主窗口首次显示时创建原生编辑句柄并重测通过。
- build.ps1 无警告成功；verify.ps1 -IncludeUi 全部通过。最终完整报告 .test-results/1552374d5c2a4ba0a96d49cecc7b9705/；新增验证覆盖自动方向、混排/公式、旧方向迁移、连续双语序列化请求独立、自动标题与交换、单一名称及默认 ICO/自定义图标恢复。实际 UI 检查及边界见 docs/VALIDATION-v0.3.2.md。
- 未读取真实 Key、调用模型服务或上传文献，用户 E 盘正在使用的旧版程序及其 Data 未改动。主界面和设置页面已用隔离离线预览检查。

## 0.3.1 本轮改动与验证

- 名称移至最左侧栏顶端：中文名与英文名分行显示，标题行留足字体高度，历史位于其下。移除历史栏底部的保留说明；历史行为不变。
- 使用原生 WinForms 自绘圆角按钮、圆形交换按钮、圆角阅读/历史/设置卡片、统一细边框输入容器。调整浅色背景为柔和冷白，保留原主题色和用户 Logo。重要操作与设置页签有矢量图标，选中页签使用主题色；下拉框的外框与箭头跟随主题，深色不再残留原生亮色内框。没有新增依赖或替换技术栈。
- 设置中的语言与主题短选项限制为 290 逻辑像素宽；模型下拉 450，条数 180；其他输入按可用宽度显示。单行、多行、可编辑下拉和数字框都有统一边框、内边距及焦点状态。
- 语言改为选择即预览，同时更新设置页、主窗口、页签、菜单及动态标题；保存后保留，取消恢复原语言，与主题行为一致。未在预览时修改已保存配置或用户内容。修正表格表头在双语反复切换时失去原始文案的问题。
- 首次视觉检查发现图标按钮文字过早省略，以及主界面新增容器默认高度撑大自动行；已修正文字测量/绘制边距、容器默认高度和主界面行高，并补充输入框及其父行边界检查。
- `build.ps1` 无警告成功，`verify.ps1 -IncludeUi` 全部通过。最终报告 `.test-results/8ac49813a132403e9e75d9dcbf047490/`；新增验证包括左侧品牌位置/高度、删除说明、输入/容器不越界、紧凑语言选择、主窗口与设置页同时即时切换、取消恢复、保存持久化和双语表头恢复。详见 `docs/VALIDATION-v0.3.1.md`。
- 本轮以实际 UI 检查为重点，未读取真实 Key、未请求模型或上传文献。已有翻译、收藏、历史、公式渲染等离线回归继续通过；没有声称新增真实翻译质量证据。

## 0.3.0 本轮改动与验证

- 产品名称为 WordsCat / 单词猫，正式入口 `publish/WordsCat.exe`；默认「猫」字图标，自定义 Logo 保留。源码目录、namespace、凭据目标和旧数据格式的兼容标识继续使用 ReadDock，升级保留 Data 即可继续使用原 Key、笔记、模板与历史。用户当前另外运行的 E 盘旧 ReadDock 没有被改动，使用新版需退出旧程序后启动正式工作区的 WordsCat.exe。
- 设置 → 外观可选简体中文 / English，保存立即应用到主窗口、阅读页签、托盘菜单与后续对话框，取消不生效，重启保留。不翻译或改写用户笔记、路径、模板内容及原文/译文。
- 历史移到左侧；主操作区左原文、右译文。保存到、置顶、阅读提示、PDF 断行选项均为与单词本选择相同的 10pt 字号，保存目标居中对齐。
- 删除追问区上方说明，框内只保留「输入语法、句意或追问当前选区其他问题」；输入框与按钮顶边/高度一致，Enter 发送。底部保留单独的运行状态行，初始仅显示就绪。
- 增加英→中 / 中→英方向，独立于界面语言；文本、粘贴、划词和截图使用相同方向。方向切换保留原文，释放旧译文、问题与当前会话；交换把首次译文放到左侧并反转方向，不自动联网。追问用界面语言回复，方向说明不覆盖回复语言。
- 历史记录附带方向，打开时恢复；旧记录默认英→中。只迁移旧版内置默认翻译要求为方向中立文案，自定义要求原样保留；明确方向优先于自定义要求中冲突的语言要求。
- `build.ps1` 无警告成功，`verify.ps1 -IncludeUi` 全部通过。最终报告 `.test-results/68e5368617e843fc84609f879bc5b3f9/`；本地自动验证与实际界面检查结论见 `docs/VALIDATION-v0.3.0.md`。普通沙箱进程曾导致 WebView2 启动失败；正常桌面验证已通过。
- 本轮没有读取真实 Key、访问模型服务或上传私有样本；中→英真实输出质量未作联网评估，复杂公式/混合 DPI 等原有边界继续保留。

## 0.2.2 本轮改动与验证

- 轻量弹窗按实际换行后的内容测量高度，短译文收缩，达到原 330 逻辑像素上限后纵向滚动；保留选区附近定位和屏幕边界限制。
- 修正 0.2.1 中跨源本地页面导致 KaTeX 数学字体未加载的问题：固定本地同源 reader.html/CSS/JS/字体，动态文本只通过 textContent 放入内存 DOM。英文正文 Times New Roman，公式使用 KaTeX 专用数学字体；清理独立公式旁的重复空白行。
- 主界面右侧新增最近翻译列表，可打开原文/译文；默认 100 条，设置范围 0—1000。缩小上限先告知实际将删除的最早条数并警告软件内不可恢复，确认才删除。0 关闭并清空。超限自动淘汰最早记录，不保留历史备份；已收藏 Markdown 不受影响。
- 截图不入历史；新选区、粘贴新原文、清空和点 × 释放当前截图、输入、译文和追问。最小化保留当前内容和任务栏按钮；点 × 隐藏到托盘，程序继续运行。
- Windows OCR 临时图片 finally 删除；启动清理遗留的精确 capture-GUID.png 和已退出进程的 WebView2 缓存。渲染器每次运行使用独立 InPrivate 缓存，退出后短暂后台清理副本等待浏览器释放文件锁再删除；无常驻服务。崩溃/强制结束遗留在下次启动补清理。配置、笔记、备份及 fixtures/private 样本保留。
- `build.ps1` 无警告成功；`verify.ps1 -IncludeUi` 全部通过。最终报告 `.test-results/f96d8ca5d1a84c20af6ed428cbed11c0/`，详见 `docs/VALIDATION-v0.2.2.md`。
- 自动验证覆盖加密历史重启读取、100→10 精确淘汰、取消/确认警告分支、设 0 后不写历史、损坏文件保护、缓存限定清理、真实 KaTeX_Main/KaTeX_Math 字体加载、短/长弹窗、历史栏恢复、最小化及 ×、连续切换。
- Computer Use 查看了实际 Times New Roman 英文、标准数学分式/上下标和右侧历史栏。没有读取真实 Key 或再次上传文献调用模型。
- 当前限制：历史仅保留首次完成的原文/译文，不保留截图和后续追问；DPAPI 历史只能由原 Windows 账户解密。这里的“不可恢复”指软件没有撤销/备份入口，不承诺存储介质层面的取证擦除。Windows 后台焦点规则、多屏和未复制文字的受限网页仍按原边界复测。

## 0.2.1 本轮改动与验证

- 主界面原文/译文增加离线公式阅读页；原文保留编辑页及原始截图页。识别和翻译提示要求保留段落、独立公式和公式编号，内部仍保存原始 LaTeX。
- 从结果小窗打开主界面时关闭并释放小窗，恢复窗口并在关闭事件完成后再次请求前台激活；隐藏期间的新选区会在显示时刷新阅读视图。
- 网页取词前先关闭浮标菜单和旧结果窗，延长阅读窗口恢复与浏览器剪贴板更新等待，检查当前前台仍是来源窗口。
- 模式/强调色/亮度/Logo 改动立即在全应用预览；保存保留、取消恢复。禁用按钮自行绘制当前主题的文字颜色，去除系统浅色阴影。
- 自定义浮标 Logo 使用固定中性背景与边框，不再铺主题强调色；默认「词」图标仍跟随强调色。
- `build.ps1` 成功，零编译警告；`verify.ps1 -IncludeUi` 全部通过。报告：`.test-results/5217ad776f234259930dfe93020e9f4b/`，持久验证说明见 `docs/VALIDATION-v0.2.1.md`。
- 使用 Computer Use 查看了原文/译文中的实际分式、上下标与独立公式；Chrome 本地合成网页 Ctrl+Alt+D 成功进入结果小窗，并用 Ctrl+Alt+R 展开主界面。自动 STA 界面回归覆盖连续三次结果小窗按钮展开、窗口可用/恢复及最新选区显示。
- 后台自动测试不能证明 Windows 前台焦点策略的所有情况；不同阅读器、混合 DPI、多屏及具体受限网页仍需用户复测。没有读取真实 Key、上传用户文献或调用模型服务。
- `--offline-preview --data-dir <隔离目录>` 可进行不读取凭据的本地预览；无 data-dir 时使用 publish/PreviewData。`--render-self-test` 是独立 STA 消息循环测试，避免在原有无界面异步自测中创建 Form 造成上下文死锁。

## 后续维护记录格式

每次修改后更新当前版本、具体行为变化、验证结果、已知限制和未完成事项；在 `CHANGELOG.md` 补充版本记录。重要设计决定同步到需求文档。
