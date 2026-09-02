using System.Data.Common;
using Desafio.Api.Infraestrutura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Desafio.Api.Tests;

/// <summary>
/// Conta os comandos SQL emitidos por requisição, pra provar a exigência de desempenho da
/// SPEC (seção 3): a quantidade de consultas não pode crescer com o tamanho da página.
///
/// Usa uma WebApplicationFactory própria, em vez da compartilhada em ApiFixture, porque só
/// este teste precisa do interceptor, registrá-lo na fábrica de todo mundo misturaria a
/// contagem com o que outros testes fazem em paralelo na mesma coleção. Ainda assim, aponta
/// pro mesmo Postgres do ApiFixture (via ConnectionString), sem subir um container novo.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public sealed class ListagemDesempenhoTests(ApiFixture fixture) : IAsyncLifetime
{
    private readonly ContadorDeConsultas _contador = new();
    private WebApplicationFactory<Program> _fabrica = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", fixture.ConnectionString);

            builder.ConfigureServices(services =>
            {
                var descritorExistente = services.SingleOrDefault(
                    servico => servico.ServiceType == typeof(DbContextOptions<AppDbContext>));

                if (descritorExistente is not null)
                {
                    services.Remove(descritorExistente);
                }

                services.AddDbContext<AppDbContext>(opcoes =>
                    opcoes.UseNpgsql(fixture.ConnectionString).AddInterceptors(_contador));
            });
        });

        _client = _fabrica.CreateClient();

        await fixture.LimparAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _fabrica.DisposeAsync();
    }

    [Fact]
    public async Task Listar_nao_deve_aumentar_consultas_conforme_o_tamanho_da_pagina()
    {
        await fixture.SemearBeneficiariosAsync(10, Planos.Bronze, "ATIVO", 100);
        await fixture.SemearBeneficiariosAsync(10, Planos.Prata, "ATIVO", 200);
        await fixture.SemearBeneficiariosAsync(10, Planos.Ouro, "ATIVO", 300);

        _contador.Zerar();
        var respostaUmItem = await _client.GetAsync("/beneficiarios?tamanho=1");
        var consultasComUmItem = _contador.Total;

        _contador.Zerar();
        var respostaCinquentaItens = await _client.GetAsync("/beneficiarios?tamanho=50");
        var consultasCom50Itens = _contador.Total;

        Assert.True(respostaUmItem.IsSuccessStatusCode);
        Assert.True(respostaCinquentaItens.IsSuccessStatusCode);

        Assert.Equal(consultasComUmItem, consultasCom50Itens);
    }
}

/// <summary>
/// Incrementa a cada comando SQL executado de fato (SELECT via leitor ou via escalar,
/// como o COUNT). Uma instância é compartilhada entre as requisições da mesma
/// WebApplicationFactory, então precisa ser zerada antes de cada medição.
/// </summary>
public sealed class ContadorDeConsultas : DbCommandInterceptor
{
    private int _total;

    public int Total => _total;

    public void Zerar() => Interlocked.Exchange(ref _total, 0);

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        Interlocked.Increment(ref _total);
        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _total);
        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    public override InterceptionResult<object> ScalarExecuting(
        DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
    {
        Interlocked.Increment(ref _total);
        return base.ScalarExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<object> result,
        CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _total);
        return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
    }
}
