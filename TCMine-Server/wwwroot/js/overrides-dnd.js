// Destaque do alvo de drop na árvore de overrides — 100% client-side.
// O highlight via Blazor Server (@ondragenter) tinha um delay grande: cada evento ia ao servidor
// (SignalR) e voltava. Aqui usamos delegação no document e togglamos a classe .ovr-drop na hora,
// sem round-trip. O move em si (drop) continua tratado pelo Blazor (@ondrop), onde latência é ok.
(function () {
    let current = null; // nó atualmente destacado
    let dragSrc = null; // nó sendo arrastado (não destacamos ele mesmo)

    function nodeFrom(e) {
        return e.target && e.target.closest ? e.target.closest('.ovr-node') : null;
    }

    function clear() {
        if (current) {
            current.classList.remove('ovr-drop');
            current = null;
        }
    }

    document.addEventListener('dragstart', function (e) {
        dragSrc = nodeFrom(e);
    }, true);

    document.addEventListener('dragover', function (e) {
        const node = nodeFrom(e);
        if (!node || node === dragSrc) {
            clear();
            return;
        }
        if (node !== current) {
            clear();
            current = node;
            node.classList.add('ovr-drop');
        }
    }, true);

    document.addEventListener('drop', function () {
        clear();
        dragSrc = null;
    }, true);

    document.addEventListener('dragend', function () {
        clear();
        dragSrc = null;
    }, true);
})();
