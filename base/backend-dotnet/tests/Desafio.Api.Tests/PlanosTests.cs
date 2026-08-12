using System.Net;
using System.Text.Json;

namespace Desafio.Api.Tests;

[Collection(ColecaoDaApi.Nome)]
public class PlanosTests(ApiFixture fixture) : IAsyncLifetime
{
    private HttpClient Client => fixture.Client;

    public Task InitializeAsync() => fixture.LimparAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Health_deve_reportar_aplicacao_e_banco_disponiveis()
    {
        var resposta = await Client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal("ok", corpo.GetProperty("status").GetString());
        Assert.Equal("ok", corpo.GetProperty("banco").GetString());
    }

    [Fact]
    public async Task Listar_deve_devolver_os_cinco_planos_da_carga_inicial()
    {
        var resposta = await Client.GetAsync("/planos");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal(5, corpo.GetArrayLength());

        var nomes = corpo.EnumerateArray()
            .Select(plano => plano.GetProperty("nome").GetString())
            .ToList();

        Assert.Contains("Bronze", nomes);
        Assert.Contains("Executivo", nomes);
    }

    [Fact]
    public async Task Obter_deve_devolver_o_plano_do_seed()
    {
        var resposta = await Client.GetAsync($"/planos/{Planos.Ouro}");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal("Ouro", corpo.GetProperty("nome").GetString());
        Assert.Equal("100003", corpo.GetProperty("codigo_registro_ans").GetString());
    }

    [Fact]
    public async Task Obter_plano_inexistente_deve_devolver_404()
    {
        var resposta = await Client.GetAsync($"/planos/{Planos.Inexistente}");

        Assert.Equal(HttpStatusCode.NotFound, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_deve_devolver_201_com_header_location()
    {
        var resposta = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Platina",
            CodigoRegistroAns = "200001"
        }));

        Assert.Equal(HttpStatusCode.Created, resposta.StatusCode);
        Assert.NotNull(resposta.Headers.Location);

        var corpo = await resposta.CorpoAsync();
        Assert.NotEqual(Guid.Empty, corpo.GetProperty("id").GetGuid());
        Assert.Equal("Platina", corpo.GetProperty("nome").GetString());
    }

    [Fact]
    public async Task Criar_com_nome_ja_usado_deve_devolver_409()
    {
        var resposta = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Bronze",
            CodigoRegistroAns = "200002"
        }));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }

    [Fact]
    public async Task Criar_com_dados_invalidos_deve_devolver_400_detalhando_os_campos()
    {
        var resposta = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "ab",
            CodigoRegistroAns = "abc"
        }));

        Assert.Equal(HttpStatusCode.BadRequest, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        var campos = corpo.GetProperty("detalhes")
            .EnumerateArray()
            .Select(detalhe => detalhe.GetProperty("campo").GetString())
            .ToList();

        Assert.Contains("nome", campos);
        Assert.Contains("codigo_registro_ans", campos);
    }

    [Fact]
    public async Task Atualizar_deve_alterar_os_dados_do_plano()
    {
        var criacao = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Titanio",
            CodigoRegistroAns = "200003"
        }));

        var id = (await criacao.CorpoAsync()).GetProperty("id").GetGuid();

        var resposta = await Client.PutAsync($"/planos/{id}", Http.Json(new
        {
            Nome = "Titanio Plus",
            CodigoRegistroAns = "200004"
        }));

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);

        var corpo = await resposta.CorpoAsync();
        Assert.Equal("Titanio Plus", corpo.GetProperty("nome").GetString());
        Assert.Equal("200004", corpo.GetProperty("codigo_registro_ans").GetString());
    }

    [Fact]
    public async Task Excluir_deve_ser_logico_e_tirar_o_plano_das_consultas()
    {
        var criacao = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Temporario",
            CodigoRegistroAns = "200005"
        }));

        var id = (await criacao.CorpoAsync()).GetProperty("id").GetGuid();

        var exclusao = await Client.DeleteAsync($"/planos/{id}");
        Assert.Equal(HttpStatusCode.NoContent, exclusao.StatusCode);

        var consulta = await Client.GetAsync($"/planos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, consulta.StatusCode);

        var listagem = await (await Client.GetAsync("/planos")).CorpoAsync();
        var ids = listagem.EnumerateArray().Select(plano => plano.GetProperty("id").GetGuid());
        Assert.DoesNotContain(id, ids);

        var novaExclusao = await Client.DeleteAsync($"/planos/{id}");
        Assert.Equal(HttpStatusCode.NotFound, novaExclusao.StatusCode);
    }

    [Fact]
    public async Task Codigo_de_plano_excluido_deve_continuar_ocupado()
    {
        var criacao = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Descontinuado",
            CodigoRegistroAns = "200006"
        }));

        var id = (await criacao.CorpoAsync()).GetProperty("id").GetGuid();
        await Client.DeleteAsync($"/planos/{id}");

        var resposta = await Client.PostAsync("/planos", Http.Json(new
        {
            Nome = "Outro Nome",
            CodigoRegistroAns = "200006"
        }));

        Assert.Equal(HttpStatusCode.Conflict, resposta.StatusCode);
    }
}
