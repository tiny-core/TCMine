// Auto-scroll do console de servidor: mantém a última linha do log sempre visível.
window.tcmineConsole = {
    scrollToBottom: function () {
        var el = document.querySelector('.server-console__log');
        if (el) el.scrollTop = el.scrollHeight;
    }
};

// Scroll genérico até o fim de um elemento (recebe o ElementReference do Blazor). Usado pelo
// BusyOverlay para manter o passo atual visível conforme a lista de progresso cresce.
window.tcmineScrollToBottom = function (el) {
    if (el) el.scrollTop = el.scrollHeight;
};
