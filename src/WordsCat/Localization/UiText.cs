using System.Runtime.CompilerServices;

namespace ReadDock;

// Only application-owned captions pass through this table. Editors, notebook names,
// paths, model output and custom template contents remain user data.
internal static class UiText
{
    public static string Language { get; set; } = "zh-CN";
    public static string T(string text)
    {
        if (Language != "en") return text;
        if (English.TryGetValue(text, out var value)) return value;
        return text.EndsWith(" *", StringComparison.Ordinal) ? T(text[..^2]) + " *" : text;
    }
    public static string F(string text, params object[] values) => string.Format(T(text), values);
    sealed class Caption { public string Original = ""; public bool Applying; }
    static readonly ConditionalWeakTable<Control, Caption> captions = new();
    static readonly ConditionalWeakTable<ToolStripItem, Caption> menuCaptions = new();
    static readonly ConditionalWeakTable<DataGridViewColumn, Caption> columnCaptions = new();
    static readonly ConditionalWeakTable<DataGridViewButtonColumn, Caption> columnButtons = new();

    public static void Apply(Control control)
    {
        if (control is Label or ButtonBase or TabPage or Form)
        {
            if (!captions.TryGetValue(control, out var caption))
            {
                caption = new Caption { Original = control.Text }; captions.Add(control, caption);
                var tracked = caption;
                control.TextChanged += (_, _) =>
                {
                    if (tracked.Applying) return;
                    tracked.Original = control.Text;
                    tracked.Applying = true;
                    try { control.Text = T(tracked.Original); } finally { tracked.Applying = false; }
                };
            }
            caption.Applying = true;
            try { control.Text = T(caption.Original); } finally { caption.Applying = false; }
        }
        if (control is DataGridView grid)
            foreach (DataGridViewColumn col in grid.Columns)
            {
                col.HeaderText = T(columnCaptions.GetValue(col, c => new Caption { Original = c.HeaderText }).Original);
                if (col is DataGridViewButtonColumn button) button.Text = T(columnButtons.GetValue(button, c => new Caption { Original = c.Text ?? "" }).Original);
            }
        foreach (Control child in control.Controls) Apply(child);
        control.Invalidate();
    }
    public static void ApplyMenu(ContextMenuStrip menu)
    {
        foreach (ToolStripItem item in menu.Items)
        {
            var caption = menuCaptions.GetValue(item, i => new Caption { Original = i.Text ?? "" });
            item.Text = T(caption.Original);
        }
    }
    public static DialogResult Show(IWin32Window owner, string text, string title, MessageBoxButtons buttons = MessageBoxButtons.OK,
        MessageBoxIcon icon = MessageBoxIcon.None, MessageBoxDefaultButton defaultButton = MessageBoxDefaultButton.Button1)
        => MessageBox.Show(owner, SecretRedactor.Redact(T(text)), SecretRedactor.Redact(T(title)), buttons, icon, defaultButton);
    public static DialogResult Show(string text, string title) => MessageBox.Show(SecretRedactor.Redact(T(text)), SecretRedactor.Redact(T(title)));

    static readonly Dictionary<string, string> English = new()
    {
        ["模型服务"] = "Model service", ["服务预设"] = "Service preset", ["模型名"] = "Model name",
        ["自定义 OpenAI 兼容"] = "Custom OpenAI-compatible",
        ["测试连接"] = "Test connection", ["视觉模型（留空时使用文本模型）"] = "Vision model (leave blank to use the text model)",
        ["模型视觉 · 数学与扫描文献"] = "Model vision · Math and scanned papers",
        ["已保存密钥；留空保留，输入新密钥替换。"] = "Key saved. Leave blank to keep it, or enter a replacement.",
        ["粘贴此服务的 API Key。"] = "Paste the API key for this service.",
        ["密钥只保存到 Windows 凭据管理器，留空保留已保存密钥。\n输入新密钥后请先保存设置，再测试连接。"] = "Keys are stored only in Windows Credential Manager. Leave blank to keep the saved key.\nSave settings after entering a new key, then test the connection.",
        ["视觉模式发送当前截图；本地模式先使用 Windows OCR。测试连接只验证文本服务。"] = "Vision sends the current capture; local mode uses Windows OCR first. Connection testing checks text service only.",
        ["正在测试连接，仅发送简短测试请求…"] = "Testing connection with a short request…",
        ["连接成功。文本服务已响应；此测试不验证视觉识别。"] = "Connected. The text service responded; this test does not verify vision.",
        ["尚未配置此服务的 API Key。请填写密钥并保存设置。"] = "No API key is configured for this service. Enter a key and save settings.",
        ["请先保存新密钥或服务地址，再测试已保存的凭据。"] = "Save the new key or service address before testing the saved credentials.",
        ["401：API Key 无效或已失效，请更新密钥。"] = "401: The API key is invalid or expired. Update the key.",
        ["402/429：额度不足或请求过于频繁，请检查服务商额度后重试。"] = "402/429: Insufficient quota or rate limit. Check your provider quota and try again later.",
        ["网络连接失败或请求超时，请检查网络和 Base URL。"] = "Network connection failed or timed out. Check the network and Base URL.",
        ["返回格式异常，接口可能不兼容 OpenAI chat completions。"] = "Unexpected response format. The endpoint may not support OpenAI chat completions.",
        ["请填写有效的 HTTPS Base URL 和模型名。"] = "Enter a valid HTTPS Base URL and model name.",
        ["连接测试已取消。"] = "Connection test cancelled.",
        ["离线预览不读取密钥，也不请求真实服务。"] = "Offline preview does not read keys or contact real services.",
        ["服务暂时不可用，请检查服务地址或稍后重试。"] = "Service unavailable. Check the service address or try again later.",
        ["API Key 格式无效，请重新粘贴完整密钥。"] = "Invalid API key format. Paste the complete key again.",
        ["更换服务地址时，请输入并保存对应服务的新密钥。"] = "When changing service address, enter and save the key for the new service.",
        ["设置模型服务"] = "Set up model service", ["先设置一个模型服务"] = "Set up a model service",
        ["尚未配置 API Key。可前往设置选择服务并保存密钥。\n你也可以稍后设置，继续使用文本视图。"] = "No API key is configured. Open Settings to choose a service and save a key.\nYou can also set it up later and keep using the text view.",
        ["打开设置"] = "Open settings", ["稍后设置"] = "Set up later",
        ["无法检查已保存密钥，请在设置中检查模型服务。"] = "Could not check the saved key. Check the model service in Settings.",
        ["显示原文"] = "Show source", ["显示译文"] = "Show translation",
        ["单词猫 WordsCat · 中英阅读助手"] = "WordsCat · Chinese / English reading",
        ["单词猫  /  WordsCat"] = "WordsCat  /  单词猫",
        ["中英互译"] = "Chinese / English translation", ["单词猫"] = "WordsCat", ["WordsCat"] = "单词猫",
        ["在阅读中理解，在语境中积累。"] = "Understand as you read. Learn words in context.",
        ["中英互译 · 手动收藏 · 本地历史"] = "Translate · Save words · Local history",
        ["单词猫 WordsCat · Ctrl+Alt+S 截图翻译"] = "WordsCat · Ctrl+Alt+S capture translation",
        ["单词猫 · 设置"] = "WordsCat · Settings",
        ["单词猫 · 收藏生词"] = "WordsCat · Save vocabulary",
        ["单词猫 · 轻量翻译"] = "WordsCat · Translation",
        ["单词猫 · 框选截图"] = "WordsCat · Capture",
        ["单词猫 · 悬浮图标"] = "WordsCat · Floating dock",
        ["划词翻译"] = "Select text", ["截图翻译"] = "Capture", ["粘贴原文"] = "Paste",
        ["收藏所选词"] = "Save word", ["打开单词本"] = "Notebook", ["设置"] = "Settings", ["退出"] = "Exit",
        ["收起侧边栏"] = "Collapse sidebar", ["展开侧边栏"] = "Expand sidebar",
        ["01   原文"] = "01   Source", ["02   译文"] = "02   Translation",
        ["自动识别 · 中英互译"] = "Auto · Chinese / English",
        ["自动 · 中文 → 英文"] = "Auto · Chinese → English", ["自动 · 英文 → 中文"] = "Auto · English → Chinese",
        ["译文已放入左侧，已自动识别语言，可重新翻译。"] = "Translation moved to the source. Language detected; translate when ready.",
        ["保存到"] = "Save to", ["窗口置顶"] = "Always on top", ["英文 → 中文"] = "English → Chinese", ["中文 → 英文"] = "Chinese → English",
        ["交换"] = "Swap", ["01   英文原文"] = "01   English source", ["01   中文原文"] = "01   Chinese source",
        ["02   中文译文"] = "02   Chinese translation", ["02   英文译文"] = "02   English translation",
        ["阅读公式 · 编辑原文 · 查看原始截图；可选词收藏。"] = "Read formulas · Edit source · View capture · Select words to save",
        ["保留段落与独立公式；追问限于当前选区。"] = "Paragraphs and formulas preserved; questions use this selection.",
        ["阅读"] = "Read", ["编辑原文"] = "Edit source", ["原始截图"] = "Capture", ["文本"] = "Text",
        ["翻译当前原文"] = "Translate source", ["合并 PDF 断行"] = "Join PDF lines", ["清空本次内容"] = "Clear selection",
        ["撤销收藏"] = "Undo save", ["取消请求"] = "Cancel", ["追问"] = "Ask", ["追问："] = "Question: ",
        ["输入语法、句意或追问当前选区其他问题"] = "Ask about grammar, meaning, or anything in this selection",
        ["就绪"] = "Ready", ["最近翻译"] = "Recent translations", ["最近翻译  {0} / {1}"] = "Recent  {0} / {1}",
        ["打开记录"] = "Open record", ["清空历史"] = "Clear history",
        ["仅保留已完成的原文与译文，截图不入历史。\n设置中可调整条数，设为 0 关闭。"] = "Completed source and translation only; captures are excluded.\nSet the limit in Settings; 0 disables history.",
        ["划词翻译          Ctrl+Alt+D"] = "Translate selection          Ctrl+Alt+D",
        ["截图翻译          Ctrl+Alt+S"] = "Capture & translate          Ctrl+Alt+S",
        ["收藏单词          Ctrl+Alt+W"] = "Save word          Ctrl+Alt+W",
        ["手动修改 / 粘贴原文"] = "Edit / paste source", ["撤销最近收藏"] = "Undo last save",
        ["显示 / 隐藏主窗口"] = "Show / hide main window", ["显示 / 隐藏悬浮图标"] = "Show / hide floating dock", ["退出单词猫"] = "Exit WordsCat",
        ["让工具适应你的阅读习惯"] = "Make WordsCat fit the way you read",
        ["模型与识别"] = "Models & OCR", ["单词本与模板"] = "Notebooks", ["外观"] = "Appearance", ["阅读偏好"] = "Reading",
        ["文本翻译模型（可编辑）"] = "Text model (editable)", ["截图识别方式"] = "Capture recognition",
        ["视觉识别模型（需要支持图片，默认 deepseek-flash）"] = "Vision model (image support required; default: deepseek-flash)",
        ["API 基础地址"] = "API base URL",
        ["DeepSeek 视觉 · 数学与扫描文献"] = "DeepSeek vision · Math and scanned papers",
        ["Windows 本地 OCR · 仅上传识别文字"] = "Windows local OCR · Send recognized text only",
        ["视觉模式：仅将本次框选图片发给所选 API，一次完成识别和翻译。\n本地模式：Windows OCR 识别后，仅发送文字；数学斜体及上下标可能误读。\nKey 保存在 Windows 凭据管理器；本地翻译历史条数可在阅读偏好调整。"] = "Vision: sends this capture to the selected API for recognition and translation.\nLocal OCR: sends recognized text only; math symbols may be misread.\nThe Key stays in Windows Credential Manager. Set history limits under Reading.",
        ["测试文本连接"] = "Test connection", ["获取 API Key"] = "Get API Key",
        ["按领域添加多个 Markdown 文件。主窗口和收藏窗口都可切换；启动时使用默认单词本。"] = "Add Markdown notebooks by subject. Switch in either window; startup uses the default.",
        ["添加单词本"] = "Add notebook", ["移除所选项"] = "Remove selected", ["字段与排版模板 JSON"] = "Fields and Markdown template (JSON)",
        ["选择模板"] = "Choose template", ["编辑模板"] = "Edit template", ["检查模板"] = "Check template", ["使用精简模板"] = "Use compact template",
        ["默认卡片仅保留：单词/词组、原形、词性、语境释义、原句及译文。\n移除单词本只移除配置项，Markdown 文件保留。自定义模板不会自动覆盖。"] = "Default fields: word, lemma, part of speech, meaning, example and translation.\nRemoving a notebook keeps its file. Custom templates are preserved.",
        ["界面语言（保存后生效）"] = "Interface language (applied on save)", ["简体中文"] = "简体中文",
        ["界面语言"] = "Interface language",
        ["语言和外观立即预览；保存后保留，取消则恢复。Logo 会复制到 Data/Appearance。"] = "Language and theme preview immediately. Save to keep; cancel to restore. Logos copy to Data/Appearance.",
        ["界面模式"] = "Theme", ["浅色"] = "Light", ["深色"] = "Dark", ["跟随系统"] = "System",
        ["主题强调色（#RRGGBB）"] = "Accent color (#RRGGBB)", ["选择颜色"] = "Choose color", ["预览外观"] = "Preview theme",
        ["背景亮度（左暗 / 右亮）"] = "Background brightness (dark / bright)",
        ["自定义 Logo（PNG / JPG / ICO；应用于悬浮、托盘和窗口图标）"] = "Custom logo (PNG / JPG / ICO; dock, tray and window icons)",
        ["选择图片"] = "Choose image", ["恢复默认 Logo"] = "Reset logo",
        ["外观调整立即预览；保存后保留，取消则恢复。Logo 会复制到 Data/Appearance。"] = "Theme changes preview immediately. Save to keep; cancel to restore. Logos copy to Data/Appearance.",
        ["保留最近 N 条翻译历史（默认 100；0 为关闭并清空）"] = "Recent translations to keep (default: 100; 0 disables and clears)",
        ["仅保留已完成的原文和译文，截图不写入历史。\n保存后将永久删除最早的 {0} 条记录，软件内无法恢复；已收藏单词不受影响。"] = "Completed text only; captures are excluded.\nSaving permanently removes the oldest {0} records. This cannot be undone; saved words are kept.",
        ["翻译要求"] = "Translation instructions",
        ["当前阅读术语表（每行一条，例如：robustness = 稳健性）"] = "Reading glossary (one per line, e.g. robustness = 稳健性)",
        ["显示悬浮图标"] = "Show floating dock", ["启动时打开主窗口"] = "Open main window on startup",
        ["Ctrl+Alt+D  划词翻译\nCtrl+Alt+S  截图翻译\nCtrl+Alt+W  收藏单词\nCtrl+Alt+R  显示 / 隐藏主窗口\n截图后先在选区附近显示轻量弹窗，点击「打开主界面」再展开详细操作。"] = "Ctrl+Alt+D  Translate selection\nCtrl+Alt+S  Capture & translate\nCtrl+Alt+W  Save word\nCtrl+Alt+R  Show / hide main window\nCaptures first open a nearby popup. Choose Open main window for more options.",
        ["保存设置"] = "Save settings", ["取消"] = "Cancel", ["名称"] = "Name", ["Markdown 文件路径"] = "Markdown file path", ["默认"] = "Default", ["选择文件"] = "Browse",
        ["永久删除历史记录"] = "Permanently delete history",
        ["历史上限从 {0} 改为 {1}。\n将永久删除最早的 {2} 条本地记录{3}。\n删除后软件内无法恢复；已收藏的单词和 Markdown 不受影响。\n是否保存此设置？"] = "Change history limit from {0} to {1}.\nPermanently delete the oldest {2} local records{3}.\nThis cannot be undone. Saved words and Markdown are kept.\nSave this setting?",
        ["，并关闭历史保存"] = " and disable history",
        ["永久删除全部 {0} 条本地历史？删除后软件内无法恢复，已收藏单词不受影响。"] = "Permanently delete all {0} history records? This cannot be undone. Saved words are kept.",
        ["收藏前可以修改所有字段。"] = "Review and edit all fields before saving.", ["把语境一起留下"] = "Save the word with its context",
        ["释义结合原句 · 按你的模板保存到 Obsidian"] = "Contextual meanings · Your Markdown template for Obsidian",
        ["词条内容"] = "Entry", ["Markdown 预览"] = "Markdown preview", ["保存到单词本"] = "Save to notebook", ["AI 补全释义"] = "AI definitions",
        ["单词 / 词组"] = "Word / phrase", ["单词/词组"] = "Word / phrase", ["原形"] = "Lemma", ["词性"] = "Part of speech", ["语境释义"] = "Meaning in context", ["文献原句"] = "Source sentence", ["原句"] = "Example", ["原句译文"] = "Example translation",
        ["收藏单词"] = "Save word", ["复制译文"] = "Copy translation", ["打开主界面"] = "Open main window", ["仅处理本次选区"] = "Current selection only",
        ["可打开主界面修改原文或检查设置。"] = "Open the main window to edit the source or check settings.",
        ["框选文字区域 · 松开鼠标完成 · Esc / 右键取消"] = "Select text · Release to capture · Esc / right click to cancel",
        ["已切换翻译方向，请翻译当前原文。"] = "Direction changed. Translate the current source when ready.",
        ["请先完成翻译，再交换原文与译文。"] = "Translate first, then swap the source and translation.",
        ["已交换方向，译文已放入左侧，可编辑后重新翻译。"] = "Direction swapped. Edit the new source on the left, then translate.",
        ["已打开本地历史 · 原始截图不保留。"] = "History opened. Original captures are not stored.",
        ["本次翻译和追问已清空。已收藏的单词会保留。"] = "Selection and questions cleared. Saved words are kept.",
        ["请先完成或取消当前操作。"] = "Finish or cancel the current operation first.", ["已取消请求。"] = "Request cancelled.", ["已取消本次请求。"] = "Request cancelled.",
        ["正在翻译当前原文…"] = "Translating the current source…", ["正在解释当前选区…"] = "Answering about the current selection…",
        ["正在请求 DeepSeek，仅发送当前原文和阅读术语表…"] = "Requesting DeepSeek with this source and glossary only…",
        ["DeepSeek 视觉识别 · 正在发送本次框选图片…"] = "DeepSeek vision · Sending this capture…",
        ["请先选取、粘贴或输入原文。"] = "Select, paste or type the source text first.",
        ["请先翻译当前原文，再继续追问。"] = "Translate the current source before asking questions.", ["请输入想追问的问题。"] = "Enter a question.",
        ["追问请控制在 4000 个字符以内。"] = "Keep questions under 4,000 characters.",
        ["本段已追问多次。请重新翻译当前原文，再开始新一轮追问。"] = "Question limit reached. Translate this source again to start a new round.",
        ["本工具适合局部阅读，请将本次选区控制在 16000 个字符以内。"] = "Keep the selection under 16,000 characters.",
        ["请选择一个单词或短语，最多 160 个字符。"] = "Select a word or phrase under 160 characters.",
        ["请先在「设置」中填写 DeepSeek API Key。"] = "Enter your DeepSeek API Key in Settings first.",
        ["本次运行还没有可撤销的收藏。"] = "No saved entry to undo in this run.", ["已撤销最近一次收藏。"] = "Last save undone.",
        ["离线预览不读取凭据，也不发起模型请求。"] = "Offline preview does not read credentials or send model requests.",
        ["请先填写要收藏的单词或词组。"] = "Enter a word or phrase first.", ["正在结合当前原文补全释义…"] = "Generating definitions from this source…",
        ["没有可核验的文献原句，已留空。可以手动补充后保存。"] = "No verifiable source sentence was found. Add one manually before saving.",
        ["这个单词已经收藏过。\n\n是否保留本次语境，追加一张新卡片？\n原有卡片会保留。选择「否」可返回修改或取消。"] = "This word is already saved.\n\nAdd a new card with this context?\nExisting cards are kept. Choose No to edit or cancel.",
        ["发现重复单词"] = "Duplicate word", ["保存时发现重复单词，请再次点击保存选择处理方式。"] = "Duplicate found. Click Save again to choose how to proceed.",
        ["文件正被占用或无法写入。请检查路径、同步状态和文件权限后重试。"] = "File is busy or not writable. Check the path, sync status and permissions.",
        ["请输入有效的 HTTPS 基础地址。"] = "Enter a valid HTTPS base URL.", ["请填写文本模型与视觉模型名称。"] = "Enter text and vision model names.",
        ["请至少保留一个单词本。"] = "Keep at least one notebook.", ["Logo 文件不存在，请重新选择。"] = "Logo file not found. Choose it again.",
        ["最多添加 40 个单词本。"] = "You can add up to 40 notebooks.", ["单词本"] = "Notebooks", ["单词本 "] = "Notebook ",
        ["模板检查"] = "Template check", ["精简模板"] = "Compact template",
        ["已选择随新版提供的精简模板。点击「保存设置」后生效。你原来的模板文件会保留。"] = "Compact template selected. Save settings to apply. Your original template file is kept.",
        ["外观预览"] = "Theme preview", ["界面示例 · 未调用 API"] = "Preview · No API request",
        ["这里将显示你框选区域的中文译文。\n\n数学变量示例：q₁、q₂、…、qₖ；p₁、p₂、…、pₖ。"] = "Your selection's translation appears here.\n\nMath variables: q₁, q₂, …, qₖ; p₁, p₂, …, pₖ.",
        ["界面示例 · 当前译文为内置示例，未调用 API。可在设置中配置后开始使用。"] = "Built-in preview · No API request. Configure Settings to start translating.",
        ["翻译完成 · 输入 {0} / 输出 {1} tokens · 可在左侧选词收藏。"] = "Translated · {0} input / {1} output tokens · Select words on the left to save.",
        ["翻译完成 · 输入 {0} / 输出 {1} tokens"] = "Translated · {0} input / {1} output tokens",
        ["已解释当前选区 · 输入 {0} / 输出 {1} tokens · 关闭窗口即清除本次上下文。"] = "Answered · {0} input / {1} output tokens · Closing clears this context.",
        ["视觉识别完成 · 输入 {0} / 输出 {1} tokens"] = "Capture translated · {0} input / {1} output tokens",
        [" · 待核对："] = " · Please check: ", ["已收藏到："] = "Saved to: ", ["设置未保存："] = "Settings not saved: ",
        ["翻译已完成，但本地历史保存失败："] = "Translated, but saving history failed: ",
        ["公式阅读视图未能启动，请安装 Microsoft Edge WebView2 Runtime。可继续使用文本页。"] = "Formula view could not start. Install Microsoft Edge WebView2 Runtime, or use the Text tab.",
        ["API Key 无效或已失效，请在设置中检查。"] = "Invalid or expired API Key. Check Settings.",
        ["DeepSeek 余额不足，请在官方平台检查余额。"] = "Insufficient DeepSeek balance. Check the official platform.",
        ["请求过于频繁或额度受限，请稍后手动重试。"] = "Rate or quota limit reached. Try again later.",
        ["API 地址或模型不存在，请检查设置。"] = "API endpoint or model not found. Check Settings.",
        ["请求参数不被接受，请检查模型名称和 API 地址。"] = "Request rejected. Check the model name and API URL.",
        ["服务暂时不可用，请稍后手动重试。"] = "Service unavailable. Try again later.",
        ["请求超时，请检查网络后手动重试。"] = "Request timed out. Check your connection and retry.",
        ["无法连接模型服务，请检查网络和 API 地址。"] = "Cannot reach the model service. Check your connection and API URL.",
        ["API 返回格式异常，请检查接口地址或稍后重试。"] = "Unexpected API response. Check the endpoint or retry later.",
        ["模型结果未完整结束，请缩小选区或减少输入后重试。"] = "Incomplete model response. Reduce the selection or input and retry.",
        ["模型未返回结果，请手动重试。"] = "No model result. Try again.", ["模型返回了空内容，请手动重试。"] = "Empty model response. Try again.",
        ["Markdown 笔记 (*.md)|*.md"] = "Markdown notes (*.md)|*.md", ["JSON 模板 (*.json)|*.json"] = "JSON templates (*.json)|*.json",
        ["图片 (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico"] = "Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico",
        ["选择或新建单词本（保留已有内容）"] = "Choose or create notebook (existing contents are kept)",
        ["正在测试文本模型，会发起一次简短 API 请求…"] = "Testing the text model with one short API request…",
        ["模板有效，共 {0} 个字段。\n"] = "Valid template with {0} fields.\n",
        ["已补全，请核对原句和释义后保存。输入 {0} / 输出 {1} tokens"] = "Review the sentence and definitions before saving. {0} input / {1} output tokens",
        [" 你仍可手动填写并保存。"] = " You can still edit and save manually.",
        ["连接成功 · {0}\n输入 {1} / 输出 {2} tokens；截图视觉能力请用实际框选验证。"] = "Connected · {0}\n{1} input / {2} output tokens. Test vision with an actual capture.",
        ["撤销最近一次收藏？\n目标：{0}\n之后手动修改过的笔记不会被自动回退。"] = "Undo the last save?\nTarget: {0}\nNotes edited manually since then will be kept.",
        ["正在获取选中文字…"] = "Getting selected text…", ["正在识别截图…"] = "Recognizing capture…",
        ["没有识别到文字。请放大 PDF 后重新框选，尽量只选一栏正文。"] = "No text recognized. Enlarge the PDF and select a single column.",
        ["本地 OCR 原文 · 尚未翻译，可打开主界面校正"] = "Local OCR source · Not translated. Open main window to correct it.",
        ["已完成本地识别。未配置 Key，数学符号建议配置视觉模式后重试。"] = "Local OCR complete. No Key configured. For math, configure vision and retry.",
        ["API Key 中包含空格或换行，请重新粘贴完整的 Key。"] = "API Key contains spaces or newlines. Paste the complete Key again.",
        ["API 地址必须是有效的 HTTPS 基础地址，不能带查询参数、密码或片段。"] = "Use an HTTPS base URL without a query, password or fragment.",
        ["请填写模型名称。"] = "Enter a model name.", ["截图文件过大，请缩小框选区域后重试。"] = "Capture is too large. Select a smaller area and retry.",
        ["文件尚未创建，请先保存一条生词，或在设置中选择已有笔记。"] = "File does not exist yet. Save a word or choose an existing note in Settings.",
        ["本地历史无法读取，原文件已保留。可清空历史后重新启用。"] = "Cannot read local history. The original file is kept. Clear history to enable it again.",
        ["视觉模型未返回完整的识别与翻译文本，请手动重试或切换视觉模型。"] = "Incomplete recognition or translation. Retry or select another vision model.",
        ["视觉识别内容为空或过长，请缩小截图区域后重试。"] = "Empty or oversized recognition result. Select a smaller capture and retry.",
        ["视觉识别结果为空或过长，请缩小框选区域后重试。"] = "Empty or oversized recognition result. Select a smaller capture and retry.",
        ["已粘贴原文，核对后点击「翻译当前原文」。"] = "Source pasted. Review it, then click Translate source.",
        ["剪贴板里没有文字，可直接在左侧输入原文。"] = "No text on the clipboard. Type the source on the left.",
        ["正在获取阅读器中选中的文字…"] = "Getting the text selected in your reader…",
        ["正在翻译所选文字…"] = "Translating the selected text…", ["未配置 API Key，可打开主界面进行设置。"] = "No API Key configured. Open the main window to set it up.",
        ["已取得原文 · 未配置 API Key，尚未翻译"] = "Source captured · No API Key configured; not translated",
        ["请框选要翻译的文字区域…"] = "Select the text area to translate…", ["已取消截图。"] = "Capture cancelled.",
        ["正在识别数学符号并翻译…"] = "Recognizing formulas and translating…", ["正在使用本地 OCR 识别…"] = "Recognizing text with local OCR…",
        ["无法切回阅读器。请在原阅读器中选中文字，使用 Ctrl+Alt+D，或改用截图翻译。"] = "Cannot switch to the reader. Select text there and press Ctrl+Alt+D, or capture it.",
        ["无法发送复制操作。请手动复制文字，或使用截图翻译。"] = "Cannot copy the selection. Copy it manually or use capture translation.",
        ["没有取得选中文字。扫描版 PDF 请用 Ctrl+Alt+S 截图翻译；也可以复制后粘贴到原文框。"] = "No text captured. For scanned PDFs, press Ctrl+Alt+S. You can also paste into the source box.",
        ["快捷键被其他软件占用：{0}。可使用悬浮菜单。"] = "Shortcuts used by another app: {0}. Use the floating menu.",
        ["启动失败："] = "Startup failed: ", ["\n请将整个文件夹解压到可写入的位置后运行。"] = "\nExtract the whole folder to a writable location before running."
    };
}
