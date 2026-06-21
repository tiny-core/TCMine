namespace TCMine_Server;

/// <summary>
/// Chave partilhada em <see cref="HttpContext.Items"/> entre a página catch-all NotFound e o
/// middleware que promove o status a 404. Necessário porque definir o 404 durante o render SSR do
/// Blazor descarta o corpo — então marcamos aqui e ajustamos o status na hora de enviar a resposta.
/// </summary>
public static class NotFoundResponseMarker
{
    public const string Key = "tcmine:not-found";
}