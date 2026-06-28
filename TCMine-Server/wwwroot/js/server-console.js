// Auto-scroll do console de servidor: mantém a última linha do log sempre visível.
window.tcmineConsole = {
    scrollToBottom: function () {
        var el = document.querySelector('.server-console__log');
        if (el) el.scrollTop = el.scrollHeight;
    }
};
