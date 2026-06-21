using Microsoft.AspNetCore.Components;

namespace TCMine_Server.Components.Pages;

public partial class NotFound : ComponentBase
{
    // Rota catch-all (':nonfile' ignora pedidos de arquivos): captura qualquer rota não mapeada
    // — ex.: /setup enquanto a página de setup não existe. Por ser uma rota *encontrada*, renderiza
    // o corpo normalmente em SSR (ao contrário do re-execute do StatusCodePages, que saía vazio).
    [Parameter] public string? Slug { get; set; }

    // Não definimos o 404 aqui: fazê-lo durante o render SSR do Blazor descarta o corpo. Em vez disso
    // marcamos o HttpContext e um middleware promove o status a 404 ao enviar (ver Program.cs).
    // [ExcludeFromInteractiveRouting] mantém esta página em SSR estático (sem circuito), garantindo
    // que o HttpContext cascateia.
    [CascadingParameter] private HttpContext? Http { get; set; }

    protected override void OnInitialized()
    {
        Http?.Items[NotFoundResponseMarker.Key] = true;
    } 
}