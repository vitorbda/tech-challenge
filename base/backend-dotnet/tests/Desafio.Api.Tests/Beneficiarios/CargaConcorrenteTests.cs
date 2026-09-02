using System.Collections.Concurrent;
using System.Net;

namespace Desafio.Api.Tests;

/// <summary>
/// Cobre a exigência do e-mail de avaliação (não escrita na SPEC): a aplicação precisa
/// continuar respondendo sem erro sob acesso concorrente, inclusive misturando operações
/// diferentes e lendo a listagem enquanto outros clientes criam registros. Não mede
/// throughput/latência (não há baseline de hardware pra comparar) — só ausência de 5xx.
/// </summary>
[Collection(ColecaoDaApi.Nome)]
public class CargaConcorrenteTests(ApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    private static object CorpoDeCriacao(string cpf) => new
    {
        NomeCompleto = "Maria Aparecida da Silva",
        Cpf = cpf,
        DataNascimento = "1990-05-12",
        PlanoId = Planos.Bronze
    };

    [Fact]
    public async Task Trafego_misto_concorrente_nao_deve_gerar_erro_de_servidor()
    {
        var existentes = await fixture.SemearBeneficiariosAsync(20, primeiraSemente: 1000);

        var tarefas = new List<Task<HttpResponseMessage>>();

        // 30 leituras de listagem, 30 criações com CPFs distintos, e uma consulta por id
        // pra cada beneficiário já existente — tudo disparado ao mesmo tempo.
        tarefas.AddRange(Enumerable.Range(0, 30)
            .Select(_ => Client.GetAsync("/beneficiarios?tamanho=10")));

        tarefas.AddRange(Enumerable.Range(0, 30)
            .Select(i => Client.PostAsync("/beneficiarios", Http.Json(CorpoDeCriacao(GeradorDeCpf.Gerar(2000 + i))))));

        tarefas.AddRange(existentes.Select(b => Client.GetAsync($"/beneficiarios/{b.Id}")));

        var respostas = await Task.WhenAll(tarefas);

        Assert.DoesNotContain(respostas, r => (int)r.StatusCode >= 500);
    }

    [Fact]
    public async Task Listagem_nao_deve_falhar_durante_criacao_concorrente_de_registros()
    {
        await fixture.SemearBeneficiariosAsync(10, primeiraSemente: 3000);

        using var cancelamento = new CancellationTokenSource();
        var statusDasLeituras = new ConcurrentBag<HttpStatusCode>();

        var tarefaDeLeituraContinua = Task.Run(async () =>
        {
            while (!cancelamento.IsCancellationRequested)
            {
                var resposta = await Client.GetAsync("/beneficiarios?tamanho=20");
                statusDasLeituras.Add(resposta.StatusCode);
            }
        });

        var respostasDeEscrita = await Task.WhenAll(Enumerable.Range(0, 20)
            .Select(i => Client.PostAsync(
                "/beneficiarios", Http.Json(CorpoDeCriacao(GeradorDeCpf.Gerar(4000 + i))))));

        cancelamento.Cancel();
        await tarefaDeLeituraContinua;

        Assert.DoesNotContain(respostasDeEscrita, r => (int)r.StatusCode >= 500);
        Assert.NotEmpty(statusDasLeituras);
        Assert.DoesNotContain(statusDasLeituras, status => (int)status >= 500);
    }
}
