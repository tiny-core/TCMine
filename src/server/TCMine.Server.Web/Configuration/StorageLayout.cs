namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Deriva de uma raiz só todos os caminhos de armazenamento.
///     Existe porque declarar quatro caminhos que são todos derivados de um é
///     trabalho sem propósito, e trabalho sem propósito erra: basta acertar três
///     e escrever o quarto diferente para o painel funcionar e os servidores de
///     jogo subirem vazios.
///     As chaves específicas continuam valendo e ganham desta derivação — quem
///     precisa pôr os blobs num disco maior que o resto não perde essa
///     possibilidade. É por isso que só o que está AUSENTE é preenchido.
/// </summary>
public static class StorageLayout
{
    public const string RootKey = "Storage:RootPath";

    /// <summary>
    ///     Preenche as chaves de caminho que ninguém definiu, a partir de
    ///     <c>Storage:RootPath</c>. Sem a raiz, não faz nada — a configuração
    ///     explícita segue sendo o caminho normal.
    /// </summary>
    public static void Apply(IConfigurationBuilder builder, IConfiguration configuration)
    {
        if (configuration[RootKey] is not { Length: > 0 } raiz)
            return;

        raiz = raiz.TrimEnd('/', '\\');

        Dictionary<string, string?> derivados = [];

        Derivar(derivados, configuration, "Database:ConnectionString",
            $"Data Source={raiz}/data/tcmine.db");

        Derivar(derivados, configuration, "BlobStorage:RootPath", $"{raiz}/data/blobs");
        Derivar(derivados, configuration, "Instances:RootPath", $"{raiz}/instances");
        Derivar(derivados, configuration, "DataProtection:KeysPath", $"{raiz}/data/keys");

        if (derivados.Count > 0)
            builder.AddInMemoryCollection(derivados);
    }

    /// <summary>
    ///     Só preenche o que está ausente. Adicionar ao fim das fontes seria
    ///     perigoso se sobrescrevesse — passaria por cima de variável de
    ///     ambiente, que é justamente como o admin configura em container.
    /// </summary>
    private static void Derivar(
        Dictionary<string, string?> destino,
        IConfiguration configuration,
        string chave,
        string valor)
    {
        if (string.IsNullOrWhiteSpace(configuration[chave]))
            destino[chave] = valor;
    }
}
