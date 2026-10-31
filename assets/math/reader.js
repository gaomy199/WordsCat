'use strict';
window.setReading = function (value) {
    document.body.style.color = value.ink;
    document.body.style.backgroundColor = value.surface;
    const content = document.getElementById('content');
    content.textContent = value.text;
    renderMathInElement(content, {
        delimiters: [{ left: '$$', right: '$$', display: true }, { left: '\\[', right: '\\]', display: true },
            { left: '$', right: '$', display: false }, { left: '\\(', right: '\\)', display: false }],
        throwOnError: false, trust: false, strict: 'ignore', maxExpand: 1000
    });
    // Display equations already create paragraph spacing. Avoid also preserving empty lines beside them.
    content.querySelectorAll('.katex-display').forEach(function (equation) {
        const block = equation.parentElement.parentElement === content ? equation.parentElement : equation;
        if (block.previousSibling && block.previousSibling.nodeType === 3)
            block.previousSibling.textContent = block.previousSibling.textContent.replace(/[ \t]*\n[\s]*$/, '');
        if (block.nextSibling && block.nextSibling.nodeType === 3)
            block.nextSibling.textContent = block.nextSibling.textContent.replace(/^[\s]*\n[ \t]*/, '');
    });
};
document.addEventListener('selectionchange', function () {
    chrome.webview.postMessage(window.getSelection().toString());
});
