'use strict';
window.setReading = function (value) {
    document.body.style.color = value.ink;
    document.body.style.backgroundColor = value.surface;
    document.body.classList.toggle('compact', !!value.compact);
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
    // Keep Chinese at 16px in both window types while English uses the 18px baseline.
    const walker = document.createTreeWalker(content, NodeFilter.SHOW_TEXT);
    const nodes = [];
    while (walker.nextNode()) {
        if (!walker.currentNode.parentElement.closest('.katex')) nodes.push(walker.currentNode);
    }
    nodes.forEach(function (node) {
        const text = node.textContent;
        const matches = Array.from(text.matchAll(/[\p{Script=Han}\u3000-\u303f\uff00-\uffef]+/gu));
        if (!matches.length) return;
        const fragment = document.createDocumentFragment();
        let offset = 0;
        matches.forEach(function (match) {
            fragment.append(document.createTextNode(text.slice(offset, match.index)));
            const span = document.createElement('span');
            span.className = 'cjk'; span.textContent = match[0]; fragment.append(span);
            offset = match.index + match[0].length;
        });
        fragment.append(document.createTextNode(text.slice(offset))); node.replaceWith(fragment);
    });
};
document.addEventListener('selectionchange', function () {
    chrome.webview.postMessage(window.getSelection().toString());
});
