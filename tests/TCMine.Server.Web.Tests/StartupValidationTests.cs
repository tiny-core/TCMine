using Microsoft.Extensions.Options;
using TCMine.Server.Web.Tests.Infrastructure;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     Configuração inválida tem de derrubar o arranque, com a chave no texto.
///     A alternativa é o que existia: <c>ValidateOnStart()</c> sem nenhuma
///     validação registrada — uma chamada decorativa. A aplicação subia "com
///     sucesso" e falhava longe da causa, dias depois e do lado do cliente.
/// </summary>
public class StartupValidationTests
{
    [Fact]
    public void Producao_sem_PublicUrl_recusa_subir()
    {
        // Sem a URL pública, o handshake entrega a todo launcher um feed de
        // atualização apontando para localhost. Nada quebra no servidor; quebra
        // em cada cliente, silenciosamente.
        using var factory = new TcMineAppFactory("Production", ("Server:PublicUrl", ""));

        var erro = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        // Específico de propósito: duas regras diferentes citam PublicUrl, e
        // asserção genérica passaria com a regra errada disparando.
        erro.Message.ShouldContain("obrigatório fora de Development");
    }

    [Fact]
    public void Producao_sem_AzureClientId_sobe_para_poder_ser_configurada()
    {
        // Já foi o contrário, e estava errado: o client id passou a morar na tela
        // de configurações, então exigi-lo no arranque impediria o servidor de
        // subir justamente para o admin poder configurá-lo. Vazio nos dois lados
        // apenas faz o pareamento recusar, com uma mensagem que diz a quem
        // recorrer — e não há como o jogador entrar por acidente.
        using var factory = new TcMineAppFactory(
            "Production",
            ("Server:PublicUrl", "https://exemplo.com/"),
            ("Server:AzureClientId", ""));

        Should.NotThrow(() => factory.CreateClient());
    }

    [Fact]
    public void AzureClientId_malformado_recusa_subir()
    {
        // Deixar de ser obrigatório não é deixar de ser verificado. Um id que não
        // é GUID atravessa o handshake inteiro e só falha no MSAL, na máquina do
        // jogador — longe de quem consegue corrigir.
        using var factory = new TcMineAppFactory(
            settings: ("Server:AzureClientId", "nao-e-um-guid"));

        var erro = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        erro.Message.ShouldContain("Server:AzureClientId");
    }

    [Fact]
    public void PublicUrl_com_esquema_invalido_recusa_subir()
    {
        using var factory = new TcMineAppFactory(settings: ("Server:PublicUrl", "ftp://exemplo.com/"));

        var erro = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        erro.Message.ShouldContain("http/https");
    }

    [Fact]
    public void Nome_vazio_recusa_subir()
    {
        using var factory = new TcMineAppFactory(settings: ("Server:Name", "   "));

        var erro = Should.Throw<OptionsValidationException>(() => factory.CreateClient());

        erro.Message.ShouldContain("Server:Name");
    }

    [Fact]
    public void Provider_de_banco_desconhecido_recusa_subir()
    {
        // O switch que escolhe o provider roda quando o DbContext é CRIADO, não
        // quando é registrado: sem esta validação, um erro de digitação aqui só
        // estourava na primeira consulta — app de pé, health verde, primeira tela
        // quebrada.
        using var factory = new TcMineAppFactory(settings: ("Database:Provider", "MySql"));

        var erro = Should.Throw<InvalidOperationException>(() => factory.CreateClient());

        erro.Message.ShouldContain("Database:Provider");
    }

    [Fact]
    public void ConnectionString_vazia_recusa_subir()
    {
        using var factory = new TcMineAppFactory(settings: ("Database:ConnectionString", ""));

        var erro = Should.Throw<InvalidOperationException>(() => factory.CreateClient());

        erro.Message.ShouldContain("Database:ConnectionString");
    }

    [Fact]
    public void Development_sobe_sem_PublicUrl()
    {
        // O outro lado da regra: em desenvolvimento o fallback para localhost é
        // prático, e exigir a URL pública só atrapalharia quem clona o repositório
        // e roda. Sem este teste, apertar a validação "por segurança" quebraria o
        // arranque local sem ninguém perceber até o próximo clone.
        using var factory = new TcMineAppFactory("Development", ("Server:PublicUrl", ""));

        Should.NotThrow(() => factory.CreateClient());
    }
}
