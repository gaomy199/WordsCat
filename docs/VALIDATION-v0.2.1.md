# 0.2.1 验证记录

日期：2026-10-29，Windows 11，当前桌面 150% 缩放。

## 构建与自动验证

`build.ps1` 成功，无编译警告。`verify.ps1 -IncludeUi` 成功。完整本机报告目录：`.test-results/5217ad776f234259930dfe93020e9f4b/`（已忽略）。

- 保留原有模板、迁移、Markdown 安全转义、多单词本去重隔离、追加字节保护、撤销和外部编辑冲突测试。
- API 请求使用本地模拟响应，验证独立翻译、结构化词条、错误和截断处理。
- Windows OCR 对合成英文图片实际返回 `The method is robust to measurement noise.`。
- 实际 WebView2 加载随包 KaTeX，DOM 验证行内和独立公式、至少两个分式、输入 HTML 不作为标记执行，以及深色阅读背景刷新。
- 实际 SettingsDialog 模式变更事件立即调用外观应用，关闭未保存窗口恢复原主题。
- 自定义 Logo 浮标在强调色换为 #00FF00 后保持相同中性背景。
- 三轮隐藏主窗口、替换原文、建立结果小窗、点击其主界面按钮：小窗释放，主窗口正常/可用，阅读视图显示本轮新选区。

界面测试使用独立 STA 消息循环。不要在无界面 `SelfTests.RunAsync(...).GetAwaiter().GetResult()` 路径中创建 Form，否则 WindowsFormsSynchronizationContext 可能使后续 await 死锁。

## Computer Use 检查

- 主界面两个阅读页实际显示 Hamiltonian 示例中的分式、上标、点号、希腊字母及独立公式，原文编辑页可查看源码。
- 查看设置及外观页。模式即时预览和取消恢复另外由上述真实窗体事件回归验证。
- 最后以隔离的深色/#00FF00 配置再次打开主界面，确认公式变为浅色，禁用“撤销收藏”呈可读灰色，无浅色文字阴影。
- Chrome 新标签打开仅含合成英文的 data 页面，选中句子并按 Ctrl+Alt+D。结果小窗显示与网页一致的句子及“未配置 API Key，尚未翻译”。用 Ctrl+Alt+R 展开主界面。
- Windows 会限制无用户输入的后台进程抢前台；自动测试断言窗口状态和内容，不把后台 SetForegroundWindow 的返回当作所有前台场景的证明。

## 依赖与边界

公式显示采用微软 WebView2 SDK 1.0.3595.46、KaTeX 0.16.22，本地 CSS/JS/字体置于 `assets/math/`，包含 KaTeX MIT LICENSE。参考：[微软本地资源映射 API](https://learn.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.setvirtualhostnametofoldermapping?view=webview2-dotnet-1.0.3595.46)、[KaTeX 浏览器用法](https://katex.org/docs/browser.html)。

当前 WebView2 Runtime 156 对 NavigateToString 的导航事件报告 data URI；程序仅允许自己生成的完整本地文档 URI，以及旧运行时的 about:blank。禁止其他页面导航，关闭新窗口，资源请求仅允许本地映射，文献内容通过 textContent 写入。

本轮没有读取真实 Key、上传用户截图或调用模型服务。公式排版验证使用手工转录的合成文本，不能作为视觉识别准确率的证据。尚未重新评估复杂扫描文献、多栏排版、全部网页或混合 DPI 多屏。
