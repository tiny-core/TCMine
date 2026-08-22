using Microsoft.Extensions.Configuration;
using TCMine.Server.Web.Configuration;

namespace TCMine.Server.Web.Tests;

/// <summary>
///     A raiz única e a checagem de coerência do bind mount.
///     As duas nasceram do mesmo episódio: instalar num NAS, cujo painel
///     reescreveu o volume para caminhos diferentes dentro e fora do container.
///     O resultado seria um servidor de jogo subindo vazio, com o painel
///     dizendo que está tudo bem.
/// </summary>
public sealed class StorageLayoutTests
{
    [Fact]
    public void Raiz_preenche_os_quatro_caminhos()
    {
        var config = Configurar(new() { ["Storage:RootPath"] = "/dados/tcmine" });

        config["Database:ConnectionString"].ShouldBe("Data Source=/dados/tcmine/data/tcmine.db");
        config["BlobStorage:RootPath"].ShouldBe("/dados/tcmine/data/blobs");
        config["Instances:RootPath"].ShouldBe("/dados/tcmine/instances");
        config["DataProtection:KeysPath"].ShouldBe("/dados/tcmine/data/keys");
    }

    [Fact]
    public void Caminho_declarado_a_parte_ganha_da_raiz()
    {
        // Quem põe os blobs num disco maior que o resto não pode perder essa
        // possibilidade por causa da conveniência de ter uma raiz.
        var config = Configurar(new()
        {
            ["Storage:RootPath"] = "/dados/tcmine",
            ["BlobStorage:RootPath"] = "/disco-grande/blobs"
        });

        config["BlobStorage:RootPath"].ShouldBe("/disco-grande/blobs");
        config["Instances:RootPath"].ShouldBe("/dados/tcmine/instances");
    }

    [Fact]
    public void Sem_raiz_nada_e_inventado()
    {
        // A configuração explícita continua sendo o caminho normal; a raiz é
        // atalho, não obrigação.
        var config = Configurar(new() { ["Instances:RootPath"] = "/so/isto" });

        config["Instances:RootPath"].ShouldBe("/so/isto");
        config["BlobStorage:RootPath"].ShouldBeNull();
    }

    [Fact]
    public void Barra_sobrando_no_fim_da_raiz_nao_duplica()
    {
        var config = Configurar(new() { ["Storage:RootPath"] = "/dados/tcmine/" });

        config["Instances:RootPath"].ShouldBe("/dados/tcmine/instances");
    }

    [Fact]
    public void Bind_com_caminhos_diferentes_e_recusado()
    {
        // O caso real: o painel do NAS levou /media/ZimaOS-HD/AppData/tcmine-server
        // do host para /DATA/AppData/tcmine no container.
        string[] mountinfo =
        [
            "25 1 8:1 / / rw,relatime - ext4 /dev/sda1 rw",
            "36 25 8:2 /AppData/tcmine-server /DATA/AppData/tcmine rw,relatime - ext4 /dev/sdb1 rw"
        ];

        var problema = MountCoherence.Analisar(mountinfo, "/DATA/AppData/tcmine/instances");

        problema.ShouldNotBeNull();
        problema.ShouldContain("caminhos diferentes");
        problema.ShouldContain("/AppData/tcmine-server");
    }

    [Fact]
    public void Bind_com_o_mesmo_caminho_passa()
    {
        string[] mountinfo =
        [
            "25 1 8:1 / / rw,relatime - ext4 /dev/sda1 rw",
            "36 25 8:1 /opt/tcmine /opt/tcmine rw,relatime - ext4 /dev/sda1 rw"
        ];

        MountCoherence.Analisar(mountinfo, "/opt/tcmine/instances").ShouldBeNull();
    }

    [Fact]
    public void Bind_dentro_de_disco_montado_passa()
    {
        // Quando a origem é um disco à parte, o mountinfo traz o caminho
        // RELATIVO a esse disco — comparar por sufixo é o que funciona nos dois
        // arranjos, e sem isso este caso viraria falso positivo.
        string[] mountinfo =
        [
            "25 1 8:1 / / rw,relatime - ext4 /dev/sda1 rw",
            "36 25 8:2 /AppData/tcmine /media/hd/AppData/tcmine rw - ext4 /dev/sdb1 rw"
        ];

        MountCoherence.Analisar(mountinfo, "/media/hd/AppData/tcmine/instances").ShouldBeNull();
    }

    [Fact]
    public void Pasta_sem_volume_nenhum_e_recusada()
    {
        // Só a raiz do container cobre o caminho: a pasta vive na camada da
        // imagem, some ao recriar o container e o daemon não a enxerga.
        string[] mountinfo = ["25 1 8:1 / / rw,relatime - overlay overlay rw"];

        var problema = MountCoherence.Analisar(mountinfo, "/app/data/instances");

        problema.ShouldNotBeNull();
        problema.ShouldContain("não está num volume montado");
    }

    [Fact]
    public void O_mount_mais_especifico_e_quem_vale()
    {
        // Dois mounts cobrem o caminho; quem manda é o mais específico. Olhar o
        // primeiro diria "coerente" e deixaria passar a divergência do segundo.
        string[] mountinfo =
        [
            "25 1 8:1 / / rw - ext4 /dev/sda1 rw",
            "30 25 8:2 /DATA /DATA rw - ext4 /dev/sdb1 rw",
            "36 30 8:2 /AppData/outra-pasta /DATA/AppData/tcmine rw - ext4 /dev/sdb1 rw"
        ];

        var problema = MountCoherence.Analisar(mountinfo, "/DATA/AppData/tcmine/instances");

        problema.ShouldNotBeNull();
        problema.ShouldContain("outra-pasta");
    }

    [Fact]
    public void Nome_final_igual_passa_mesmo_sem_dar_para_confirmar()
    {
        // Limite conhecido da checagem: origem "/AppData/tcmine" e ponto
        // "/DATA/AppData/tcmine" são coerentes SE o disco estiver montado em
        // /DATA no host — e o mountinfo de dentro do container não diz onde o
        // disco está montado lá fora. Na dúvida, não acusa: um falso positivo
        // impediria o arranque de uma instalação correta.
        string[] mountinfo =
        [
            "25 1 8:1 / / rw - ext4 /dev/sda1 rw",
            "36 25 8:2 /AppData/tcmine /DATA/AppData/tcmine rw - ext4 /dev/sdb1 rw"
        ];

        MountCoherence.Analisar(mountinfo, "/DATA/AppData/tcmine/instances").ShouldBeNull();
    }

    [Fact]
    public void Fora_de_container_a_coerencia_nao_e_verificada()
    {
        // Regressão: a primeira versão deduzia "estamos em container" pela
        // existência de /proc/self/mountinfo, que existe em TODO Linux. O
        // resultado foi a aplicação recusando subir no runner do CI — Linux,
        // sem container, com a pasta de desenvolvimento em data/instances.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Instances:RootPath"] = "data/instances"
            })
            .Build();

        Should.NotThrow(() => MountCoherence.Verify(config, emContainer: false));
    }

    [Fact]
    public void O_escape_desliga_a_verificacao_mesmo_em_container()
    {
        // Saída para o arranjo em que a heurística erra: sem ela, um falso
        // positivo deixaria a instalação sem como subir.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Instances:RootPath"] = "/qualquer/coisa",
                [MountCoherence.SkipKey] = "true"
            })
            .Build();

        Should.NotThrow(() => MountCoherence.Verify(config, emContainer: true));
    }

    private static IConfigurationRoot Configurar(Dictionary<string, string?> valores)
    {
        var builder = new ConfigurationBuilder();
        builder.AddInMemoryCollection(valores);

        var parcial = builder.Build();
        StorageLayout.Apply(builder, parcial);

        return builder.Build();
    }
}
