namespace TCMine.Server.Web.Configuration;

/// <summary>
///     Servidor SMTP de captura, para testar o envio sem provedor nenhum.
///     Recebe o que o TCMine manda e guarda em memória para o painel exibir —
///     não entrega nada a ninguém. É o que permite exercitar recuperação de
///     senha e convites numa máquina de desenvolvimento.
/// </summary>
public sealed class DevMailOptions
{
    public const string SectionName = "DevMail";

    /// <summary>
    ///     Desligado por padrão. Ligar abre uma porta que aceita e-mail sem
    ///     autenticar de verdade — inofensivo preso ao loopback, que é onde ele
    ///     escuta, e uma péssima ideia em qualquer outro lugar.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    ///     2525 e não 25: a porta baixa exige privilégio em Linux e costuma
    ///     estar ocupada por um MTA de sistema.
    /// </summary>
    public int Port { get; set; } = 2525;

    /// <summary>
    ///     Quantas mensagens manter. É caixa de teste, não arquivo: passar disso
    ///     descarta a mais antiga, e o processo não vira um vazamento de memória
    ///     em quem esquecer a opção ligada.
    /// </summary>
    public int Capacity { get; set; } = 50;
}
