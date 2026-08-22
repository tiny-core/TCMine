// Carregamento sob demanda do Monaco.
//
// Antes, os três scripts do editor eram declarados no App.razor e desciam em
// TODA página do painel — a lista de servidores, o dashboard, o login. Uma
// página vazia puxava treze arquivos do Monaco (o loader, cinco
// monaco.contribution, a api do editor, os workers) sem ter editor nenhum.
// Aqui eles só descem quando a página de overrides pede.
//
// O módulo guarda a promessa, não um booleano: dois componentes pedindo ao
// mesmo tempo compartilham o mesmo carregamento em vez de injetar os scripts
// duas vezes.
let carregamento = null;

export function ensure(urls) {
    carregamento ??= carregar(urls);
    return carregamento;
}

async function carregar(urls) {
    // Em série, e não em paralelo: o editor.main depende do loader ter definido
    // o AMD, e o jsInterop do BlazorMonaco depende dos dois.
    for (const url of urls)
        await injetar(url);

    await esperarMonaco();
}

function injetar(url) {
    return new Promise((resolve, reject) => {
        if (document.querySelector(`script[data-monaco="${CSS.escape(url)}"]`)) {
            resolve();
            return;
        }

        const script = document.createElement('script');
        script.src = url;
        script.dataset.monaco = url;
        script.onload = () => resolve();
        script.onerror = () => reject(new Error(`Não foi possível carregar ${url}`));
        document.head.appendChild(script);
    });
}

// O onload do editor.main resolve quando o ARQUIVO chegou, não quando o Monaco
// terminou de se registrar. Criar o editor antes disso falha com "monaco is not
// defined", e de forma intermitente — depende da máquina do admin.
function esperarMonaco() {
    const limite = Date.now() + 20000;

    return new Promise((resolve, reject) => {
        (function tentar() {
            if (window.monaco?.editor) {
                resolve();
                return;
            }

            if (Date.now() > limite) {
                reject(new Error('O editor não ficou pronto a tempo.'));
                return;
            }

            setTimeout(tentar, 25);
        })();
    });
}
