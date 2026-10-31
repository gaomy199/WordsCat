# 离线公式资源

KaTeX 0.16.22，来自官方 npm 包 `https://registry.npmjs.org/katex/-/katex-0.16.22.tgz`。

保留 dist/katex.min.js、dist/katex.min.css、dist/contrib/auto-render.min.js、dist/fonts/ 和 MIT LICENSE。运行时从本地目录映射读取，不访问 CDN。

reader.html / reader.js 是 ReadDock 自己的阅读模板。页面、样式和字体须保持同源；跨源 font 请求会被安全策略阻止，造成公式字体回退。动态原文用 textContent 写入内存，不写进模板文件或 data URL。不要移除字体实际 loaded 状态的回归检查。

更新资源时同步版本、LICENSE，并运行 `build.ps1` 与 `verify.ps1 -IncludeUi`。KaTeX 文档：<https://katex.org/docs/browser.html>。
