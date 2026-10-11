using TCMine.Server.Application.Abstractions;

namespace TCMine.Server.Application.Tests.Fakes;

/// <summary>Conta os pedidos de sincronização de DNS, sem sincronizar nada.</summary>
public sealed class FakeDnsSyncSignal : IDnsSyncSignal
{
    public int Requests { get; private set; }

    public void Request() => Requests++;
}
